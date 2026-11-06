# Log swallowed db exceptions — Plan Brief

> Full plan: `context/changes/log-swallowed-db-exceptions/plan.md`

## What & Why

Three controllers catch `DbUpdateException` and silently convert it into a
generic user-facing error, with zero logging. A genuine double-booking race
and an unrelated DB failure (deadlock, dropped connection) look identical —
invisible — to anyone on call. This plan adds `ILogger<T>` to all three and
logs the caught exception with non-PII domain context, proven with
integration tests that genuinely trigger the exception.

## Starting Point

`AdminReservationsController.cs:170`, `TeacherReservationsController.cs:134`,
and `AdminTeachersController.cs:90` each have `catch (DbUpdateException) { ...
no log ... }`. The only `ILogger` usage in the whole codebase is in
`Data/AdminUserSeeder.cs` (startup-only). No controller currently has a
logger dependency.

## Desired End State

Each catch block logs a `Warning` with the exception and relevant domain ids
(room/day/time/student/teacher/ApplicationUser) before falling back to the
existing user-facing message — same behavior as today, plus a diagnostic
trail. Proven by 3 new integration tests that force a real
`DbUpdateException` via a test-only fault-injecting `DbContext` and assert
the log entry appears.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Scope | Only add logging to the 3 existing catch blocks | Minimal, fast-to-review change; related findings (#4 unchecked compensating delete, #5 silent orphaned-account symptom) are separate changes | Plan |
| Log level | Always `Warning`, no distinction between a confirmed race and any other DB failure | Simple, one rule, easy to test; inspecting `SqlException` codes is extra complexity not needed to close the core gap | Plan |
| Log context | Domain ids only (room/day/time/student/teacher/ApplicationUser id) — no email/name | Enough to diagnose and correlate without logging PII | Plan |
| Test strategy | Integration test via a dedicated fault-injecting `ApplicationDbContext` + capturing `ILoggerProvider`, not a unit-test mock | Matches the existing `WebApplicationFactory`-based test style; avoids adding a mocking library; the existing app-level pre-check makes the catch block unreachable via a plain sequential request, so a real throw must be forced | Plan |

## Scope

**In scope:**
- `ILogger<T>` + a `LogWarning` call in each of the 3 catch blocks.
- A test-only fault-injecting `DbContext` subclass, a capturing logger
  provider, a dedicated `WebApplicationFactory` variant, and 3 integration
  tests proving the logging.

**Out of scope:**
- Checking `AdminTeachersController`'s compensating `_userManager.DeleteAsync(user)` result (audit finding #4).
- Logging when `TeacherController.Index` finds `teacher is null` (audit finding #5).
- Distinguishing a confirmed unique-index race from any other `DbUpdateException` cause.
- Any change to the user-facing message, redirect target, or response status.

## Architecture / Approach

Phase 1 is a pure production-code addition: inject `ILogger<T>`, log before
the existing fallback, no behavior change. Phase 2 proves it: since the
controllers already pre-check conflicts before saving, the catch block is
only reachable via a genuine DB failure — so the test harness adds a
test-only `ApplicationDbContext` subclass that can be armed to throw
`DbUpdateException` exactly once, wired through a brand-new
`WebApplicationFactory<Program>` variant kept separate from the existing
shared `MelodyWebApplicationFactory` (used by 14 other test files) so
nothing else is affected.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Log the caught exception | `ILogger<T>` + `LogWarning` in all 3 catch blocks, no behavior change | Low — purely additive |
| 2. Prove the logs actually appear | Fault-injecting DbContext + capturing logger provider + 3 integration tests | Medium — the "arm the throw" mechanism must not leak across tests/seeding; needs to be verified non-flaky |

**Prerequisites:** None beyond the existing repo/test setup.
**Estimated effort:** ~1 session, 2 phases (Phase 1 is small; Phase 2 is the bulk of the effort).

## Open Risks & Assumptions

- The exact mechanism to "arm" the fault-injecting context for one specific
  request (scoped service vs `AsyncLocal`) is left as an implementation
  detail to resolve during Phase 2 — whichever proves reliable when written
  and run.
- Assumes Application Insights/the platform log collector (if enabled in
  Azure) will actually surface these new `Warning`-level entries — that's
  outside this repo's control (see the observability audit's capture model).

## Success Criteria (Summary)

- All 3 catch blocks log a Warning with the exception and domain ids before
  falling back to the existing behavior.
- 3 new integration tests prove this, and the full existing test suite still
  passes unchanged.
