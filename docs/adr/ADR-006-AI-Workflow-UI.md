# ADR-006: AI Workflow UI Design

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

The backend AI orchestration system (`CentralOrchestrator`) runs workflows asynchronously. Workflows can:
1. Complete immediately (synchronous response)
2. Enter `IN_PROGRESS` state (background processing)
3. Require user clarification (`NEEDS_USER_INPUT`) before continuing
4. Reach terminal states: `COMPLETED`, `FAILED`, `APPROVED`, `REJECTED`, `WAITING_FOR_HUMAN_APPROVAL`

The key backend continuation semantics:
- `NEEDS_USER_INPUT`: call `POST /api/procurement-requests/ai/process` **again** with the same `workflowId` and a new `objective` containing the user's clarification answer.
- There is no separate "continue workflow" endpoint.

Options considered for IN_PROGRESS polling:
- WebSockets / SignalR
- Server-Sent Events
- Polling via `GET /api/procurement-requests/ai/workflows/{id}`

## Decision

Use **polling** (`GET /api/procurement-requests/ai/workflows/{workflowId}`) at 3-second intervals while the workflow is `IN_PROGRESS`. Stop polling on any terminal status or `NEEDS_USER_INPUT`.

For `NEEDS_USER_INPUT`: display `clarificationPrompt` to the user, collect their text input, and call `POST /api/procurement-requests/ai/process` with the existing `workflowId` and the answer as `objective`.

**Explicit submission:** The AI workflow creates a DRAFT procurement request. The employee must navigate to the request and explicitly click "Submit Request". The frontend must **not** auto-submit the request when the workflow completes.

## Rationale

- WebSockets/SignalR are not implemented in the backend and are explicitly excluded by the project constraints.
- 3-second polling is adequate for this project's use case and is simple to implement and explain.
- The actual backend continuation semantics (same endpoint, same workflowId, new objective) are implemented as-is — no invented endpoints.
- Separation of AI workflow state from request lifecycle state is critical to avoid confusing `COMPLETED` (workflow) with `SUBMITTED` (request).

## Consequences

- Polling stops automatically on terminal states; no memory leaks from runaway intervals.
- Users see a live-updating plan and audit trail during processing.
- The explicit submission requirement prevents the AI from completing the user's approval workflow without human review.
