# Admin Roster Management — Plan Brief

> Full plan: `context/changes/admin-roster-management/plan.md`

## What & Why

Give the Administrator a real panel to manage the school's roster: add,
edit, and remove teachers (each with a working login account) and students,
and assign each student to exactly one teacher. Today `Views/Admin/Index.cshtml`
is a placeholder saying "coming soon" — this closes that gap (PRD FR-003
through FR-006, roadmap slice S-01).

## Starting Point

`Teacher` and `Student` EF Core models and their database constraints
already exist (from the `role-based-authorization` change) — including a
required `Teacher ↔ ApplicationUser` link and `Restrict`-on-delete foreign
keys that already block orphaning assignments/reservations at the database
level. No controller or view currently creates, edits, or deletes either
entity. A legacy, unrelated Identity "Register" page is still reachable by
Administrators and would conflict with the new flow.

## Desired End State

An Administrator logs into `/Admin` and sees links to a teacher list and a
student list. They can add a teacher (name + login email + password) who
can immediately log in; add a student (name, class, instrument, assigned
teacher); edit either; and delete either — with deletion blocked and
clearly explained whenever it would orphan an assignment or an active
reservation.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Teacher login credentials | Administrator types the password directly in the Create form | No email/SMS infrastructure exists; mirrors the existing `AdminUserSeeder` pattern; matches single-school scale |
| Teacher deletion with assigned students | Block with a message naming the students; admin reassigns manually via Edit | Database already enforces this (Restrict FK); a bulk-reassign-at-delete-time feature would add real complexity for a single-school's few students-per-teacher |
| Student deletion with reservations | Block with a message listing the reservations | Matches FR-006's literal "blocking" wording; no cascade that could silently drop a booked lesson |
| Teacher edit scope | Name only — no email/password change | Credential management is a separate concern, deferred past this MVP slice |
| Room management | Fully out of scope | No PRD requirement or roadmap slice covers Room CRUD; rooms are seeded separately before S-03 needs them |
| Empty-teacher state | Disable the "add student" form with a hint instead of allowing an unassigned student | `Student.TeacherId` is a required FK — there's no valid "unassigned" state to support |
| Legacy `/Identity/Account/Register` page | Disabled (redirects to the new teacher-create form) | It auto-signs-in the admin as the newly created account and never creates a `Teacher` profile — a dangerous duplicate path now that a real one exists |
| Test coverage | FR-006 blocking rules + authorization boundary only | Matches the existing test project's integration-test pattern; full CRUD-per-action coverage would be redundant with manual verification for a roster-admin screen |

## Scope

**In scope:**
- `AdminTeachersController` + views: list/create/edit/delete teachers
- `AdminStudentsController` + views: list/create/edit/delete students, assign/reassign teacher
- Disabling the legacy Identity Register page
- `Views/Admin/Index.cshtml` navigation update
- Integration tests for the FR-006 blocking rules and the new controllers' role boundary

**Out of scope:**
- Room (rehearsal room) management
- Teacher email/password changes from the edit screen
- Any reservation viewing/editing/canceling UI (roadmap S-05)
- Bulk import of students/teachers
- Auto-generated or "show once" passwords

## Architecture / Approach

Two new flat `[Authorize(Roles = "Administrator")]` controllers
(`AdminTeachersController`, `AdminStudentsController`) follow the existing
project convention (no MVC Areas beyond the pre-existing Identity scaffold),
each with standard `Index`/`Create`/`Edit`/`Delete` actions and matching
Razor views. New `Models/ViewModels/` types separate form input (including
the teacher `<select>` list for students) from the EF Core domain entities.
Teacher creation layers `UserManager<ApplicationUser>.CreateAsync` +
`AddToRoleAsync` (same pattern as `AdminUserSeeder`) with a compensating
delete if the follow-up `Teacher` profile row fails to save.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Teacher roster management | Create/edit/delete teachers with real login accounts; legacy Register page disabled | Two-step account creation (Identity + domain row) can leave an orphaned login if the second step fails — mitigated with a compensating delete |
| 2. Student roster management | Create/edit/delete students with teacher assignment; empty-teacher-state handling | `TeacherId` is required — the create form must degrade gracefully with zero teachers instead of showing a broken empty dropdown |
| 3. Admin dashboard navigation | Replaces the "coming soon" placeholder with working links | Low risk — single view edit |
| 4. Automated verification | Integration tests for FR-006 blocking rules + role boundary | Low risk — follows the existing `AuthorizationTests.cs` pattern directly |

**Prerequisites:** `role-based-authorization` (F-01) must be implemented —
it is (roles, seeded admin, `[Authorize]` enforcement all already in place).
**Estimated effort:** ~2-3 sessions across 4 phases.

## Open Risks & Assumptions

- Assumes the single-school scale (a handful of teachers, tens of students)
  makes one-at-a-time manual reassignment during teacher deletion
  acceptable — if the school turns out to have many students per teacher,
  this may warrant a bulk-reassign UI later.
- Assumes disabling (not deleting) the legacy Register page is safe; if
  anything else in the Identity scaffolding links to it, a redirect
  preserves the route.

## Success Criteria (Summary)

- Administrator can fully manage the teacher and student roster from `/Admin`
  without touching the database directly.
- No path exists to create a `Teacher` without a working login, or a
  `Student` without an assigned teacher.
- Deleting a teacher or student that would orphan an assignment or active
  reservation is always blocked with a clear explanation — never silent
  data loss.
