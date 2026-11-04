# Administrator Reservation Oversight — Plan Brief

> Full plan: `context/changes/admin-reservation-oversight/plan.md`

## What & Why

Administrators currently have no way to see or manage any reservation in the system — only teachers can create bookings, and nobody can view the full schedule, fix a mistake, or free up a room. This change adds an Administrator-only screen to view every reservation and edit (Room/Day/Time) or cancel any of them (FR-013, FR-014), closing the oversight gap called out in the roadmap's S-05 slice.

## Starting Point

`TeacherReservationsController.Create` is the only reservation-creation path today, and it already enforces the two business rules that matter here: a hard double-booking block (FR-010) and a soft specialist-instrument mismatch warning (FR-011) with a warn-then-confirm UI pattern. There is no admin-facing reservation view at all — `Admin/Index.cshtml` only links to teacher/student management.

## Desired End State

An Administrator opens "View all reservations" from the Admin hub, sees every booking in the system (Teacher, Student, Room, Day, Start–End), can edit a reservation's Room/Day/Time — subject to the exact same conflict and mismatch rules as the teacher path — and can cancel any reservation through a dedicated confirmation page.

## Key Decisions Made

| Decision                         | Choice                                         | Why (1 sentence)                                                                 | Source |
| --------------------------------- | ----------------------------------------------- | --------------------------------------------------------------------------------- | ------ |
| Edit scope                        | Room/Day/Time only; Student/Teacher fixed       | Matches the "edit the booking" reading of FR-014 and keeps reservation ownership unambiguous | Plan   |
| Mismatch-warning reuse on edit     | Yes — same warn-then-confirm flow as teacher path | Keeps FR-011 consistent everywhere a reservation can be set, not just at creation  | Plan   |
| Cancel confirmation               | Dedicated confirm page (mirrors AdminStudents Delete) | Consistent with the existing admin delete pattern; prevents accidental cancels     | Plan   |
| List presentation                 | Flat list, sorted Teacher → Day → Start time    | Simplest reading of FR-013's "view all"; FR-015 filter/search is explicitly deferred | Plan   |
| Navigation entry point             | Link on the `Admin/Index.cshtml` hub            | Zero layout changes; matches the existing "Manage teachers/students" pattern       | Plan   |
| Availability helper on edit        | Reuse the existing-reservations table from the teacher Create form | Consistent UX for spotting conflicts before submitting                            | Plan   |
| Test depth                        | Full integration-test depth, matching S-03/S-04 rigor | Edit introduces a new "exclude self" conflict-check edge case worth locking down automatically | Plan   |

## Scope

**In scope:**
- `AdminReservationsController`: Index (list all), Edit (GET/POST), Delete/cancel (GET/POST)
- 3 new view models (list item, edit form, delete-confirm) + 3 new views
- Reuse of `Reservation.Overlaps` and `Reservation.IsInstrumentMismatch` from the domain model
- Hub navigation link
- Full integration test coverage (list, edit success, edit double-booking block with exclude-self, edit mismatch warn-then-confirm, cancel, authorization boundary)

**Out of scope:**
- Reassigning a reservation's Student or Teacher during edit
- FR-015 (filter/search reservations)
- FR-016 (notify affected teacher on admin edit/cancel)
- Any change to `TeacherReservationsController` or its view/viewmodel
- Top-nav changes to `_Layout.cshtml`

## Architecture / Approach

A new, separate `AdminReservationsController` under `[Authorize(Roles = "Administrator")]` reuses the two existing static domain rules (`Reservation.Overlaps`, `Reservation.IsInstrumentMismatch`) rather than duplicating them. The edit flow structurally mirrors the teacher booking flow's warn-then-confirm pattern, with one behavioral delta: the double-booking conflict query excludes the reservation's own id, since an edit (unlike a create) can legitimately resubmit its own current slot.

## Phases at a Glance

| Phase                                   | What it delivers                                              | Key risk                                                                 |
| ---------------------------------------- | --------------------------------------------------------------- | --------------------------------------------------------------------------- |
| 1. AdminReservationsController + view models | Controller + 3 view models reusing the existing conflict/mismatch rules | Forgetting the "exclude self" clause would falsely block no-op edits       |
| 2. Views and navigation                  | Index/Edit/Delete views + hub link                              | Drift from the teacher Create.cshtml's warn-then-confirm hidden-field pattern |
| 3. Integration tests                     | Full automated coverage incl. exclude-self and authorization boundary | Missing the no-op-edit regression test would let a future change reintroduce it |

**Prerequisites:** F-01 (role-based authorization) and S-03 (teacher-books-room) — both already `done` per the roadmap.
**Estimated effort:** ~1-2 sessions across 3 phases, similar scope to the already-completed specialist-instrument-warning change.

## Open Risks & Assumptions

- Assumes `HadInstrumentMismatchWarning` should be recalculated and overwritten on every edit (reflecting the *current* Room/Student pairing), not left as whatever it was set to at original creation time.
- Assumes the admin edit path reuses the same generic "This room is already booked at that time." error message as the teacher path, for consistency, rather than a distinct admin-facing message.

## Success Criteria (Summary)

- An Administrator can see every reservation in the system in one place
- An Administrator can edit a reservation's Room/Day/Time without bypassing the double-booking guard or the specialist-mismatch warning
- An Administrator can cancel any reservation through a confirm step
- All of the above is covered by passing automated integration tests, with no regressions to the teacher-facing booking flow
