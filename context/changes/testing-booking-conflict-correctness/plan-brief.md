# Booking-Conflict Correctness — Plan Brief

> Full plan: `context/changes/testing-booking-conflict-correctness/plan.md`
> Research: `context/changes/testing-booking-conflict-correctness/research.md`

## What & Why

Rollout Phase 1 of `test-plan.md` targets Risk #1 — double-booking the same
room/time slot. Research found the hard-block is already thoroughly tested
for sequential conflicts (exact-match, partial-overlap, back-to-back), but no
test proves the negative-space case: the same room/time on a *different* day
of week must be allowed, not blocked.

## Starting Point

`tests/melody.Tests/TeacherBooksRoomTests.cs` already has DB-row-count-verified
integration tests for exact-duplicate rejection, partial-overlap rejection, and
back-to-back success (`:54-128`). Each uses a distinct `DayOfWeek`, so none
actually proves conflict detection is correctly scoped *by* day of week.

## Desired End State

A new integration test confirms booking the same room/time on a different day
succeeds and both reservations persist independently. The known
partial-overlap concurrency race is explicitly documented as an accepted risk
in the plan, so it's visible to future readers without re-deriving it from
research.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Partial-overlap race gap | Document as accepted risk, no code fix | Fixing it needs a production-code change out of scope for a test-rollout phase, and the test harness's shared SQLite connection can't faithfully prove a concurrent fix anyway | Plan |
| DB-level unique-index test | Skip | Existing sequential integration coverage of the exact-match case is sufficient | Plan |
| Cross-day-of-week test | Add to Phase 1 | Closes the one real, cheap-to-close negative-space gap in existing coverage | Plan |

## Scope

**In scope:**
- One new integration test: same room/time, different `DayOfWeek`, both
  bookings succeed.
- Documenting the partial-overlap race as an accepted, explicit risk.

**Out of scope:**
- Any production-code change to booking/conflict logic.
- A DB-level test isolating the unique index from the controller.
- Simulating true concurrent requests against the test harness.

## Architecture / Approach

Single new `[Fact]` in the existing integration suite, following its
established seed-helper + DB-row-count-assertion pattern. No new
infrastructure, no production code touched.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Cross-day-of-week regression test | New passing integration test; full suite green | None — pure test addition |

**Prerequisites:** None — test infra and seed helpers already exist.
**Estimated effort:** ~15–30 minutes, single phase.

## Open Risks & Assumptions

- Partial-overlap concurrency race remains unfixed and untested by design
  (see plan's "Accepted Risk" section) — revisit only if it surfaces as a
  real production incident.

## Success Criteria (Summary)

- `dotnet test --filter FullyQualifiedName~TeacherBooksRoomTests.Create_SameRoomAndTime_DifferentDayOfWeek_Succeeds` passes.
- Full `dotnet test` suite passes with no regressions.
