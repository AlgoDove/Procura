# ADR-007: Native Mobile Capability Selection

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

The SE3090 rubric requires at least one native mobile capability in the Flutter application. "Native capability" means functionality that uses device hardware or OS-provided APIs beyond basic UI rendering.

Candidates evaluated:
1. **Date picker** for `RequiredByDate` field — uses OS native date picker dialog
2. Camera — for receipt/document upload
3. GPS/Location — for delivery address
4. Push notifications — for status updates
5. QR code scanner — for vendor lookup

## Decision

Use the **native OS date picker dialog** (`showDatePicker` from Flutter's `material` library) for the `RequiredByDate` field in the Create/Edit Procurement Request screen.

Minimum selectable date is **tomorrow** (enforced both in the picker and by backend validation: `RequiredByDate.Value.Date < DateTime.UtcNow.Date` returns 400).

No other native capabilities are implemented.

## Rationale

- The date picker is the **only native capability that is directly required by an existing backend field** with a concrete validation constraint.
- Camera, GPS, notifications, and QR scanning would require inventing features not backed by any existing backend API.
- Adding native capabilities without backend integration creates a dishonest demonstration of full-stack integration.
- The project scope instruction says: "Do not add unnecessary native mobile capabilities."

## Consequences

- `showDatePicker` is called with `firstDate: DateTime.now().add(Duration(days: 1))` to enforce the future-date constraint before the API call.
- The selected date is sent to the backend as an ISO 8601 UTC string.
- On Android, this uses the Material date picker dialog. On iOS, it uses the Cupertino date picker.
