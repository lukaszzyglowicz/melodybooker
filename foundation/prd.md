---
project: MelodyBooker
version: 1
status: draft
created: 2026-10-01
context_type: greenfield
product_type: web-app
target_scale:
  users: medium
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 3
  hard_deadline: 2026-12-10
  after_hours_only: true
---

## Vision & Problem Statement

Music teachers at a music school need to book a room for each lesson, but rooms with fixed, non-portable instruments (piano, xylophone, drums) are a scarce resource: students who play those instruments MUST use one of those rooms, while students who play portable instruments (violin, viola, trumpet, etc.) can bring their own instrument to any regular room. Without a system, there is no way to enforce this priority or prevent double-booking of the specialty rooms, so conflicts are resolved manually and inconsistently.

The insight that makes this worth building: room priority isn't static. A student preparing for a competition can occasionally need a one-time, higher-than-normal priority for a specific type of specialty room — even if it means temporarily displacing another student's existing reservation. A purely static priority rule (portable-instrument students always yield to fixed-instrument students) misses this case; the system needs to support a deliberate, one-off override a teacher can grant.

## User & Persona

**Primary persona:** Music teacher at a single music school. Reaches for the product every week when scheduling lessons for the students assigned to them — each student has one instrument, one teacher, and two lessons per week for the full school year, so scheduling is effectively done once per term rather than continuously.

### Secondary persona

Administrator. Manages the roster (students, teachers, assignments) and has full visibility/edit rights over all reservations, but does not teach lessons themselves.

## Success Criteria

### Primary
- A teacher can log in, see their assigned students, book a room for a lesson (with specialty-room priority enforced), and see their weekly schedule.

### Secondary
- The system shows scheduling conflicts and highlights freed-up rooms when a conflict/override occurs.

### Guardrails
- Reservations may only be scheduled between 7:00 and 16:00.
- A teacher's lessons should be back-to-back where possible — a teacher should not have more than a 20-minute gap between consecutive lessons (e.g., 7:00–7:20 then 7:20–7:40).
- Specialty-room priority (piano/xylophone/drums) must be strictly enforced — never silently bypassed.
- No double-booking of the same room at the same time.

## User Stories

### US-01: Teacher reserves a room for a student

- **Given** a logged-in teacher with an assigned student who plays a specific instrument
- **When** they select the student, day, and time for a lesson
- **Then** the system shows available rooms — with strict priority given to specialty rooms (piano/xylophone/drums) for students who play those instruments — and creates the reservation on confirmation, visible in the teacher's schedule

#### Acceptance Criteria
- A specialty room (piano/xylophone/drums) cannot be booked by a student who plays a different instrument if a regular room is available
- Specialty-room priority is strictly enforced, regardless of whether a conflict currently exists
- Lesson duration is auto-suggested based on the student's class/grade, but the teacher can manually override it
- The same room/time slot cannot be double-booked
- A teacher can grant a one-time competition-prep priority override that takes precedence over the normal specialty-room priority rule for a single booking

## Functional Requirements

### Administration
- FR-001: Administrator can add/edit students (name, class/grade, instrument). Priority: must-have
- FR-002: Administrator can add/edit music teachers. Priority: must-have
- FR-003: Administrator can assign a student to a teacher. Priority: must-have
- FR-004: Administrator can view and edit ALL reservations. Priority: must-have

### Scheduling
- FR-005: Teacher can reserve a room for an assigned student; lesson duration is auto-suggested by the student's class/grade (20 min for grades 1–4, 40 min for grades 5–8) but can be manually overridden by the teacher. Priority: must-have
  > Socrates: Counter-argument considered: "automatic duration may be too rigid — sometimes a lesson runs shorter/longer." Resolution: kept; duration is auto-suggested but the teacher can manually adjust it per booking.
- FR-006: System enforces strict priority for students who play piano/xylophone/drums to book the rooms containing those instruments, regardless of whether another student currently wants the room. Priority: must-have
  > Socrates: Counter-argument considered: "rigid enforcement may be frustrating when a specialty room is free and nobody else needs it." Resolution: kept as strict/always-enforced — the school wants priority to be a hard rule, not a conditional one.
- FR-007: Teacher can grant a one-time competition-prep priority override, letting a student take over an already-reserved specialty room for a single booking. Priority: must-have
  > Socrates: Counter-argument considered: "this may feel unfair to the displaced student" and "the exception logic may be too complex for an MVP and could wait for v2." Resolution: kept as must-have — this is a core domain rule the school specifically called out, not an edge case to defer.
- FR-008: Teacher can view their weekly lesson schedule. Priority: must-have
  > Socrates: No counter-argument considered; it stands as written.
- FR-010: System prevents double-booking of the same room at the same time. Priority: must-have
  > Socrates: No counter-argument considered; it stands as written.

### Access
- FR-009: An unauthenticated visitor can view the school's public offering but cannot make reservations. Priority: must-have
  > Socrates: Counter-argument considered: "a public offering view may be unnecessary for MVP — could require login immediately instead." Resolution: kept; matches the school's existing public-facing marketing need.

## Non-Functional Requirements

- Reservation confirmation appears to the user in near-instant time (no visible delay for the common case).
- A student's data (name, instrument, schedule) is visible only to their assigned teacher and to administrators — never to other teachers.
- The application works on the latest versions of major desktop browsers (Chrome, Firefox, Safari, Edge).

## Business Logic

System assigns rooms to reservations based on the student's instrument — students who play stationary instruments (piano, xylophone, drums) have strict priority over the rooms containing those instruments, unless a teacher grants a one-time override giving a competition-prep student a higher, temporary priority that can displace an existing reservation.

This rule consumes two user-facing inputs: the student's instrument (determines whether they need a specialty room) and an optional one-time "competition priority" flag a teacher can set for a specific booking. Its output is which room(s) are offered/assigned for a given lesson slot. The teacher encounters this rule every time they try to book a room: available rooms are filtered and ordered by this priority rule, and if a competition override is invoked, the system surfaces the conflict and re-assigns the room.

## Access Control

Two roles:

- **Administrator** — creates and edits student and teacher accounts, assigns students to teachers, and can view/edit all reservations school-wide.
- **Teacher** — can only reserve rooms for students explicitly assigned to them by an administrator; cannot manage other teachers' students or edit reservations outside their own.

Login is email + password. There is no public self-signup — administrators create all teacher accounts. Unauthenticated visitors can view the school's public offering but are blocked from any reservation action and redirected to log in.

## Non-Goals

- No desktop application — web-only for MVP.
- No mobile application — web-only for MVP.
- No payments/billing for lessons — the school is public and does not charge per lesson, so no billing logic is needed.

## Open Questions

(none — the class/grade range was confirmed as 1–8, with 20-minute lessons for grades 1–4 and 40-minute lessons for grades 5–8.)
