# Reservation Form & Duration Validation Hardening — Plan Brief

> Full plan: `context/changes/testing-reservation-form-duration-validation/plan.md`
> Research: `context/changes/testing-reservation-form-duration-validation/research.md`

## What & Why

Rollout Phase 3 of the project's test plan closes two risks: lesson-duration derivation
(FR-009) mishandling the class 4/5 boundary, and reservation-form validation silently
accepting an invalid student class. Research found the "silent default" half of Risk #5 is
already disproven by existing validation, but the class 1-8 range — especially the 4/5
boundary — has zero test coverage, and Risk #4 (instrument-mismatch) is already well covered
elsewhere.

## Starting Point

`Student.LessonDuration` (`src/melody/Models/Domain/Student.cs:27`) is a one-line computed
property with a single boundary check; the only existing test of its output uses class 6
(`TeacherBooksRoomTests.cs:36`). `StudentFormViewModel.Class` carries `[Range(1,8)]`
(`StudentFormViewModel.cs:24`), enforced by `AdminStudentsController`'s `ModelState.IsValid`
gate — confirmed by an empirical probe to reject invalid values, but with no test proving it.

## Desired End State

Two new pure-unit test files exist (`StudentLessonDurationTests.cs`,
`StudentFormValidationTests.cs`) proving every class 1-8 maps to the correct duration and that
an invalid `Class` value fails DataAnnotations validation — closing both gaps at the cheapest
possible test layer, with the full suite still green.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Scope | Risks #4/#5 only, not the broader AdminStudents validation gap | Keeps the phase narrow and matches the test-plan's assigned risks | Plan |
| Class-validation test layer | Pure unit via `Validator.TryValidateObject`, not integration/HTTP | Cheapest layer that still proves the DataAnnotations contract (cost × signal) | Plan |
| Class range breadth | Full Theory over all 8 classes | Pins the exact 4/5 boundary instead of a representative subset | Plan |
| Combined duration+mismatch scenario | Skipped | Research found the two rules are independent/adjacent, and mismatch already has strong coverage | Research / Plan |

## Scope

**In scope:**
- `Student.LessonDuration` Theory covering classes 1-8 (20 vs 40 min, boundary at 4/5).
- `StudentFormViewModel.Class` validation facts for 0, 9, -1 (invalid) and 1-8 (valid).

**Out of scope:**
- Integration tests of any kind (WebApplicationFactory/HTTP).
- A combined duration + instrument-mismatch scenario.
- Broader `AdminStudentsController` validation coverage beyond `Class`.
- Any production code change.

## Architecture / Approach

Two standalone pure-unit xUnit test files, following the existing `InstrumentMismatchTests.cs`
pattern exactly (no DB, no host, no shared fixture needed).

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Lesson-duration boundary and Class-validation unit tests | Two new test files closing Risk #4/#5's confirmed gaps | Low — pure unit, no production code touched |

**Prerequisites:** None — additive test-only change.
**Estimated effort:** Single short session.

## Open Risks & Assumptions

- Assumes `Validator.TryValidateObject` against a ViewModel instance faithfully represents what
  ASP.NET Core's model binder/validation pipeline does for `[Range]` on a non-nullable `int` —
  this was the deciding factor for choosing the unit layer over integration, and was confirmed
  consistent with the research probe's empirical HTTP-level result (both approaches rejected
  `Class=0`).

## Success Criteria (Summary)

- `dotnet test` passes, including the two new files, with no regressions in the existing suite.
- Every class 1-8 has an explicit, named assertion of its derived `LessonDuration`.
- Missing/zero and out-of-range `Class` values are proven to fail validation at the unit layer.
