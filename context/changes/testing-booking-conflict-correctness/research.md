---
date: 2026-11-02T20:13:59+01:00
researcher: Copilot CLI
git_commit: a94bbaf6c2cd4aa5362f5c190fa8ee526cd9d9d6
branch: m3l1
repository: lukaszzyglowicz/melodybooker
topic: "Rollout Phase 1 (test-plan.md Risk #1) — double-booking hard-block correctness at boundaries and in persisted DB state"
tags: [research, codebase, booking, reservation, overlap, concurrency]
status: complete
last_updated: 2026-11-02
last_updated_by: Copilot CLI
---

# Research: Double-booking hard-block correctness (test-plan.md Risk #1)

**Date**: 2026-11-02T20:13:59+01:00
**Researcher**: Copilot CLI
**Git Commit**: a94bbaf6c2cd4aa5362f5c190fa8ee526cd9d9d6
**Branch**: m3l1
**Repository**: lukaszzyglowicz/melodybooker

## Research Question

Ground rollout Phase 1 of `context/foundation/test-plan.md` (Risk #1: "A room/time
slot that is already booked gets booked again anyway — including the boundary case
where one lesson ends exactly when another starts"). Verify the real failure path
in code, locate existing tests, identify the cheapest useful test layer, and flag
whether the hot-spot evidence or response guidance needs correction.

## Summary

The double-booking hard-block (FR-008/FR-010) is implemented across two layers —
an in-request overlap check and a DB-level unique index — and the half-open
interval boundary rule (`end == start` is allowed, not a conflict) is already
correctly implemented and unit-tested
(`tests/melody.Tests/ReservationOverlapTests.cs`). An integration test suite
(`tests/melody.Tests/TeacherBooksRoomTests.cs`) already proves, end-to-end and with
persisted-DB-state assertions, that: an exact-duplicate slot is rejected with no
second row created, a partial overlap at a different start time is rejected with no
second row created, and a genuinely back-to-back booking (no gap) succeeds and both
rows persist.

The one gap this research identifies: the DB-level unique index only enforces
exact-match conflicts (same `RoomId`+`DayOfWeek`+`StartTime`); it provides **no**
protection against two concurrent requests for overlapping-but-different-start-time
slots (e.g. 9:00-9:40 and 9:20-10:00) — that case is guarded **only** by the
in-request `Overlaps` query-then-insert, which is a classic check-then-act race. This
was already known and documented as a limitation when the slice was first planned,
but it has not been revisited since, and no existing test exercises genuine
concurrent requests (the two "overlap" integration tests that exist are both
strictly sequential — each awaits its HTTP response before issuing the next).

## Detailed Findings

### Overlap boundary logic (pure, already covered)

- `Reservation.Overlaps(startA, endA, startB, endB)` is a half-open interval test —
  `startA < endB && startB < endA` — that returns `false` when `endA == startB`
  (back-to-back, no conflict) by construction of strict `<` comparisons
  (`src/melody/Models/Domain/Reservation.cs:43-44`).
- `tests/melody.Tests/ReservationOverlapTests.cs:1-54` unit-tests this function
  directly (no DB/host) across 5 cases: exact-match ranges (true), overlapping with
  a different start (true), back-to-back ranges (false), disjoint ranges (false),
  and full containment (true). This fully covers the pure boundary logic in
  isolation.

### End-to-end enforcement path (controller + DB)

- `TeacherReservationsController.Create` (POST,
  `src/melody/Controllers/TeacherReservationsController.cs:56-143`) computes
  `startTime`/`endTime` from the posted form, loads all existing reservations for
  the same `RoomId`+`DayOfWeek` (`:81-83`), and filters them in memory with
  `Reservation.Overlaps` (`:85-86`). If any match, it adds a model error and
  re-renders without saving (`:88-92`) — this is the primary guard and the one that
  actually implements FR-010's "overlapping at a different start time" case.
- `ApplicationDbContext.OnModelCreating`
  (`src/melody/Data/ApplicationDbContext.cs:53-56`) declares a unique index on
  `(RoomId, DayOfWeek, StartTime)` — comment explicitly scopes it: "hard-block
  double-booking the same room at the same weekly time slot". This index can only
  reject an **exact** `StartTime` repeat; it cannot see or reject a differently
  start-timed row whose range overlaps an existing one.
- The controller wraps `SaveChangesAsync()` in a `try/catch (DbUpdateException)`
  (`src/melody/Controllers/TeacherReservationsController.cs:130-141`) with a comment
  calling this "Unique-index race (FR-010): another booking for the same
  room/day/time was saved concurrently." This catch only fires for the exact-match
  case the unique index actually covers — it is not a backstop for the
  partial-overlap, different-start-time case, because no DB constraint exists for
  that case at all.
- This scope split — unique index covers exact-match only, application layer must
  cover partial overlap — was explicitly called out as a known design decision (not
  an oversight) when the slice was originally planned: "It does **not** catch two
  reservations in the same room/day whose time ranges overlap at different start
  times... the application layer must add that check"
  (`context/archive/2026-10-19-teacher-books-room/plan.md:10`). The plan did not
  propose, and the current code does not have, any DB-level or transactional guard
  for that case — the in-memory check-then-insert is the only protection.

### Existing integration coverage (sequential only)

`tests/melody.Tests/TeacherBooksRoomTests.cs` is a `WebApplicationFactory`-based
integration suite that already covers, with persisted-row-count assertions against
the real `ApplicationDbContext`:

- `Create_SuccessfulBooking_CreatesReservationWithStudentsLessonDuration` (:34-52) —
  happy path, duration derived correctly.
- `Create_ExactSameRoomDayTimeTwice_SecondAttemptIsRejected_NoSecondRowCreated`
  (:54-76) — exact-match duplicate rejected, DB row count stays 1.
- `Create_OverlappingDifferentStartTime_SameRoomAndDay_IsRejected` (:78-103) —
  partial overlap (9:00-9:40 existing vs. a 9:20 start) rejected, DB row count
  stays 1.
- `Create_BackToBackStartTime_NoGapFromExistingBooking_Succeeds` (:105-128) — a
  booking starting exactly when the prior one ends succeeds, DB row count becomes
  2 — this is the boundary case named in Risk #1 and it is already covered,
  end-to-end, with DB-state verification, not just an HTTP-status assertion.
- Ownership/role-boundary tests (:130-180) are out of scope for Risk #1 (they map
  to Risk #2, already tracked separately in `test-plan.md`).

**Gap**: every test above issues one HTTP request, awaits the response, and only
then issues the next (e.g. `:61-66`, `:84-91`). None exercises two requests racing
concurrently against the same room/day/time window. The query-then-insert logic in
the controller (`:81-92` then `:123-133`) is therefore unverified under true
concurrency for the partial-overlap case — only the exact-match case has any
guard at all under concurrency (the DB unique index), and even that path has no
test proving the `DbUpdateException` catch actually fires under a real race rather
than just being exercised via the sequential "second attempt" test above (which
never reaches the `catch` block, since the in-memory check already rejects it
before `SaveChangesAsync` is called).

### Test harness detail relevant to planning a concurrency test

`tests/melody.Tests/MelodyWebApplicationFactory.cs:32-38` keeps a single, shared
`SqliteConnection` to `DataSource=:memory:` open for the lifetime of the factory,
and `:58-69` configures every `ApplicationDbContext` instance (one per DI scope) to
use that one connection via `UseSqlite(_connection)`. A concurrency test built on
top of this factory sends genuinely parallel requests through one underlying ADO.NET
connection; SQLite serializes command execution on a single connection, so a
naively parallel test (`Task.WhenAll` of two `HttpClient.PostAsync` calls) may
serialize away the very race it is meant to prove, rather than reproducing the
interleaving that would occur against the real SQL Server target in production
(confirmed by inspection of the factory only — not yet proven by running a test;
this is an open question for `/10x-plan` to resolve, e.g. via a semaphore-released
barrier inside a custom test hook, or by accepting that this layer cannot
faithfully prove the race and treating it as a documented limitation instead).

## Code References

- `src/melody/Models/Domain/Reservation.cs:43-44` - `Overlaps` half-open interval
  check (boundary rule)
- `src/melody/Controllers/TeacherReservationsController.cs:56-143` - `Create` POST
  action: in-memory overlap check, then insert, then `DbUpdateException` catch
- `src/melody/Data/ApplicationDbContext.cs:53-56` - unique index on
  `(RoomId, DayOfWeek, StartTime)`, exact-match only
- `tests/melody.Tests/ReservationOverlapTests.cs:1-54` - unit tests for the
  boundary rule
- `tests/melody.Tests/TeacherBooksRoomTests.cs:34-128` - sequential integration
  tests for exact-match, partial-overlap, and back-to-back cases, with
  persisted-row-count assertions
- `tests/melody.Tests/MelodyWebApplicationFactory.cs:32-69` - single shared SQLite
  in-memory connection used by every test DI scope

## Architecture Insights

- The project deliberately splits double-booking protection into two layers with
  different, non-overlapping scopes (exact-match at the DB layer, partial-overlap
  at the application layer) rather than one unified constraint — this is a known,
  documented tradeoff, not an accidental gap in the exact-match case.
- Integration tests in this codebase already follow a strong pattern of asserting
  persisted DB state (row counts via `context.Reservations.CountAsync(...)`)
  alongside HTTP-level assertions, rather than trusting the HTTP response alone —
  this matches exactly what Risk #1's response guidance asks for ("verify DB
  state, not just the HTTP response") and gives Phase 1 a reference pattern to
  reuse rather than invent.

## Historical Context (from prior changes)

- `context/archive/2026-10-19-teacher-books-room/plan.md:10` - original design
  decision explicitly scoping the unique index to exact-match only and assigning
  partial-overlap protection to the application layer; this is the source of the
  concurrency gap this research surfaces, not a new design proposal.

## Related Research

None — this is the first research document for `testing-booking-conflict-correctness`.

## Open Questions

- Can a faithful concurrent-request test be built against the shared single-SQLite-connection
  test harness, or does `/10x-plan` need to either (a) add a synchronization hook to
  force two requests to interleave at the check-then-insert boundary, or (b)
  accept that true concurrency cannot be proven at this layer and document the
  partial-overlap race as an accepted risk instead of a tested one. This is the
  one decision `/10x-plan` must make explicitly before writing Phase 1's test
  sub-phases.
