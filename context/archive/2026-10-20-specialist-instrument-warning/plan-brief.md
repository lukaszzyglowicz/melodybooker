# Plan Brief: Specialist-Instrument Mismatch Warning

Full plan: [`plan.md`](./plan.md)

## What & Why

Teachers booking a specialist room (piano/xylophone/drums) for a student who doesn't play that instrument currently get no feedback at all. FR-011/US-02 require a **soft** warning — visible, but never blocking the booking. This finishes a domain field (`HadInstrumentMismatchWarning`) that was scaffolded in S-03's migration but never populated.

## Starting Point

- `Reservation.HadInstrumentMismatchWarning` exists in the DB/model but is always `false` — no code sets it.
- `Room.SpecialistInstrument` / `Room.IsSpecialistRoom` already model specialist rooms; `Student.Instrument` already exists.
- `TeacherReservationsController.Create` (POST) does: validate → room-exists check (`AnyAsync`) → hard conflict block (FR-010) → save. No mismatch check, no warn/confirm mechanism.
- No specialist-indicator in the Room dropdown (kept as-is from S-03, confirmed to stay unchanged).

## Desired End State

Booking a mismatched specialist room shows a yellow warning banner (room's instrument vs. student's instrument) and does **not** save. Clicking "Book anyway" re-submits the same Room/Day/Time with a pinned confirmation and saves with the flag set `true`. Matching instrument or non-specialist room → saves immediately, flag `false`, no warning. Changing Room/Day/Time after a warning invalidates the pinned confirmation and re-warns. FR-010 hard-block still always takes priority.

## Key Decisions Made

| # | Decision | Source |
|---|----------|--------|
| 1 | Confirmation via two-step server-side resubmit (hidden fields + "Book anyway"), no JS | Plan |
| 2 | Confirmation pinned to exact Room+Day+StartMinutes combo; any field change invalidates it | Plan |
| 3 | `Instrument.Other` treated as a normal mismatch, no special-casing | Plan |
| 4 | Room dropdown stays name-only — no specialist indicator added | Plan |
| 5 | Warning shown only on POST "Book room", not on GET "Check availability" | Plan |
| 6 | Detailed warning message, styled `alert-warning` (yellow), not `text-danger` | Plan |
| 7 | Scope is Teacher-only; no Admin/S-05 prep work | Plan |
| 8 | Full test coverage — 5 integration test cases | Plan |

## Scope

**In scope:**
- `Reservation.IsInstrumentMismatch(Room, Student)` domain rule + unit tests
- Controller warn/confirm/save branching logic (room fetched as entity, not just existence check)
- ViewModel pending-confirmation fields + warning-display fields
- View warning banner + hidden pinned-confirmation fields + button label change
- 5 integration tests covering the full flow, including stale-confirmation invalidation

**Out of scope:**
- FR-012 hard-block on mismatch (explicit v2/nice-to-have)
- Room dropdown specialist indicator
- Warning on GET "Check availability"
- Any Admin-side reservation flow (S-05)
- Client-side/JS confirmation

## Architecture / Approach

Bottom-up, mirroring S-03's exact pattern: a standalone, unit-tested domain rule first (Phase 1), then controller/view wiring on top of it (Phase 2), then integration tests closing the loop (Phase 3). The hard double-booking check always runs and short-circuits before the soft mismatch check — no change to that ordering.

## Phases at a Glance

| Phase | Name | Summary |
|-------|------|---------|
| 1 | Instrument-mismatch domain rule | `Reservation.IsInstrumentMismatch` + unit tests |
| 2 | Warn-then-confirm controller/view flow | ViewModel + controller + view changes |
| 3 | Integration tests | 5 end-to-end test cases |

**Prerequisites**: S-03 (teacher-books-room), archived and done. **Estimated effort**: Small — no schema/migration changes, 3 focused phases touching ~4 files plus one new test file.

## Open Risks & Assumptions

- Assumes `Room.SpecialistInstrument` and `Student.Instrument` are reliably populated for all seeded/admin-created test data (true today per S-03's seeding).
- The pinned-confirmation hidden fields rely on the user not tampering with the form; no server-side signing/HMAC is used — acceptable given the single-school, trusted-teacher-login threat model (no prior security decision suggests otherwise).

## Success Criteria Summary

`dotnet build` and `dotnet test` pass (including 5+ new integration tests and the domain-rule unit tests); manual walkthrough confirms warn → confirm → save and matching/non-specialist flows behave as described above.
