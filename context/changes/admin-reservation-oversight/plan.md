# Administrator Reservation Oversight — Implementation Plan

## Overview

Give an Administrator a way to view every room reservation in the system and edit (Room/Day/Time) or cancel any of them (FR-013, FR-014/S-05), reusing the exact double-booking guard and specialist-instrument mismatch warn-then-confirm flow already built for the Teacher-facing booking path (S-03/S-04), rather than reimplementing or diverging from that business logic.

## Current State Analysis

- `TeacherReservationsController.Create` (`src/melody/Controllers/TeacherReservationsController.cs`) is the only place reservations are currently created. It already implements, in order: hard double-booking block via `Reservation.Overlaps` (FR-010), then soft instrument-mismatch warn-then-confirm via `Reservation.IsInstrumentMismatch` (FR-011) with a `Confirmed`/`ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes`/`ConfirmedStudentId` pin pattern on `ReservationFormViewModel`.
- There is no admin-facing reservation screen at all today — `AdminController.Index()` only links to `AdminTeachers`/`AdminStudents`.
- `AdminStudentsController` (`src/melody/Controllers/AdminStudentsController.cs`) is the closest existing CRUD pattern: `[Authorize(Roles = "Administrator")]`, Index/Create/Edit/Delete(GET)/Delete(POST, `ActionName("Delete")`), a dedicated confirm-before-delete view (`Views/AdminStudents/Delete.cshtml`) that blocks deletion when a dependent exists — here cancellation has no such block (a reservation has no dependents).
- `Reservation` (`src/melody/Models/Domain/Reservation.cs`) already carries everything an edit needs: `RoomId`, `StudentId`, `TeacherId`, `DayOfWeek`, `StartTime`, `Duration` (derived once from the student's class at creation time and never re-derived), `HadInstrumentMismatchWarning`.
- `Admin/Index.cshtml` (`src/melody/Views/Admin/Index.cshtml`) is a one-line hub page with inline links — the pattern this plan's nav entry follows.

### Key Discoveries:

- Editing only Room/Day/Time (Student and Teacher fixed) means the admin edit flow never posts back a `StudentId` — unlike the Teacher flow's `ConfirmedStudentId` pin (added for S-04's F3 triage fix to guard against a tampered hidden `StudentId` field), the admin edit form has no student field to tamper with at all, since the student comes from the loaded `Reservation` by route id, not from the form. The confirmation pin here only needs to cover Room/Day/Time, matched against the route-bound reservation id.
- The double-booking conflict query in `TeacherReservationsController.Create` has no "exclude self" clause because creation never conflicts with its own not-yet-existing row. The admin edit path **must** exclude the reservation being edited (`r.Id != model.Id`) from the conflict query, otherwise a no-op edit (resubmitting the same Room/Day/Time) would always incorrectly report a conflict against itself.
- `Student.LessonDuration` is derived from class and never user input (FR-009) — the admin edit path keeps `Duration` unchanged (recomputed from the unchanged `Student`, not copied from the old `Reservation.Duration`, so it would self-correct if a student's `Class` were ever changed, though this plan does not add a path to change it).

## Desired End State

An Administrator can open a new "All reservations" screen listing every reservation (Teacher, Student, Room, Day, Start–End), sorted by Teacher then Day then Start time. From there they can edit a reservation's Room/Day/Time — subject to the same hard double-booking block and the same soft specialist-mismatch warn-then-confirm flow the Teacher booking path enforces — or cancel it via a dedicated confirmation page. A link from the `Admin/Index.cshtml` hub reaches the new screen.

### Automated verification covers: `dotnet build melodybooker.sln` succeeds; `dotnet test` passes, including new integration tests for view-all, edit success, edit still double-booking-blocked (excluding the reservation's own prior slot), edit into a mismatch re-warns then saves on confirm, cancel removes the reservation, and non-Administrator roles are forbidden on every `AdminReservations` action.
### Manual verification covers: an end-to-end walkthrough as an Administrator — viewing the full list, editing a reservation to a new time, triggering and confirming a mismatch warning, and cancelling a reservation.

## What We're NOT Doing

- Not allowing the admin to reassign a reservation's Student or Teacher — edit is limited to Room/Day/Time (explicit user decision; keeps the data model's "whose student is this" invariant from S-01/S-02 intact).
- Not implementing FR-015 (filter/search reservations) — explicit nice-to-have, deferred.
- Not implementing FR-016 (notify the affected teacher on admin edit/cancel) — explicit nice-to-have, deferred.
- Not adding a top-nav item in `_Layout.cshtml` — the entry point is a link on the existing `Admin/Index.cshtml` hub (explicit user decision).
- Not changing `TeacherReservationsController` or its view/viewmodel — the admin path is a new, separate controller/viewmodel set that reuses the shared `Reservation` domain rules, not the teacher-facing MVC types.
- Not adding a "can't cancel" block of any kind — unlike student deletion (blocked when reservations exist), a reservation has no dependents, so cancellation is always allowed once confirmed.

## Implementation Approach

Mirror the proven S-03/S-04 shape on the admin side: a controller + view models that reuse `Reservation.Overlaps` and `Reservation.IsInstrumentMismatch` directly (Phase 1), the views including the same warn-then-confirm UI pattern and availability table (Phase 2), then integration tests proving parity with the teacher-facing guarantees plus the cross-cutting admin concerns like exclude-self conflict checking and cancellation (Phase 3).

## Phase 1: AdminReservationsController and view models

### Overview

Add the Administrator-only controller (Index/Edit GET+POST/Delete GET+POST) and its three view models, reusing the existing domain rules for conflict and mismatch detection.

### Changes Required:

#### 1. List view model

**File**: `src/melody/Models/ViewModels/AdminReservationListItemViewModel.cs` (new)

**Intent**: Flatten a `Reservation` plus its related `Teacher`/`Student`/`Room` names into the row shape the Index view renders.

**Contract**: `Id`, `TeacherFullName`, `StudentFullName`, `RoomName`, `DayOfWeek`, `StartTime`, `EndTime`, `HadInstrumentMismatchWarning` (bool, for an optional visual indicator in the view).

#### 2. Edit form view model

**File**: `src/melody/Models/ViewModels/AdminReservationEditViewModel.cs` (new)

**Intent**: Back the edit form the same way `ReservationFormViewModel` backs the teacher's create form, minus the Student-selection concern (student is fixed, loaded by route id) and with a `ReservationId` instead of `StudentId` driving the confirmation pin.

**Contract**: `[Required] public int Id { get; set; }` (hidden, drives the DB lookup and the pin check); `[ValidateNever]` display-only `TeacherFullName`, `StudentFullName`, `StudentLessonDuration`; `[Required] RoomId`, `[Required][EnumDataType(typeof(DayOfWeek))] DayOfWeek`, `[Required][Range(0,1439)] StartMinutes` (same shape as `ReservationFormViewModel`); `[ValidateNever]` `Rooms`/`TimeSlots` select lists; `[ValidateNever] List<StudentReservationSummary> ExistingReservationsForRoomAndDay` (reused type from `TeacherReservations`, excluding the reservation being edited); `[ValidateNever]` `Confirmed`, `ConfirmedRoomId`, `ConfirmedDayOfWeek`, `ConfirmedStartMinutes` (no `ConfirmedStudentId` equivalent — see Key Discoveries); `[ValidateNever]` `ShowMismatchWarning`, `MismatchWarningMessage` (controller-set only).

#### 3. Cancel confirm view model

**File**: `src/melody/Models/ViewModels/AdminReservationDeleteViewModel.cs` (new)

**Intent**: Back the dedicated cancel-confirmation page, showing enough context (who/what/when) for the admin to confirm they're cancelling the right reservation.

**Contract**: `Id`, `TeacherFullName`, `StudentFullName`, `RoomName`, `DayOfWeek`, `StartTime`, `EndTime`.

#### 4. Controller

**File**: `src/melody/Controllers/AdminReservationsController.cs` (new)

**Intent**: `[Authorize(Roles = "Administrator")]` controller with `Index()` (list all, sorted Teacher → Day → Start), `Edit(int id)` GET (load reservation + related Teacher/Student/Room, 404 if missing), `Edit(AdminReservationEditViewModel)` POST (re-run the same priority-ordered checks as `TeacherReservationsController.Create`: hard double-booking block first — excluding the reservation's own id — then soft mismatch warn-then-confirm, then save), `Delete(int id)` GET (confirm page), `Delete(int id)` POST (`ActionName("Delete")`, hard-deletes and redirects to Index).

**Contract**: The edit POST's "confirmed for this combination" check is `model.Confirmed && model.ConfirmedRoomId == model.RoomId && model.ConfirmedDayOfWeek == model.DayOfWeek && model.ConfirmedStartMinutes == model.StartMinutes` (no student-id leg needed, per Key Discoveries). The conflict query is `_context.Reservations.Where(r => r.RoomId == model.RoomId && r.DayOfWeek == model.DayOfWeek && r.Id != model.Id)` before the `Reservation.Overlaps` check — the `r.Id != model.Id` exclusion is the one behavioral delta from the teacher-create path's equivalent query. On successful save, update the existing tracked `Reservation`'s `RoomId`, `DayOfWeek`, `StartTime`, and `HadInstrumentMismatchWarning` in place (not add/remove), keeping the same row id. Catch `DbUpdateException` around `SaveChangesAsync` with the same generic "already booked" fallback as the teacher path.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds

#### Manual Verification:

- None — this phase has no UI yet; manual verification happens in Phase 2 once views exist

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Views and navigation

### Overview

Add the three views (Index, Edit, Delete-confirm) and the hub link, following the established UI patterns from `TeacherReservations/Create.cshtml` (warn-then-confirm + availability table) and `AdminStudents/Delete.cshtml` (confirm page).

### Changes Required:

#### 1. List view

**File**: `src/melody/Views/AdminReservations/Index.cshtml` (new)

**Intent**: Render the flat, pre-sorted list of `AdminReservationListItemViewModel` rows with Edit/Cancel row-action links (each with an `aria-label` identifying the row's subject, per repo convention).

**Contract**: Table columns Teacher / Student / Room / Day / Start–End; a row action link to `Edit` and one to `Delete`, each `aria-label="Edit reservation for @item.StudentFullName"` / `aria-label="Cancel reservation for @item.StudentFullName"`.

#### 2. Edit view

**File**: `src/melody/Views/AdminReservations/Edit.cshtml` (new)

**Intent**: Mirror `TeacherReservations/Create.cshtml`'s structure — display-only Teacher/Student/Lesson-duration block, editable Room/Day/Time selects, the mismatch warning banner + pinned hidden fields when `ShowMismatchWarning` is true, and the existing-reservations availability table for the selected Room/Day (excluding the reservation itself).

**Contract**: Hidden `Id` field; submit button label reflects pending state ("Confirm change" vs "Save change"), same `@if (Model.ShowMismatchWarning)` gating pattern as the teacher view (hidden `ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes`/`Confirmed` fields only rendered inside that block).

#### 3. Cancel confirm view

**File**: `src/melody/Views/AdminReservations/Delete.cshtml` (new)

**Intent**: Show the reservation's Teacher/Student/Room/Day/Time and a confirm/cancel action pair, following `AdminStudents/Delete.cshtml`'s structure (minus the dependent-blocking branch, since reservations have no dependents).

**Contract**: `<form asp-action="Delete" method="post">` with hidden `Id`, a `btn-danger` "Cancel reservation" submit and a `btn-link` "Back" cancel-out link.

#### 4. Hub navigation link

**File**: `src/melody/Views/Admin/Index.cshtml`

**Intent**: Add a third inline link alongside the existing "Manage teachers"/"Manage students" links, reaching the new Index view.

**Contract**: `<a asp-controller="AdminReservations" asp-action="Index">View all reservations</a>` appended to the existing `·`-separated link list.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds

#### Manual Verification:

- The hub page's "View all reservations" link reaches a list of every reservation in the system, sorted by Teacher then Day then Start time
- Editing a reservation to a new Room/Day/Time succeeds and the change is reflected in the list
- Editing a reservation into a double-booking conflict is hard-blocked with an error, same message as the teacher path
- Editing a reservation into a specialist-mismatch room shows the warning banner; confirming ("Confirm change") saves it
- Cancelling a reservation removes it from the list after confirming on the dedicated Delete page

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Integration tests

### Overview

Prove the full admin oversight flow end-to-end against the real controller/view stack (SQLite-in-memory), covering view-all, edit (success, double-booking block with exclude-self, mismatch warn-then-confirm), cancel, and the Administrator-only authorization boundary.

### Changes Required:

#### 1. Integration test suite

**File**: `tests/melody.Tests/AdminReservationOversightTests.cs` (new)

**Intent**: Follow `TeacherBooksRoomTests.cs`/`SpecialistInstrumentWarningTests.cs`'s established pattern (xUnit, `MelodyWebApplicationFactory`, `CreateClientAs("Administrator", ...)`, DB-row assertions, a linear seeding helper).

**Contract**: At minimum: (1) Index lists reservations across multiple teachers/students/rooms; (2) editing Room/Day/Time on an existing reservation succeeds and persists the new values; (3) editing a reservation to conflict with a *different* existing reservation is hard-blocked, 0 rows changed; (4) resubmitting a reservation's *own current* Room/Day/Time (no-op edit) is **not** falsely blocked as a self-conflict; (5) editing into a specialist-mismatch room without confirmation re-renders the warning and does not save, then confirming (pinned Room/Day/Time) saves with `HadInstrumentMismatchWarning == true`; (6) cancelling via the Delete POST removes the row; (7) every `AdminReservations` action returns forbidden/redirect-to-login for a non-Administrator (Teacher) caller, consistent with `AuthorizationTests`'s existing role-boundary coverage.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds (final build after all phases)
- `dotnet test` passes in full (all new + existing tests)

#### Manual Verification:

- None — this phase's purpose is converting the Phase 2 manual checks into permanent automated coverage

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None new — `Reservation.Overlaps` and `Reservation.IsInstrumentMismatch` are already unit-tested by `ReservationOverlapTests.cs`/`InstrumentMismatchTests.cs`; this plan only reuses them from a new call site.

### Integration Tests:

- Full POST round-trips through `AdminReservationsController.Edit` and `.Delete`, covering list-all, edit success, edit double-booking block (excluding self), edit mismatch warn-then-confirm, cancel, and non-Administrator role rejection.

### Manual Testing Steps:

1. As an Administrator, open "View all reservations" from the hub and confirm every teacher's bookings appear, sorted by Teacher then Day then Start time
2. Edit a reservation to a free Room/Day/Time slot — confirm it saves and the list reflects the change
3. Edit a reservation to a slot already taken by a different reservation — confirm the same hard-block error as the teacher path
4. Edit a reservation into a specialist room mismatching the student's instrument — confirm the warning banner appears, and "Confirm change" saves it
5. Cancel a reservation via the dedicated confirm page — confirm it's removed from the list

## Migration Notes

None — no schema changes; this plan only adds a new controller/viewmodels/views reading and updating the existing `Reservation` table.

## References

- Direct precedent for pattern and file layout: `context/archive/2026-10-20-specialist-instrument-warning/plan.md`, `context/archive/2026-10-19-teacher-books-room/plan.md`
- PRD: `context/foundation/prd.md` (FR-013, FR-014; FR-015/FR-016 explicitly deferred)
- Roadmap: `context/foundation/roadmap.md` (S-05)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: AdminReservationsController and view models

#### Automated

- [x] 1.1 `dotnet build melodybooker.sln` succeeds — 5b940bf

### Phase 2: Views and navigation

#### Automated

- [x] 2.1 `dotnet build melodybooker.sln` succeeds — 419b88c

#### Manual

- [ ] 2.2 The hub page's "View all reservations" link reaches a list of every reservation in the system, sorted by Teacher then Day then Start time
- [ ] 2.3 Editing a reservation to a new Room/Day/Time succeeds and the change is reflected in the list
- [ ] 2.4 Editing a reservation into a double-booking conflict is hard-blocked with an error, same message as the teacher path
- [ ] 2.5 Editing a reservation into a specialist-mismatch room shows the warning banner; confirming ("Confirm change") saves it
- [ ] 2.6 Cancelling a reservation removes it from the list after confirming on the dedicated Delete page

### Phase 3: Integration tests

#### Automated

- [ ] 3.1 Final `dotnet build melodybooker.sln` succeeds
- [ ] 3.2 `dotnet test` passes in full (all new + existing tests)
