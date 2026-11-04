# Deployment & Migration Safety Gate Implementation Plan

## Overview

Rollout Phase 4 (Risk #3) closes a complete coverage gap: `Program.cs:77-98`
deliberately substitutes `EnsureCreatedAsync()` for `Database.MigrateAsync()`
in the `Testing` environment because the shipped migration is raw SQL authored
against SQL Server and cannot run on the Sqlite/InMemory providers the test
suite uses. As a result, the actual production migration path has never been
executed by any test, CI job, or local run — a hazard discovered live, not
hypothetical, since `deployment-skeleton` (a separate, still-`implementing`
change) already deferred this exact gap and still has its own SQL grant for
the managed identity unchecked. This plan adds a CI-gate: a migration
dry-run against a real, disposable SQL Server instance that proves the
shipped migration applies cleanly and produces the exact schema objects the
application depends on.

## Current State Analysis

- `src/melody/melody.csproj` already references `Microsoft.EntityFrameworkCore.SqlServer`
  (production dependency), so `tests/melody.Tests/melody.Tests.csproj` gets
  `UseSqlServer` transitively via its existing `ProjectReference` — no new
  NuGet package is required.
- `tests/melody.Tests/AdminUserSeederTests.cs:15-26` shows the established
  pattern for a test that builds its own `ServiceProvider`/`ApplicationDbContext`
  without going through `MelodyWebApplicationFactory` — the new test reuses
  this shape, substituting `UseSqlServer(...)` for `UseInMemoryDatabase(...)`.
- `src/melody/Data/Migrations/20261005143616_InitialCreate.cs` is the only
  migration. It defines SQL-Server-specific objects the gate must assert
  exist post-migration:
  - `IX_Reservations_RoomId_DayOfWeek_StartTime` — unique index on
    `(RoomId, DayOfWeek, StartTime)` (`:292-296`), the DB-level backstop for
    the double-booking invariant rollout Phase 1 already covers in
    application code.
  - `RoleNameIndex` — filtered unique index on `AspNetRoles.NormalizedName`
    (`:258-263`).
  - Three `FOREIGN KEY` constraints on `Reservations` (`RoomId`, `StudentId`,
    `TeacherId`, all `ReferentialAction.Restrict`) (`:233-250`).
- `.github/workflows/deploy.yml` runs on `ubuntu-latest` with exactly two
  jobs (`build-and-test`, `deploy`); it has no service container, no
  `dotnet-ef` CLI step, and no tool manifest (`.config/dotnet-tools.json`
  does not exist in the repo).
- `mssql/server` as a GitHub Actions service container on `ubuntu-latest` is
  a standard, well-documented pattern; the hosted runner's 16GB RAM / 4 vCPU
  comfortably exceeds the image's ~2GB/2-core minimum.

## Desired End State

A new CI job runs a migration dry-run against a real, disposable SQL Server
container on every `pull_request` and every `push` to `main`. The dry-run
applies the shipped migration from empty and asserts the exact schema
objects the application relies on exist afterward. If the migration fails or
an expected object is missing, the job fails and hard-blocks both PR merge
(via the existing branch's required-check expectation) and the `deploy` job
(via a `needs` dependency) — there is no override path. Locally, in the
absence of the CI-provided connection string, the test no-ops (passes
without asserting) so `dotnet test` continues to work unchanged for
developers without a local SQL Server instance.

### Key Discoveries:

- `Program.cs:82-89`'s own code comment is the authoritative statement of
  why this gap exists — the implementer should read it before touching the
  gate, since it explains the precise Testing-environment behavior the gate
  does **not** change (the Testing environment keeps using
  `EnsureCreatedAsync()`; this gate runs as a separate, dedicated CI job/test
  invocation with its own connection string, not a modification to
  `Program.cs`'s startup branching).
- `context/changes/deployment-skeleton/plan.md:128-130,382` shows the SQL
  grant for the managed identity is still an open, unchecked item in a
  separate change. This plan's gate validates the migration's SQL
  correctness only; it does not touch, fix, or depend on that grant.

## What We're NOT Doing

- Not fixing or completing `deployment-skeleton`'s unchecked SQL grant for
  the managed identity — that remains that change's own scope.
- Not adding a `dotnet-ef` CLI tool manifest or using `dotnet ef database
  update` — the dry-run goes through `Database.MigrateAsync()` directly
  (the exact method the production app calls), avoiding a second migration
  mechanism to keep in sync.
- Not changing `Program.cs`'s existing `Testing`-environment
  `EnsureCreatedAsync()` branch — Sqlite/InMemory-based tests keep working
  exactly as they do today.
- Not adding a new standalone test project — the new test lives in the
  existing `tests/melody.Tests` project.
- Not asserting on every column/type in the schema — only the three
  categories of SQL-Server-specific objects the application's correctness
  depends on (unique indexes, the filtered index, foreign keys).
- Not running the gate on a schedule or as an optional/advisory check — it
  is a required job on every PR and push to `main`, same trigger surface as
  `build-and-test`.

## Implementation Approach

Two phases: first author the dry-run test itself (so it can be validated
locally against any ad-hoc SQL Server instance before wiring CI), then wire
a new CI job that provisions the disposable SQL Server service container,
runs only that test, and gates `deploy` on its success.

## Phase 1: Migration dry-run test

### Overview

Add a new xUnit test class that builds a standalone `ApplicationDbContext`
against `UseSqlServer`, applies the shipped migration via
`Database.MigrateAsync()`, and asserts the resulting schema contains the
exact objects the application depends on. The test reads its connection
string from an environment variable and no-ops when it is absent, so local
`dotnet test` runs are unaffected without a local SQL Server instance.

### Changes Required:

#### 1. New migration dry-run test

**File**: `tests/melody.Tests/MigrationSafetyGateTests.cs`

**Intent**: Prove the shipped migration applies cleanly against a real SQL
Server target and produces the schema objects the application relies on,
closing the gap where `Testing`-environment runs bypass migrations entirely
via `EnsureCreatedAsync()`.

**Contract**: A single `[Fact]` (e.g. `MigrateAsync_AgainstRealSqlServer_AppliesCleanlyAndCreatesExpectedSchema`) that:
- Reads the connection string from `Environment.GetEnvironmentVariable("MIGRATION_GATE_CONNECTION_STRING")`; if null/whitespace, returns immediately (local no-op — CI always sets this).
- Builds a `ServiceProvider` with `AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString))`, following the `AdminUserSeederTests.BuildServiceProvider()` shape.
- Calls `Database.MigrateAsync()` against a fresh (empty) database — each CI run gets a clean container, so no explicit teardown/drop is required.
- Asserts the migration applied without throwing.
- Queries `INFORMATION_SCHEMA`/`sys` catalog views (or EF's raw SQL execution) to assert: `IX_Reservations_RoomId_DayOfWeek_StartTime` exists and `is_unique = 1`; `RoleNameIndex` exists on `AspNetRoles.NormalizedName`; the three `FK_Reservations_*` foreign keys exist.
- Wraps the whole body so any assertion failure reports which specific object was missing (not just "the test failed") — satisfies the Risk #3 anti-pattern of "one large integration test that confirms it works without pinpointing which piece regressed."

### Success Criteria:

#### Automated Verification:

- New test compiles and is discoverable: `dotnet build melodybooker.sln --configuration Release`
- Test no-ops cleanly without the env var set (existing local/CI behavior unaffected): `dotnet test melodybooker.sln --configuration Release --filter FullyQualifiedName~MigrationSafetyGateTests`
- Full suite still passes unaffected: `dotnet test melodybooker.sln --configuration Release`

#### Manual Verification:

- Run the test locally against an ad-hoc `docker run mcr.microsoft.com/mssql/server` container with `MIGRATION_GATE_CONNECTION_STRING` set, confirm it passes against a real instance and fails with a clear message if a migration file is reverted/corrupted on purpose.

---

## Phase 2: CI wiring — service container and gating

### Overview

Add a new `migration-safety-gate` job to `deploy.yml` that provisions a
disposable `mssql/server` service container, sets the connection-string
environment variable, and runs only the Phase 1 test. The existing `deploy`
job gets this new job added to its `needs` list so a migration failure
blocks deploys the same way it already blocks `build-and-test` failures.

### Changes Required:

#### 1. New CI job

**File**: `.github/workflows/deploy.yml`

**Intent**: Run the Phase 1 dry-run test against a real SQL Server instance
on every PR and every push to `main`, and make that result a hard
prerequisite for deploying.

**Contract**: A new top-level job alongside `build-and-test`:
```yaml
  migration-safety-gate:
    runs-on: ubuntu-latest
    services:
      mssql:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          SA_PASSWORD: ${{ secrets.MIGRATION_GATE_SA_PASSWORD }}
          ACCEPT_EULA: "Y"
        ports:
          - 1433:1433
        options: >-
          --health-cmd "exit 0"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Migration dry-run against SQL Server
        env:
          MIGRATION_GATE_CONNECTION_STRING: "Server=localhost,1433;Database=melodybooker_migration_gate;User Id=sa;Password=${{ secrets.MIGRATION_GATE_SA_PASSWORD }};TrustServerCertificate=True"
        run: dotnet test melodybooker.sln --configuration Release --filter FullyQualifiedName~MigrationSafetyGateTests
```
and the `deploy` job's `needs:` updated from `needs: build-and-test` to
`needs: [build-and-test, migration-safety-gate]`. `MIGRATION_GATE_SA_PASSWORD`
is a new repository secret (the implementer provisions its value out of
band; it is not committed).

### Success Criteria:

#### Automated Verification:

- `deploy.yml` is valid YAML and the new job/dependency parse correctly (CI run itself is the validation — no local YAML linter exists in this repo today).
- On push to a branch/PR, the `migration-safety-gate` job appears in the Actions run and passes.

#### Manual Verification:

- Confirm in the GitHub Actions UI that `deploy` only starts after both `build-and-test` and `migration-safety-gate` succeed on a push to `main`.
- Temporarily break the migration (e.g. rename an index in a scratch commit) in a throwaway branch/PR to confirm the gate actually fails red, then revert — proves the gate isn't silently green.

---

## Testing Strategy

### Unit Tests:

- `MigrationSafetyGateTests` is itself the primary artifact of this change; no other unit tests are affected.

### Integration Tests:

- The CI-only run against the real `mssql/server` service container (Phase 2) is the integration layer — this is a process/CI-gate, not a classic unit test, per the Risk #3 response intent.

### Manual Testing Steps:

1. Run the Phase 1 test locally against an ad-hoc Docker SQL Server container to confirm pass/fail behavior before pushing.
2. Open a throwaway PR to observe the new `migration-safety-gate` job run green in GitHub Actions.
3. Break the migration intentionally in a scratch branch to confirm the gate fails and blocks `deploy`'s `needs`, then revert.

## References

- Related research: `context/changes/testing-deployment-migration-safety-gate/research.md`
- Prior deferred decision: `context/changes/deployment-skeleton/plan.md:128-130,382`
- Migration file: `src/melody/Data/Migrations/20261005143616_InitialCreate.cs:233-296`
- Testing-environment bypass: `src/melody/Program.cs:77-98`
- Standalone DbContext test pattern: `tests/melody.Tests/AdminUserSeederTests.cs:15-26`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Migration dry-run test

#### Automated

- [x] 1.1 New test compiles and is discoverable — 20e5c2c
- [x] 1.2 Test no-ops cleanly without the env var set — 20e5c2c
- [x] 1.3 Full suite still passes unaffected — 20e5c2c

#### Manual

- [x] 1.4 Run locally against ad-hoc SQL Server container, confirm pass/fail behavior — 20e5c2c

### Phase 2: CI wiring — service container and gating

#### Automated

- [x] 2.1 deploy.yml is valid YAML and new job/dependency parse correctly
- [ ] 2.2 migration-safety-gate job appears and passes on push/PR

#### Manual

- [ ] 2.3 Confirm deploy only starts after both jobs succeed on push to main
- [ ] 2.4 Temporarily break the migration to confirm the gate fails red, then revert
