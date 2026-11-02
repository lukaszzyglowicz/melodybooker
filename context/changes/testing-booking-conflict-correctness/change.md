---
change_id: testing-booking-conflict-correctness
title: Harden double-booking hard-block tests at boundaries and DB state
status: implementing
created: 2026-11-02
updated: 2026-11-02
archived_at: null
---

## Notes

Open a change folder for rollout Phase 1 of context/foundation/test-plan.md: "Booking-conflict correctness".
Risks covered: #1 (a room/time slot already booked gets booked again anyway, including the end=start boundary case).
Test types planned: unit + integration.
Risk response intent: prove an overlapping room/time booking — including the end=start boundary — is always rejected, and the database never ends up holding two overlapping confirmed reservations for the same room; verify DB state, not just the HTTP response.
