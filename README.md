[README.md](https://github.com/user-attachments/files/31264256/README.md)
# Procura

University group project — procurement request management system.

**Status:** Actively under development (Phase 1). Not production-ready.

This README documents what is currently implemented, how to set up your local environment, how to run the backend, the current repository structure, and how the team collaborates through Git.

---

## Table of Contents

- [Current Implementation Status](#current-implementation-status)
- [Tech Stack (Backend)](#tech-stack-backend)
- [Repository Structure](#repository-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Clone the Repository](#clone-the-repository)
  - [Local Secrets Configuration (Required)](#local-secrets-configuration-required)
  - [Running the Backend](#running-the-backend)
  - [Database Migrations](#database-migrations)
- [Authentication](#authentication)
- [Procurement Request Module](#procurement-request-module)
- [Procurement Request Lifecycle](#procurement-request-lifecycle)
- [Business Rules](#business-rules)
- [Authorization / Roles](#authorization--roles)
- [API Testing (Swagger)](#api-testing-swagger)
- [Verification Performed So Far](#verification-performed-so-far)
- [Git Collaboration Workflow](#git-collaboration-workflow)

---

## Current Implementation Status

Phase 1 currently contains the implemented **Procurement Request backend**, built as an ASP.NET Core Web API following a **modular monolith** structure.

Implemented so far:

- User registration and login (JWT authentication)
- Role-based authorization
- Procurement Request module (create, retrieve, update, delete, submit, change status)
- Procurement Request lifecycle with enforced state transitions
- PostgreSQL persistence via EF Core, with migrations already applied
- Swagger/OpenAPI documentation with JWT support

Directories such as `ai-service/`, `database/`, `frontend-mobile/`, `frontend-web/`, and `scripts/` currently exist as placeholders in the repository structure. **They are not yet implemented.** Do not assume functionality exists in these areas.

---

## Tech Stack (Backend)

- C# 12
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8.0.8
- PostgreSQL
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.8
- JWT Bearer Authentication
- BCrypt.Net-Next 4.2.0
- Swashbuckle.AspNetCore 6.5.0 (Swagger/OpenAPI)

---

## Repository Structure

```
Procura/
├── .gitignore
├── LICENSE
├── README.md
│
├── ai-service/                  # Not yet implemented
│
├── backend/
│   ├── Procura.sln
│   └── Procura.API/
│       ├── appsettings.Development.json
│       ├── appsettings.json
│       ├── Procura.API.csproj
│       ├── Procura.API.http
│       ├── Program.cs
│       │
│       ├── Migrations/          # EF Core migrations
│       │
│       ├── Modules/
│       │   └── ProcurementRequest/
│       │       ├── Controllers/
│       │       ├── DTOs/
│       │       ├── Entities/
│       │       ├── Enums/
│       │       ├── Repositories/
│       │       └── Services/
│       │
│       ├── Properties/
│       │   └── launchSettings.json
│       │
│       └── Shared/
│           ├── Authentication/
│           ├── Authorization/
│           ├── Data/
│           ├── Entities/
│           └── Enums/
│
├── database/                    # Not yet implemented
├── docs/
│   ├── adr/
│   ├── api-design/
│   ├── architecture/
│   ├── diagrams/
│   └── meeting-notes/
│
├── frontend-mobile/              # Not yet implemented
├── frontend-web/                 # Not yet implemented
└── scripts/                      # Not yet implemented
```

Only `backend/` currently contains implemented code. Everything else listed above as "not yet implemented" is an empty scaffold directory reserved for future work.

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- PostgreSQL (running locally or accessible instance)
- A tool for managing .NET User Secrets (built into the .NET SDK via `dotnet user-secrets`)

### Clone the Repository

```bash
git clone <repository-url>
cd Procura/backend/Procura.API
```

### Local Secrets Configuration (Required)

**Sensitive values must never be committed to the repository.** `appsettings.json` intentionally contains only non-sensitive configuration:

- PostgreSQL host, port, database name, and username (no password)
- JWT issuer
- JWT audience
- JWT expiry

The **database password** and **JWT signing key** are not stored in the repository at all. They are managed locally per-developer using [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

**Each team member must configure their own local secrets before running the backend.**

From `backend/Procura.API/`, initialize and set your secrets:

```bash
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=procura;Username=your_username;Password=your_password"

dotnet user-secrets set "Jwt:Key" "your-local-jwt-signing-key"
```

Notes:

- `ConnectionStrings:DefaultConnection` and `Jwt:Key` are read from User Secrets in the Development environment and are **not** present in `appsettings.json` or `appsettings.Development.json`.
- Do not paste real secrets into any file that gets committed to Git.
- Each developer will have their own local PostgreSQL credentials and can use their own JWT key value for local development.

### Running the Backend

From `backend/Procura.API/`:

```bash
dotnet build
dotnet run
```

The API will start based on the profile configuration in `Properties/launchSettings.json`.

### Database Migrations

EF Core migrations already exist under `backend/Procura.API/Migrations/`. To apply them to your local PostgreSQL database:

```bash
dotnet ef database update
```

Ensure `ConnectionStrings:DefaultConnection` is correctly set via User Secrets before running this command.

---

## Authentication

Implemented endpoints:

```
POST /api/Auth/register
POST /api/Auth/login
```

- Authentication uses JWT Bearer tokens.
- Passwords are hashed using BCrypt.
- JWT claims include:
  - `sub`
  - `email`
  - `role`

---

## Procurement Request Module

Location: `backend/Procura.API/Modules/ProcurementRequest/`

Structured into:

- `Controllers/`
- `DTOs/`
- `Entities/`
- `Enums/`
- `Repositories/`
- `Services/`

Implemented functionality:

- Create procurement request
- Retrieve procurement requests
- Retrieve a procurement request by ID
- Update a procurement request
- Delete a procurement request
- Submit a procurement request
- Change procurement request status

### Entities

Main entities managed via EF Core:

- `User`
- `ProcurementRequest`
- `ProcurementRequestItem`

A `ProcurementRequest` can contain multiple `ProcurementRequestItem` records. `ProcurementRequestItem` has a foreign key relationship to `ProcurementRequest`.

The database is currently configured for local development only.

---

## Procurement Request Lifecycle

Primary flow:

```
DRAFT → SUBMITTED → UNDER_EVALUATION → PENDING_APPROVAL → APPROVED → COMPLETED
```

Alternative transitions:

```
PENDING_APPROVAL → REJECTED
PENDING_APPROVAL → REVISION_REQUESTED
REVISION_REQUESTED → DRAFT
```

The service layer enforces valid state transitions and role permissions for each transition. Invalid transitions are rejected at the service layer.

---

## Business Rules

- A procurement request must contain at least one item.
- `estimatedTotal` is calculated automatically as `Quantity × EstimatedUnitPrice`.
- Request numbers are generated using the format `PR-{Year}-{Count:D5}` (e.g. `PR-2026-00002`).

---

## Authorization / Roles

JWT-based authentication combined with role-based authorization is implemented.

Roles currently defined:

- `EMPLOYEE`
- `PROCUREMENT_OFFICER`
- `MANAGER`
- `ADMIN`

The Procurement Request module enforces role-based authorization alongside lifecycle-dependent status transitions (i.e. which roles can perform which status changes is validated in the service layer).

---

## API Testing (Swagger)

Swagger/OpenAPI is configured and available during development. JWT Bearer authentication is configured in Swagger, allowing authenticated endpoints to be tested directly from the Swagger UI.

Once the API is running, use the Swagger UI to:

1. Register or log in via `/api/Auth/register` or `/api/Auth/login`.
2. Copy the returned JWT.
3. Authorize in Swagger using the JWT (Bearer token).
4. Call Procurement Request endpoints.

A `Procura.API.http` file is also available in `backend/Procura.API/` for manual HTTP requests outside Swagger.

---

## Verification Performed So Far

The backend has been tested locally. Verified functionality includes:

- Registration
- Login
- JWT generation
- JWT authentication
- Role-based authorization
- PostgreSQL persistence
- Procurement request creation
- Procurement request retrieval
- Procurement request update
- Procurement request submission
- Procurement request deletion
- Lifecycle validation
- Swagger/OpenAPI

**Known fix applied:** A PUT update issue related to EF Core entity tracking of newly created `ProcurementRequestItem` entities during update operations was identified, fixed, and tested successfully.

The project currently builds successfully with `dotnet build` and runs with `dotnet run`.

---

## Git Collaboration Workflow

- Do not commit secrets (database passwords, JWT keys) to the repository. Use .NET User Secrets locally as described above.
- `appsettings.json` and `appsettings.Development.json` should only ever contain non-sensitive configuration.
- Check `.gitignore` before committing to confirm local/sensitive files are excluded.
- Coordinate module-level changes (especially inside `Modules/ProcurementRequest/`) with the team to avoid conflicting work on the same entities/services.
- Documentation, architecture decisions, and meeting notes belong under `docs/` (`adr/`, `api-design/`, `architecture/`, `diagrams/`, `meeting-notes/`) — keep these updated as the project evolves.

---

For architectural decisions, API design notes, and meeting notes, see the `docs/` directory.
