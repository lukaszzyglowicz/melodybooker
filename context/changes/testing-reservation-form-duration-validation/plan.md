# Reservation Form & Duration Validation Hardening Implementation Plan

## Overview

Close the two test-plan risks assigned to rollout Phase 3 (`context/foundation/test-plan.md`
§3 Phase 3) with targeted pure-unit tests: Risk #5 (lesson-duration derivation at the class
4/5 boundary, and an invalid/missing class) and the narrow residual slice of Risk #4
(reservation-form validation) that isn't already covered by existing instrument-mismatch
tests. No production code changes — this is test-only hardening.

## Current State Analysis

- `Student.LessonDuration` (`src/melody/Models/Domain/Student.cs:27`) derives lesson duration
  from `Class`: `Class <= 4 ? 20min : 40min` (FR-009). It has no dedicated unit test; the only
  existing assertion of its output is `TeacherBooksRoomTests.cs:36`
  (`Create_SuccessfulBooking_CreatesReservationWithStudentsLessonDuration`), which uses a single
  class value (6) in the 40-minute bucket only.
- `StudentFormViewModel.Class` (`src/melody/Models/ViewModels/StudentFormViewModel.cs:24`)
  carries `[Range(1, 8)]` but no `[Required]`. `AdminStudentsController.Create`/`Edit`
  (`src/melody/Controllers/AdminStudentsController.cs:48-65`, `:89-115`) both gate persistence
  on `ModelState.IsValid`. An empirical probe (run and discarded during research) confirmed a
  missing or `Class=0` POST is rejected (HTTP 200 re-render, no row saved) — the test-plan's
  "silent default" framing for Risk #5 does not hold for this inspected path, but the behavior
  itself has zero existing test coverage.
- Risk #4 (instrument-mismatch, FR-011) already has strong coverage:
  `InstrumentMismatchTests.cs` (5 pure-unit facts) and `SpecialistInstrumentWarningTests.cs`
  (6 integration facts). Research also confirmed duration-derivation and instrument-mismatch
  are independent, adjacent reads in `TeacherReservationsController.Create`, not shared logic —
  per the research decision, no combined test is needed to prove they don't interfere.
- No test anywhere in `tests/melody.Tests/` uses class 1, 2, 4, 5, or 8 (confirmed via
  repo-wide grep) — every seeded/posted student uses class 3, 6, or 7.
- Full findings: `context/changes/testing-reservation-form-duration-validation/research.md`.

## Desired End State

Two new pure-unit test files exist, following the established `InstrumentMismatchTests.cs`
pattern (plain `xUnit` facts/theories, no `WebApplicationFactory`, no database):

1. A full Theory over `Student.LessonDuration` for every class 1-8, pinning the exact 20/40-minute
   split and specifically asserting the class 4 → class 5 boundary.
2. A set of facts proving `StudentFormViewModel.Class` validation (via
   `System.ComponentModel.DataAnnotations.Validator.TryValidateObject`) rejects a missing/default
   (0) value and out-of-range values (9, -1), and accepts the full valid 1-8 range.

Verification: `dotnet test` passes with both new files included, and the full existing suite
(53+ facts before this change) remains green.

### Key Discoveries:

- `src/melody/Models/Domain/Student.cs:27` — the sole FR-009 derivation point, a `[NotMapped]`
  computed property with no branch beyond the single `<= 4` check.
- `src/melody/Models/ViewModels/StudentFormViewModel.cs:24` — `Class` has `[Range(1, 8)]`,
  non-nullable `int`, no `[Required]` (redundant on a non-nullable value type).
- `tests/melody.Tests/InstrumentMismatchTests.cs` — the established pure-unit pattern this plan
  follows exactly (plain `public class ...Tests`, no `IClassFixture`, no async lifetime).
- No existing test in this codebase uses `Validator.TryValidateObject` — this plan introduces
  the pattern for the first time to validate a ViewModel's DataAnnotations without an HTTP
  round-trip, which is the cheapest layer for this specific risk (cost × signal principle,
  §1 of `test-plan.md`).

## What We're NOT Doing

- No new integration tests (confirmed decision: the Class-validation gap is covered at the
  unit layer via `Validator.TryValidateObject`, not via `WebApplicationFactory`/HTTP).
- No combined duration + instrument-mismatch integration scenario (confirmed decision:
  research found these rules are independent/adjacent, and instrument-mismatch already has
  strong, separate coverage).
- No broader `AdminStudentsController.Create`/`Edit` validation test suite beyond the `Class`
  field (confirmed decision: out of scope for this phase; `FullName`/`Instrument`/`TeacherId`
  validation is not part of Risk #4/#5).
- No production code changes — `Student.cs`, `StudentFormViewModel.cs`, and
  `AdminStudentsController.cs` are not modified.

## Implementation Approach

Single phase, two new pure-unit test files, mirroring the existing `InstrumentMismatchTests.cs`
style. No shared fixture is needed since neither file touches the database or HTTP pipeline.

## Phase 1: Lesson-duration boundary and Class-validation unit tests

### Overview

Add the two new pure-unit test files closing the confirmed Risk #4/#5 gaps.

### Changes Required:

#### 1. Lesson-duration boundary coverage

**File**: `tests/melody.Tests/StudentLessonDurationTests.cs`

**Intent**: Prove `Student.LessonDuration` returns the correct duration for every class 1-8,
with an explicit assertion at the exact 4→5 boundary where the derivation switches buckets —
closing the gap where only class 6 was ever exercised.

**Contract**: A `[Theory]` with `[InlineData]` rows for classes 1-8, each asserting
`new Student { Class = n }.LessonDuration` equals `TimeSpan.FromMinutes(20)` for classes 1-4 and
`TimeSpan.FromMinutes(40)` for classes 5-8. Follow `InstrumentMismatchTests.cs`'s file-level
summary-comment convention (cite `Student.LessonDuration` and FR-009).

#### 2. Class-range validation coverage

**File**: `tests/melody.Tests/StudentFormValidationTests.cs`

**Intent**: Prove `StudentFormViewModel`'s `[Range(1, 8)]` on `Class` rejects a missing/default
(0) value and out-of-range values, and accepts the full valid range — at the cheapest layer
(DataAnnotations validation directly), without needing a full HTTP round-trip through
`AdminStudentsController`.

**Contract**: Facts/Theories constructing a `StudentFormViewModel` with required fields
(`FullName`, `Instrument`, `TeacherId`) populated validly and `Class` set to the value under
test, then calling `Validator.TryValidateObject(model, new ValidationContext(model), results,
validateAllProperties: true)`. Assert `TryValidateObject` returns `false` and `results` contains
a `ValidationResult` referencing `Class` for `Class = 0` (the model-binder default when the
field is missing/empty) and for out-of-range values `9` and `-1`. Assert it returns `true` for
each of classes 1-8.

### Success Criteria:

#### Automated Verification:

- New tests pass: `dotnet test --filter "FullyQualifiedName~StudentLessonDurationTests"`
- New tests pass: `dotnet test --filter "FullyQualifiedName~StudentFormValidationTests"`
- Full existing suite remains green: `dotnet test`

#### Manual Verification:

- Review both new test files follow the `InstrumentMismatchTests.cs` pure-unit style (no
  `WebApplicationFactory`, no database) and that test names clearly identify the scenario under
  test.

**Implementation Note**: After completing this phase and all automated verification passes,
pause here for manual confirmation from the human that the manual review was successful.

---

## Testing Strategy

### Unit Tests:

- `Student.LessonDuration` for every class 1-8 (the 4/5 boundary is the specific risk).
- `StudentFormViewModel.Class` validation for 0 (missing/default), 9 and -1 (out-of-range), and
  the full 1-8 valid range.

### Integration Tests:

- None added in this phase (confirmed decision — see "What We're NOT Doing").

### Manual Testing Steps:

1. Confirm both new test files compile and run without a database/HTTP host.
2. Confirm `dotnet test` reports the same existing-suite pass count plus the new facts, with no
   regressions.

## Performance Considerations

None — pure unit tests with no I/O.

## Migration Notes

Not applicable — no schema or data changes.

## References

- Related research: `context/changes/testing-reservation-form-duration-validation/research.md`
- Existing pure-unit pattern to follow: `tests/melody.Tests/InstrumentMismatchTests.cs`
- Existing (narrow) duration assertion: `tests/melody.Tests/TeacherBooksRoomTests.cs:36`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Lesson-duration boundary and Class-validation unit tests

#### Automated

- [ ] 1.1 New tests pass: `dotnet test --filter "FullyQualifiedName~StudentLessonDurationTests"`
- [ ] 1.2 New tests pass: `dotnet test --filter "FullyQualifiedName~StudentFormValidationTests"`
- [ ] 1.3 Full existing suite remains green: `dotnet test`

#### Manual

- [ ] 1.4 Review both new test files follow the `InstrumentMismatchTests.cs` pure-unit style and have clear test names
