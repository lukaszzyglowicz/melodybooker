# Deployment & Migration Safety Gate — Plan Brief

> Full plan: `context/changes/testing-deployment-migration-safety-gate/plan.md`
> Research: `context/changes/testing-deployment-migration-safety-gate/research.md`

## What & Why

The shipped EF Core migration is raw SQL authored for SQL Server, but the
`Testing`-environment code path (`Program.cs:77-98`) bypasses it entirely via
`EnsureCreatedAsync()` because Sqlite/InMemory can't run it. That means **no
test, CI job, or local run has ever executed the actual migration** — not a
provider-divergence risk, but a complete coverage gap. This plan adds a CI
gate that proves the migration applies cleanly against a real, disposable
SQL Server and produces the exact schema the app depends on.

## Starting Point

`deploy.yml` has two jobs (`build-and-test`, `deploy`) on `ubuntu-latest`,
neither touching SQL Server. The one migration file
(`20261005143616_InitialCreate.cs`) defines FK constraints, a filtered
unique index, and the unique index backing the double-booking invariant —
none of which any test currently verifies post-migration. A separate,
unfinished change (`deployment-skeleton`) already identified and explicitly
deferred this exact gap.

## Desired End State

Every PR and every push to `main` runs a migration dry-run against a real
SQL Server container. If the migration fails, or an expected index/FK is
missing, the gate fails and hard-blocks both the PR and the `deploy` job —
with no override. Local `dotnet test` runs are unaffected.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Scope boundary vs. deployment-skeleton | Gate only; do not fix the SQL grant | Keeps this change's blast radius to verification, leaving the unrelated grant to its own change | Plan |
| Dry-run mechanism | Integration test calling `Database.MigrateAsync()` | Reuses the exact method production calls, no second CLI-based migration path to keep in sync | Plan |
| Pipeline placement | New job, runs on both PR and push | Catches a broken migration before merge, not just before deploy | Plan |
| Failure handling | Hard block (no override) | Matches Risk #3's "no override" framing already used for double-booking in Phase 1 | Plan |
| Schema verification depth | Migration applies + key constraints asserted | Pinpoints which specific object regressed instead of one opaque pass/fail | Plan |
| Test location | Existing `tests/melody.Tests` project | SqlServer package already flows transitively via ProjectReference — no new project or package needed | Plan |

## Scope

**In scope:**
- New dry-run test asserting migration success + key schema objects (unique indexes, filtered index, FKs)
- New CI job with an `mssql/server` service container
- Gating `deploy` on the new job via `needs`

**Out of scope:**
- Fixing `deployment-skeleton`'s unchecked SQL grant for the managed identity
- `dotnet-ef` CLI / tool manifest
- Changing the `Testing`-environment `EnsureCreatedAsync()` bypass
- Asserting every column/type in the schema

## Architecture / Approach

Phase 1 adds a self-contained xUnit test that builds its own
`ApplicationDbContext` (same pattern as `AdminUserSeederTests`), pointed at
a connection string from an environment variable, and no-ops locally when
that variable is absent. Phase 2 adds a CI job that provisions a disposable
SQL Server via GitHub Actions service containers (a standard, well-resourced
pattern on `ubuntu-latest`), sets that variable, runs only this test, and
adds itself to `deploy`'s `needs` list.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Migration dry-run test | New test proving migration + schema correctness, local no-op without env var | Asserting the wrong/incomplete set of schema objects |
| 2. CI wiring | New `migration-safety-gate` job, gates `deploy` | Secret/connection-string misconfiguration causing false negatives |

**Prerequisites:** A new `MIGRATION_GATE_SA_PASSWORD` repository secret must be provisioned before Phase 2's CI job can run green.
**Estimated effort:** ~1-2 sessions across 2 phases.

## Open Risks & Assumptions

- Assumes GitHub-hosted `ubuntu-latest` runners can pull and start `mcr.microsoft.com/mssql/server:2022-latest` without additional allowlisting — a standard, widely-used pattern, but not previously used in this repo's CI.
- The new repository secret (`MIGRATION_GATE_SA_PASSWORD`) must be added by someone with repo admin access before Phase 2 can be verified end-to-end in CI.

## Success Criteria (Summary)

- A broken migration (wrong index, missing FK, bad SQL) fails the gate and blocks both merge and deploy — proven by an intentional break-then-revert test.
- A correct migration passes the gate on every PR and push to `main` without manual intervention.
- Local `dotnet test` behavior is unchanged for developers without a local SQL Server instance.
