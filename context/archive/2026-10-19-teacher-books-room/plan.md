# Teacher Books a Room for a Weekly Lesson — Implementation Plan

## Overview

Give a Teacher a way to reserve a rehearsal room for one of their own assigned students' weekly lessons. Lesson duration is auto-derived from the student's class (already modeled), the booking repeats weekly for the whole school year, and double-booking a room (exact match or any overlapping time range at a different start time) is always hard-blocked with no override. This is S-03 on the roadmap — the "north star" slice proving the core product idea.

## Current State Analysis

- The domain model for this slice already exists and matches the PRD exactly: `Reservation` (RoomId/StudentId/TeacherId/DayOfWeek/StartTime/Duration/computed EndTime), `Room` (Name/SpecialistInstrument/IsSpecialistRoom), `Student.LessonDuration` (`src/melody/Models/Domain/Student.cs:27` — 20 min for classes 1-4, 40 min for 5-8, FR-009). No domain model changes are needed.
- `ApplicationDbContext` (`src/melody/Data/ApplicationDbContext.cs`) already has a unique index on `(RoomId, DayOfWeek, StartTime)`, enforcing exact-match double-booking at the DB layer (partial FR-010). It does **not** catch two reservations in the same room/day whose time ranges overlap at different start times (e.g. 10:00-10:40 vs 10:20-11:00) — the application layer must add that check.
- **Rooms table is currently empty.** No Room CRUD and no Room seeding exist anywhere, and the PRD has no FR covering Room provisioning. Per user decision, this plan adds a fixed-room startup seeder (`src/melody/Data/RoomSeeder.cs`), following the exact pattern already established by `RoleSeeder.cs` and `AdminUserSeeder.cs` (static class, `SeedAsync(IServiceProvider services)`, invoked once from `Program.cs`'s startup scope) — no new admin UI, no new migration (the `Rooms` table schema already exists).
- `TeacherController.Index` (`src/melody/Controllers/TeacherController.cs`) establishes the "scope to the logged-in teacher's own data" pattern this plan reuses: resolve `Teacher` via `UserManager.GetUserId(User)` → `Teacher.ApplicationUserId`, then query only rows where the owning `TeacherId` matches.
- `AdminStudentsController`/`AdminStudents/Create.cshtml`/`_StudentFormFields.cshtml` establish the form conventions this plan follows: `asp-validation-summary="ModelOnly"`, `Html.GetEnumSelectList<T>()` for enum dropdowns, `[ValidateNever]` on controller-populated `SelectListItem` collections, `_ValidationScriptsPartial` in a `Scripts` section.
- `tests/melody.Tests` already exists (xUnit, `Microsoft.AspNetCore.Mvc.Testing`, EF Core SQLite-in-memory via `MelodyWebApplicationFactory`, `TestAuthHandler` for role-based auth headers, `CreateClientAs(role, userId)`). `TeacherStudentListTests.cs` is the closest precedent for this plan's integration tests (teacher-scoped data isolation, role-boundary checks).
- `StudentDeleteViewModel.cs` already defines `StudentReservationSummary` (RoomName/DayOfWeek/StartTime/EndTime) — reused as-is for the "existing bookings in this room/day" read-only display, with no student/teacher identity exposed (keeps the generic-error-and-no-cross-teacher-detail decision consistent).

### Key Discoveries:

- `src/melody/Models/Domain/Reservation.cs` has no overlap-detection logic yet — this plan adds one static method (`Reservation.Overlaps`) rather than a new service class/folder, keeping with the existing "logic lives on the domain model" pattern (c.f. `Student.LessonDuration`, `Room.IsSpecialistRoom`).
- `Program.cs`'s seeder-invocation block (`RoleSeeder.SeedAsync` then `AdminUserSeeder.SeedAsync`) runs inside both normal (`MigrateAsync`) and `Testing` (`EnsureCreatedAsync`) startup paths, so a `RoomSeeder` added to the same block seeds consistently in both real runs and the test host.
- `Views/Admin/Index.cshtml` establishes the only existing "dashboard link" convention (plain anchor tags, not a navbar entry) — this plan adds a "Book a room" link per student row on `Teacher/Index.cshtml` rather than a new nav item, matching that convention.

## Desired End State

A signed-in Teacher, from `Teacher/Index`, can click "Book a room" next to one of their own students, pick a Room, Day of week, and a fixed time slot, optionally check existing bookings for that Room+Day before submitting, and create a weekly-recurring Reservation. Submitting a conflicting slot (exact match or overlap) is always rejected with a generic error and no data is saved; back-to-back slots (one ends exactly when another starts) are allowed. A fixed set of rooms (including one each for Piano/Xylophone/Drums) exists in every environment via a startup seeder.

### Automated verification covers: `dotnet build melodybooker.sln` succeeds; `dotnet test` passes, including new unit tests for the overlap rule and new integration tests for the booking flow (success, exact-duplicate block, overlap block, back-to-back allowed, cross-teacher ownership block, role-boundary block).
### Manual verification covers: an end-to-end booking walkthrough as a Teacher (pick room/day/time, see existing bookings, submit, see it reflected), confirming the specialist rooms show up by name only (no instrument badge yet — that's S-04).

## What We're NOT Doing

- Not implementing the specialist-instrument mismatch warning (FR-011) — that's S-04, a separate downstream slice. `Reservation.HadInstrumentMismatchWarning` stays `false` for every reservation created by this plan.
- Not building an Admin Room CRUD UI — rooms are fixed, seeded data for this slice (explicit user decision). A future change can add CRUD if the school needs to add/rename rooms.
- Not giving the teacher an edit/cancel/list-of-own-reservations view — FR-014 reserves edit/cancel for Administrator only (that's S-05), and this slice is create-only per explicit user decision.
- Not handling school-holiday/break exceptions to the weekly recurrence — out of scope per the PRD's Non-Goals and the roadmap's own open question (owner: user, non-blocking for creation).
- Not adding any JavaScript/AJAX — the "show existing bookings before submit" UX is a plain GET round-trip (dual submit buttons with `formmethod`), consistent with the rest of the app having no client-side framework.
- Not restricting which days of the week are bookable — all seven `DayOfWeek` values are offered; the PRD doesn't restrict lessons to weekdays only.

## Implementation Approach

Build bottom-up: first make rooms exist and the conflict rule independently correct and unit-testable (Phase 1), then build the teacher-facing controller/views on top of it (Phase 2), then close the loop with integration tests covering the full flow end-to-end (Phase 3). Each phase is independently buildable and testable before the next starts.

## Phase 1: Room seeding + conflict-detection core

### Overview

Seed a fixed seat of rooms at startup and add a standalone, unit-testable overlap-detection rule that Phase 2's controller will call.

### Changes Required:

#### 1. Room seeder

**File**: `src/melody/Data/RoomSeeder.cs` (new)

**Intent**: Populate the empty `Rooms` table with a fixed inventory on startup, following the same static-seeder pattern as `RoleSeeder`/`AdminUserSeeder`, since no Room CRUD exists and the PRD has no FR for Room provisioning.

**Contract**: `public static class RoomSeeder { public static async Task SeedAsync(IServiceProvider services) }`. Resolves `ApplicationDbContext`, does nothing if `Rooms` already has any row (idempotent, matches `RoleSeeder`'s `if (!exists)` guard style), otherwise inserts 6 rooms: 3 general-purpose (`SpecialistInstrument = null`) named "Room 1"/"Room 2"/"Room 3", and one each named "Piano Room" / "Xylophone Room" / "Drum Room" with `SpecialistInstrument` set to `Instrument.Piano` / `Instrument.Xylophone` / `Instrument.Drums` respectively.

#### 2. Wire the seeder into startup

**File**: `src/melody/Program.cs`

**Intent**: Run `RoomSeeder.SeedAsync` once at startup, in the same scope as the existing seeders, so it runs identically in real deployments and the test host (`Testing` environment's `EnsureCreatedAsync` path).

**Contract**: Add `await RoomSeeder.SeedAsync(scope.ServiceProvider);` immediately after the existing `await AdminUserSeeder.SeedAsync(...)` call.

#### 3. Overlap-detection rule

**File**: `src/melody/Models/Domain/Reservation.cs`

**Intent**: Add the single rule the whole FR-010 guarantee rests on: two time ranges conflict only when they genuinely overlap, not when one simply touches the other's boundary (back-to-back bookings in the same room are allowed per user decision).

**Contract**: `public static bool Overlaps(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB) => startA < endB && startB < endA;` — half-open interval overlap test. Document on the XML comment that equal boundaries (`endA == startB`) return `false` (no conflict).

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds
- `dotnet test --filter FullyQualifiedName~ReservationOverlapTests` passes, covering: exact-match ranges overlap; overlapping-different-start ranges overlap; back-to-back ranges (one ends exactly when the other starts) do NOT overlap; fully-disjoint ranges do NOT overlap; one range fully containing another overlaps
- `dotnet test --filter FullyQualifiedName~RoomSeederTests` passes, asserting the seeded `Rooms` table has 6 rows after host startup, including exactly one room per specialist instrument (Piano/Xylophone/Drums) and 3 non-specialist rooms, and that re-running startup does not duplicate rows

#### Manual Verification:

- None — this phase is pure data/logic with full automated coverage

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Teacher booking controller + views

### Overview

Let a Teacher create a reservation for one of their own students: a scoped controller, a booking form view model, and the Create view with Room/Day/Time selects and an existing-bookings check, wired from a new link on `Teacher/Index.cshtml`.

### Changes Required:

#### 1. Reservation form view model

**File**: `src/melody/Models/ViewModels/ReservationFormViewModel.cs` (new)

**Intent**: Back the Create form: the student is fixed (chosen via the entry link, not re-selectable), Room/Day/Time are the teacher's choices, and the controller-populated dropdown/summary collections must never be posted-back-validated.

**Contract**: Properties: `StudentId` (int, required, hidden field), `[ValidateNever] StudentFullName` (string, display-only), `[ValidateNever] StudentLessonDuration` (TimeSpan, display-only), `[Required] RoomId` (int), `[Required] DayOfWeek` (DayOfWeek), `[Required] StartMinutes` (int — minutes since midnight, bound from the time-slot `<select>`), `[ValidateNever] Rooms` / `Days` / `TimeSlots` (each `IEnumerable<SelectListItem>`), `[ValidateNever] ExistingReservationsForRoomAndDay` (`List<StudentReservationSummary>`, reusing the existing type from `StudentDeleteViewModel.cs`).

#### 2. Teacher-scoped reservations controller

**File**: `src/melody/Controllers/TeacherReservationsController.cs` (new)

**Intent**: Create-only controller scoped to the logged-in teacher's own students (same ownership pattern as `TeacherController`), implementing the full double-booking guard before saving.

**Contract**: `[Authorize(Roles = "Teacher")]`. `GET Create(int studentId, int? roomId, DayOfWeek? dayOfWeek)`: resolve the teacher via `UserManager`, load the student by `studentId`, return `NotFound()` if the student doesn't exist or isn't assigned to this teacher (ownership scoping — same guarantee `TeacherController.Index` already provides); build the view model with `Rooms` (all rooms ordered by name, name only — no specialist indicator, per decision), `Days` (`Html.GetEnumSelectList<DayOfWeek>()`-compatible source, i.e. all 7 values), `TimeSlots` (fixed 15-minute increments from 08:00 to 18:00, formatted `HH:mm`, value = minutes since midnight); when `roomId`+`dayOfWeek` are both supplied, populate `ExistingReservationsForRoomAndDay` from reservations in that room/day (ordered by start time, projected into `StudentReservationSummary` — room name + day + start/end only, no identifying student/teacher data). `POST Create(ReservationFormViewModel model)`: re-check student ownership (`NotFound()` if not owned); on `ModelState.IsValid` failure, rebuild the three dropdown collections + `ExistingReservationsForRoomAndDay` and return the view; otherwise compute `StartTime = TimeSpan.FromMinutes(model.StartMinutes)`, `Duration = student.LessonDuration`, query existing reservations for the same Room+Day and check each against the new range with `Reservation.Overlaps`; if any conflict, add a model error ("This room is already booked at that time.") and re-render the form (rebuilding dropdowns/summary) with no save; otherwise create and save the `Reservation` (`RoomId`, `StudentId`, `TeacherId` = this teacher's id, `DayOfWeek`, `StartTime`, `Duration`), wrapping `SaveChangesAsync` in a try/catch for `DbUpdateException` (the DB unique-index race for an exact-match conflict) that falls back to the same generic error message and re-render; on success redirect to `TeacherController.Index`.

#### 3. Create view

**File**: `src/melody/Views/TeacherReservations/Create.cshtml` (new)

**Intent**: Render the booking form following the `AdminStudents/Create.cshtml` conventions (`asp-validation-summary="ModelOnly"`, `_ValidationScriptsPartial`), with a dual-submit-button pattern that lets the teacher reload the page via GET (to see existing bookings for the currently-selected Room+Day) without losing the ability to submit the final booking via POST.

**Contract**: `@model ReservationFormViewModel`. Shows `StudentFullName` and `StudentLessonDuration` read-only. One `<form asp-action="Create" method="post">` containing: hidden `StudentId`; `RoomId` select (`asp-items="Model.Rooms"`); `DayOfWeek` select (`asp-items="Html.GetEnumSelectList<DayOfWeek>()"`); `StartMinutes` select (`asp-items="Model.TimeSlots"`); a table of `Model.ExistingReservationsForRoomAndDay` shown only when non-empty; two submit buttons:
```html
<button type="submit" formmethod="get" class="btn btn-outline-secondary">Check availability</button>
<button type="submit" class="btn btn-primary">Book room</button>
```
The GET button submits the same selected `RoomId`/`DayOfWeek` as query-string params to the `Create` GET action (browser-native `formmethod` behavior), which re-renders with `ExistingReservationsForRoomAndDay` populated for that combination.

#### 4. Entry link from the teacher's student list

**File**: `src/melody/Views/Teacher/Index.cshtml`

**Intent**: Give the teacher a way to reach the booking form for a specific student.

**Contract**: Add a "Book a room" link per row in the existing students table, e.g. `asp-controller="TeacherReservations" asp-action="Create" asp-route-studentId="@student.Id"`.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds

#### Manual Verification:

- Signed in as a Teacher with at least one assigned student, click "Book a room" from `Teacher/Index`, pick a Room/Day/Time, click "Check availability" and confirm existing bookings for that Room+Day (if any) are shown without saving anything
- Submit a booking with no conflict — confirm success (redirect to `Teacher/Index`) and the reservation is later visible via the DB/admin side
- Attempt to submit the exact same Room/Day/Time again — confirm it's rejected with the generic "already booked" message and no duplicate is created
- Attempt to submit an overlapping-but-different-start time in the same Room/Day — confirm it's rejected the same way
- Attempt to submit a back-to-back time (starts exactly when an existing booking ends) — confirm it's accepted
- Confirm the Room dropdown shows room names only, no specialist-instrument indicator

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Integration tests

### Overview

Lock in the double-booking guarantee and the teacher-scoping guarantee with end-to-end integration tests, following the existing `MelodyWebApplicationFactory`/`TeacherStudentListTests.cs` pattern.

### Changes Required:

#### 1. Booking flow integration tests

**File**: `tests/melody.Tests/TeacherBooksRoomTests.cs` (new)

**Intent**: Prove the full create-reservation flow end-to-end against the real controller/view stack (SQLite-in-memory), covering the scenarios manually verified in Phase 2 plus the role/ownership boundary.

**Contract**: `IClassFixture<MelodyWebApplicationFactory>`, following `TeacherStudentListTests.cs`'s seeding-helper style. Test cases: successful booking creates a `Reservation` with `Duration` matching the student's `LessonDuration`; posting the exact same Room/Day/Time twice is rejected on the second attempt (no second row created); posting an overlapping-but-different-start time in the same Room/Day is rejected; posting a back-to-back time (starts exactly when an existing booking ends) succeeds; a teacher cannot reach `Create` (GET or POST) for a student assigned to a different teacher (`NotFound`); an Administrator or anonymous request to `/TeacherReservations/Create` is forbidden/redirected (role-boundary regression, mirroring `AuthorizationTests.cs`'s existing coverage style).

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds (final build after all phases)
- `dotnet test` passes in full, including all new `ReservationOverlapTests`, `RoomSeederTests`, and `TeacherBooksRoomTests` cases alongside the existing suite (no regressions)

#### Manual Verification:

- None — this phase's purpose is converting the Phase 2 manual checks into permanent automated coverage

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `Reservation.Overlaps` — exact match, overlap at different start, back-to-back (no conflict), fully disjoint (no conflict), containment.

### Integration Tests:

- Room seeding produces the expected fixed inventory and is idempotent across repeated startups.
- Full booking create flow: success, exact-duplicate block, overlap block, back-to-back allowed, cross-teacher ownership block, role-boundary block.

### Manual Testing Steps:

1. Run `dotnet run --project src/melody/melody.csproj`, log in as a seeded Teacher with an assigned student.
2. From `Teacher/Index`, click "Book a room" for a student.
3. Pick a Room + Day, click "Check availability" — confirm existing bookings (if any) are listed with no data saved.
4. Pick a free Time slot and submit — confirm the booking succeeds.
5. Re-open the form for the same Room/Day and try to re-book the exact same Time — confirm it's rejected.
6. Try an overlapping-but-different-start Time in the same Room/Day — confirm it's rejected.
7. Try a back-to-back Time (starts exactly when the existing booking ends) — confirm it's accepted.

## Performance Considerations

None — this is low-volume CRUD-scale data (a single school's rooms/reservations); no caching or batching needed.

## Migration Notes

No new EF Core migration is required — `Rooms`/`Reservations` tables already exist from `InitialCreate`. `RoomSeeder` is a data-only, idempotent startup step, not a schema change.

## References

- `src/melody/Models/Domain/Reservation.cs`, `Room.cs`, `Student.cs` — existing domain model this plan builds on
- `src/melody/Data/ApplicationDbContext.cs` — existing `(RoomId, DayOfWeek, StartTime)` unique index
- `src/melody/Data/RoleSeeder.cs`, `AdminUserSeeder.cs` — seeder pattern followed by `RoomSeeder`
- `src/melody/Controllers/TeacherController.cs` — teacher-ownership scoping pattern reused
- `src/melody/Controllers/AdminStudentsController.cs`, `Views/AdminStudents/Create.cshtml`, `_StudentFormFields.cshtml` — form/validation conventions followed
- `src/melody/Models/ViewModels/StudentDeleteViewModel.cs` — `StudentReservationSummary` type reused
- `tests/melody.Tests/MelodyWebApplicationFactory.cs`, `TeacherStudentListTests.cs` — integration test pattern followed
- `context/foundation/roadmap.md` — S-03 scope, prerequisites, and risk callout

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Room seeding + conflict-detection core

#### Automated

- [x] 1.1 `dotnet build melodybooker.sln` succeeds — d307d94
- [x] 1.2 `ReservationOverlapTests` pass (exact match, overlap, back-to-back, disjoint, containment) — d307d94
- [x] 1.3 `RoomSeederTests` pass (6 rooms seeded, correct specialist split, idempotent) — d307d94

### Phase 2: Teacher booking controller + views

#### Automated

- [x] 2.1 `dotnet build melodybooker.sln` succeeds — f922544

#### Manual

- [x] 2.2 Check-availability round trip shows existing bookings without saving — f922544
- [x] 2.3 Successful booking with no conflict — f922544
- [x] 2.4 Exact-duplicate Room/Day/Time rejected — f922544
- [x] 2.5 Overlapping-different-start Room/Day/Time rejected — f922544
- [x] 2.6 Back-to-back Room/Day/Time accepted — f922544
- [x] 2.7 Room dropdown shows names only, no specialist indicator — f922544

### Phase 3: Integration tests

#### Automated

- [x] 3.1 Final `dotnet build melodybooker.sln` succeeds — 536df88
- [x] 3.2 `dotnet test` passes in full (all new + existing tests) — 536df88
