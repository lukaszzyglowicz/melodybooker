---
date: 2026-11-04T13:52:21+01:00
researcher: Copilot CLI
git_commit: 84e238bc533b4575f60adc8ab71d9001ea8c452f
branch: m3l2
repository: lukaszzyglowicz/melodybooker
topic: "Rollout Phase 2 — Authorization & ownership boundaries (test-plan.md Risk #2)"
tags: [research, codebase, authorization, idor, controllers, testing]
status: complete
last_updated: 2026-11-04
last_updated_by: Copilot CLI
---

# Research: Rollout Phase 2 — Authorization & ownership boundaries (Risk #2)

**Date**: 2026-11-04T13:52:21+01:00
**Researcher**: Copilot CLI
**Git Commit**: 84e238bc533b4575f60adc8ab71d9001ea8c452f
**Branch**: m3l2
**Repository**: lukaszzyglowicz/melodybooker

## Research Question

Ground rollout Phase 2 of `context/foundation/test-plan.md` (Risk #2 — a teacher
reaches/mutates another teacher's student/reservation data by ID, or a
role-boundary refactor momentarily lets the wrong role reach an admin-only
action). Verify the risk-response guidance in test-plan.md §2 ("prove a request
for another teacher's student/reservation is always denied... admin-only
actions are unreachable to a teacher-role request"), locate existing tests,
identify the cheapest useful remaining test layer, and flag speculative risk
framing or misleading hot-spot evidence.

## Summary

Risk #2's two sub-scenarios are **already covered to different degrees**, and
the test-plan's implicit framing (that this is untested ground) is **stale for
one sub-scenario and accurate for the other**:

1. **Teacher-ownership IDOR on student/reservation data** (a teacher reaching
   another teacher's student by ID) is **already fully tested**, for both GET
   and POST, in `tests/melody.Tests/TeacherBooksRoomTests.cs:156-181`. No
   further test is needed for this sub-scenario — the plan should be corrected
   to cite this existing coverage rather than re-investigate it.
2. **Role-boundary unreachability of admin-only actions to a Teacher-role
   request** is tested **completely for `AdminReservationsController`**
   (`tests/melody.Tests/AdminReservationOversightTests.cs:202-224`, covering
   every action: Index, Edit GET/POST, Delete GET/POST) but **only partially
   for `AdminTeachersController`/`AdminStudentsController`**
   (`tests/melody.Tests/AdminRosterAuthorizationTests.cs:36-44` covers only the
   `Index` GET action of each controller — `Create`, `Edit`, `Delete`, and
   `DeleteConfirmed` are untested for Teacher-role denial on those two
   controllers). This is the one real, grounded gap for this rollout phase.

The cheapest useful remaining layer is **integration** (matches the plan's
guidance), reusing the exact pattern already established in
`AdminReservationOversightTests.cs:203-224`
(`Teacher_IsForbidden_OnEveryAdminReservationsAction`) — extend it to
`AdminTeachersController` and `AdminStudentsController`'s mutating actions.

No code changes are needed to close the gap: authorization is enforced purely
via `[Authorize(Roles = "Administrator")]`/`[Authorize(Roles = "Teacher")]`
class-level attributes (verified for every one of the 7 controllers, see Code
References), with no shared base-controller or policy indirection that could
silently drift between controllers — each controller declares its own
attribute independently, which is itself a (minor) risk the plan's phrase
"role-boundary refactor momentarily lets the wrong role reach an admin-only
action" correctly anticipates, since nothing currently guards against one
controller's attribute being edited/removed without the others.

## Detailed Findings

### Ownership enforcement model (claims vs. DB lookup)

Every "current identity" lookup in the five Teacher/Admin-oriented controllers
that need it resolves the ASP.NET Identity user ID via
`_userManager.GetUserId(User)` and then looks up the domain `Teacher` row by
`ApplicationUserId == userId` — a DB lookup keyed off the authenticated
identity claim, not a trusted client-supplied ID:

- `src/melody/Controllers/TeacherReservationsController.cs:160-163`
- `src/melody/Controllers/TeacherController.cs:25-26`

Ownership scoping for Teacher-role actions that accept a `studentId` is
enforced by filtering the DB query on both the record ID **and** the resolved
teacher's ID in one query (not a separate "fetch then compare" check that
could be skipped):

```csharp
// src/melody/Controllers/TeacherReservationsController.cs:166-174
private async Task<Student?> GetOwnedStudentAsync(Teacher? teacher, int studentId)
{
    if (teacher is null) { return null; }
    return await _context.Students
        .FirstOrDefaultAsync(s => s.Id == studentId && s.TeacherId == teacher.Id);
}
```

Both `TeacherReservationsController.Create` overloads (GET at
`TeacherReservationsController.cs:28`, POST at `:56`) call this helper before
doing anything else with `studentId`/`model.StudentId`; a non-owned or
nonexistent ID returns `null` → `NotFound()` either way, so the response
cannot distinguish "wrong owner" from "doesn't exist" (correct — this is what
the plan's "always denied (403/404)" guidance expects, confirmed, not just
assumed).

`TeacherController.Index()` (student list) is scoped the same way via
`.Where(s => s.TeacherId == teacher.Id)` at `TeacherController.cs:34` — a
query-level filter, not a post-fetch check.

### Admin controllers are intentionally unscoped by ID — this is not an IDOR

`AdminTeachersController`, `AdminStudentsController`, and
`AdminReservationsController` all fetch records by ID alone
(`FindAsync(id)` / `FirstOrDefaultAsync(x => x.Id == id)`) with **no**
per-record ownership filter — e.g.
`src/melody/Controllers/AdminStudentsController.cs:73`,
`src/melody/Controllers/AdminReservationsController.cs:60,101,187,201`. This
is correct by design (PRD: Administrator manages/sees/edits *all*
teachers/students/reservations — there is no "owner" to scope against for an
Administrator), **not** a missed ownership check. The plan's risk framing
("admin-only actions are unreachable to a teacher-role request") correctly
targets the *role* boundary around these controllers, not a per-record
ownership boundary — confirming the plan's own distinction between the two
risk components is the right one to test separately.

### Role-boundary test coverage — existing vs. gap

| Controller | Role attribute | Teacher-role denial test | Coverage |
|---|---|---|---|
| `AdminController` | `[Authorize(Roles = "Administrator")]` (`AdminController.cs:6`) | `AuthorizationTests.cs:67-75` (`Teacher_CanAccessTeacher_ButNotAdmin`) | `Index` only (its only action) — complete |
| `AdminReservationsController` | `[Authorize(Roles = "Administrator")]` (`AdminReservationsController.cs:16`) | `AdminReservationOversightTests.cs:202-224` (`Teacher_IsForbidden_OnEveryAdminReservationsAction`) | **Index, Edit GET, Edit POST, Delete GET, Delete POST — all 5 actions. Complete.** |
| `AdminTeachersController` | `[Authorize(Roles = "Administrator")]` (`AdminTeachersController.cs:15`) | `AdminRosterAuthorizationTests.cs:36-44` (`Teacher_CannotAccessAdminTeachers_OrAdminStudents`) | **`Index` GET only.** `Create` GET/POST, `Edit` GET/POST, `Delete` GET, `DeleteConfirmed` POST are untested for Teacher-role denial. |
| `AdminStudentsController` | `[Authorize(Roles = "Administrator")]` (`AdminStudentsController.cs:15`) | same test as above | **`Index` GET only.** Same gap as `AdminTeachersController`. |
| `TeacherController` | `[Authorize(Roles = "Teacher")]` (`TeacherController.cs:11`) | `AuthorizationTests.cs:90-97` (`Teacher_RootRedirectsToTeacherController`) + `AuthorizationTests.cs:55-63` (`Administrator_CanAccessAdmin_ButNotTeacher`) | `Index` only (its only action) — complete |
| `TeacherReservationsController` | `[Authorize(Roles = "Teacher")]` (`TeacherReservationsController.cs:16`) | `TeacherBooksRoomTests.cs:189-196` (`Create_Administrator_IsForbidden`) | `Create` GET only tested for admin-denial; no admin-role POST-denial test exists, but POST already requires passing the ownership check first (see below), so an admin identity has no matching `Teacher` row and would 404, not reach a role gate ambiguity — lower-value gap |
| `HomeController` | `[Authorize]` (any authenticated role) (`HomeController.cs:8`) | `AuthorizationTests.cs:24-30` (anonymous-only redirect) | No role-exclusion applicable (any authenticated role is allowed) — not a gap |

The gap is narrow and well-defined: **`AdminTeachersController` and
`AdminStudentsController`'s mutating actions (`Create`, `Edit`, `Delete`,
`DeleteConfirmed`) have no test proving a Teacher-role request is denied**,
even though the identical class-level attribute pattern is already proven to
work for `Index` on the same two controllers and for all 5 actions on
`AdminReservationsController`. The risk this leaves open matches the plan's
own framing: a future edit to either controller (e.g. splitting `Edit` into a
new action, or copy-pasting a new admin controller) could regress the
attribute without any test catching it, because today's suite only exercises
`Index`.

### Teacher-ownership IDOR — already covered (correct existing test, not a gap)

`tests/melody.Tests/TeacherBooksRoomTests.cs:156-181` contains two tests that
directly match the plan's stated proof target ("a request for another
teacher's student/reservation, made directly at the endpoint with a different
owner's ID, is always denied"):

```csharp
// TeacherBooksRoomTests.cs:156-165
[Fact]
public async Task Create_Get_ForAnotherTeachersStudent_ReturnsNotFound()
{
    var (_, _, studentAId) = await SeedTeacherWithStudentAsync("Teacher A Owner", classNumber: 3);
    var (teacherBUserId, _, _) = await SeedTeacherWithStudentAsync("Teacher B Other", classNumber: 3);
    var client = _factory.CreateClientAs("Teacher", teacherBUserId);

    var response = await client.GetAsync($"/TeacherReservations/Create?studentId={studentAId}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

and an equivalent POST variant (`:171-181`,
`Create_Post_ForAnotherTeachersStudent_ReturnsNotFound`) that posts Teacher A's
`studentId` while authenticated as Teacher B, using a validly-obtained
antiforgery token from Teacher B's own page (so the mismatch under test is
genuinely the posted `StudentId`, not a forgery-token failure) — this
correctly avoids the plan's named anti-pattern ("testing only the 'own
student' happy path and treating absence of an error as proof of correct
scoping"), since it explicitly exercises the cross-owner path and asserts a
denial, not an omission.

**Correction to test-plan.md**: §2's Risk #2 row and §3 Phase 2 framing
implicitly treat this sub-scenario as unverified ("Context `/10x-research`
must ground: How ownership is checked today... whether it's enforced per-
action or only via shared query scoping"). It is already both grounded
(per-action query-level filtering, confirmed above) and already tested
end-to-end. No new test should be written to re-prove this.

### Test infrastructure (cheapest layer confirmation)

- `TestAuthHandler.cs:22-51` authenticates a request only when the
  `X-Test-Role` header is present, builds `NameIdentifier`/`Name`/`Role`
  claims from `X-Test-Role`/`X-Test-UserId`, and otherwise returns
  `AuthenticateResult.NoResult()` (anonymous) — this is a deliberate
  test-only shortcut around real Identity sign-in, confirmed by its
  `:35-39` early-return.
- `MelodyWebApplicationFactory.CreateClientAs(role, userId?)` sets those two
  headers (`MelodyWebApplicationFactory.cs:127-140`); passing a specific
  `userId` (as `TeacherBooksRoomTests.cs` does) is what lets a test select
  *which* seeded teacher is "logged in" — this is the existing, proven
  mechanism for cross-owner tests and is directly reusable for the
  `AdminTeachersController`/`AdminStudentsController` gap (role-only, no
  specific owner ID is needed there, matching `AdminRosterAuthorizationTests.cs`'s
  existing `CreateClientAs("Teacher")` call with no second argument).
- The factory uses a single open **SQLite in-memory** connection
  (`MelodyWebApplicationFactory.cs:32`, `DataSource=:memory:`), shared across
  tests in the same test class via `IClassFixture`
  (`:144-151` — connection stays open for the factory's lifetime). Each test
  seeds its own uniquely-named records (GUID-suffixed emails/room names), so
  tests in the same class don't collide despite the shared DB — this is the
  established pattern to follow for any new test in this phase (integration,
  `IClassFixture<MelodyWebApplicationFactory>`, `IAsyncLifetime` with
  `InitializeAsync` calling `_factory.EnsureSeededAsync()`).
- `AdminRosterAuthorizationTests.cs` is the **smallest existing file** testing
  this exact pattern (2 tests, no seeding needed beyond what
  `EnsureSeededAsync()` already provides) — it is the natural home for the
  new assertions, or a direct sibling file following the same shape, rather
  than introducing a new test layer.

## Code References

- `src/melody/Controllers/TeacherReservationsController.cs:16` — `[Authorize(Roles = "Teacher")]` class attribute
- `src/melody/Controllers/TeacherReservationsController.cs:160-163` — current-teacher resolution via `UserManager` + `ApplicationUserId` claim lookup
- `src/melody/Controllers/TeacherReservationsController.cs:166-174` — `GetOwnedStudentAsync`, the single-query ownership filter (`s.Id == studentId && s.TeacherId == teacher.Id`)
- `src/melody/Controllers/TeacherController.cs:11,25-26,34` — role attribute, identity resolution, query-level ownership scoping for the student list
- `src/melody/Controllers/AdminTeachersController.cs:15,103,126,145,166-169` — role attribute; unscoped-by-ID fetches in `Edit`/`Delete`/`DeleteConfirmed` (correct for Administrator)
- `src/melody/Controllers/AdminStudentsController.cs:15,73,105,126,143` — role attribute; unscoped-by-ID fetches in `Edit`/`Delete`/`DeleteConfirmed` (correct for Administrator)
- `src/melody/Controllers/AdminReservationsController.cs:16,60,101,187,201` — role attribute; unscoped-by-ID fetches in `Edit`/`Delete`/`DeleteConfirmed` (correct for Administrator)
- `src/melody/Controllers/AdminController.cs:6` — role attribute, single `Index` action
- `src/melody/Controllers/HomeController.cs:8` — bare `[Authorize]`, no role restriction
- `src/melody/Program.cs:31-34` — global `FallbackPolicy` requiring authentication; no named policies registered, role checks live entirely in per-controller attributes
- `tests/melody.Tests/TeacherBooksRoomTests.cs:156-165` — `Create_Get_ForAnotherTeachersStudent_ReturnsNotFound` (existing cross-owner GET denial test)
- `tests/melody.Tests/TeacherBooksRoomTests.cs:171-181` — `Create_Post_ForAnotherTeachersStudent_ReturnsNotFound` (existing cross-owner POST denial test)
- `tests/melody.Tests/TeacherBooksRoomTests.cs:189-196` — `Create_Administrator_IsForbidden`
- `tests/melody.Tests/TeacherBooksRoomTests.cs:200-207` — `Create_Anonymous_RedirectsToLogin`
- `tests/melody.Tests/AdminRosterAuthorizationTests.cs:24-33` — `Administrator_CanAccessAdminTeachers_AndAdminStudents` (Index only, both controllers)
- `tests/melody.Tests/AdminRosterAuthorizationTests.cs:36-44` — `Teacher_CannotAccessAdminTeachers_OrAdminStudents` (Index only, both controllers — **the gap**)
- `tests/melody.Tests/AdminReservationOversightTests.cs:202-224` — `Teacher_IsForbidden_OnEveryAdminReservationsAction` (the complete pattern to replicate for the two roster controllers)
- `tests/melody.Tests/AdminReservationOversightTests.cs:231-244` — `Anonymous_RedirectsToLogin_OnEveryAdminReservationsAction`
- `tests/melody.Tests/AuthorizationTests.cs:24-97` — 7 role/anonymous redirect tests at the controller-root level (`/`, `/Teacher`, `/Admin`, `/Identity/Account/Register`); none use a specific record ID
- `tests/melody.Tests/TestAuthHandler.cs:22-51` — test-only auth handler; header-driven role/user-id claim injection
- `tests/melody.Tests/MelodyWebApplicationFactory.cs:32,60-71,127-140,144-151` — SQLite in-memory factory setup, `CreateClientAs`, shared-connection lifetime
- `tests/melody.Tests/AdminRosterDeletionTests.cs:1-15` — unrelated (FR-006 delete-blocking rules, not authorization) but confirms the antiforgery-token-extraction helper pattern reusable for any new POST test

## Architecture Insights

- Authorization in this codebase is **100% attribute-based**
  (`[Authorize(Roles = "...")]`), with zero custom `IAuthorizationHandler` or
  named policies (`Program.cs:31-34` only sets a `FallbackPolicy` requiring
  authentication). This means the "role-boundary refactor" risk named in the
  plan is realistic in a specific, narrow way: **removing or renaming** a
  controller's attribute (or extracting a shared base controller without
  carrying the attribute along) is the only way this regresses — there's no
  shared policy object whose single edit could silently affect multiple
  controllers at once (so the blast radius of one bad edit is one controller,
  not systemic).
- Ownership scoping is implemented consistently as a **single filtered query**
  (`Where`/`FirstOrDefaultAsync` with both the ID and the owner-claim
  condition) rather than "fetch then compare" — this is the stronger pattern
  (can't accidentally skip the check after the fetch) and is confirmed in both
  places it's used (`TeacherReservationsController`, `TeacherController`).
- The existing test suite already establishes two *different* granularities
  of role-boundary test: a single "spot check the Index page" pattern
  (`AdminRosterAuthorizationTests.cs`) and a complete "every action, GET and
  POST" pattern (`AdminReservationOversightTests.cs`). The latter is strictly
  the stronger/safer pattern and should be the target shape for closing the
  gap, not a regression to the former's narrower shape.

## Historical Context (from prior changes)

- No existing `context/changes/**/research.md` or `context/archive/**/` folder
  discusses authorization design directly (archive is currently empty except
  for `README.md`; no prior change folder for
  `admin-reservation-oversight` remains on disk to consult — its artifacts are
  not present under `context/changes/` or `context/archive/` at this commit).
  This finding is scoped to what's on disk now; no inference is made about
  why that folder is absent.
- `context/foundation/test-plan.md` §2 Risk #2 row and its Risk Response
  Guidance table are the only existing planning document for this phase; both
  are addressed above (one sub-claim corrected, one confirmed).

## Related Research

- `context/changes/testing-booking-conflict-correctness/research.md` — prior
  rollout phase (Risk #1), same stack and `MelodyWebApplicationFactory`
  pattern; not re-read in full for this research since its subject
  (double-booking conflict logic) is unrelated to authorization.

## Open Questions

None blocking. The scope requested (ground Risk #2, verify response guidance,
locate existing tests, identify cheapest layer, flag speculative framing) is
fully answered. The only judgment call left for `/10x-plan` is whether to
extend `AdminRosterAuthorizationTests.cs` in place or add a new sibling file —
both are consistent with the codebase's existing per-concern test-file
convention (see `AdminReservationOversightTests.cs` as a sibling to
`AdminRosterAuthorizationTests.cs` rather than a merge into one file).
