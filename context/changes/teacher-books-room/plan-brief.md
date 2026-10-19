# Teacher Books a Room for a Weekly Lesson — Plan Brief

> Full plan: `context/changes/teacher-books-room/plan.md`

## What & Why

Let a Teacher reserve a rehearsal room for an assigned student's weekly lesson — duration auto-derived from the student's class, repeating for the whole school year, with double-booking always hard-blocked. This is S-03, the roadmap's "north star": the smallest slice that proves the core product idea (a spreadsheet can't auto-derive duration or hard-block double-booking; this slice can).

## Starting Point

The domain model (`Reservation`, `Room`, `Student.LessonDuration`) is already fully built and matches the PRD. A DB unique index already blocks exact-match double-booking. There is no Room CRUD or seed data (table is empty) and no overlap-conflict logic beyond the exact-match index, and no teacher-facing booking UI exists yet.

## Desired End State

A Teacher clicks "Book a room" next to one of their own students on `Teacher/Index`, picks a Room/Day/Time, optionally checks existing bookings for that Room+Day first, and submits. Conflicting slots (exact match or overlap) are always rejected with a generic message; back-to-back slots are allowed. A fixed set of 6 rooms (3 general + Piano/Xylophone/Drums) exists in every environment via a startup seeder.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Room provisioning | Fixed startup seeder (`RoomSeeder`), no admin CRUD | Matches the single-school, fixed-inventory reality and the existing `RoleSeeder`/`AdminUserSeeder` pattern; avoids expanding scope with a new CRUD area not covered by any FR | Plan |
| Overlap semantics | Back-to-back allowed; any real time-range overlap blocked | Standard scheduling convention (half-open intervals); matches teacher expectations for tight daily schedules | Plan |
| Teacher scope | Create-only, no "my reservations" list view | FR-008 only grants "reserve"; FR-014 reserves edit/cancel/oversight for Administrator (S-05) | Plan |
| Time input UI | Dropdown of fixed 15-min slots (08:00-18:00) | Avoids invalid/non-standard times; simpler than a free-form time picker, no JS needed | Plan |
| Availability guidance | Show existing bookings for the selected Room+Day before submit (GET round-trip, no JS) | Avoids blind trial-and-error submits; no client-side framework exists in the app to justify AJAX | Plan |
| Specialist-room labeling | Room name only, no specialist badge in S-03 | Keeps this slice scoped to FR-008/009/010; all instrument-aware UI belongs to S-04 | Plan |
| Testing approach | Extend the existing `melody.Tests` project (unit + integration) | A test project already exists in the repo; this is the single highest-risk rule in the MVP per the roadmap's own risk note | Plan |
| Conflict error detail | Generic "already booked" message, no cross-teacher booking detail shown | Avoids leaking another teacher's schedule info; keeps the UX simple | Plan |

## Scope

**In scope:**
- `RoomSeeder` (fixed 6-room startup seed) + wiring into `Program.cs`
- `Reservation.Overlaps` static overlap-detection rule
- `TeacherReservationsController` (Create GET/POST, teacher-ownership scoped)
- `ReservationFormViewModel` + `Create.cshtml` (Room/Day/Time selects, existing-bookings check)
- "Book a room" link from `Teacher/Index.cshtml`
- Unit tests (overlap rule, room seeding) + integration tests (full booking flow, ownership, role boundary)

**Out of scope:**
- Specialist-instrument mismatch warning (FR-011 / S-04)
- Admin Room CRUD UI
- Teacher edit/cancel or "my reservations" list view (FR-014 / S-05)
- School holiday/break exception handling in the weekly recurrence
- Any client-side JavaScript/AJAX

## Architecture / Approach

Bottom-up: seed data + a pure, unit-tested overlap rule (Phase 1) → a teacher-scoped controller and form built on top of it, following existing `AdminStudents`/`TeacherController` conventions (Phase 2) → integration tests locking in the full flow against the real stack via the existing `MelodyWebApplicationFactory` test host (Phase 3).

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Room seeding + conflict-detection core | Fixed room inventory at startup; `Reservation.Overlaps` rule, unit-tested | Getting the half-open interval boundary condition (back-to-back) wrong |
| 2. Teacher booking controller + views | End-to-end create-a-booking UI, scoped to the teacher's own students | DB unique-index race vs. app-layer check ordering; dropdown rebuild on validation failure |
| 3. Integration tests | Automated coverage of the full flow, ownership, and role boundaries | None significant — mechanical test-writing against an established pattern |

**Prerequisites:** F-01 (role-based authorization) and S-02 (teacher-student-list) — both already `done`.
**Estimated effort:** ~2-3 sessions across 3 phases.

## Open Risks & Assumptions

- Assumed a reasonable fixed time-slot window (08:00-18:00, 15-min increments) — not explicitly specified by the user; adjust easily if the school's actual hours differ.
- Assumed 6 fixed rooms (3 general + 1 each for Piano/Xylophone/Drums) as a reasonable placeholder inventory — the real room count/names are a product detail the school will need to confirm before go-live; easy to edit in `RoomSeeder` later.
- School holiday/break handling remains an explicit open roadmap question (owner: user) — does not block this plan's creation flow.

## Success Criteria (Summary)

- A Teacher can book a room for their own student in under a minute, seeing existing bookings for their chosen Room+Day before committing.
- No two reservations in the same room can ever overlap in time, except back-to-back slots.
- All of the above is covered by automated tests, not just manual verification.
