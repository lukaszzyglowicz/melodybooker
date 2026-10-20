<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Teacher Books a Room for a Weekly Lesson

- **Plan**: context/changes/teacher-books-room/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-10-20
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Unvalidated RoomId masked behind a generic DbUpdateException catch

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: src/melody/Controllers/TeacherReservationsController.cs (POST Create, ~lines 60-101)
- **Detail**: The posted `RoomId` is never checked against the `Rooms` table before the conflict check; it relies entirely on the DB FK constraint to reject an invalid value via `DbUpdateException`. The catch block treats *every* `DbUpdateException` as "This room is already booked at that time." — so a bad/bypassed `RoomId` (submitted directly via POST, skipping the dropdown) produces a misleading "already booked" message instead of a real validation error, and masks any other save failure. Note: `AdminStudentsController.Create`/`Edit` have the identical gap for `TeacherId` — this is a pre-existing codebase pattern, not a new regression, so fixing it here only would be an inconsistency with that precedent.
- **Fix A**: Explicitly validate `RoomId` exists (`await _context.Rooms.AnyAsync(r => r.Id == model.RoomId)`) before the conflict check, adding a `ModelState` error if not, and keep the `DbUpdateException` catch as defense-in-depth only for the genuine unique-index race.
  - Strength: Produces a correct, specific error for a bad `RoomId`; closes a real (if narrow) gap exploitable via direct POST.
  - Tradeoff: Diverges from the existing `AdminStudentsController` pattern, which has the same unvalidated gap for `TeacherId` and isn't being touched in this change.
  - Confidence: HIGH — the fix is a one-line existence check with a clear precedent (`GetOwnedStudentAsync` already does an analogous ownership existence check).
  - Blind spot: Doesn't address the identical, older gap in `AdminStudentsController` — a future reviewer may ask why only this controller got the fix.
- **Fix B ⭐ Recommended**: Leave as-is for this change, consistent with the existing (unvalidated) codebase convention, and record a lesson so a future, dedicated pass fixes both controllers together.
  - Strength: Keeps this change scoped to its plan; avoids inconsistent partial hardening across the codebase.
  - Tradeoff: The narrow gap (a malicious/bypassed POST with an invalid `RoomId`) stays open a little longer.
  - Confidence: MED — reasonable given the app's single-school, authenticated-only, low-adversarial-risk profile, but not zero risk.
  - Blind spot: Haven't checked whether any non-Teacher caller could reach this action with attacker-controlled input at a trust boundary beyond what's already authorized.
- **Decision**: FIXED (via Fix A) — added explicit `Rooms.AnyAsync(r => r.Id == model.RoomId)` check before the conflict check in `TeacherReservationsController.Create` (POST), returning a `ModelState` error on a missing room.

### F2 — DayOfWeek has no enum-range validation

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: src/melody/Models/ViewModels/ReservationFormViewModel.cs (DayOfWeek property)
- **Detail**: `DayOfWeek` is a plain CLR enum with no `[EnumDataType]`/range constraint. A direct POST with `DayOfWeek=99` (bypassing the `Html.GetEnumSelectList<DayOfWeek>()` dropdown, which is not a security boundary) binds successfully, satisfies `[Required]`, and is persisted as an out-of-range value — corrupting downstream displays of the day (e.g. renders as "99" anywhere `DayOfWeek` is shown).
- **Fix**: Add `[EnumDataType(typeof(DayOfWeek))]` (or `[Range(0, 6)]`) to the `DayOfWeek` property on `ReservationFormViewModel`.
- **Decision**: FIXED — added `[EnumDataType(typeof(DayOfWeek))]` to `ReservationFormViewModel.DayOfWeek`.

### F3 — StartMinutes has no bounds validation

- **Severity**: ⭕ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: src/melody/Models/ViewModels/ReservationFormViewModel.cs (StartMinutes property)
- **Detail**: `[Required]` on a non-nullable `int` is a no-op (default `0` already satisfies it). A negative value or one beyond a single day (>1439) posted directly bypasses the UI's 15-minute dropdown and produces a nonsensical `StartTime`/`EndTime`, which nothing currently rejects. This is consistent with the same weak pattern already used for `RoomId`/`StudentId` elsewhere in the codebase, not a new deviation.
- **Fix**: Add `[Range(0, 1439)]` to the `StartMinutes` property on `ReservationFormViewModel`.
- **Decision**: FIXED — added `[Range(0, 1439)]` to `ReservationFormViewModel.StartMinutes`.
