# Teacher Student List Implementation Plan

## Overview

Replace the `TeacherController.Index()` placeholder ("Your assigned students list is coming soon") with a real, self-scoped student list: a logged-in Teacher sees only the students assigned to them (FR-007), implementing roadmap slice S-02.

## Current State Analysis

- `TeacherController` (`src/melody/Controllers/TeacherController.cs`) is a bare `[Authorize(Roles = "Teacher")]` controller whose `Index()` action returns a static placeholder view.
- `Teacher` (`src/melody/Models/Domain/Teacher.cs`) has `ApplicationUserId` linking it to the logged-in `ApplicationUser`, and a `Students` collection.
- `Student` (`src/melody/Models/Domain/Student.cs`) has `TeacherId` as the FK used to scope the query, plus `FullName`, `Class` (1-8), and `Instrument`.
- `AdminStudentsController.Index()` (`src/melody/Controllers/AdminStudentsController.cs:24-38`) is the closest existing pattern: EF `Include` + `OrderBy(FullName)` + `Select` into a ViewModel, rendered by `Views/AdminStudents/Index.cshtml`'s table markup — but it lists *all* students and includes a `TeacherFullName` column that's redundant for a teacher viewing their own list.
- The test harness (`tests/melody.Tests/MelodyWebApplicationFactory.cs`, `tests/melody.Tests/TestAuthHandler.cs`) authenticates `CreateClientAs("Teacher")` with a fixed, fake `NameIdentifier` claim (`"test-teacher"`) that isn't tied to any real seeded `Teacher`/`ApplicationUser` row. This is sufficient for today's role-boundary tests (200 vs 403) but cannot verify "a teacher sees only their own students" without an extension.

## Desired End State

A Teacher who logs in and navigates to `/Teacher` sees a table of only the students where `Student.TeacherId` matches their own `Teacher.Id`, ordered by name, showing Name/Class/Instrument. A teacher with zero assigned students sees a friendly empty-state message instead of an empty table. Verified by: integration tests proving data isolation between two teachers, the empty-state path, and the existing Administrator/anonymous role-boundary checks still pass.

### Key Discoveries:

- `Teacher.ApplicationUserId` (`src/melody/Models/Domain/Teacher.cs:13`) is the FK connecting the ASP.NET Identity user to the domain `Teacher` row — this is how the controller resolves "my own students."
- `TestAuthHandler.HandleAuthenticateAsync` (`tests/melody.Tests/TestAuthHandler.cs:30-44`) hardcodes the `NameIdentifier` claim as `$"test-{role}"` — this needs a backward-compatible extension point so tests can authenticate as a specific real `ApplicationUserId`.
- `AdminRosterDeletionTests` (`tests/melody.Tests/AdminRosterDeletionTests.cs`) already shows the pattern for seeding a real `ApplicationUser` + `Teacher` + `Student` via `UserManager`/`ApplicationDbContext` in a test — reusable for seeding the scoping test fixtures.

## What We're NOT Doing

- No edit/delete actions on this page — FR-007 is read-only; roster mutation stays Administrator-only (FR-004/FR-005).
- No reservation/schedule information on this list — booking (S-03) doesn't exist yet.
- No pagination/search/filtering — out of scope for MVP per FR-007 ("current list sufficient for MVP").
- No change to the `TeacherFullName` column pattern used by `AdminStudentsController` — this new view model is separate and deliberately omits that column since it's always the viewing teacher.

## Implementation Approach

Add a scoped query to `TeacherController.Index()` that resolves the current user's `Teacher` row via `ApplicationUserId`, then projects their `Student`s into a new `TeacherStudentListItemViewModel`. Replace the placeholder view with an `AdminStudents`-style table plus a friendly empty state. Extend the test harness with a backward-compatible way to authenticate as a specific seeded `Teacher`, then add integration tests proving the isolation guarantee.

## Phase 1: Scoped controller query

### Overview

Resolve the logged-in Teacher and scope the student query to only their own students.

### Changes Required:

#### 1. Teacher-scoped student list ViewModel

**File**: `src/melody/Models/ViewModels/TeacherStudentListItemViewModel.cs`

**Intent**: A row in the teacher's own student list — no `TeacherFullName` column since it's always the viewing teacher.

**Contract**: Properties `Id` (int), `FullName` (string, `[Display(Name = "Full name")]`), `Class` (int, `[Display(Name = "Class")]`), `Instrument` (string), mirroring `StudentListItemViewModel` minus `TeacherFullName`.

#### 2. Scoped `Index` action

**File**: `src/melody/Controllers/TeacherController.cs`

**Intent**: Resolve the current user's `Teacher` row, then query only `Student`s with a matching `TeacherId`, ordered by `FullName`, projected into `TeacherStudentListItemViewModel`. If no `Teacher` row exists for the current user (defensive edge case — shouldn't happen in practice since Administrators create Teacher accounts together with their login), return an empty list rather than erroring.

**Contract**: Inject `ApplicationDbContext` (matching `AdminStudentsController`'s constructor-injection pattern) and `UserManager<ApplicationUser>` (matching `AdminTeachersController`'s pattern). Resolve the current user's id via `_userManager.GetUserId(User)`, look up `_context.Teachers.FirstOrDefaultAsync(t => t.ApplicationUserId == userId)`, then query `_context.Students.Where(s => s.TeacherId == teacher.Id)`. `Index()` becomes `async Task<IActionResult>`.

### Success Criteria:

#### Automated Verification:

- Build succeeds: `dotnet build melodybooker.sln`

#### Manual Verification:

- N/A for this phase alone (covered by Phase 4 integration tests and Phase 2's manual check)

---

## Phase 2: View update

### Overview

Replace the `Teacher/Index.cshtml` placeholder with the students table and a friendly empty state.

### Changes Required:

#### 1. Teacher Index view

**File**: `src/melody/Views/Teacher/Index.cshtml`

**Intent**: Show the scoped student list using the same table structure as `Views/AdminStudents/Index.cshtml` (Name/Class/Instrument columns, no action column since this view is read-only), replacing the "coming soon" placeholder text. When the list is empty, show a friendly message (e.g. "You don't have any assigned students yet.") instead of an empty table.

**Contract**: `@model List<TeacherStudentListItemViewModel>`; `ViewData["Title"] = "My Students"` (or similar); no edit/delete links per FR-007's read-only scope.

### Success Criteria:

#### Automated Verification:

- Build succeeds: `dotnet build melodybooker.sln`

#### Manual Verification:

- Logging in as a Teacher with assigned students shows those students' Name/Class/Instrument in a table
- Logging in as a Teacher with no assigned students shows the friendly empty-state message, not a blank table

---

## Phase 3: Test infrastructure extension

### Overview

Extend the test harness so integration tests can authenticate as a specific seeded `Teacher`'s `ApplicationUserId`, without changing the default behavior existing tests rely on.

### Changes Required:

#### 1. `TestAuthHandler` user-id override

**File**: `tests/melody.Tests/TestAuthHandler.cs`

**Intent**: Allow a test to supply a specific user id for the `NameIdentifier` claim (so it matches a real seeded `ApplicationUser.Id`/`Teacher.ApplicationUserId`), while keeping the existing fixed `"test-{role}"` behavior as the default when no id is supplied.

**Contract**: Add a new request header constant (e.g. `UserIdHeaderName = "X-Test-UserId"`). In `HandleAuthenticateAsync`, if that header is present and non-empty, use its value as the `NameIdentifier` claim instead of `$"test-{role}"`; all other claims (`Name`, `Role`) are unaffected.

#### 2. `CreateClientAs` overload

**File**: `tests/melody.Tests/MelodyWebApplicationFactory.cs`

**Intent**: Let tests request a client authenticated as a specific user id, in addition to the existing role-only overload.

**Contract**: Add `CreateClientAs(string? role, string? userId)` (or an optional `userId = null` parameter on the existing method) that also sets the `X-Test-UserId` header when `userId` is supplied. Existing single-argument calls keep their current behavior unchanged.

### Success Criteria:

#### Automated Verification:

- All existing tests still pass unchanged: `dotnet test --filter FullyQualifiedName~AuthorizationTests`
- All existing tests still pass unchanged: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests`

#### Manual Verification:

- N/A

---

## Phase 4: Integration tests

### Overview

Verify the data-isolation guarantee, the empty-state path, and the existing role-boundary behavior.

### Changes Required:

#### 1. Teacher student list tests

**File**: `tests/melody.Tests/TeacherStudentListTests.cs`

**Intent**: Prove the three behaviors this feature must guarantee: a teacher sees only their own students (not another teacher's), a teacher with zero students sees the friendly empty state, and non-Teacher roles remain forbidden on `/Teacher` (regression check against the existing `AuthorizationTests.Administrator_CanAccessAdmin_ButNotTeacher`/`Teacher_CanAccessTeacher_ButNotAdmin` coverage, exercised here with the new user-id-scoped client).

**Contract**: Use the `AdminRosterDeletionTests`-style seeding pattern (real `ApplicationUser` via `UserManager`, `Teacher`, `Student` via `ApplicationDbContext`) to seed two teachers with distinct students, then `CreateClientAs("Teacher", teacherAUser.Id)` and assert the response body contains Teacher A's student name(s) and not Teacher B's. A third seeded teacher with no students asserts the empty-state message appears.

### Success Criteria:

#### Automated Verification:

- New tests pass: `dotnet test --filter FullyQualifiedName~TeacherStudentListTests`
- Full test suite passes: `dotnet test`

#### Manual Verification:

- N/A

---

## Testing Strategy

### Unit Tests:

- N/A — this feature is thin enough that integration tests (below) cover it directly; no isolated unit-testable logic beyond the EF query itself.

### Integration Tests:

- Teacher A's `/Teacher` page shows only Teacher A's students, never Teacher B's (FR-002 data isolation).
- A teacher with zero assigned students sees the friendly empty-state message.
- Non-Teacher roles (Administrator, anonymous) remain forbidden/redirected on `/Teacher`, confirming the existing role boundary didn't regress.

### Manual Testing Steps:

1. Log in as a seeded Teacher with assigned students and confirm `/Teacher` shows exactly those students.
2. Log in as a Teacher with no assigned students and confirm the friendly empty-state message appears.

## References

- Pattern reused: `src/melody/Controllers/AdminStudentsController.cs:24-38`, `src/melody/Views/AdminStudents/Index.cshtml`
- Test seeding pattern reused: `tests/melody.Tests/AdminRosterDeletionTests.cs`
- Roadmap outcome: `context/foundation/roadmap.md` (S-02)
- Requirement: `context/foundation/prd.md` (FR-007)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Scoped controller query

#### Automated

- [x] 1.1 Build succeeds: `dotnet build melodybooker.sln` — ae2e05f

### Phase 2: View update

#### Automated

- [x] 2.1 Build succeeds: `dotnet build melodybooker.sln` — 7d9dfa0

#### Manual

- [x] 2.2 Logging in as a Teacher with assigned students shows those students' Name/Class/Instrument in a table — 7d9dfa0
- [x] 2.3 Logging in as a Teacher with no assigned students shows the friendly empty-state message, not a blank table — 7d9dfa0

### Phase 3: Test infrastructure extension

#### Automated

- [x] 3.1 All existing tests still pass unchanged: `dotnet test --filter FullyQualifiedName~AuthorizationTests` — cc2f956
- [x] 3.2 All existing tests still pass unchanged: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests` — cc2f956

### Phase 4: Integration tests

#### Automated

- [x] 4.1 New tests pass: `dotnet test --filter FullyQualifiedName~TeacherStudentListTests`
- [x] 4.2 Full test suite passes: `dotnet test`
