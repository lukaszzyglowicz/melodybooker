<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Specialist-Instrument Mismatch Warning

- **Plan**: context/changes/specialist-instrument-warning/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-10-21
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Findings

### F1 — Unplanned CI/roadmap files bundled into Phase 2 commit

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: `.github/scripts/sync-roadmap-issues.py`, `.github/workflows/sync-roadmap-issues.yml`, `context/foundation/roadmap.md`
- **Detail**: Commit `73046bf` (which lands Phase 2's code) also contains a new GitHub Actions workflow + Python script that auto-closes issues when roadmap items are marked `done`, plus a `roadmap.md` edit. None of this is in the plan's "Changes Required" — it was explicit out-of-band work requested by the user earlier in the same session and bundled by the user's own manual commit, not scope creep introduced during implementation.
- **Fix**: No code change needed. Note it in `change.md`'s Notes section (or this review) that the Phase 2 commit SHA also carries unrelated CI tooling, so a future reader of `git show 73046bf` isn't confused about why a plan-scoped commit touches `.github/`.
- **Decision**: FIXED — note added to `context/changes/specialist-instrument-warning/change.md` Notes section.

### F2 — Hidden confirmation fields use raw `name=` instead of `asp-for`

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: `src/melody/Views/TeacherReservations/Create.cshtml` (hidden `ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes`/`Confirmed` inputs)
- **Detail**: Every other bound field in this view uses `asp-for="..."` tag helpers; the four new pinned-confirmation hidden inputs use raw `name="..."` attributes instead. Functionally correct (model binder handles both), but stylistically inconsistent with the rest of the file.
- **Fix**: Switch the four hidden inputs to `<input type="hidden" asp-for="ConfirmedRoomId" />` etc., matching the rest of the form.
- **Decision**: SKIPPED — the proposed `asp-for` swap would regress functionality: `asp-for="ConfirmedRoomId"` renders `value=@Model.ConfirmedRoomId`, not `value=@Model.RoomId`. The code intentionally pins the *just-submitted* Room/Day/Time (not whatever was previously posted as `ConfirmedRoomId`, which can be null on first mismatch). The raw `name=`/`value=` pattern is deliberate cross-field pinning, not a stylistic lapse — kept as-is.

### F3 — Pinned confirmation doesn't include `StudentId`

- **Severity**: ⚪ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `src/melody/Controllers/TeacherReservationsController.cs` (`confirmedForThisCombination`)
- **Detail**: The pinned confirmation check compares `ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes` but not a `ConfirmedStudentId`. If the hidden `StudentId` field were tampered with between the warning render and the confirmed re-submit, a pinned Room/Day/Time confirmation could be silently reused for a different student than the one the warning named. Matches the plan's literal spec ("pinned to the exact Room/Day/Time combination"); `GetOwnedStudentAsync` still enforces the student belongs to the requesting teacher, and this only bypasses an informational warning, not a hard block — low real-world impact.
- **Fix**: Add `ConfirmedStudentId` to the pinned fields and the equality check if a stronger guarantee is wanted; otherwise accept as-is since the plan's contract didn't call for it.
- **Decision**: FIXED — added `ConfirmedStudentId` to `ReservationFormViewModel`, the controller's `confirmedForThisCombination` check, the hidden-field markup in `Create.cshtml`, and the `PostCreateAsync` test helper, plus a new regression test `Create_ConfirmedForDifferentStudent_ReWarnsInsteadOfSaving`. Full suite: 42/42 passing (was 41).

### F4 — Redundant reservations query on re-render

- **Severity**: ⚪ OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `src/melody/Controllers/TeacherReservationsController.cs` (`Create` POST → `ReRenderCreateAsync`)
- **Detail**: `existingReservations` is already fetched once for the conflict check, then `GetExistingReservationsAsync` queries the same Room/Day again inside `ReRenderCreateAsync` when re-rendering a mismatch warning — one avoidable extra DB round-trip per warned submission. Not a loop/N+1, just a minor duplicate query.
- **Fix**: Pass the already-fetched `existingReservations` into `ReRenderCreateAsync` instead of re-querying, when available.
- **Decision**: FIXED — `ReRenderCreateAsync` now takes an optional `existingReservationsForRoomAndDay` parameter; the mismatch-warning call site builds the summary list from the already-fetched `existingReservations` + `room.Name` instead of re-querying. Full suite: 42/42 passing.
