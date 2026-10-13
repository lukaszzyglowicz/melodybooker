# Teacher Student List — Plan Brief

> Full plan: `context/changes/teacher-student-list/plan.md`

## What & Why

Replace the `TeacherController.Index()` placeholder ("Your assigned students list is coming soon") with a real student list, scoped so each Teacher sees only the students assigned to them — implementing FR-007 and roadmap slice S-02.

## Starting Point

`TeacherController` exists only as a bare `[Authorize(Roles = "Teacher")]` controller with a static placeholder view. The domain model already supports scoping (`Student.TeacherId` → `Teacher`, `Teacher.ApplicationUserId` → the logged-in user), and `AdminStudentsController`/`Views/AdminStudents/Index.cshtml` provide a directly reusable query+table pattern (minus its `TeacherFullName` column, redundant here).

## Desired End State

A Teacher who logs in and opens `/Teacher` sees a table of only their own students (Name, Class, Instrument), ordered by name. A teacher with no assigned students sees a friendly message instead of an empty table.

## Key Decisions Made

| Decision          | Choice                                                      | Why (1 sentence)                                                                 | Source |
| ----------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------- | ------ |
| Page structure     | Merge list into existing `/Teacher` Index                   | Simplest; matches the roadmap outcome literally; no other Teacher pages exist yet | Plan   |
| Columns shown      | Name, Class, Instrument only                                | Matches FR-007 scope; no booking data exists yet to show more                     | Plan   |
| Empty state        | Friendly message, not a blank table                          | Avoids a confusing blank page for a new teacher with no students yet              | Plan   |
| Scoping test auth  | Extend `TestAuthHandler`/`CreateClientAs` with a user-id header | Keeps `ApplicationUserId` a real, meaningful FK; reusable for future scoped tests | Plan   |
| Test coverage depth | Scoping + empty-state + auth-boundary regression            | Covers everything that could silently break, cheaply given existing test patterns | Plan   |

## Scope

**In scope:**
- Scoped `TeacherController.Index()` query (own students only)
- `TeacherStudentListItemViewModel` + updated `Teacher/Index.cshtml`
- Test harness extension for per-user scoped authentication in tests
- Integration tests: data isolation, empty state, role-boundary regression

**Out of scope:**
- Edit/delete actions on this page (Administrator-only, unchanged)
- Reservation/schedule info (booking slice S-03 doesn't exist yet)
- Pagination/search/filtering

## Architecture / Approach

`TeacherController.Index()` resolves the current user's `Teacher` row via `UserManager.GetUserId(User)` → `Teacher.ApplicationUserId`, then queries `Student`s filtered by that `Teacher.Id`, projecting into a new read-only ViewModel. The view reuses the `AdminStudents` table pattern without action columns. Test infrastructure gets a minimal, backward-compatible extension so tests can simulate "logged in as this specific seeded Teacher."

## Phases at a Glance

| Phase                              | What it delivers                                          | Key risk                                                        |
| ----------------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------ |
| 1. Scoped controller query          | `Index()` returns only the current teacher's students       | Edge case where no `Teacher` row matches the user — handled defensively |
| 2. View update                      | Real table + friendly empty state replacing the placeholder | None significant                                                   |
| 3. Test infrastructure extension    | `CreateClientAs` can target a specific seeded user           | Must not break existing `AuthorizationTests`/`AdminRosterAuthorizationTests` |
| 4. Integration tests                | Proves data isolation, empty state, and role-boundary regression | None significant                                                   |

**Prerequisites:** `role-based-authorization` (F-01) and `admin-roster-management` (S-01) — both already archived/done.
**Estimated effort:** ~1 session across 4 phases.

## Open Risks & Assumptions

- Assumes every real Teacher login always has a matching `Teacher` row (created alongside the account by an Administrator); Phase 1 handles the edge case defensively (empty list) rather than erroring, but this path isn't expected to occur in practice.

## Success Criteria (Summary)

- A Teacher sees exactly their own assigned students and never another teacher's.
- A Teacher with no assigned students sees a clear, friendly message instead of a confusing blank table.
- Existing role-boundary tests (Administrator/anonymous/Teacher access checks) continue to pass unchanged.
