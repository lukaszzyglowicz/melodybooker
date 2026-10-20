# Specialist-Instrument Mismatch Warning — Implementation Plan

## Overview

When a Teacher attempts to book a specialist room (piano/xylophone/drums) for a student who doesn't play that instrument, show a soft warning that doesn't block the booking (FR-011/US-02). This is S-04 on the roadmap, built directly on top of S-03's booking flow.

## Current State Analysis

- `Reservation.HadInstrumentMismatchWarning` (bool) already exists in the domain model and DB schema (`src/melody/Models/Domain/Reservation.cs:36`, migration `20261005143616_InitialCreate.cs:228`) but is never set — every reservation created by S-03 persists it as `false` regardless of actual mismatch.
- `Room.SpecialistInstrument` (nullable `Instrument`) and `Room.IsSpecialistRoom` (`src/melody/Models/Domain/Room.cs:16,18`) already model which rooms are specialist rooms.
- `InstrumentExtensions.IsSpecialist()` (`src/melody/Models/Domain/Instrument.cs:32`) flags Piano/Xylophone/Drums as specialist instruments, but mismatch detection itself (room's specialist instrument vs. student's instrument) has no existing rule anywhere in the codebase.
- `TeacherReservationsController.Create` (POST) (`src/melody/Controllers/TeacherReservationsController.cs:57-106`) currently does exactly one pass: validate → room-exists check → hard-block conflict check → save. There is no mechanism for a non-blocking warning that requires explicit confirmation to proceed.
- `ReservationFormViewModel` (`src/melody/Models/ViewModels/ReservationFormViewModel.cs`) has no fields for a pending-confirmation state.
- `Create.cshtml` (`src/melody/Views/TeacherReservations/Create.cshtml`) renders a single "Book room" submit button and a secondary "Check availability" GET button; no warning-banner pattern exists yet in this view.
- Per S-03's plan (now archived at `context/archive/2026-10-19-teacher-books-room/plan.md`), the Room dropdown deliberately shows room names only, no specialist indicator (verified manually as item 2.7) — this plan does not change that.
- `tests/melody.Tests/TeacherBooksRoomTests.cs` establishes the integration-test pattern this plan's Phase 3 follows: xUnit + `MelodyWebApplicationFactory` (SQLite in-memory), `CreateClientAs("Teacher", ...)`, DB-row-count assertions, a linear (no-branching) seeding helper.
- `tests/melody.Tests/ReservationOverlapTests.cs` establishes the domain-rule unit-test pattern this plan's Phase 1 follows: a standalone static method on `Reservation`, tested in isolation with no DB/HTTP involved.

### Key Discoveries:

- The DB column and domain property for this exact feature (`HadInstrumentMismatchWarning`) were already scaffolded in the very first migration, well before S-03 was implemented — this plan is "finishing" a field that was anticipated from day one, not introducing new schema.
- The hard-block conflict check (FR-010) and the soft mismatch warning (FR-011) must run in that priority order: a double-booking conflict always wins and is reported first, regardless of any instrument mismatch, since FR-010 has no override under any circumstance while FR-011 explicitly allows the teacher to proceed.
- `TeacherReservationsController.Create` currently validates `RoomId` existence with `AnyAsync` (boolean-only); this plan needs the actual `Room` entity (to read `SpecialistInstrument`), so that check becomes a `FirstOrDefaultAsync` fetch instead — a direct, minor adaptation of existing code, not a new pattern.

## Desired End State

A Teacher who submits a booking for a specialist room with a mismatched student sees a yellow warning banner naming the room's instrument and the student's actual instrument, with a "Book anyway" action that re-submits the exact same Room/Day/Time; completing that second submission creates the reservation with `HadInstrumentMismatchWarning = true`. Changing any of Room/Day/Time after seeing the warning (without yet re-confirming) re-evaluates the check from scratch — a stale confirmation never silently carries over to a different room/time. A matching instrument, or a non-specialist room, bookings proceed exactly as in S-03 with `HadInstrumentMismatchWarning = false` and no banner shown. The existing hard double-booking block (FR-010) continues to take priority and is unaffected by this change.

### Automated verification covers: `dotnet build melodybooker.sln` succeeds; `dotnet test` passes, including new unit tests for the mismatch rule and new integration tests for the full warn → confirm → save flow (mismatch blocked pending confirmation, mismatch saved after confirmation with the flag set, matching instrument saved with the flag unset, non-specialist room unaffected, confirmation invalidated by a field change).
### Manual verification covers: an end-to-end walkthrough as a Teacher — booking a mismatched specialist room, seeing the warning, confirming, and seeing the reservation land with no further friction; and confirming a matching-instrument booking shows no warning at all.

## What We're NOT Doing

- Not implementing FR-012 (hard-blocking specialist-room mismatches) — that's explicit v2/nice-to-have scope, parked in the roadmap.
- Not touching the Room dropdown to add a specialist-instrument indicator — S-03's existing name-only behavior stays as-is (explicit user decision this session).
- Not surfacing the mismatch warning on the "Check availability" GET round-trip — it only appears on the actual "Book room" POST attempt (explicit user decision).
- Not touching any Admin-side reservation flow — S-05 (Admin oversight) is a separate, not-yet-planned slice; this change is scoped to `TeacherReservationsController` only (explicit user decision).
- Not adding any JavaScript/client-side confirmation dialog — the warn/confirm flow is a plain server-side round-trip, consistent with the rest of the app having no client-side framework (explicit user decision).
- Not treating `Instrument.Other` as a special case — it is compared like any other instrument value; a specialist room always mismatches a student whose instrument isn't that exact specialist instrument, "Other" included (explicit user decision).

## Implementation Approach

Build bottom-up, mirroring S-03's own structure: first make the mismatch rule an independently correct, unit-testable domain fact (Phase 1), then wire the warn → confirm → save flow into the controller/viewmodel/view on top of it (Phase 2), then close the loop with integration tests covering the full flow end-to-end, including the confirmation-invalidation edge case (Phase 3).

## Phase 1: Instrument-mismatch domain rule

### Overview

Add a single, standalone, unit-testable rule for "does this room mismatch this student's instrument", following the exact pattern `Reservation.Overlaps` established in S-03.

### Changes Required:

#### 1. Mismatch rule

**File**: `src/melody/Models/Domain/Reservation.cs`

**Intent**: Give Phase 2's controller a single, testable source of truth for whether a room/student pairing triggers the FR-011 warning, instead of inlining the comparison in the controller.

**Contract**: `public static bool IsInstrumentMismatch(Room room, Student student) => room.IsSpecialistRoom && room.SpecialistInstrument != student.Instrument;` — a non-specialist room (`SpecialistInstrument == null`) never mismatches; a specialist room mismatches any student whose `Instrument` isn't exactly that specialist instrument (including `Instrument.Other`).

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds
- `dotnet test --filter FullyQualifiedName~InstrumentMismatchTests` passes, covering: specialist room + matching instrument → false; specialist room + different specialist instrument → true; specialist room + non-specialist instrument (e.g. Violin) → true; specialist room + `Instrument.Other` → true; non-specialist room + any instrument → false

#### Manual Verification:

- None — this phase is pure logic with full automated coverage

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Warn-then-confirm controller/view flow

### Overview

Extend the booking form and controller so a detected mismatch re-renders the form with a warning banner and a pinned confirmation instead of saving, and a matching re-submission (same Room/Day/Time, explicitly confirmed) proceeds to save with the flag set.

### Changes Required:

#### 1. View model: pending-confirmation state

**File**: `src/melody/Models/ViewModels/ReservationFormViewModel.cs`

**Intent**: Carry the "this exact Room/Day/Time combination was already warned-and-confirmed" state across the POST round-trip, plus the controller-rendered warning content for the view to display.

**Contract**: Add `[ValidateNever] public bool Confirmed { get; set; }` and `[ValidateNever] public int? ConfirmedRoomId { get; set; }`, `[ValidateNever] public DayOfWeek? ConfirmedDayOfWeek { get; set; }`, `[ValidateNever] public int? ConfirmedStartMinutes { get; set; }` (posted back from the hidden fields the view renders only when a warning was shown), and `[ValidateNever] public bool ShowMismatchWarning { get; set; }` + `[ValidateNever] public string? MismatchWarningMessage { get; set; }` (controller-set only, never bound from the form).

#### 2. Controller: priority-ordered checks + pinned confirmation

**File**: `src/melody/Controllers/TeacherReservationsController.cs`

**Intent**: After the existing hard double-booking block (unchanged, still takes priority), check `Reservation.IsInstrumentMismatch` against the fetched `Room` entity and the already-loaded `Student`. If mismatched and not confirmed for this *exact* Room/Day/Time combination, re-render with the warning instead of saving. If mismatched and confirmed for this exact combination, save with `HadInstrumentMismatchWarning = true`. If not mismatched, save with the flag `false` as before.

**Contract**: Change the existing `AnyAsync` room-existence check to `FirstOrDefaultAsync` so the `Room` entity (with `SpecialistInstrument`) is available for the mismatch check. "Confirmed for this exact combination" means `model.Confirmed && model.ConfirmedRoomId == model.RoomId && model.ConfirmedDayOfWeek == model.DayOfWeek && model.ConfirmedStartMinutes == model.StartMinutes` — any field mismatch between the pinned and current values is treated as not-yet-confirmed, re-triggering the warning. When re-rendering for an unconfirmed mismatch, set `ShowMismatchWarning = true` and `MismatchWarningMessage` naming the room's specialist instrument and the student's actual instrument.

#### 3. View: warning banner + confirmation hidden fields

**File**: `src/melody/Views/TeacherReservations/Create.cshtml`

**Intent**: Render a yellow (`alert-warning`, not `text-danger`) banner with the controller's message when `Model.ShowMismatchWarning` is true, and include hidden fields pinning the current Room/Day/Time plus `Confirmed=true`, so the next submission from this exact form state carries its own confirmation. The submit button's label reflects the pending state ("Book anyway" vs. "Book room").

**Contract**: `<div class="alert alert-warning">@Model.MismatchWarningMessage</div>` plus hidden inputs for `ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes`/`Confirmed`, rendered only inside the `@if (Model.ShowMismatchWarning)` block — never present when no warning is pending, so a plain first submission never looks "pre-confirmed".

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds

#### Manual Verification:

- Booking a specialist room for a mismatched student shows the warning banner and does not create a reservation
- Clicking "Book anyway" on the warned form creates the reservation
- Booking a specialist room for a matching-instrument student creates the reservation immediately, no warning shown
- Booking a non-specialist room never shows the warning, regardless of student instrument

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Integration tests

### Overview

Prove the full warn → confirm → save flow end-to-end against the real controller/view stack (SQLite-in-memory), covering every scenario manually verified in Phase 2 plus the confirmation-invalidation edge case.

### Changes Required:

#### 1. Integration test suite

**File**: `tests/melody.Tests/SpecialistInstrumentWarningTests.cs` (new)

**Intent**: Convert Phase 2's manual checks into permanent automated coverage, following `TeacherBooksRoomTests.cs`'s established pattern (xUnit, `MelodyWebApplicationFactory`, `CreateClientAs("Teacher", ...)`, DB-row-count + flag-value assertions, a linear seeding helper with no untested branches).

**Contract**: At minimum five test cases: (1) mismatched specialist room, no confirmation → 0 reservations saved, response shows the warning; (2) same mismatch, posted with the matching pinned `Confirmed`/`ConfirmedRoomId`/`ConfirmedDayOfWeek`/`ConfirmedStartMinutes` fields → 1 reservation saved with `HadInstrumentMismatchWarning == true`; (3) specialist room with a matching-instrument student → 1 reservation saved with `HadInstrumentMismatchWarning == false`, no warning; (4) non-specialist room, any student instrument → 1 reservation saved with the flag `false`; (5) a confirmed mismatch posted with a *different* `RoomId` (or `DayOfWeek`/`StartMinutes`) than the pinned values → 0 reservations saved, warning shown again (proves stale confirmations don't carry over).

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds (final build after all phases)
- `dotnet test` passes in full (all new + existing tests)

#### Manual Verification:

- None — this phase's purpose is converting the Phase 2 manual checks into permanent automated coverage

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `Reservation.IsInstrumentMismatch` against every combination of specialist/non-specialist room × matching/mismatched/Other student instrument

### Integration Tests:

- Full POST round-trip through `TeacherReservationsController.Create`, covering warn-without-confirm, confirm-and-save, matching-instrument save, non-specialist-room save, and confirmation invalidated by a changed field

### Manual Testing Steps:

1. As a Teacher, book a specialist room (e.g. Piano Room) for a student who plays a different instrument — confirm the warning banner appears and nothing is saved yet
2. Click "Book anyway" — confirm the reservation now appears
3. Book a specialist room for a student who plays the matching instrument — confirm no warning appears and the reservation saves immediately
4. Book a non-specialist room for any student — confirm no warning ever appears

## Migration Notes

None — `HadInstrumentMismatchWarning` and `Room.SpecialistInstrument` already exist in the current schema; no new migration is needed.

## References

- Prior slice (direct precedent for pattern and file layout): `context/archive/2026-10-19-teacher-books-room/plan.md`
- PRD: `context/foundation/prd.md` (FR-011, FR-012, US-02)
- Roadmap: `context/foundation/roadmap.md` (S-04)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Instrument-mismatch domain rule

#### Automated

- [x] 1.1 `dotnet build melodybooker.sln` succeeds — dd6fc16
- [x] 1.2 `dotnet test --filter FullyQualifiedName~InstrumentMismatchTests` passes (matching, mismatched-specialist, non-specialist-instrument, Other, non-specialist-room cases) — dd6fc16

### Phase 2: Warn-then-confirm controller/view flow

#### Automated

- [ ] 2.1 `dotnet build melodybooker.sln` succeeds

#### Manual

- [ ] 2.2 Booking a specialist room for a mismatched student shows the warning banner and does not create a reservation
- [ ] 2.3 Clicking "Book anyway" on the warned form creates the reservation
- [ ] 2.4 Booking a specialist room for a matching-instrument student creates the reservation immediately, no warning shown
- [ ] 2.5 Booking a non-specialist room never shows the warning, regardless of student instrument

### Phase 3: Integration tests

#### Automated

- [ ] 3.1 Final `dotnet build melodybooker.sln` succeeds
- [ ] 3.2 `dotnet test` passes in full (all new + existing tests)
