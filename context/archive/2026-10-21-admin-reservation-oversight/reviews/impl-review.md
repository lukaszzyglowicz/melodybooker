<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Administrator Reservation Oversight

- **Plan**: context/changes/admin-reservation-oversight/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-10-22
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Findings

### F1 — "Check availability" button is non-functional in the admin Edit view

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Pattern Consistency
- **Location**: src/melody/Views/AdminReservations/Edit.cshtml:70 (button); src/melody/Controllers/AdminReservationsController.cs:51 (`Edit(int id)` GET)
- **Detail**: The plan's Phase 2 Changes Required says to "mirror `TeacherReservations/Create.cshtml`'s structure", which includes a `formmethod="get"` "Check availability" button that resubmits the selected `RoomId`/`DayOfWeek`/`StartMinutes` as a GET so the availability table refreshes for the newly-picked combination before saving. `TeacherReservationsController.Create(int studentId, int? roomId, DayOfWeek? dayOfWeek)` binds and uses those optional query params. The admin `Edit(int id)` GET action only binds `id` — clicking "Check availability" after changing Room/Day/Time silently reloads the original persisted values instead of the admin's new selection, so the button does nothing useful. This was not caught by Phase 3 tests (no test exercises this button's round-trip) and is not a safety issue — the POST's own conflict/mismatch checks still run correctly on save — but it is a copied UI affordance that doesn't work as implied by its own label.
- **Fix A ⭐ Recommended**: Add `int? roomId, DayOfWeek? dayOfWeek, int? startMinutes` optional parameters to `Edit(int id, ...)` GET, mirroring `Create`'s pattern, and when present use them (instead of the loaded reservation's persisted values) to pre-select the form and populate `ExistingReservationsForRoomAndDay` (still excluding the reservation's own id).
  - Strength: Restores full parity with the precedent this view explicitly mirrors; the button then does what its label promises.
  - Tradeoff: Touches both the controller and view in a phase that's otherwise closed; needs its own small test to avoid recurrence.
  - Confidence: HIGH — `TeacherReservationsController.Create` is a direct, already-working template for exactly this change.
  - Blind spot: None significant — the exclude-self id is already available via the route `id`.
- **Fix B**: Remove the non-functional "Check availability" button from `Edit.cshtml` since the availability table already loads on page entry (for the reservation's current Room/Day) and the POST re-renders it correctly on conflict/mismatch.
  - Strength: Zero-risk, one-line change; removes a misleading affordance instead of half-fixing it.
  - Tradeoff: Loses the "preview before committing" convenience the teacher-facing Create flow offers.
  - Confidence: MEDIUM — simpler, but diverges further from the Create.cshtml precedent the plan asked to mirror.
  - Blind spot: Haven't confirmed whether admins rely on this preview step in practice (no usage data).
- **Decision**: FIXED via Fix A — `Edit(int id, int? roomId, DayOfWeek? dayOfWeek, int? startMinutes)` now binds the optional query params and uses them (falling back to the reservation's persisted values) to pre-select the form and compute `ExistingReservationsForRoomAndDay`. Build + full test suite (50/50) re-verified green.

### F2 — `HadInstrumentMismatchWarning` is carried on the list row but never rendered

- **Severity**: ⚪ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: src/melody/Models/ViewModels/AdminReservationListItemViewModel.cs:1; src/melody/Views/AdminReservations/Index.cshtml
- **Detail**: The plan's own contract for this field calls it "bool, for an optional visual indicator in the view" — so leaving it unrendered is explicitly within the plan's scope, not drift. Flagged only because the field is currently dead weight in the view model with no consumer.
- **Fix**: Either render a small badge/icon in the Index row when `HadInstrumentMismatchWarning` is true (e.g., next to the Room column), or leave as-is since the plan marked it optional — no action required unless the team wants the indicator now.
- **Decision**: FIXED — added a `Mismatch` badge next to the Room column in `Index.cshtml` when `HadInstrumentMismatchWarning` is true. Build + full test suite (50/50) re-verified green.
