---
date: 2026-11-04T14:45:00+01:00
researcher: lukaszzyglowicz
git_commit: a225c40a7bff5fe571a115a084be7a7e6b1e41a8
branch: m3l2
repository: lukaszzyglowicz/melodybooker
topic: "Rollout Phase 4 — Deployment & migration safety gate (test-plan.md Risk #3)"
tags: [research, codebase, ef-core, migrations, ci-cd, deployment]
status: complete
last_updated: 2026-11-04
last_updated_by: lukaszzyglowicz
---

# Research: Deployment & migration safety gate (rollout Phase 4)

**Date**: 2026-11-04T14:45:00+01:00
**Researcher**: lukaszzyglowicz
**Git Commit**: a225c40a7bff5fe571a115a084be7a7e6b1e41a8
**Branch**: m3l2
**Repository**: lukaszzyglowicz/melodybooker

## Research Question

Ground rollout Phase 4 of `context/foundation/test-plan.md` (Risk #3: "An EF
Core migration behaves differently against the real SQL Server target than it
does under the InMemory/SQLite providers the test suite uses"). Verify the
risk response guidance, locate the real failure path, confirm or correct the
"cheapest layer" recommendation (process/CI-gate dry-run), and flag anything
speculative or already decided elsewhere.

## Summary

The risk is real and **already a live, currently-unresolved hazard**, not a
hypothetical: this exact migration-safety gap was identified and explicitly
deferred by a prior change (`deployment-skeleton`, still `status: implementing`)
rather than invented for this phase. Three independent facts compound:

1. **The EF Core migration path has never executed against any database in
   this codebase** — not SQL Server, not Sqlite, not InMemory. Every test (all
   8 `WebApplicationFactory`-based integration tests plus the one bare-DI
   `AdminUserSeederTests`) builds its schema via `EnsureCreatedAsync()` or
   `UseInMemoryDatabase(...)`, never `Database.Migrate()`
   (`src/melody/Program.cs:82-89`, `tests/melody.Tests/MelodyWebApplicationFactory.cs:57-69`,
   `tests/melody.Tests/AdminUserSeederTests.cs:16-22`). The single existing
   migration (`20261005143616_InitialCreate.cs`) is only ever applied by
   `Database.MigrateAsync()` at production startup
   (`src/melody/Program.cs:92`) — the first time it runs against a real
   database is in production, after deploy.
2. **CI has no SQL Server at all.** `.github/workflows/deploy.yml` runs
   `dotnet build` then `dotnet test` (both against the Sqlite/InMemory test
   doubles above) with no service container, no `dotnet ef database update`,
   and no migration-related step of any kind.
3. **This exact gap was already found and deliberately NOT closed** by the
   `deployment-skeleton` change (`context/changes/deployment-skeleton/plan.md:128-130`):
   *"Not changing the EF Core migration strategy — `MigrateAsync()` at Web App
   startup stays exactly as implemented today; the pipeline does not add a
   separate migration step or approval gate."* That same plan also flags a
   concrete, still-open failure mode: the Web App's managed identity may not
   yet have been granted a database user inside the live `melodybookerdb`
   (`context/changes/deployment-skeleton/plan.md:71-75`); its own Progress
   section shows this grant step still unchecked
   (`context/changes/deployment-skeleton/plan.md:382`, `- [ ] 1.2 SQL grant
   script run once against melodybookerdb`). If that grant is missing,
   `MigrateAsync()` throws at startup and the Web App crash-loops — exactly
   the "migration behaves differently against the real target" failure Risk
   #3 describes, except the mechanism research found is broader than a
   schema/constraint mismatch: it's "the migration code path is completely
   unexercised before it reaches production, for *any* reason" (permissions,
   SQL-Server-only syntax, or environment drift).

Confirmed: the test-plan's cheapest-layer recommendation — **process/CI-gate
migration dry-run against a disposable SQL Server instance** — is correct and
is the *only* layer that can catch this; no unit or InMemory/Sqlite-backed
integration test can, because none of them ever invoke `Migrate()` against a
SQL-Server-compatible schema (the migration includes SQL Server-specific DDL:
a filtered unique index and multiple FK constraints — see Detailed Findings).

**Correction to the risk's framing**: §2 says "than it does under the
InMemory/SQLite providers the test suite uses," which could be read as "the
migration runs on Sqlite but might behave differently on SQL Server." The
more precise, grounded statement is: **the migration doesn't run under any
test provider at all** — tests bypass it entirely via `EnsureCreatedAsync()`/
`UseInMemoryDatabase()`. This is a stronger case for the gate, not a weaker
one: zero existing coverage, not "different coverage."

## Detailed Findings

### EF Core setup

- `ApplicationDbContext` — `src/melody/Data/ApplicationDbContext.cs:7`
  (namespace `melody.Data`), with `DbSet<Teacher> Teachers` (`:13`),
  `DbSet<Student> Students` (`:14`), `DbSet<Room> Rooms` (`:15`),
  `DbSet<Reservation> Reservations` (`:16`). It also derives from
  `IdentityDbContext` (via `.AddEntityFrameworkStores<ApplicationDbContext>()`
  at `Program.cs:21`), so ASP.NET Core Identity's own tables/migrations are
  part of the same migration set.
- Registration, SQL Server only, no conditional provider selection:
  `src/melody/Program.cs:12-13`.
  ```csharp
  builder.Services.AddDbContext<ApplicationDbContext>(options =>
      options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
  ```
- Connection strings: `appsettings.json:9-11` ships an empty
  `"DefaultConnection": ""`; `appsettings.Development.json:8-10` has a
  LocalDB connection string (`Server=(localdb)\MSSQLLocalDB;...`). No
  `appsettings.Production.json` exists — production config comes from Azure
  App Service Application Settings (per `infrastructure.md`'s "Secrets"
  section), not a checked-in file.
- Provider packages: `src/melody/melody.csproj:19` references
  `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12 (production only).
  `tests/melody.Tests/melody.Tests.csproj:13-14` references
  `Microsoft.EntityFrameworkCore.InMemory` 10.0.12 and
  `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 (tests only). No project
  references all three, and no test ever constructs a `UseSqlServer`-backed
  context, including against LocalDB.

### Migration content (SQL-Server-specific DDL that an alternate provider cannot meaningfully validate)

- Migration folder: `src/melody/Data/Migrations/` contains
  `20261005143616_InitialCreate.cs`, its `.Designer.cs`, and
  `ApplicationDbContextModelSnapshot.cs` — a single migration, no
  `migrationBuilder.Sql`/`InsertData`/`UpdateData`/`DeleteData` calls (no
  raw SQL or seed data inside the migration itself; seeding happens
  separately via `RoleSeeder`/`AdminUserSeeder`/`RoomSeeder` at
  `Program.cs:95-97`).
- Foreign-key constraints: `src/melody/Data/Migrations/20261005143616_InitialCreate.cs:233-250`.
- A filtered unique index (Identity's `NormalizedName` column, SQL-Server
  `WHERE` filter syntax): `src/melody/Data/Migrations/20261005143616_InitialCreate.cs:258-263`.
- A unique composite index enforcing no double-booking at the schema level:
  `IX_Reservations_RoomId_DayOfWeek_StartTime`,
  `src/melody/Data/Migrations/20261005143616_InitialCreate.cs:292-296` — this
  is a second, DB-level enforcement of the same invariant rollout Phase 1
  (Risk #1) already covers in application code and persisted-state tests;
  relevant context if Phase 4's gate also wants to confirm this index exists
  post-migration on the real provider.

### Startup migration application — the exact mechanism and its test bypass

`src/melody/Program.cs:77-98`:
```csharp
using (var scope = app.Services.CreateScope())
{
    // Phase 0: no CI/CD migration step exists yet, so the app applies pending
    // EF Core migrations itself at startup using its own managed identity.
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
    {
        // Migrations are raw SQL authored against the real (SQL Server) provider, so
        // they can't run against the SQLite in-memory database WebApplicationFactory-based
        // tests substitute in. EnsureCreatedAsync builds the schema straight from the model
        // instead, which is provider-agnostic and sufficient for a throwaway test database.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    await RoleSeeder.SeedAsync(scope.ServiceProvider);
    await AdminUserSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    await RoomSeeder.SeedAsync(scope.ServiceProvider);
}
```
The code's own comment (`:84-87`) already documents the provider-divergence
risk precisely: migrations are SQL-Server-specific and cannot run against
Sqlite, so `EnsureCreatedAsync()` is substituted for tests — confirming, in
the implementation's own words, that the migration path is untested by
design, not by oversight. This is the single most load-bearing anchor for
Phase 4: whatever test/gate this phase adds must exercise `MigrateAsync()`
itself (or an `dotnet ef database update`/`migrations script` equivalent)
against a real SQL-Server-compatible target, because nothing in the existing
suite does.

### Test provider landscape (all 9 DB-touching test files)

- Shared `WebApplicationFactory`-based fixture:
  `tests/melody.Tests/MelodyWebApplicationFactory.cs:41-45` sets
  `builder.UseEnvironment("Testing")`; `:57-69` removes the production
  `DbContextOptions<ApplicationDbContext>` registration and replaces it with
  `options.UseSqlite(_connection)` (an in-memory Sqlite connection). Used by
  8 test files: `AdminRosterAuthorizationTests.cs`,
  `AdminRosterDeletionTests.cs`, `AdminReservationOversightTests.cs`,
  `AuthorizationTests.cs`, `SpecialistInstrumentWarningTests.cs`,
  `RoomSeederTests.cs`, `TeacherStudentListTests.cs`,
  `TeacherBooksRoomTests.cs`.
- Standalone pattern: `tests/melody.Tests/AdminUserSeederTests.cs:16-22`
  builds a bare `ServiceProvider` with
  `options.UseInMemoryDatabase(Guid.NewGuid().ToString())` — no web host, no
  `Testing` environment switch (not subject to the `MigrateAsync()`/
  `EnsureCreatedAsync()` branch at all, since it never goes through
  `Program.cs`).
- Pure-unit files (`InstrumentMismatchTests.cs`, and this session's new
  `StudentLessonDurationTests.cs`/`StudentFormValidationTests.cs`) use no
  database.
- No test anywhere in the repo uses `UseSqlServer`, including against
  LocalDB — confirmed via a targeted search across `tests/melody.Tests/`.

### CI/CD pipeline — no SQL Server, no migration step

`.github/workflows/deploy.yml`:
- Triggers on PR and push to `main` (`:15-18`).
- `build-and-test` job: checkout (`:29`) → `actions/setup-dotnet@v4` (`:31-34`)
  → `dotnet build melodybooker.sln --configuration Release` (`:36-37`) →
  `dotnet test melodybooker.sln --configuration Release --no-build`
  (`:39-40`, whole solution, no filter). No `services:` block anywhere in the
  workflow — no `mcr.microsoft.com/mssql/server` container, no SQL Server of
  any kind available in CI today.
- Deploy job (push-to-`main` only, after `build-and-test` succeeds,
  `:43-46`): `dotnet publish` (`:56-57`) → zip (`:59-62`) →
  `azure/webapps-deploy@v3` using `secrets.AZURE_WEBAPP_PUBLISH_PROFILE`
  (`:64-69`) → `curl` smoke test against `/health` (`:71-75`).
- No `dotnet ef database update`, no `dotnet ef migrations script`, and no
  other EF CLI invocation anywhere in either workflow file
  (`deploy.yml`, `sync-roadmap-issues.yml`).
- The only secrets referenced are `AZURE_WEBAPP_NAME`,
  `AZURE_WEBAPP_PUBLISH_PROFILE`, and (unrelated) `GITHUB_TOKEN` for the
  roadmap-sync workflow — nothing implies a SQL Server connection string is
  available to CI.

## Code References

- `src/melody/Data/ApplicationDbContext.cs:7-16` — DbContext + DbSets.
- `src/melody/Program.cs:12-13` — SQL-Server-only `AddDbContext` registration.
- `src/melody/Program.cs:77-98` — startup migration/seed block;
  Testing-vs-production branch between `EnsureCreatedAsync()` and
  `MigrateAsync()`.
- `src/melody/Data/Migrations/20261005143616_InitialCreate.cs:233-250,258-263,292-296` —
  FK constraints, filtered unique index, unique composite reservation index.
- `tests/melody.Tests/MelodyWebApplicationFactory.cs:41-45,57-69` — shared
  Sqlite test fixture, 8 consumers.
- `tests/melody.Tests/AdminUserSeederTests.cs:16-22` — standalone InMemory
  provider pattern.
- `.github/workflows/deploy.yml:15-75` — full CI/CD pipeline, no SQL Server,
  no migration step.
- `context/foundation/infrastructure.md` — Risk Register row: *"EF Core
  migrations applied during a bad deploy are not automatically reversible |
  Operational story / Research finding | L | H | Always take an Azure SQL
  point-in-time backup checkpoint before applying migrations in production;
  write down-migrations alongside up-migrations."* This is the original
  Source citation for Risk #3 in `test-plan.md` §2.

## Architecture Insights

- The app is self-migrating: there is no separate migration-apply step
  anywhere in the deploy pipeline. `MigrateAsync()` runs in-process, in the
  same request that boots the Web App after a zip-deploy, using the Web
  App's own managed identity. A gate added by this phase therefore cannot
  assume a dedicated "migration step" exists to wrap — it either (a) adds a
  genuinely new CI step that dry-runs the migration against a disposable SQL
  Server instance *before* merge/deploy, independent of the app's own
  startup logic, or (b) changes the startup logic itself (out of scope per
  Risk #3's framing, which is about verification, not architecture change).
- The `Testing` ASP.NET Core environment name is already a meaningful,
  reserved switch point in `Program.cs:82` — any new gate that wants the app
  to actually run `MigrateAsync()` against a real SQL Server needs either a
  new environment name/flag or a way to force the `else` branch in a
  CI-only, SQL-Server-backed run.

## Historical Context (from prior changes)

- `context/changes/deployment-skeleton/plan.md:54-59,71-75,128-130` — prior
  change in the SAME rollout family (not yet complete, `status: implementing`)
  already identified this exact gap, named it a "Phase 0" stopgap in the
  code's own comment, and explicitly chose NOT to close it, deferring to
  "later" — which is this Phase 4. Its Progress section
  (`:382`) shows the one concrete manual precondition (SQL grant for the
  managed identity) still unchecked, meaning the crash-loop failure mode is
  live today, not hypothetical.
- `context/changes/azure-deployment-plan/plan.md:51-54,123-124` — an earlier,
  informal, superseded plan (predates `infra/`) already flagged the same
  open question: whether to run `dotnet ef database update` as a CI/CD step
  versus an EF Core migration bundle — "do ustalenia" (to be decided) at a
  later stage. That later stage is this phase.
- `context/foundation/infrastructure.md` Risk Register — already scored this
  risk Likelihood L / Impact H with mitigation "take an Azure SQL
  point-in-time backup checkpoint before applying migrations in production;
  write down-migrations alongside up-migrations" — a *process* mitigation,
  consistent with the test-plan's "process/CI-gate, not a classic unit test"
  framing. No down-migration exists yet for `InitialCreate` (it's the first
  migration), so that part of the original mitigation is not yet actionable
  until a second migration exists.

## Related Research

- `context/changes/testing-booking-conflict-correctness/research.md` (Phase 1)
  and `context/changes/testing-authorization-ownership-boundaries/research.md`
  (Phase 2) both used the same `MelodyWebApplicationFactory`/Sqlite fixture
  documented above — confirms this phase is the first to need a genuinely
  different (real-provider) test harness, not an extension of the existing
  one.

## Open Questions

- **Disposable SQL Server availability in CI**: GitHub Actions hosted
  runners support Docker service containers
  (`mcr.microsoft.com/mssql/server`), but this repo's workflow doesn't use
  one today and no prior research in this repo confirms the hosted runner's
  resource limits are sufficient for a SQL Server container alongside the
  existing build+test job. This is a planning-time decision (service
  container vs. ephemeral Azure SQL vs. local-only `dotnet-ef` dry-run
  against LocalDB in a Windows runner), not something further codebase
  research can resolve — flagging for `/10x-plan`.
  Alternative avoided: a LocalDB-based dry-run inside the already-Windows
  CI host was not independently investigated and is not the recommended
  layer in §2; noted only as an alternative the plan should weigh, not a
  grounded finding.
- **Interaction with `deployment-skeleton`**: that change is still
  `implementing` and owns the actual `MigrateAsync()`/managed-identity
  pathway. Phase 4's plan should state explicitly whether it only adds a
  verification gate (recommended, per Risk #3's framing) or also touches
  `deployment-skeleton`'s unfinished Phase 1/2/3 — these are two different
  change folders and should not silently merge scope.
