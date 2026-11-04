# Booking-Conflict Correctness (Rollout Phase 1) Implementation Plan

## Overview

Close the one unverified correctness gap in the double-booking hard-block
(FR-008/FR-010) identified by `research.md`: the negative-space case of a
*different* `DayOfWeek` at the same room/time, which no existing test proves is
correctly **allowed**. Document the one genuine, pre-existing risk research
surfaced — a check-then-insert race for partial-overlap conflicts — as an
explicitly accepted risk rather than attempting to test or fix it this phase.

## Current State Analysis

- The boundary rule (`end == start` allowed) and the exact-match /
  partial-overlap conflict paths are already covered end-to-end with
  persisted-DB-state assertions in `tests/melody.Tests/TeacherBooksRoomTests.cs`
  (`:34-128`).
- No existing test proves the same room/time slot on a **different**
  `DayOfWeek` is allowed — all four existing conflict-focused tests use a
  single, distinct `DayOfWeek` each (Monday/Tuesday/Wednesday/Thursday), so
  none of them actually exercises two bookings sharing a room+time but
  differing only in `DayOfWeek`.
- A genuine TOCTOU race exists for partial-overlap conflicts (different start
  times, overlapping ranges): protected only by the controller's in-memory
  check-then-insert (`TeacherReservationsController.cs:81-92` then `:123-133`),
  with no DB-level backstop (the unique index at
  `ApplicationDbContext.cs:53-56` only catches exact-match `StartTime`
  repeats). This was a deliberate, documented design tradeoff from the
  original slice plan (`context/archive/2026-10-19-teacher-books-room/plan.md:10`),
  not an oversight.

## Desired End State

A new integration test proves that booking the same room at the same time on
a different day of the week succeeds and both rows persist independently
(no false-positive conflict). `plan.md` (this file) records the
partial-overlap race as an accepted, documented risk so future readers of
`test-plan.md` and this change folder understand it was evaluated and
consciously deferred, not missed.

### Key Discoveries:

- `Reservation.Overlaps` and the controller's conflict query are both scoped
  by `RoomId` **and** `DayOfWeek` together
  (`TeacherReservationsController.cs:81-83`), so a different `DayOfWeek` is
  never even loaded into the in-memory overlap check — confirming the
  negative-space case should pass today, but it is currently unverified by
  any test.
- `SeedRoomAsync()` and `SeedTeacherWithStudentAsync()` in
  `TeacherBooksRoomTests.cs` are reusable per-test seed helpers; the new test
  follows the same two-POST, DB-row-count-assertion pattern as
  `Create_BackToBackStartTime_NoGapFromExistingBooking_Succeeds`.

## What We're NOT Doing

- **Not** adding a production-code fix (e.g. a DB-level constraint, a
  transaction/locking mechanism, or an application-level mutex) for the
  partial-overlap concurrency race. Confirmed with the user: document as an
  accepted risk only.
- **Not** adding a DB-level test with two `DbContext` instances proving the
  unique index alone rejects a concurrent exact-duplicate insert. Confirmed
  with the user: skip — existing sequential integration coverage of the
  exact-match case is considered sufficient.
- **Not** attempting to simulate true concurrent HTTP requests against the
  test harness's single shared SQLite in-memory connection
  (`MelodyWebApplicationFactory.cs:32-38`), since SQLite serializes commands
  on one connection and a naive `Task.WhenAll` test would not faithfully
  reproduce the race it's meant to prove.

## Implementation Approach

Add one integration test to the existing `TeacherBooksRoomTests.cs` suite,
reusing its established seed-helper and DB-row-count-assertion conventions.
No production code changes. Record the accepted race risk directly in this
plan (this section and the Testing Strategy section below) so it's visible
without needing to re-read `research.md`.

## Phase 1: Cross-day-of-week regression test

### Overview

Add an integration test proving that booking the same room at the same start
time on a **different** `DayOfWeek` from an existing booking is allowed —
closing the one negative-space gap in the conflict-detection test coverage.

### Changes Required:

#### 1. New regression test

**File**: `tests/melody.Tests/TeacherBooksRoomTests.cs`

**Intent**: Prove the room/time conflict check is correctly scoped by
`DayOfWeek` — the same room+time slot on a different day must never be
treated as a conflict. Follows the existing pattern: seed a teacher+student,
POST a first booking, POST a second booking for the same room and
`StartMinutes` but a different `DayOfWeek`, then assert via DB row count that
both reservations persisted independently.

**Contract**: New `[Fact]` method
`Create_SameRoomAndTime_DifferentDayOfWeek_Succeeds`, placed after
`Create_BackToBackStartTime_NoGapFromExistingBooking_Succeeds` (around line
128), following the same signature/structure as its neighbors (two
sequential `PostCreateAsync` calls via `SeedTeacherWithStudentAsync` +
`SeedRoomAsync`, then a `context.Reservations.CountAsync` assertion — this
time filtered by `RoomId` only, asserting a count of 2 across both days,
plus an exact per-day count of 1 each).

### Success Criteria:

#### Automated Verification:

- New test passes: `dotnet test --filter FullyQualifiedName~TeacherBooksRoomTests.Create_SameRoomAndTime_DifferentDayOfWeek_Succeeds`
- Full test suite still passes (no regressions): `dotnet test`

#### Manual Verification:

- None required — this is a pure test-only change with no user-facing or
  production-code impact.

---

## Testing Strategy

### Integration Tests:

- `Create_SameRoomAndTime_DifferentDayOfWeek_Succeeds` (new): same room,
  same `StartMinutes`, two different `DayOfWeek` values — both bookings must
  succeed and persist.

### Accepted Risk (not tested this phase):

- **Partial-overlap concurrency race**: two concurrent requests booking
  overlapping-but-different-start-time slots in the same room/day are
  protected only by the controller's in-memory check-then-insert
  (`TeacherReservationsController.cs:81-92`, `:123-133`); there is no DB-level
  constraint backstop for this case (only the exact-match case is protected
  by the unique index at `ApplicationDbContext.cs:53-56`). This is a
  pre-existing, deliberate design tradeoff from the original booking slice,
  re-confirmed and explicitly accepted during this planning session rather
  than fixed or tested, because: (a) fixing it requires a production-code
  change (e.g. a broader uniqueness constraint or transactional locking)
  that's out of scope for a test-rollout phase, and (b) the existing test
  harness's single shared SQLite connection cannot faithfully reproduce true
  concurrent interleaving to prove a fix anyway. Revisit if this becomes a
  reported production issue or if the test harness changes to support
  per-request connections.

## References

- Related research: `context/changes/testing-booking-conflict-correctness/research.md`
- Existing conflict tests: `tests/melody.Tests/TeacherBooksRoomTests.cs:54-128`
- Original design tradeoff: `context/archive/2026-10-19-teacher-books-room/plan.md:10`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Cross-day-of-week regression test

#### Automated

- [x] 1.1 New test passes: `dotnet test --filter FullyQualifiedName~TeacherBooksRoomTests.Create_SameRoomAndTime_DifferentDayOfWeek_Succeeds` — 9706864
- [x] 1.2 Full test suite still passes (no regressions): `dotnet test` — 9706864
