---
date: 2026-11-04T14:19:10+01:00
researcher: Copilot CLI
git_commit: 030b242ec662aab133a31fe9ebe8e48d18de00dd
branch: m3l2
repository: lukaszzyglowicz/melodybooker
topic: "Rollout Phase 3 grounding: reservation-form validation (Risk #4) and lesson-duration derivation (Risk #5)"
tags: [research, codebase, reservations, students, validation, duration, test-plan]
status: complete
last_updated: 2026-11-04
last_updated_by: Copilot CLI
---

# Research: Ground rollout Phase 3 of context/foundation/test-plan.md (Risks #4 and #5)

**Date**: 2026-11-04T14:19:10+01:00
**Researcher**: Copilot CLI
**Git Commit**: 030b242ec662aab133a31fe9ebe8e48d18de00dd
**Branch**: m3l2
**Repository**: lukaszzyglowicz/melodybooker

## Research Question

Ground rollout Phase 3 ("Reservation form & duration validation hardening") of
`context/foundation/test-plan.md`. Verify Risk #4 (reservation-form validation —
student class × instrument × room — silently breaks one condition while fixing
another) and Risk #5 (lesson-duration derivation mishandles the class 4/5
boundary, or a student with no assigned class) against the current code, locate
existing tests, identify the cheapest useful test layer, and flag speculative
risk/hot-spot evidence.

## Summary

- **Risk #4's premise holds only narrowly.** The reservation-form's two rules —
  instrument-mismatch warning (FR-011) and duration derivation (FR-009) — are
  **not shared/coupled code**; they are independent, adjacent reads in
  `TeacherReservationsController.Create` (`src/melody/Controllers/TeacherReservationsController.cs`).
  Instrument-mismatch itself already has near-complete coverage (5 pure unit
  facts + 6 integration facts, see below), so the realistic residual risk is
  narrower than the test-plan implies: not "instrument-mismatch is untested",
  but "no single test exercises the full class × instrument × room combination
  end-to-end to prove one rule's fix/regression doesn't silently alter the
  other's computed output".
- **Risk #5's "silent default" framing is contradicted by the inspected code
  and an empirical probe; the "4/5 boundary is untested" framing is confirmed.**
  `AdminStudentsController.Create`/`Edit` (`src/melody/Controllers/AdminStudentsController.cs:52`,
  `:97`) check `ModelState.IsValid` before persisting, and `StudentFormViewModel.Class`
  carries `[Range(1, 8)]` (`src/melody/Models/ViewModels/StudentFormViewModel.cs:24`).
  An integration probe posting to `/AdminStudents/Create` with `Class` entirely
  omitted, and separately with `Class=0`, both resulted in `ModelState.IsValid ==
  false` (HTTP 200 re-render with validation markup, no row saved) — for this
  inspected path (Admin create form POST), a missing/zero `Class` is rejected,
  not silently defaulted. However, **the 4/5 boundary and most of the 1-8 range
  are genuinely untested**: across the entire existing test suite, every
  seeded student uses `classNumber: 3`, `6`, or `7` only (`grep` across
  `tests/melody.Tests/*.cs` — no test anywhere uses class 1, 2, 4, 5, or 8, and
  `Student.LessonDuration` itself has no pure unit test). This is the real,
  confirmed gap for Risk #5.
- **A second, broader gap exists independent of both risks**: no integration
  tests exist at all for `AdminStudentsController.Create`/`Edit`'s validation
  behavior (confirmed via `glob` — no `AdminStudents*Tests.cs` file exists in
  `tests/melody.Tests/`). The Admin roster deletion/authorization tests cover
  other `AdminStudentsController` actions but not form validation.
- **The DB column has no server-side constraint on `Class`** — the EF Core
  snapshot (`src/melody/Data/Migrations/ApplicationDbContextModelSnapshot.cs:297`)
  defines `Class` as a plain `int` column with no `CHECK` constraint, so the
  `[Range(1,8)]` guard is enforced only at the MVC `ModelState` layer, not the
  database. No seeder creates `Student` rows (`grep "new Student"` across
  `src/` returned no matches), so this inspected codebase has no code path that
  bypasses the validated controller POST to write a `Student` row — the gap is
  theoretical for now, not an active hole, but worth a one-line note rather than
  a new test (no code path currently exercises it).

## Detailed Findings

### Risk #4 — reservation-form validation (class × instrument × room)

- `ReservationFormViewModel` (`src/melody/Models/ViewModels/ReservationFormViewModel.cs`)
  has only simple DataAnnotations (`[Required]` on `RoomId`/`DayOfWeek`/`StartMinutes`)
  — no `IValidatableObject` cross-field validation lives on the view model itself.
- `TeacherReservationsController.Create` (POST) reads `student.LessonDuration`
  (duration, FR-009) and separately evaluates `Reservation.IsInstrumentMismatch`
  (FR-011) to decide whether to show a soft warning vs. save — these are two
  independent reads against the same `Student`/`Room` pair, not shared logic a
  single bug could break for both. This resolves the test-plan's open question
  about whether the two rules are coupled: **they are not**.
- `Reservation.IsInstrumentMismatch` and `Reservation.Overlaps` are static pure
  functions on `src/melody/Models/Domain/Reservation.cs`.
- Existing coverage for the instrument-mismatch sub-scenario is already strong:
  - `tests/melody.Tests/InstrumentMismatchTests.cs` — 5 pure-unit `[Fact]`s
    covering every specialist-room/instrument combination, including
    `Instrument.Other`.
  - `tests/melody.Tests/SpecialistInstrumentWarningTests.cs` — 6 integration
    `[Fact]`s covering the full warn → confirm → save flow, stale-confirmation
    pinning, and non-specialist rooms.
- **No test combines a specific class (duration bucket) with a specific
  instrument/room mismatch and asserts both outputs (computed `Duration` AND
  warning-shown/hard-block) from one request.** `TeacherBooksRoomTests.cs`'s
  duration-assertion test and `SpecialistInstrumentWarningTests.cs`'s
  mismatch tests use different, non-overlapping fixtures, so a regression that
  silently makes one rule leak into the other's computed value (e.g., duration
  calculation accidentally keying off instrument instead of class) would not be
  caught by either existing file alone.

### Risk #5 — lesson-duration derivation (class boundary, missing class)

- `Student.LessonDuration` (`src/melody/Models/Domain/Student.cs:27`) is the
  sole derivation point: `Class <= 4 ? TimeSpan.FromMinutes(20) :
  TimeSpan.FromMinutes(40)`. `Class` is `[Range(1, 8)]`, `int`, non-nullable,
  not `[Required]` (redundant on a non-nullable value type, but explicitly
  absent regardless).
- `StudentFormViewModel.Class` (`src/melody/Models/ViewModels/StudentFormViewModel.cs:24`)
  carries the same `[Range(1, 8)]` and is the only field that maps to `Student.Class`
  on the Admin create/edit path.
- `AdminStudentsController.Create` (POST, `src/melody/Controllers/AdminStudentsController.cs:48-65`)
  and `Edit` (POST, `:89-115`) both check `ModelState.IsValid` before touching
  `_context.Students.Add`/mutating `student.Class`, and both re-render the form
  (no save) on failure.
- **Empirical verification** (integration probe against the real controller/view
  stack via `WebApplicationFactory`, SQLite in-memory, run then deleted — not a
  committed test): POST to `/AdminStudents/Create` with `Class` entirely absent
  from the form body → HTTP 200 (re-rendered form, not a redirect), no `Student`
  row persisted, response body contains validation-error markup. Same result
  for an explicit `Class=0`. For this inspected path, the test-plan's "silently
  defaults" premise for Risk #5 does **not** hold — the Admin form path
  explicitly rejects an invalid/missing class.
- **The actual confirmed gap is the untested range, not a silent-default bug.**
  A repo-wide `grep` for `classNumber: \d|Class = \d` across
  `tests/melody.Tests/` found every existing seeded/posted student uses class
  **3** (`TeacherBooksRoomTests.cs`, `SpecialistInstrumentWarningTests.cs`,
  `AdminReservationOversightTests.cs`, `AdminRosterDeletionTests.cs`,
  `AdminRosterAuthorizationTests.cs`), class **6** (`TeacherBooksRoomTests.cs:36`,
  `AdminRosterDeletionTests.cs:165`), or class **7**
  (`TeacherBooksRoomTests.cs:80,107,132`). No test anywhere uses class 1, 2, 4,
  5, or 8 — in particular, the class 4/5 boundary (the exact point where
  `LessonDuration` switches from 20 to 40 minutes) has zero coverage, and
  `Student.LessonDuration` has no dedicated pure-unit test at all (unlike
  `Reservation.Overlaps`/`IsInstrumentMismatch`, which do).
- **No DB-level constraint backs `[Range(1,8)]`.** The EF Core model snapshot
  (`src/melody/Data/Migrations/ApplicationDbContextModelSnapshot.cs:297-298`)
  defines `Class` as `b.Property<int>("Class").HasColumnType("int")` with no
  `CHECK` constraint — the guard exists only in MVC's `ModelState` validation.
  A `grep` for `"new Student"` across `src/` found no seeder or script that
  creates `Student` rows outside the two controller POST actions, so this is a
  latent property of the schema, not an exploitable gap in the currently
  inspected codebase — worth one sentence in a test/plan doc, not a dedicated
  test (there is no code path to exercise).

### Existing test coverage inventory (for scoping the Phase 3 plan)

| File | Scope | Relevant to |
|---|---|---|
| `InstrumentMismatchTests.cs` | 5 pure-unit facts, all instrument/room combos | Risk #4 (well covered) |
| `SpecialistInstrumentWarningTests.cs` | 6 integration facts, warn→confirm→save flow | Risk #4 (well covered) |
| `TeacherBooksRoomTests.cs` | 1 duration assertion, class 6 only | Risk #5 (narrow gap: no 20-min bucket, no boundary, no invalid class) |
| *(none)* | `AdminStudentsController.Create`/`Edit` validation | Risk #5 / broader gap (zero coverage) |
| *(none)* | `Student.LessonDuration` pure unit test | Risk #5 (zero coverage) |

## Code References

- `src/melody/Models/Domain/Student.cs:27` — `LessonDuration` computed property, the sole FR-009 derivation point.
- `src/melody/Models/ViewModels/StudentFormViewModel.cs:24` — `Class` with `[Range(1, 8)]`, no `[Required]`.
- `src/melody/Controllers/AdminStudentsController.cs:48-65` — `Create` POST, `ModelState.IsValid` gate before persistence.
- `src/melody/Controllers/AdminStudentsController.cs:89-115` — `Edit` POST, same gate.
- `src/melody/Controllers/TeacherReservationsController.cs` — adjacent, independent reads of `student.LessonDuration` and `Reservation.IsInstrumentMismatch`.
- `src/melody/Models/Domain/Reservation.cs` — `Overlaps` (FR-010) and `IsInstrumentMismatch` (FR-011) static pure functions.
- `src/melody/Data/Migrations/ApplicationDbContextModelSnapshot.cs:297-298` — `Class` column has no DB-level `CHECK` constraint.
- `tests/melody.Tests/TeacherBooksRoomTests.cs:36` — the one existing duration assertion, class 6 only.
- `tests/melody.Tests/InstrumentMismatchTests.cs`, `tests/melody.Tests/SpecialistInstrumentWarningTests.cs` — existing Risk #4 coverage.

## Architecture Insights

- Domain logic for both FR-009 and FR-011 deliberately lives as properties/static
  methods directly on domain models (`Student.LessonDuration`, `Reservation.Overlaps`,
  `Reservation.IsInstrumentMismatch`) rather than in a separate service layer —
  confirmed as an established, repeated pattern (also noted in the archived
  `2026-10-19-teacher-books-room/plan.md`). Any new pure-logic test for
  `LessonDuration` should follow the `InstrumentMismatchTests.cs` style: a plain
  `[Theory]`/`[Fact]` class with no `WebApplicationFactory`, since the property
  requires no database or HTTP context.
- All controller-level form validation in this codebase (both `AdminStudentsController`
  and, per the archived plan, `TeacherReservationsController`) follows the same
  `ModelState.IsValid` → rebuild dropdowns → re-render pattern; no custom
  model-binder or validation-attribute behavior overrides ASP.NET Core defaults
  anywhere in this codebase.

## Historical Context (from prior changes)

- `context/archive/2026-10-19-teacher-books-room/plan.md:9` — confirms
  `Student.LessonDuration` already existed and was unchanged by that slice
  ("The domain model for this slice already exists and matches the PRD
  exactly... `Student.LessonDuration`... No domain model changes are needed").
  Supported: this corroborates that the computed property has been stable
  since before the reservation-booking feature and has never had direct test
  coverage added for it in any slice to date (only indirectly exercised via
  `TeacherBooksRoomTests.cs`'s class-6 case).
- `context/archive/2026-10-20-specialist-instrument-warning/plan.md` (not
  re-read in full this pass, but corroborated by the already-reviewed
  `SpecialistInstrumentWarningTests.cs` file) — established the instrument-mismatch
  warn/confirm/save flow now covered by 6 integration facts.

## Related Research

- `context/changes/testing-authorization-ownership-boundaries/research.md` — prior
  rollout-Phase-2 research; same test-plan rollout, same `/10x-research` grounding
  pattern (role/ownership boundary verification via direct-endpoint probing), no
  direct code overlap with this phase.

## Open Questions

- None blocking planning. The one empirical question this research set out to
  resolve (does `AdminStudentsController.Create` silently default an invalid
  `Class`?) is answered: no, for the inspected POST path.
