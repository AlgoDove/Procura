# ADR-004: API and Environment Configuration

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

Both frontends must communicate with the ASP.NET Core backend. The backend URL changes between local development and production deployment. Hardcoding `http://localhost:5071` in application logic is forbidden.

## Decision

**React (Vite):** Use `VITE_API_BASE_URL` environment variable.
- Local: `.env.local` (gitignored) sets `VITE_API_BASE_URL=http://localhost:5071`
- `.env.example` is committed to source control as a template
- Production: inject `VITE_API_BASE_URL=https://api.procura.example.com` at build time

**Flutter:** Use `--dart-define=API_URL=...` at build/run time.
- Local dev: `flutter run --dart-define=API_URL=http://10.0.2.2:5071` (Android emulator) or appropriate LAN IP
- Production: `flutter build apk --dart-define=API_URL=https://api.procura.example.com`
- A compile-time constant `const String apiUrl = String.fromEnvironment('API_URL', defaultValue: 'http://10.0.2.2:5071');`

## Rationale

- Vite's `import.meta.env` is the standard approach for Vite projects; values are inlined at build time.
- `--dart-define` is Flutter's official mechanism for compile-time constants; does not require runtime file parsing.
- Both approaches prevent secrets and URLs from being hardcoded in source control.
- Production CORS must also be configured: `Cors__AllowedOrigins__0=https://deployed-frontend-url` on the backend.

## Consequences

- CI must set `VITE_API_BASE_URL` when running `npm run build` for the React app.
- Flutter builds in CI must pass `--dart-define=API_URL=...`.
- Developers must create `.env.local` locally (instructions in README).
