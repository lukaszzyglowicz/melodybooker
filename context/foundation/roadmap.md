---
project: "MelodyBooker"
version: 1
status: draft
created: 2026-10-06
updated: 2026-10-06
prd_version: 1
main_goal: speed
top_blocker: capacity
milestone_id: first-usable-booking-mvp
milestone_seq: 1
milestone_status: open
---

# Roadmap: MelodyBooker

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: First usable booking MVP** — Status: open

- **Intent:** Prove that a digitized booking system can replace the school's manual Excel process end-to-end: an administrator manages the roster, a teacher books recurring weekly lessons with the correct auto-derived duration and an unbreakable double-booking guardrail, the system softly warns on specialist-instrument mismatches, and an administrator can oversee every reservation — deployed so the school can actually use it before the 2026-12-10 deadline.
- **Source materials:** `context/foundation/prd.md` (v1)
- **Done when:** every F-NN and S-NN below is `done`.
- **Scope anchors:** FR-001 through FR-011, FR-013, FR-014, US-01, US-02 (see `## At a glance` for the per-item mapping).

## Vision recap

Music teachers currently plan room reservations by hand in Excel, which cannot enforce that rooms with non-portable specialist instruments (piano, xylophone, drums) are prioritized for the students who actually play them — causing booking conflicts and wasted coordination time. The fix is a web app, used only by the school's own administrator and teachers, that encodes this priority rule and makes double-booking structurally impossible.

## North star

**S-03: Teacher books a room for a weekly lesson** — the smallest end-to-end flow that proves the core idea works: a spreadsheet can't auto-derive lesson duration or hard-block double-booking, this slice can.

> A reader-facing note on what "north star" means here: the smallest end-to-end slice whose successful delivery proves the product solves the stated problem — placed as early as its prerequisites allow, because every later slice only matters if this one works.

## At a glance

| ID   | Change ID                   | Outcome (user can …)                                                              | Prerequisites | PRD refs                       | Status   |
| ---- | ---------------------------- | ---------------------------------------------------------------------------------- | -------------- | ------------------------------- | -------- |
| F-01 | role-based-authorization     | (foundation) Role-scoped login/authorization enforced; admin account seeded        | —              | FR-001, FR-002, Access Control  | ready    |
| F-02 | deployment-skeleton          | (foundation) App deploys to Azure App Service via GitHub Actions on merge          | —              | tech-stack.md deployment target | blocked  |
| S-01 | admin-roster-management      | Administrator adds/edits/removes teachers and students, assigns student→teacher    | F-01           | FR-003, FR-004, FR-005, FR-006  | proposed |
| S-02 | teacher-student-list         | Teacher logs in and sees the list of their assigned students                        | F-01, S-01     | FR-007                          | proposed |
| S-03 | teacher-books-room           | Teacher reserves a room for a student's weekly lesson (auto duration, no double-booking) | F-01, S-02 | FR-008, FR-009, FR-010, US-01   | proposed |
| S-04 | specialist-instrument-warning| Teacher sees a soft warning before confirming a specialist-room/mismatched-student booking | S-03    | FR-011, US-02                   | proposed |
| S-05 | admin-reservation-oversight  | Administrator views and edits/cancels any reservation in the system                 | F-01, S-03     | FR-013, FR-014                  | proposed |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme                   | Chain                                 | Note                                                                              |
| ------ | ----------------------- | -------------------------------------- | ---------------------------------------------------------------------------------- |
| A      | Core booking path        | `F-01` → `S-01` → `S-02` → `S-03`      | Carries the north star `S-03`; everything else branches off its tail.             |
| B      | Specialist warning        | `S-04`                                 | Joins Stream A at `S-03`.                                                         |
| C      | Admin oversight            | `S-05`                                 | Joins Stream A at `S-03`; can run in parallel with Stream B.                       |
| D      | Go-live infra               | `F-02`                                 | Standalone; independent of auth/booking work, needed before the hard deadline.     |

## Baseline

What's already in place in the codebase as of `2026-10-06` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** partial — default Razor scaffold only (`Views/Home`, `Views/Shared`, `_LoginPartial`); no domain-specific views (students/teachers/rooms/reservations).
- **Backend / API:** partial — only `HomeController` exists; no domain controllers.
- **Data:** partial — EF Core `ApplicationDbContext` with `Teacher`, `Student`, `Room`, `Reservation`, `Instrument` models and an applied `InitialCreate` migration; a unique index on `(RoomId, DayOfWeek, StartTime)` already enforces part of FR-010 at the database layer. No application-layer booking/conflict services yet.
- **Auth:** partial — ASP.NET Core Identity wired (`ApplicationUser`, cookie auth), `Administrator`/`Teacher` roles seeded at startup (`RoleSeeder`). No `[Authorize(Roles=...)]` enforcement on any controller yet, no seeded admin account, no role-aware redirects.
- **Deploy / infra:** absent — `.github/workflows` exists but is empty; no Dockerfile or IaC, despite `tech-stack.md` targeting Azure App Service + GitHub Actions auto-deploy-on-merge.
- **Observability:** present — Application Insights wired (`AddApplicationInsightsTelemetry`) and a `/health` endpoint backed by a DB health check.

## Foundations

### F-01: Role-based authorization & seeded administrator account

- **Outcome:** (foundation) Every controller/area enforces the correct role (`Administrator` vs `Teacher`); an administrator account exists at startup so the first admin can log in without any public sign-up flow (per Access Control, there is none).
- **Change ID:** role-based-authorization
- **PRD refs:** FR-001, FR-002, Access Control section, NFR (minors' data accessible only to Administrator and the assigned Teacher)
- **Unlocks:** S-01, S-02, S-03, S-05 — every slice needs its role boundary to be real, not just scaffolded, before it can be verified as correct; also closes the data-privacy NFR gap.
- **Prerequisites:** —
- **Parallel with:** F-02
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Identity and roles are already seeded (baseline), but nothing currently enforces them on a controller and no admin account exists to log in with — leaving this until later would make every downstream slice impossible to verify for its most important guardrail (teachers only see their own students; minors' data stays scoped).
- **Status:** ready

### F-02: Deployment skeleton (Azure App Service + CI)

- **Outcome:** (foundation) The app builds, migrates, and deploys to Azure App Service automatically on merge via GitHub Actions, so the school can actually use the MVP once it's functionally done.
- **Change ID:** deployment-skeleton
- **PRD refs:** PRD frontmatter `timeline_budget.hard_deadline` (2026-12-10); `tech-stack.md` deployment target
- **Unlocks:** a real usage/verification path for the whole milestone before the hard deadline — without it, a functionally complete MVP still can't reach the school in time.
- **Prerequisites:** —
- **Parallel with:** F-01, S-01
- **Blockers:** —
- **Unknowns:**
  - Is an Azure subscription / App Service resource already provisioned for this school, or does one need to be created first? — Owner: user. Block: yes.
- **Risk:** Given capacity (after-hours-only solo developer) is the top blocker, discovering Azure/CI friction only at the very end — right before the deadline — is the riskiest possible sequencing; better to surface provisioning gaps now.
- **Status:** blocked

## Slices

### S-01: Administrator manages the roster

- **Outcome:** Administrator can add, edit, and remove teachers and students, and assign each student to exactly one teacher — including safe handling (reassign or block) when a teacher/student with active assignments or reservations is removed.
- **Change ID:** admin-roster-management
- **PRD refs:** FR-003, FR-004, FR-005, FR-006
- **Prerequisites:** F-01
- **Parallel with:** F-02
- **Blockers:** —
- **Unknowns:** —
- **Risk:** FR-006's reassign-or-block deletion semantics must be built in from the start — retrofitting it after S-02/S-03 exist and already reference students/teachers would be far riskier.
- **Status:** proposed

### S-02: Teacher views assigned students

- **Outcome:** Teacher logs in and sees the list of students assigned to them (and only them).
- **Change ID:** teacher-student-list
- **PRD refs:** FR-007, FR-002
- **Prerequisites:** F-01, S-01
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Small in scope, but the "only their own students" guardrail checked here is reused directly by S-03's booking screen — getting the scoping query wrong here propagates downstream.
- **Status:** proposed

### S-03: Teacher books a room for a weekly lesson

- **Outcome:** Teacher reserves a room for an assigned student's weekly lesson (day of week + time); duration is auto-derived from the student's class; the booking repeats weekly for the whole school year; booking an already-taken room/time slot is always hard-blocked, with no override.
- **Change ID:** teacher-books-room
- **PRD refs:** FR-008, FR-009, FR-010, US-01
- **Prerequisites:** F-01, S-02
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:**
  - How exactly should school holidays/breaks be handled in the weekly, full-school-year schedule? — Owner: user. Block: no (PRD's Non-Goals already settle this as manual, teacher-driven cancel/move of single occurrences; the mechanism for that doesn't gate creating the recurring booking itself, but affects a later refinement).
- **Risk:** The double-booking guardrail is the single highest-risk piece of business logic in the whole MVP — the existing DB unique index only catches exact `(room, day, start time)` matches, so the application layer must also reject overlapping-duration conflicts at different start times in the same room.
- **Status:** proposed

### S-04: System warns on specialist-instrument mismatch

- **Outcome:** When a teacher books a specialist room (piano/xylophone/drums) for a student who doesn't play that instrument, they see a warning before final confirmation but can still complete the booking.
- **Change ID:** specialist-instrument-warning
- **PRD refs:** FR-011, US-02
- **Prerequisites:** S-03
- **Parallel with:** S-05
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Low — a validation/UI addition on top of S-03's booking flow; the main risk is ordering (warn before confirm, not after), which the PRD's acceptance criteria already pin down.
- **Status:** proposed

### S-05: Administrator oversees all reservations

- **Outcome:** Administrator can view every room reservation in the system and edit or cancel any of them.
- **Change ID:** admin-reservation-oversight
- **PRD refs:** FR-013, FR-014
- **Prerequisites:** F-01, S-03
- **Parallel with:** S-04
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Admin edits/cancels must still go through the same no-double-booking guardrail as S-03's teacher-facing path — a second, looser code path here would reopen the exact conflict risk S-03 closed.
- **Status:** proposed

## Backlog Handoff

| Roadmap ID | Change ID                      | Suggested issue title                                              | Ready for `/10x-plan` | Notes                                      |
| ---------- | -------------------------------- | ---------------------------------------------------------------------- | ---------------------- | --------------------------------------------- |
| F-01       | role-based-authorization         | Enforce role-based authorization and seed an administrator account     | yes                     | Run `/10x-plan role-based-authorization`       |
| F-02       | deployment-skeleton              | Stand up Azure App Service deploy + GitHub Actions CI                  | no                      | Blocked on Azure subscription/resource confirmation |
| S-01       | admin-roster-management          | Admin: add/edit/remove teachers & students, assign student to teacher  | no                      | Blocked on F-01                                |
| S-02       | teacher-student-list              | Teacher: view list of assigned students                                | no                      | Blocked on F-01, S-01                          |
| S-03       | teacher-books-room                | Teacher: book a weekly recurring room reservation (auto duration, no double-booking) | no   | Blocked on F-01, S-02 — this is the north star |
| S-04       | specialist-instrument-warning     | Warn teacher on specialist-room/instrument mismatch before confirming   | no                      | Blocked on S-03                                |
| S-05       | admin-reservation-oversight       | Admin: view and edit/cancel any reservation                            | no                      | Blocked on F-01, S-03                          |

## Open Roadmap Questions

1. **How exactly should school holidays/breaks be handled in the weekly, full-school-year schedule?** — Owner: user. Block: S-03 (non-blocking for creation; relevant to any future exception-handling refinement of the recurring schedule).
2. **Is an Azure subscription / App Service resource already provisioned for this school?** — Owner: user. Block: F-02.

## Parked

- **FR-012 — Hard-blocking specialist-room bookings for mismatched students** — Why parked: explicit nice-to-have in the PRD (v2 scope); MVP uses soft warning only (FR-011).
- **FR-015 — Admin filter/search of reservations** — Why parked: explicit nice-to-have in the PRD; MVP ships with the raw list from FR-013.
- **FR-016 — Notify teacher when admin edits/cancels their reservation** — Why parked: explicit nice-to-have in the PRD; adds scope beyond the 3-week MVP budget.
- **Mobile app** — Why parked: explicit PRD Non-Goal; web-only.
- **Desktop app** — Why parked: explicit PRD Non-Goal; web-only.
- **Multi-tenant / multiple schools** — Why parked: explicit PRD Non-Goal; single school only.
- **Automatic room-assignment optimization** — Why parked: explicit PRD Non-Goal; teacher picks the room manually.
- **Bulk import of students/teachers from Excel** — Why parked: explicit PRD Non-Goal; one-by-one add only (FR-003).
- **Email/SMS notifications (beyond FR-016)** — Why parked: explicit PRD Non-Goal.
- **Student login/accounts** — Why parked: explicit PRD Non-Goal; students have no account.
- **Payments/billing for lessons** — Why parked: explicit PRD Non-Goal.
- **Automatic holiday/break handling in the schedule** — Why parked: explicit PRD Non-Goal; handled manually by the teacher (see Open Roadmap Question 1).
- **WCAG-AA accessibility certification** — Why parked: explicit PRD Non-Goal for MVP.

## Milestone History

(empty — this is the first milestone)

## Done

(empty — nothing archived yet)
