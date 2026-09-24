# ADR-005: Authentication and Session Handling

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

The backend issues JWT tokens on login. There is no `/api/Auth/me` endpoint. JWT tokens expire after 120 minutes (configured in `appsettings`). There are no refresh tokens.

The JWT payload contains:
- `sub` — UserId (Guid string)
- `email` — user email
- `role` — SystemRole enum string (EMPLOYEE, PROCUREMENT_OFFICER, MANAGER, ADMIN)
- `exp` — expiry timestamp (Unix epoch)

The backend also sets `ClaimTypes.Role` for the ASP.NET Core `[Authorize(Roles=...)]` attribute — this is the same value as `role` and is not relevant to the frontend.

## Decision

**React:**
- Store JWT in `localStorage` under key `token`.
- Decode JWT payload client-side using `jwt-decode` to extract `sub`, `email`, `role`.
- Validate `exp` on decode — treat expired tokens as absent.
- React to Axios 401 responses by clearing `localStorage` and dispatching a `storage` event, which `AuthContext` listens to.
- No `/api/Auth/me` call; no refresh tokens.

**Flutter:**
- Store JWT in `flutter_secure_storage` (encrypted on-device storage).
- Decode JWT payload manually using `base64Url` decode of the middle segment.
- Validate `exp` on decode.
- React to 401 responses from Dio by clearing stored token and navigating to login.

## Rationale

- `localStorage` is appropriate for a university project SPA where the threat model is not production-grade.
- `flutter_secure_storage` is the Flutter standard for sensitive mobile storage (uses Keychain on iOS, Keystore on Android).
- Client-side JWT decode is necessary because no `/me` endpoint exists.
- Expiry validation prevents indefinitely cached stale tokens.

## Consequences

- After 120 minutes, the next API call returns 401, which clears the session and redirects to login.
- No silent refresh is possible without backend changes (out of scope).
- MANAGER role exists in the JWT `role` claim but must not be exposed as a frontend login path. The frontend does not offer role selection at registration.
