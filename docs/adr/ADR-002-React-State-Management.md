# ADR-002: React State Management

**Date:** 2026-09-24  
**Status:** Accepted  
**Deciders:** Procura SE3090 team

## Context

The React web application needs state management for:
1. Authentication (current user, JWT token, role)
2. Server data (procurement requests, vendors, workflow state)
3. UI state (form inputs, filter values, modal visibility)

Options considered:
- TanStack Query + React Context (built-in React)
- Zustand + TanStack Query
- Redux Toolkit + TanStack Query

## Decision

Use **TanStack Query** (server state) + **React Context** (auth state only). No additional state management library.

UI state is kept in local component `useState`.

## Rationale

- TanStack Query handles caching, background refetch, and invalidation for server state, which is the dominant state type in a CRUD app.
- `AuthContext` is a small, well-defined piece of global state (token + decoded claims). React Context is the correct tool for this scope.
- Adding Zustand or Redux would add cognitive overhead for no concrete benefit at this project scale.
- The constraint "project must remain understandable to a university student" favors fewer abstractions.

## Consequences

- Server data is not in a global store; it lives in the React Query cache, keyed by query keys.
- Authentication state is available via `useAuth()` throughout the tree.
- If future requirements genuinely require cross-component derived state beyond what Context + Query provides, Zustand may be added then.
