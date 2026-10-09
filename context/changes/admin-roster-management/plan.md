# Admin Roster Management Implementation Plan

## Overview

Give the Administrator a working roster-management panel: add/edit/remove
teachers (each backed by a real Identity login account) and students, and
assign each student to exactly one teacher — replacing the current
`Views/Admin/Index.cshtml` "coming soon" placeholder. Implements FR-003,
FR-004, FR-005, FR-006 (roadmap slice S-01).

## Current State Analysis

- `Teacher` (`src/melody/Models/Domain/Teacher.cs`) has a required 1:1
  relationship to `ApplicationUser` (`ApplicationUserId`) — a `Teacher` row
  cannot exist without a backing Identity login account. There is currently
  no code path that creates a `Teacher` at all.
- `Student` (`src/melody/Models/Domain/Student.cs`) has a required
  (non-nullable) `TeacherId` FK — a student always belongs to exactly one
  teacher from the moment it's created (matches FR-005 directly).
- `ApplicationDbContext.OnModelCreating` (`src/melody/Data/ApplicationDbContext.cs:24-45`)
  already sets `OnDelete(DeleteBehavior.Restrict)` on `Student → Teacher`,
  `Reservation → Student`, `Reservation → Teacher`, and `Reservation → Room`.
  This means the database **already enforces** the core of FR-006 ("block
  deletion while active reservations/assignments exist") — this plan's job
  is to catch that condition gracefully in the application layer and show a
  clear message, not to invent new blocking logic.
- `AdminController` (`src/melody/Controllers/AdminController.cs`) exists as a
  bare `[Authorize(Roles = "Administrator")]` dashboard with a single
  `Index` action; `Views/Admin/Index.cshtml` literally says "The roster
  management page is coming soon."
- `AdminUserSeeder.cs` establishes the project's existing convention for
  creating an `ApplicationUser` via `UserManager<ApplicationUser>.CreateAsync(user, password)`
  followed by `AddToRoleAsync`, including how `IdentityResult` errors are
  surfaced (`result.Errors`). This plan's teacher-creation flow follows the
  same pattern.
- **Key discovery**: `src/melody/Areas/Identity/Pages/Account/Register.cshtml.cs`
  is the default ASP.NET Core Identity scaffolding, restricted to
  `[Authorize(Roles = "Administrator")]` in the prior `role-based-authorization`
  change. It creates a bare `ApplicationUser` with no role and no `Teacher`
  profile, and — critically — calls `_signInManager.SignInAsync(user, isPersistent: false)`
  on success, which would **log the Administrator out of their own session
  and into the newly created account**. Now that `AdminTeachersController`
  becomes the one real way to create a teacher account, this page is a
  dangerous, orphaned duplicate path and must be disabled (user decision:
  disable it).
- No roadmap slice or FR covers `Room` CRUD — rooms are out of scope for
  this change (confirmed).

## Desired End State

An Administrator can, from `/Admin`:
- See a list of teachers, add a new teacher (name + login email + password),
  edit a teacher's name, and delete a teacher — blocked with a clear message
  if that teacher still has assigned students.
- See a list of students, add a new student (name, class 1–8, instrument,
  assigned teacher), edit those fields (including reassigning the teacher),
  and delete a student — blocked with a clear message listing any active
  reservations if that student has them.
- The "add student" form is disabled with a hint when no teacher exists yet
  (a student cannot be created without one, since `TeacherId` is required).

### Key Discoveries:

- `Student.TeacherId` is non-nullable — every student form (create and
  edit) requires picking a teacher; there is no "unassigned" state to
  support.
- `ApplicationDbContext`'s existing `Restrict` FKs mean the application
  layer only needs to **catch** the blocking condition before attempting
  delete (so the user gets a friendly message instead of a raw DB
  exception) — not implement the blocking rule itself.
- The legacy `/Identity/Account/Register` page must be disabled as part of
  this change (see Current State Analysis) to avoid two divergent,
  inconsistent account-creation paths.

## What We're NOT Doing

- Room (`Room` entity) management — no CRUD, no seed data; out of scope,
  confirmed with the user. A future slice/seed step will populate rooms
  before S-03 needs them.
- Changing a teacher's login email or resetting their password from the
  edit screen — edit only touches `Teacher.FullName`. Credential changes
  are deferred to a future change.
- Auto-generated passwords, "show once" screens, or forced password change
  on first login — the Administrator types the initial password directly,
  mirroring how `AdminUserSeeder` already works.
- Building any reservation-management UI (viewing, canceling, editing
  reservations) — that is roadmap slice S-05. This plan only needs to
  **read** `Student.Reservations` to decide whether a delete is blocked and
  to list what's blocking it.
- Bulk import of students/teachers (explicitly deferred in the PRD, FR-003).
- An inline "reassign all students to X" bulk action during teacher
  deletion — deletion is blocked with a message; the admin reassigns
  students one at a time via the existing student edit form.
- Cascading deletes of reservations when a student is deleted — deletion is
  blocked instead, never silently cascades.

## Implementation Approach

Follow the codebase's existing flat-controller convention (no MVC Areas
beyond the pre-existing `Identity` scaffolding area): add two new
`[Authorize(Roles = "Administrator")]` controllers,
`AdminTeachersController` and `AdminStudentsController`, each with
`Index`/`Create`/`Edit`/`Delete` actions and matching Razor views, following
the same shape as the existing `AdminController`/`TeacherController` pair.
View models live in a new `Models/ViewModels/` folder to keep
create/edit input shapes (and the `Teacher` `<select>` list) separate from
the EF Core domain entities. `Views/Admin/Index.cshtml` is updated to link
into both new areas, replacing the placeholder text.

## Critical Implementation Details

**Two-step account creation consistency**: creating a teacher is not a
single `SaveChanges()` — it's `UserManager.CreateAsync` (creates the
`ApplicationUser`, commits immediately via Identity's own store) followed by
`AddToRoleAsync`, followed by a separate `ApplicationDbContext.Teachers.Add(...)` +
`SaveChangesAsync()` for the `Teacher` profile row. If that last step throws
(e.g. a DB error unrelated to Identity), the result is an orphaned
`ApplicationUser` with a role but no `Teacher` profile — a login that goes
nowhere useful. `AdminTeachersController.Create` must catch that case and
compensate by deleting the just-created `ApplicationUser` via
`userManager.DeleteAsync`, so a failed save never leaves a half-created
account behind.

**Disabling the legacy Register page**: `Register.cshtml.cs`'s
`OnGetAsync`/`OnPostAsync` must short-circuit (e.g. redirect to
`/AdminTeachers/Create`) rather than being deleted outright — deleting the
Identity-scaffolded page risks breaking other scaffolded links/references
that assume the route exists. A redirect keeps the route resolvable while
removing the dangerous account-creation/auto-sign-in behavior.

## Phase 1: Teacher roster management

### Overview

Administrator can list, create (with a real login account), rename, and
delete teachers, with deletion blocked when students are still assigned.
Also disables the legacy Register page per the Critical Implementation
Details above.

### Changes Required:

#### 1. Teacher view models

**File**: `src/melody/Models/ViewModels/TeacherCreateViewModel.cs`

**Intent**: Capture the inputs needed to create both the `ApplicationUser`
login and the `Teacher` profile in one form.

**Contract**: `FullName` (required, max 200), `Email` (required,
`[EmailAddress]`), `Password` (required, `[DataType(DataType.Password)]`),
`ConfirmPassword` (`[Compare(nameof(Password))]`).

**File**: `src/melody/Models/ViewModels/TeacherEditViewModel.cs`

**Intent**: Edit-only inputs — name change alone, per the "NOT doing"
scope decision on credentials.

**Contract**: `Id` (int), `FullName` (required, max 200).

#### 2. `AdminTeachersController`

**File**: `src/melody/Controllers/AdminTeachersController.cs`

**Intent**: `[Authorize(Roles = "Administrator")]` controller providing
`Index` (list all teachers with student counts), `Create` (GET form / POST
creates `ApplicationUser` + `Teacher`, assigns `Teacher` role, surfaces
`IdentityResult.Errors` as model errors on failure, compensates by deleting
the user if the `Teacher` row fails to save — see Critical Implementation
Details), `Edit` (GET/POST — updates `Teacher.FullName` and keeps
`ApplicationUser.DisplayName` in sync with it), and `Delete` (GET shows a
confirmation page or, if `Teacher.Students.Any()`, a blocking message
listing those students' names instead of a confirm button; POST re-checks
the same condition server-side before deleting both the `Teacher` row and
its `ApplicationUser` via `userManager.DeleteAsync`).

**Contract**: injects `UserManager<ApplicationUser>` and
`ApplicationDbContext`, following the constructor-injection pattern already
used elsewhere in the project (e.g. `AdminUserSeeder`'s service resolution).

#### 3. Teacher views

**File**: `src/melody/Views/AdminTeachers/Index.cshtml`, `Create.cshtml`,
`Edit.cshtml`, `Delete.cshtml`

**Intent**: Standard Razor CRUD views following the existing project's
Bootstrap-based layout (`_Layout.cshtml`); `Delete.cshtml` branches between
"confirm delete" and "blocked — reassign these students first" based on a
view-model flag/list passed from the controller.

**Contract**: `Index` links to `Create`/`Edit`/`Delete` per row; `Delete`'s
blocked state lists the assigned students' names (no edit links needed —
the admin navigates to `AdminStudentsController.Edit` separately).

#### 4. Disable the legacy Register page

**File**: `src/melody/Areas/Identity/Pages/Account/Register.cshtml.cs`

**Intent**: Remove the dangerous duplicate account-creation path now that
`AdminTeachersController.Create` is the real one (user decision: disable).

**Contract**: `OnGetAsync` and `OnPostAsync` both immediately
`RedirectToAction("Create", "AdminTeachers")` instead of rendering/processing
the registration form. Leave the `[Authorize(Roles = "Administrator")]`
attribute in place (defense in depth).

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- Existing tests still pass: `dotnet test`

#### Manual Verification:

- Administrator can create a teacher with a working login (can log in as
  that teacher in a separate/incognito session).
- Creating a teacher with an email that's already in use shows a clear
  error and does not create a duplicate/partial account.
- Editing a teacher's name updates what's shown in the teacher list.
- Deleting a teacher with no students succeeds; deleting one with assigned
  students is blocked with a message naming those students.
- Visiting `/Identity/Account/Register` as Administrator redirects to the
  new teacher-create form instead of showing the old registration form.

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that
the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Student roster management

### Overview

Administrator can list, create, edit (including reassigning the teacher),
and delete students, with the add-student form disabled when no teacher
exists yet and deletion blocked when the student has active reservations.

### Changes Required:

#### 1. Student view model

**File**: `src/melody/Models/ViewModels/StudentFormViewModel.cs`

**Intent**: Single shape reused for both create and edit (the fields are
identical in both cases).

**Contract**: `Id` (int, `0` for create), `FullName` (required, max 200),
`Class` (`[Range(1, 8)]`), `Instrument` (`Instrument` enum, required),
`TeacherId` (int, required), `Teachers` (`IEnumerable<SelectListItem>` —
populated by the controller, not bound from the form).

#### 2. `AdminStudentsController`

**File**: `src/melody/Controllers/AdminStudentsController.cs`

**Intent**: `[Authorize(Roles = "Administrator")]` controller providing
`Index` (list all students with their teacher and instrument), `Create`
(GET/POST — GET returns a view flagged "disabled, add a teacher first" when
`Teachers` table is empty instead of rendering a dropdown with no options),
`Edit` (GET/POST — same fields, including reassigning `TeacherId`), and
`Delete` (GET shows confirmation or, if `Student.Reservations.Any()`, a
blocking message listing each reservation's room/day/time; POST re-checks
server-side before deleting).

**Contract**: injects `ApplicationDbContext`; `Create`/`Edit` GET actions
populate `StudentFormViewModel.Teachers` from `context.Teachers` ordered by
`FullName`.

#### 3. Student views

**File**: `src/melody/Views/AdminStudents/Index.cshtml`, `Create.cshtml`,
`Edit.cshtml`, `Delete.cshtml`

**Intent**: Same CRUD view pattern as Phase 1's teacher views.

**Contract**: `Create.cshtml` renders a disabled form with a "add a teacher
first" hint and a link to `AdminTeachersController.Create` when no teachers
exist, instead of an empty `<select>`.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- Existing tests still pass: `dotnet test`

#### Manual Verification:

- With zero teachers in the system, the "add student" form shows the
  disabled/hint state instead of a broken empty dropdown.
- Administrator can create a student assigned to a specific teacher, and
  that student appears under the teacher's assigned-students count.
- Editing a student can reassign them to a different teacher.
- Deleting a student with no reservations succeeds; deleting one with
  active reservations is blocked with a message listing those reservations.

**Implementation Note**: After completing this phase and all automated
verification passes, pause here for manual confirmation from the human that
the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Admin dashboard navigation

### Overview

Replace the "coming soon" placeholder with real navigation into the new
teacher/student management screens.

### Changes Required:

#### 1. Admin dashboard

**File**: `src/melody/Views/Admin/Index.cshtml`

**Intent**: Give the Administrator a starting point to reach the new
screens instead of a dead-end placeholder.

**Contract**: Replace the placeholder paragraph with links to
`AdminTeachersController.Index` and `AdminStudentsController.Index` (e.g.
"Manage teachers" / "Manage students").

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`

#### Manual Verification:

- Logging in as Administrator and landing on `/Admin` shows working links
  to both the teacher and student lists.

---

## Phase 4: Automated verification

### Overview

Cover the two FR-006 blocking rules and confirm the new controllers inherit
the existing role-authorization boundary, following the pattern established
in `tests/melody.Tests/AuthorizationTests.cs`.

### Changes Required:

#### 1. Roster authorization tests

**File**: `tests/melody.Tests/AdminRosterAuthorizationTests.cs`

**Intent**: Confirm a Teacher-role user gets `403 Forbidden` on the new
Administrator-only routes, matching the existing
`Teacher_CanAccessTeacher_ButNotAdmin`-style coverage.

**Contract**: Uses `MelodyWebApplicationFactory.CreateClientAs("Teacher")`
to assert `GET /AdminTeachers` and `GET /AdminStudents` both return
`403 Forbidden`, and `CreateClientAs("Administrator")` asserts both return
`200 OK`.

#### 2. FR-006 blocking-rule tests

**File**: `tests/melody.Tests/AdminRosterDeletionTests.cs`

**Intent**: Prove the deletion-blocking behavior from Phases 1–2 actually
fires, using the SQLite in-memory test database already wired by
`MelodyWebApplicationFactory`.

**Contract**: One test seeds a `Teacher` with an assigned `Student` and
asserts the delete action does not remove the teacher (still present via a
follow-up query) and surfaces a blocking indication; one test seeds a
`Student` with a `Reservation` and asserts the equivalent for student
deletion.

### Success Criteria:

#### Automated Verification:

- New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests`
- New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterDeletionTests`
- Full suite passes: `dotnet test`

---

## Testing Strategy

### Unit Tests:

- Not applicable as a separate layer — the project's existing pattern is
  integration tests against `WebApplicationFactory`, reused here.

### Integration Tests:

- Role boundary on the two new controllers (Phase 4.1).
- FR-006 deletion-blocking for both teacher→students and student→reservations
  (Phase 4.2).

### Manual Testing Steps:

1. Log in as the seeded Administrator; create a teacher; log in as that
   teacher in a separate session to confirm the account works.
2. Attempt to create a teacher with a duplicate email; confirm a clear
   error and no orphaned account.
3. With no teachers in the system, confirm the "add student" form shows the
   disabled/hint state.
4. Create a student assigned to a teacher; delete that teacher and confirm
   it's blocked with a message naming the student.
5. Reassign the student to a different teacher via Edit, then delete the
   original teacher successfully.
6. Visit `/Identity/Account/Register` as Administrator and confirm it
   redirects to the new teacher-create form.

## Performance Considerations

None — list/CRUD screens over a single-school-scale dataset (tens of
teachers/students), no pagination or caching needed.

## Migration Notes

None — `Teacher` and `Student` tables and their FKs already exist from the
`InitialCreate` migration; this plan adds no new columns or tables.

## References

- Roadmap: `context/foundation/roadmap.md` — S-01
- PRD: `context/foundation/prd.md` — FR-003, FR-004, FR-005, FR-006
- Prior change (pattern source): `context/changes/role-based-authorization/plan.md`
- `src/melody/Data/AdminUserSeeder.cs` — account-creation pattern this plan follows
- `src/melody/Data/ApplicationDbContext.cs:24-45` — existing `Restrict` FKs this plan's deletion checks rely on

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Teacher roster management

#### Automated

- [x] 1.1 Solution builds: `dotnet build melodybooker.sln` — 7cbb029
- [x] 1.2 Existing tests still pass: `dotnet test` — 7cbb029

#### Manual

- [x] 1.3 Administrator can create a teacher with a working login — 7cbb029
- [x] 1.4 Duplicate email shows a clear error, no partial account created — 7cbb029
- [x] 1.5 Editing a teacher's name updates the teacher list — 7cbb029
- [x] 1.6 Deleting a teacher with no students succeeds; with students is blocked with a message — 7cbb029
- [x] 1.7 `/Identity/Account/Register` redirects to the new teacher-create form for Administrator — 7cbb029

### Phase 2: Student roster management

#### Automated

- [x] 2.1 Solution builds: `dotnet build melodybooker.sln` — 6135901
- [x] 2.2 Existing tests still pass: `dotnet test` — 6135901

#### Manual

- [x] 2.3 Add-student form shows disabled/hint state with zero teachers — 6135901
- [x] 2.4 Administrator can create a student assigned to a specific teacher — 6135901
- [x] 2.5 Editing a student can reassign them to a different teacher — 6135901
- [x] 2.6 Deleting a student with no reservations succeeds; with reservations is blocked with a message — 6135901

### Phase 3: Admin dashboard navigation

#### Automated

- [ ] 3.1 Solution builds: `dotnet build melodybooker.sln`

#### Manual

- [ ] 3.2 `/Admin` shows working links to both teacher and student lists

### Phase 4: Automated verification

#### Automated

- [ ] 4.1 New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests`
- [ ] 4.2 New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterDeletionTests`
- [ ] 4.3 Full suite passes: `dotnet test`
