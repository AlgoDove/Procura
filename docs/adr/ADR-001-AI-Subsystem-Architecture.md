# ADR-001: Agentic AI Subsystem Architecture & Integration

## Status
Accepted

## Context
Procura is a Vendor & Procurement Management System with an ASP.NET Core (.NET 8) backend, PostgreSQL database, and Entity Framework Core. The final system will incorporate four specialized AI agents:
1. Procurement Request Agent (Student 1)
2. Vendor Management Agent (Student 2)
3. Vendor Evaluation & Recommendation Agent (Student 3)
4. Approval Workflow Agent (Student 4)

Given a university timeline (10–15 days remaining) and strict requirements for verifiable security, auditability, and clear separation of concerns, we required an AI architecture that is functionally complete, testable, and demonstrable without introducing excessive microservice or distributed-system overhead.

## Decisions

### 1. In-Process C# Modular Architecture
- **Decision**: Implement the Central Orchestrator and Procurement Request Agent directly within `backend/Procura.API/AI/` rather than a detached Python microservice.
- **Rationale**: Eliminates multi-runtime configuration friction, inter-process authentication overhead, and latency. The agent can invoke the existing, tested `IProcurementRequestService` via dependency injection while maintaining atomic transactions.

### 2. ASP.NET Core as Sole Public Trust Boundary
- **Decision**: All AI workflow requests enter through `POST /api/procurement-requests/ai/process`, protected by JWT authentication.
- **Rationale**: The client cannot call Gemini directly, cannot call an unauthenticated AI service, and cannot spoof `RequesterId` or `Role`. The backend extracts verified claims from the JWT and injects trusted identity into the workflow context.

### 3. Centralized Hub-and-Spoke Orchestrator
- **Decision**: Implement a single `CentralOrchestrator` managing workflow state, multi-step structured plans, stage transitions, and audit trajectories.
- **Rationale**: Agents are specialized workers that return structured results to the orchestrator. Agents never call each other directly. Step 1 (`PROCUREMENT_REQUEST`) is active; Steps 2–4 exist as planned future stages.

### 4. Specialized Procurement Request Agent
- **Decision**: The agent has a single, narrow responsibility: extract requirements from natural language, validate them deterministically, and invoke permitted draft tools.
- **Rationale**: Avoids generic chatbot anti-patterns. Ensures predictable, machine-readable output (`ProcurementAgentOutput`).

### 5. Strict Tool Allow-Listing & Excluded Operations
- **Decision**: Only four tools are permitted: `ValidateDraftData`, `CreateDraftRequest`, `GetProcurementRequest`, `UpdateDraftRequest`.
- **Rationale**: Operations like `SubmitRequest`, `DeleteRequest`, and `UpdateStatus` are strictly excluded from agent autonomy. Unknown tool requests are deterministically blocked and logged with `IsSecurityViolation = true`.

### 6. Separation of LLM Extraction from Deterministic Business Validation
- **Decision**: The LLM parses natural language into JSON; deterministic C# code validates constraints (e.g. quantities $\ge 1$, prices $\ge 0$, future dates) *before* any tool execution.
- **Rationale**: The LLM is an interpretation tool, never the final authority on business rules or security.

### 7. Human-in-the-Loop & Approval Checkpoint Outside Autonomous Agent Authority
- **Decision**: If information is missing, the agent returns `NEEDS_USER_INPUT` without mutating the database. Consequential transitions (`SUBMITTED`, `APPROVED`) require human actions and are not executed autonomously by Agent 1.
- **Rationale**: Prevents accidental or hallucinated commitments of organizational funds.

### 8. Configuration-Driven Gemini Model
- **Decision**: The Gemini model name is loaded strictly from `Gemini:Model` in configuration with no code fallback. The API key is stored in .NET User Secrets (`Gemini:ApiKey`).
- **Rationale**: Prevents the codebase from depending on transient model versions and protects credentials from Git exposure.

### 9. Durable PostgreSQL Workflow Persistence
- **Decision**: Persist workflow instances (`WorkflowInstances` table) with structured plans, context, and audit trajectories in PostgreSQL via EF Core.
- **Rationale**: Guarantees durability across server restarts and provides auditable execution records.

## Consequences & Extension Points for Agents 2–4
- **Adding Future Agents**: Students 2, 3, and 4 will implement `IAgent` (`VendorManagementAgent`, `VendorEvaluationAgent`, `ApprovalWorkflowAgent`), register their own allow-listed tools, and plug into the `CentralOrchestrator`'s execution loop without refactoring Agent 1.
- **Shared Contracts**: All agents share `WorkflowContext`, `WorkflowPlan`, and `WorkflowAuditEntry`.
