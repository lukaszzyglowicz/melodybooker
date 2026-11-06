# Log swallowed db exceptions Implementation Plan

## Overview

Three controllers (`AdminReservationsController`, `TeacherReservationsController`,
`AdminTeachersController`) each have a `catch (DbUpdateException)` block that
converts the exception into a generic user-facing error with no logging at
all. Any `DbUpdateException` — the expected double-booking unique-index race,
or an unrelated DB failure (deadlock, dropped connection) — looks identical
to an on-call responder: nothing. This plan adds `ILogger<T>` to the three
controllers and logs the caught exception (with non-PII domain context)
before the existing fallback behavior, and proves it with integration tests
that genuinely trigger a `DbUpdateException`.

## Current State Analysis

- `AdminReservationsController.cs:170`, `TeacherReservationsController.cs:134`,
  `AdminTeachersController.cs:90` — identical pattern: `catch (DbUpdateException)`
  with a comment explaining the expected race, `ModelState.AddModelError(...)`,
  and a re-render. No `ILogger` call in any of them.
- No controller in the codebase takes an `ILogger<T>` constructor dependency.
  The only precedent for calling `ILogger` is `Data/AdminUserSeeder.cs:21,50`
  (startup-only code), which already uses the correct pattern
  (`logger.LogWarning(...)` / `logger.LogError(...)`).
- All three controllers already have an app-level pre-check that prevents
  the catch block from firing on an ordinary sequential conflict:
  - `AdminReservationsController.Edit` queries `existingReservations` and
    checks `Reservation.Overlaps(...)` before calling `SaveChangesAsync`
    (`AdminReservationsController.cs`, before line ~160).
  - `TeacherReservationsController.Create` does the same.
  - This means the catch block is only reachable via a genuine concurrent
    race (two requests passing the pre-check at the same instant) or an
    unrelated DB failure — it is not hit by the existing
    `Edit_IntoConflictWithAnotherReservation_IsHardBlocked_NoChangePersisted`
    test (`tests/melody.Tests/AdminReservationOversightTests.cs:106`), which
    only proves the app-level pre-check.
- `ApplicationDbContext` (`src/melody/Data/ApplicationDbContext.cs`) has a
  public constructor and a non-sealed class; `DbContext.SaveChangesAsync` is
  `virtual`, so it can be subclassed and overridden for test fault injection.
- `tests/melody.Tests/MelodyWebApplicationFactory.cs` is the shared
  `WebApplicationFactory<Program>` used by 14 existing test files (SQLite
  in-memory DB, `TestAuthHandler` for role-based auth). It must not be
  modified in a way that changes behavior for those existing tests.

## Desired End State

Each of the three catch blocks logs a `Warning`-level entry containing the
original exception and non-PII domain ids identifying what was being saved,
before falling back to the existing generic user-facing message. The
existing generic message and redirect/re-render behavior are unchanged —
this plan only adds a diagnostic signal, it does not change any user-visible
behavior for either the race case or the success path.

Verified by: 3 new integration tests (one per controller) that force a real
`DbUpdateException` via a test-only fault-injecting `DbContext` and assert a
Warning-level log entry was captured with the expected exception and domain
ids.

### Key Discoveries:

- `AdminReservationsController.cs:170`, `TeacherReservationsController.cs:134`,
  `AdminTeachersController.cs:90` — the three swallow sites.
- `Data/AdminUserSeeder.cs:21,50` — the existing `ILogger` convention to
  follow (`logger.LogWarning(...)` / `logger.LogError(...)`, structured
  message templates with named placeholders).
- `Data/ApplicationDbContext.cs:8-14` — non-sealed, public constructor,
  subclassable for fault injection.
- `tests/melody.Tests/MelodyWebApplicationFactory.cs` — the pattern this
  plan's test harness mirrors (swap `ApplicationDbContext`'s registration
  via `services.RemoveAll<...>()` + `AddDbContext`), but as a *separate*,
  dedicated factory so the 14 existing test files stay untouched.

## What We're NOT Doing

- Not distinguishing a confirmed unique-index race from any other
  `DbUpdateException` cause (e.g. inspecting `SqlException.Number`/constraint
  name) — every occurrence logs at the same `Warning` level. (Decision:
  `log_level=warning_always`.)
- Not checking the `IdentityResult` returned by `AdminTeachersController`'s
  compensating `_userManager.DeleteAsync(user)` call (audit finding #4) —
  out of scope for this change. (Decision: `scope=strict`.)
- Not adding a log call to `TeacherController.Index`'s `teacher is null`
  branch (audit finding #5) — out of scope for this change. (Decision:
  `scope=strict`.)
- Not logging any PII (email, name) — only numeric/enum domain ids
  (room/day/time/student/teacher/reservation/ApplicationUser ids).
  (Decision: `log_context=domain_ids`.)
- Not adding a mocking library (Moq/NSubstitute) to the test project — the
  fault-injection harness uses a real `DbContext` subclass instead of a
  mock. (Decision: `testing=capturing_provider`, not `unit_mock`.)

## Implementation Approach

Phase 1 is a pure production-code change: inject `ILogger<T>` into each of
the three controllers and add one `LogWarning` call per catch block,
immediately above the existing `ModelState.AddModelError(...)` line. No
behavior change, no new dependencies.

Phase 2 proves Phase 1 actually works. Because the catch block is only
reachable via a genuine DB-level failure (not reachable by posting a plain
conflicting request, which the app-level pre-check intercepts first), the
test harness needs a way to force `SaveChangesAsync` to throw without
relying on real concurrency (which would be flaky against the shared,
single, always-open SQLite in-memory connection the existing test factory
uses). The harness adds a test-only `ApplicationDbContext` subclass that
throws `DbUpdateException` on demand, wired through a brand-new, separate
`WebApplicationFactory<Program>` subclass used only by these three new
tests — the existing `MelodyWebApplicationFactory` and its 14 consumer test
files are not touched.

## Critical Implementation Details

**Fault injection must not break startup seeding.** `RoleSeeder`,
`AdminUserSeeder`, and `RoomSeeder` all call `SaveChangesAsync` (directly or
via `UserManager`/`RoleManager`) during host startup, before any test gets a
chance to flip a "throw now" switch. The fault-injecting context must only
throw when explicitly armed by the test (e.g. a settable static/instance
flag the production `ApplicationDbContext` has no concept of), and must
reset itself after throwing once, so it never affects seeding or any later
request in the same test run.

## Phase 1: Log the caught exception

### Overview

Add `ILogger<T>` to the three controllers and log the caught
`DbUpdateException` (exception + non-PII domain ids) before the existing
fallback. No behavior change to the response, redirect, or user-facing
message.

### Changes Required:

#### 1. `AdminReservationsController`

**File**: `src/melody/Controllers/AdminReservationsController.cs`

**Intent**: Log the caught exception with the reservation/room/day/time
being saved, so a responder can tell a genuine race from an unrelated DB
failure, before falling back to the existing "already booked" message.

**Contract**: Add a `private readonly ILogger<AdminReservationsController> _logger;`
field, take `ILogger<AdminReservationsController> logger` as a constructor
parameter (alongside the existing `ApplicationDbContext context`), and in
the `Edit` POST action's `catch (DbUpdateException ex)` block (changing
`catch (DbUpdateException)` to bind the exception), call:

```csharp
_logger.LogWarning(ex,
    "Failed to save reservation {ReservationId} to Room {RoomId} on {DayOfWeek} at {StartTime}: possible double-booking race or another database failure.",
    reservation.Id, model.RoomId, model.DayOfWeek, startTime);
```

placed immediately before the existing `ModelState.AddModelError(...)` line.

#### 2. `TeacherReservationsController`

**File**: `src/melody/Controllers/TeacherReservationsController.cs`

**Intent**: Same as above, for a teacher creating a reservation.

**Contract**: Add `ILogger<TeacherReservationsController>` the same way. In
the `Create` POST action's catch block:

```csharp
_logger.LogWarning(ex,
    "Failed to save new reservation for Student {StudentId} in Room {RoomId} on {DayOfWeek} at {StartTime}: possible double-booking race or another database failure.",
    student.Id, model.RoomId, model.DayOfWeek, startTime);
```

#### 3. `AdminTeachersController`

**File**: `src/melody/Controllers/AdminTeachersController.cs`

**Intent**: Log the caught exception identifying the `ApplicationUser` whose
Teacher-profile save failed (triggering the existing compensating delete),
using the user id only — no email/name (no PII).

**Contract**: Add `ILogger<AdminTeachersController>` the same way. In the
`Create` POST action's catch block, before the existing compensating
`await _userManager.DeleteAsync(user)` call:

```csharp
_logger.LogWarning(ex,
    "Failed to save Teacher profile for ApplicationUser {ApplicationUserId}; rolling back the created account.",
    user.Id);
```

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds.
- All existing tests still pass: `dotnet test melodybooker.sln --filter FullyQualifiedName~AdminReservationOversightTests|FullyQualifiedName~TeacherBooksRoomTests|FullyQualifiedName~AdminRosterAuthorizationTests|FullyQualifiedName~AdminRosterDeletionTests` (existing "already booked" / roster behavior unchanged).

#### Manual Verification:

- Read the diff for each of the 3 files: confirm the user-facing message,
  redirect target, and compensating-delete call are byte-for-byte unchanged
  except for the added logging line.

---

## Phase 2: Prove the logs actually appear

### Overview

Add a test-only fault-injecting `ApplicationDbContext` subclass, a capturing
`ILoggerProvider`, and a dedicated `WebApplicationFactory<Program>` variant,
then write one integration test per controller that forces a real
`DbUpdateException` and asserts the expected Warning-level log entry was
captured.

### Changes Required:

#### 1. Capturing logger provider

**File**: `tests/melody.Tests/CapturingLoggerProvider.cs` (new)

**Intent**: A minimal `ILoggerProvider`/`ILogger` pair that records every log
call (category, level, formatted message, exception) into a thread-safe list
the test can assert against, instead of writing anywhere real.

**Contract**: Public type `CapturingLoggerProvider : ILoggerProvider`
exposing a `IReadOnlyList<CapturedLogEntry> Entries` (or equivalent) where
`CapturedLogEntry` has `LogLevel Level`, `string Category`, `string Message`,
`Exception? Exception`. Registered via `builder.ConfigureLogging(logging =>
logging.AddProvider(provider))` in the Phase 2 factory below.

#### 2. Fault-injecting DbContext

**File**: `tests/melody.Tests/ThrowingApplicationDbContext.cs` (new)

**Intent**: A `DbContext` subclass that throws a `DbUpdateException` from
`SaveChangesAsync` exactly once, only when armed by the test, so seeding and
unrelated requests behave normally.

**Contract**: `public class ThrowingApplicationDbContext : ApplicationDbContext`
with a constructor matching the base class
(`DbContextOptions<ApplicationDbContext> options`), and an instance-level
settable flag (e.g. `public bool ThrowOnNextSave { get; set; }`) resolved
per-request via DI (scoped), not a static — this avoids cross-test/parallel-run
interference since xUnit may run other test classes concurrently. Overrides
`SaveChangesAsync(CancellationToken)`:

```csharp
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    if (ThrowOnNextSave)
    {
        ThrowOnNextSave = false;
        throw new DbUpdateException(
            "Simulated failure for test",
            new InvalidOperationException("Simulated inner DB failure"));
    }

    return base.SaveChangesAsync(cancellationToken);
}
```

A scoped flag means the test must arm it through the same DI scope the
request will use — expose a way to reach the armed instance (e.g. a scoped
marker service the test resolves from `_factory.Services.CreateScope()` and
sets, resolved by the same scope `ThrowingApplicationDbContext` reads from)
or, simpler: key the "armed" state off the HTTP request itself (e.g. arm by
setting a static `AsyncLocal<bool>` immediately before issuing the one
`PostAsync` call in each test, since `WebApplicationFactory`'s in-process
`TestServer` dispatches requests through the same async flow without a real
network hop, preserving `ExecutionContext`). Pick whichever mechanism proves
reliable when the test is written and run — this is an implementation detail
for Phase 2, not a product decision.

#### 3. Fault-injecting factory

**File**: `tests/melody.Tests/FaultInjectingWebApplicationFactory.cs` (new)

**Intent**: A separate `WebApplicationFactory<Program>` that mirrors
`MelodyWebApplicationFactory`'s SQLite-in-memory + `TestAuthHandler` setup,
but additionally swaps `ApplicationDbContext`'s registration for
`ThrowingApplicationDbContext`, and registers `CapturingLoggerProvider`. Kept
entirely separate from `MelodyWebApplicationFactory` so none of its 14
existing consumer test files are affected.

**Contract**: `public class FaultInjectingWebApplicationFactory : WebApplicationFactory<Program>`
exposing `CapturingLoggerProvider CapturedLogs { get; }` and whatever
arming mechanism Phase 2's item 2 settled on, plus a `CreateClientAs(string
role)` helper mirroring `MelodyWebApplicationFactory`'s for consistency.

#### 4. Integration tests

**File**: `tests/melody.Tests/SwallowedExceptionLoggingTests.cs` (new)

**Intent**: One test per controller: seed the minimum valid data, arm the
fault injector, post the request that would normally succeed, and assert
(a) the response still shows the existing generic fallback behavior
unchanged, and (b) `CapturedLogs.Entries` contains exactly one `Warning`
entry whose category matches the controller and whose exception is the
simulated one.

**Contract**: Three `[Fact]` tests:
- `Edit_WhenSaveFails_LogsWarningWithReservationContext` (AdminReservationsController)
- `Create_WhenSaveFails_LogsWarningWithReservationContext` (TeacherReservationsController)
- `Create_WhenSaveFails_LogsWarningWithApplicationUserId` (AdminTeachersController)

Each asserts the captured entry's `Level == LogLevel.Warning`, `Exception`
is not null, and the formatted message contains the expected domain id(s)
(e.g. the seeded room id, student id, or `ApplicationUser.Id`).

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds.
- New tests pass: `dotnet test melodybooker.sln --filter FullyQualifiedName~SwallowedExceptionLoggingTests`.
- Full suite still green: `dotnet test melodybooker.sln`.

#### Manual Verification:

- Run the new tests twice in a row (and alongside the full suite) to
  confirm no flakiness from the arming mechanism chosen in Phase 2, item 2.

---

## Testing Strategy

### Unit Tests:

- None needed beyond the integration tests below — the change is entirely
  within request-handling controller actions, which this codebase already
  tests at the integration level via `WebApplicationFactory`.

### Integration Tests:

- 3 new tests (Phase 2) proving each catch block now logs the exception
  with the right domain context when a real `DbUpdateException` occurs.
- Existing tests covering the user-facing "already booked" message and
  teacher-account-creation rollback must still pass unchanged (Phase 1
  success criteria).

### Manual Testing Steps:

1. Read the 3 diffs in Phase 1 to confirm no behavior change beyond the
   added log line.
2. Run the new Phase 2 tests twice to rule out flakiness in the fault
   injection/arming mechanism.

## References

- Observability audit: `context/audits/observability/2026-11-06_booking-roster-auth.md`
  (findings #1, #2, #3 — this plan closes root cause 1 for those three).
- `src/melody/Data/AdminUserSeeder.cs:21,50` — existing `ILogger` convention.
- `tests/melody.Tests/MelodyWebApplicationFactory.cs` — pattern this plan's
  Phase 2 factory mirrors without modifying.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Log the caught exception

#### Automated

- [x] 1.1 `dotnet build melodybooker.sln` succeeds
- [x] 1.2 Existing tests still pass (AdminReservationOversightTests, TeacherBooksRoomTests, AdminRosterAuthorizationTests, AdminRosterDeletionTests)

#### Manual

- [x] 1.3 Diff review: user-facing message/redirect/compensating-delete unchanged except for added logging

### Phase 2: Prove the logs actually appear

#### Automated

- [ ] 2.1 `dotnet build melodybooker.sln` succeeds
- [ ] 2.2 New tests pass (SwallowedExceptionLoggingTests)
- [ ] 2.3 Full suite still green (`dotnet test melodybooker.sln`)

#### Manual

- [ ] 2.4 New tests run twice (plus alongside full suite) with no flakiness
