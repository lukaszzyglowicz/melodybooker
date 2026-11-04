# Authorization & Ownership Boundaries (Rollout Phase 2) Implementation Plan

## Overview

Close the one grounded gap from `research.md` for test-plan.md Risk #2: prove
that a Teacher-role identity is denied on **every** mutating action of
`AdminTeachersController` and `AdminStudentsController`, not just `Index`. No
production code changes — this is a test-only phase.

## Current State Analysis

- `AdminReservationsController` already has complete Teacher-role denial
  coverage across all 5 of its actions (Index, Edit GET/POST, Delete GET/POST)
  in `tests/melody.Tests/AdminReservationOversightTests.cs:202-224`
  (`Teacher_IsForbidden_OnEveryAdminReservationsAction`).
- `AdminTeachersController` and `AdminStudentsController` carry the identical
  `[Authorize(Roles = "Administrator")]` class attribute
  (`AdminTeachersController.cs:15`, `AdminStudentsController.cs:15`) but their
  existing test, `AdminRosterAuthorizationTests.cs:36-44`
  (`Teacher_CannotAccessAdminTeachers_OrAdminStudents`), only calls `GET
  /AdminTeachers` and `GET /AdminStudents` — i.e. only `Index`.
- Teacher-ownership IDOR on student/reservation data (the other half of Risk
  #2) is already fully tested in `TeacherBooksRoomTests.cs:156-181` — out of
  scope for this phase, no new test needed.

### Key Discoveries:

- `AdminTeachersController` has 7 actions: `Index` (`:27`), `Create` GET
  (`:45`) / POST (`:52`), `Edit` GET (`:101`) / POST (`:114`), `Delete` GET
  (`:143`) / POST as `DeleteConfirmed` with `[ActionName("Delete")]` (`:161`).
- `AdminStudentsController` has the same 7-action shape: `Index` (`:25`),
  `Create` GET (`:43`) / POST (`:50`), `Edit` GET (`:71`) / POST (`:92`),
  `Delete` GET (`:121`) / POST as `DeleteConfirmed` with
  `[ActionName("Delete")]` (`:138`).
- The exact pattern to replicate is
  `AdminReservationOversightTests.cs:202-224`: one `[Fact]` per controller
  that authenticates once as `CreateClientAs("Teacher")`, then asserts
  `HttpStatusCode.Forbidden` on each action call in sequence (GET actions via
  `GetAsync`, POST actions via `PostAsync` with a `FormUrlEncodedContent`
  body — no antiforgery token is needed because the `[Authorize]` role check
  runs before antiforgery validation and rejects with 403 first).
- `AdminRosterAuthorizationTests.cs` seeds no extra data beyond what
  `_factory.EnsureSeededAsync()` already provides (it only hits `Index`,
  which lists possibly-empty collections). The new tests need a real
  `teacherId`/`studentId` to target non-empty `Edit`/`Delete` route segments,
  so each new test must seed at least one teacher and one student first (the
  student needs a teacher to reference via `TeacherId`).

## Desired End State

`AdminRosterAuthorizationTests.cs` has two new `[Fact]` tests —
`Teacher_IsForbidden_OnEveryAdminTeachersAction` and
`Teacher_IsForbidden_OnEveryAdminStudentsAction` — each asserting HTTP 403 on
all 7 actions of its controller when called with a Teacher-role identity.
Verified by: `dotnet test` passes, including the two new facts, and a
deliberate-break check (temporarily loosening one action's attribute) turns
the corresponding assertion red for the right reason.

### Key Discoveries:

(see Current State Analysis above — the shape, line numbers, and pattern to
copy are the material discoveries for this phase)

## What We're NOT Doing

- Not adding a new test file — extending the existing
  `AdminRosterAuthorizationTests.cs` (per user decision).
- Not adding an anonymous-denial parity test for
  `AdminTeachersController`/`AdminStudentsController` (per user decision) —
  anonymous access is already covered generically by the global
  `FallbackPolicy` + root-redirect tests in `AuthorizationTests.cs`, and this
  phase's confirmed scope is Teacher-role denial only.
- Not touching `TeacherReservationsController` or
  `TeacherBooksRoomTests.cs` — the ownership-IDOR sub-scenario of Risk #2 is
  already fully covered there (see `research.md`).
- Not changing any production controller code — all 7 actions on both
  controllers already carry the correct `[Authorize(Roles = "Administrator")]`
  attribute; this phase only adds missing test coverage.
- Not adding a shared base-controller/policy abstraction to prevent future
  attribute drift — out of scope; flagged only as an "Open Risks &
  Assumptions" item in the brief.

## Implementation Approach

Mirror `AdminReservationOversightTests.cs:202-224`'s pattern exactly: one
`[Fact]` per controller, a single `CreateClientAs("Teacher")` client, and a
sequential list of `GetAsync`/`PostAsync` calls each asserting
`HttpStatusCode.Forbidden`. Each test seeds its own teacher/student rows
up front (GUID-suffixed names/emails, following the existing convention in
`TeacherBooksRoomTests.cs`/`AdminReservationOversightTests.cs`) so it needs no
shared fixture changes and can't collide with other tests sharing the SQLite
in-memory connection.

## Phase 1: Authorization parity tests for AdminTeachers/AdminStudents

### Overview

Add the two missing Teacher-role denial tests to
`AdminRosterAuthorizationTests.cs`, covering every action of
`AdminTeachersController` and `AdminStudentsController`.

### Changes Required:

#### 1. New test: `Teacher_IsForbidden_OnEveryAdminTeachersAction`

**File**: `tests/melody.Tests/AdminRosterAuthorizationTests.cs`

**Intent**: Prove a Teacher-role request is denied (403) on every action of
`AdminTeachersController` — not just `Index` — closing the gap identified in
`research.md`.

**Contract**: One `[Fact]` that seeds a teacher (via the existing
`UserManager`/`ApplicationDbContext` pattern already used in
`AdminRosterDeletionTests.cs`'s `SeedTeacherWithAssignedStudentAsync`, but
without an assigned student so `DeleteConfirmed` isn't blocked by the FR-006
rule — that rule is orthogonal to this phase and would make the assertion
ambiguous between "blocked by role" and "blocked by business rule"), obtains
a `CreateClientAs("Teacher")` client, and asserts `HttpStatusCode.Forbidden`
on, in order: `GET /AdminTeachers`, `GET /AdminTeachers/Create`,
`POST /AdminTeachers/Create` (minimal `FormUrlEncodedContent` body),
`GET /AdminTeachers/Edit/{id}`, `POST /AdminTeachers/Edit/{id}` (with `Id`
field set), `GET /AdminTeachers/Delete/{id}`, `POST /AdminTeachers/Delete/{id}`
(with `Id` field set, matching `AdminReservationOversightTests.cs`'s POST-body
shape for its `Delete` call).

#### 2. New test: `Teacher_IsForbidden_OnEveryAdminStudentsAction`

**File**: `tests/melody.Tests/AdminRosterAuthorizationTests.cs`

**Intent**: Prove a Teacher-role request is denied (403) on every action of
`AdminStudentsController` — not just `Index`.

**Contract**: One `[Fact]` that seeds a teacher and one student owned by that
teacher (student has no reservations, so `DeleteConfirmed` isn't blocked by
the FR-006 rule for the same reason as above), obtains a
`CreateClientAs("Teacher")` client, and asserts `HttpStatusCode.Forbidden` on,
in order: `GET /AdminStudents`, `GET /AdminStudents/Create`,
`POST /AdminStudents/Create` (minimal body), `GET /AdminStudents/Edit/{id}`,
`POST /AdminStudents/Edit/{id}` (with `Id` field set),
`GET /AdminStudents/Delete/{id}`, `POST /AdminStudents/Delete/{id}` (with `Id`
field set).

### Success Criteria:

#### Automated Verification:

- New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests`
- Full suite passes with no regressions: `dotnet test`
- Deliberate-break check: temporarily removing
  `[Authorize(Roles = "Administrator")]` from `AdminTeachersController` (or
  `AdminStudentsController`) turns the corresponding new test red with a 200
  (not 403) for the right reason, then the attribute is restored via
  `git checkout --`

#### Manual Verification:

- None — this phase is a pure integration-test addition with no UI-visible
  behavior change; automated verification is sufficient.

---

## Testing Strategy

### Unit Tests:

- Not applicable — this phase is integration-only (role/HTTP-boundary
  behavior cannot be meaningfully unit-tested below the MVC pipeline).

### Integration Tests:

- The two new `[Fact]` tests described above are the entire testing strategy
  for this phase.

### Manual Testing Steps:

- None required (see Manual Verification above).

## Performance Considerations

None — test-only change, no production code or runtime behavior affected.

## Migration Notes

Not applicable — no schema or data changes.

## References

- Related research: `context/changes/testing-authorization-ownership-boundaries/research.md`
- Pattern to replicate: `tests/melody.Tests/AdminReservationOversightTests.cs:202-224`
- Existing (incomplete) test being extended: `tests/melody.Tests/AdminRosterAuthorizationTests.cs:36-44`
- Controllers under test: `src/melody/Controllers/AdminTeachersController.cs`, `src/melody/Controllers/AdminStudentsController.cs`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Authorization parity tests for AdminTeachers/AdminStudents

#### Automated

- [x] 1.1 New tests pass: `dotnet test --filter FullyQualifiedName~AdminRosterAuthorizationTests`
- [x] 1.2 Full suite passes with no regressions: `dotnet test`
- [x] 1.3 Deliberate-break check: removing the Administrator role attribute turns the new test red for the right reason, then is restored
