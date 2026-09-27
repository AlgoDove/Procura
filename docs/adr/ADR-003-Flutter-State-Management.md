# ADR-003: Flutter State Management

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

The Flutter mobile application needs state management for:
1. Authentication (current user, JWT token, role)
2. Procurement request list and detail data
3. AI workflow state
4. UI state (form inputs, loading, error)

Options considered:
- Provider (official Flutter recommended for simple cases)
- Riverpod
- BLoC/Cubit
- GetX

## Decision

Use **Provider** for cross-widget shared state (auth, request list), plus `setState` and local widget state for per-screen UI state.

## Rationale

- Provider is the Flutter team's recommended solution for state management at this scale.
- The application has straightforward data flow: auth state shared globally, list/detail data loaded per screen.
- Riverpod and BLoC are more appropriate for large teams and complex reactive chains — overkill here.
- "Project must remain understandable to a university student": Provider is taught in official Flutter documentation and courses.

## Consequences

- `AuthProvider` is available at the root of the widget tree.
- Screens use `Consumer<AuthProvider>` or `context.watch<AuthProvider>()` to read auth state.
- HTTP requests are made directly in screen `initState` / user actions using Dio, not through a reactive layer.
