# Authorization & Ownership Boundaries (Rollout Phase 2) — Plan Brief

> Full plan: `context/changes/testing-authorization-ownership-boundaries/plan.md`
> Research: `context/changes/testing-authorization-ownership-boundaries/research.md`

## What & Why

Prove that role and ownership checks hold at the endpoint, independent of the
UI, for `test-plan.md` Risk #2 (a teacher reaching another teacher's data, or
a role-boundary regression letting the wrong role reach an admin-only
action). Research found the ownership-IDOR half of this risk already fully
tested; this plan closes the one remaining gap: Teacher-role denial coverage
for `AdminTeachersController`/`AdminStudentsController`'s mutating actions.

## Starting Point

`AdminReservationsController` already has complete Teacher-role denial tests
across all 5 of its actions (`AdminReservationOversightTests.cs:202-224`).
`AdminTeachersController`/`AdminStudentsController` carry the identical
`[Authorize(Roles = "Administrator")]` attribute, but their existing test
(`AdminRosterAuthorizationTests.cs:36-44`) only exercises `Index` — `Create`,
`Edit`, `Delete`, and `DeleteConfirmed` on both controllers are untested for
Teacher-role denial.

## Desired End State

`AdminRosterAuthorizationTests.cs` asserts 403 for a Teacher-role identity on
every action of both roster controllers, matching the coverage level already
proven for `AdminReservationsController`. No production code changes.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Teacher-ownership IDOR sub-scenario | No new test needed | `TeacherBooksRoomTests.cs:156-181` already covers it end-to-end (GET + POST cross-owner denial) | Research |
| Test file placement | Extend `AdminRosterAuthorizationTests.cs` | Keeps authorization tests for these two controllers in one file, matching how `Index`-only coverage already lives there | Plan |
| Test scope | Teacher-role denial on all 7 actions per controller; no anonymous-parity test | The confirmed risk is role-boundary regression for Teacher, not anonymous access (already covered generically by the global `FallbackPolicy`) | Plan |
| Pattern to replicate | `AdminReservationOversightTests.cs:202-224`'s single-`[Fact]`-per-controller, sequential-assert shape | Already proven, avoids inventing a new test shape | Research |
| Seed data for Delete/DeleteConfirmed | Teacher/student seeded *without* existing assignments/reservations | Avoids the FR-006 delete-blocking rule making the 403 assertion ambiguous with a business-rule block | Plan |

## Scope

**In scope:**
- `Teacher_IsForbidden_OnEveryAdminTeachersAction` (new, 7 actions)
- `Teacher_IsForbidden_OnEveryAdminStudentsAction` (new, 7 actions)

**Out of scope:**
- Anonymous-denial parity tests for these two controllers
- Any change to `TeacherReservationsController`/`TeacherBooksRoomTests.cs`
- Any production controller code change
- A shared base-controller/policy mechanism to prevent future attribute drift

## Architecture / Approach

Pure integration-test addition using the existing
`MelodyWebApplicationFactory` + `TestAuthHandler` + `CreateClientAs("Teacher")`
infrastructure — no new test layer, no new fixture, no production code touched.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Authorization parity tests | Two new `[Fact]` tests covering all 7 actions each of `AdminTeachersController`/`AdminStudentsController` for Teacher-role denial | Low — pattern already proven elsewhere in the suite; main risk is seeding data that accidentally triggers an FR-006 block instead of the role check |

**Prerequisites:** None — builds entirely on existing test infrastructure.
**Estimated effort:** ~1 session, single phase.

## Open Risks & Assumptions

- Authorization in this codebase is 100% attribute-based with no shared
  policy object (see `research.md` Architecture Insights) — a future
  controller split/refactor could still drop the attribute without any
  compile-time guard. This plan only closes the *test-coverage* gap, not the
  structural risk; out of scope by user decision.

## Success Criteria (Summary)

- `dotnet test` passes, including the two new facts, with no regressions.
- A deliberate-break check (temporarily removing one controller's role
  attribute) turns the corresponding new test red for the right reason, then
  is restored.
