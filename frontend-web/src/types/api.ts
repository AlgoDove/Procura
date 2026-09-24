// ─── Auth ──────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
}

export interface JwtPayload {
  sub: string;   // UserId (Guid)
  email: string;
  role: SystemRole;
  exp: number;
}

// ─── Enums ─────────────────────────────────────────────────────────────────

export type SystemRole = 'EMPLOYEE' | 'PROCUREMENT_OFFICER' | 'MANAGER' | 'ADMIN';

export type Priority = 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';

export type RequestStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_EVALUATION'
  | 'PENDING_APPROVAL'
  | 'APPROVED'
  | 'REJECTED'
  | 'REVISION_REQUESTED'
  | 'COMPLETED';

export type VendorStatus = 'ACTIVE' | 'INACTIVE';

// ─── Procurement Request DTOs ──────────────────────────────────────────────

export interface ProcurementRequestItemInput {
  itemName: string;
  description: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice: number;
}

export interface CreateProcurementRequestDto {
  title: string;
  description: string;
  justification: string;
  priority: Priority;
  requiredByDate: string; // ISO 8601 UTC
  items: ProcurementRequestItemInput[];
}

export type UpdateProcurementRequestDto = CreateProcurementRequestDto;

export interface ProcurementRequestItemResponse {
  id: string;
  itemName: string;
  description: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice: number;
}

export interface ProcurementRequestResponse {
  id: string;
  requestNumber: string;
  requesterId: string;
  title: string;
  description: string;
  justification: string;
  priority: Priority;
  requiredByDate: string;
  estimatedTotal: number;
  status: RequestStatus;
  createdAt: string;
  updatedAt: string;
  items: ProcurementRequestItemResponse[];
}

// ─── AI / Workflow DTOs ────────────────────────────────────────────────────

export interface ProcessAiRequest {
  objective: string;
  existingRequestId?: string;
  workflowId?: string;
}

export type WorkflowStatus =
  | 'NOT_STARTED'
  | 'IN_PROGRESS'
  | 'NEEDS_USER_INPUT'
  | 'STAGE_COMPLETED'
  | 'WAITING_FOR_HUMAN_APPROVAL'
  | 'APPROVED'
  | 'REJECTED'
  | 'REVISION_REQUESTED'
  | 'COMPLETED'
  | 'FAILED';

export type WorkflowStage =
  | 'PROCUREMENT_REQUEST'
  | 'VENDOR_SELECTION'
  | 'VENDOR_EVALUATION'
  | 'APPROVAL_WORKFLOW'
  | 'COMPLETED';

export type StepStatus =
  | 'NOT_STARTED'
  | 'PENDING'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'FAILED'
  | 'SKIPPED';

export interface WorkflowStep {
  stepNumber: number;
  stage: WorkflowStage;
  agentName: string;
  objective: string;
  status: StepStatus;
  startedAt?: string;
  completedAt?: string;
  outcomeSummary?: string;
}

export interface WorkflowPlan {
  steps: WorkflowStep[];
}

export interface WorkflowAuditEntry {
  id: string;
  workflowId: string;
  timestamp: string;
  stage: WorkflowStage;
  actor: string;
  action: string;
  toolName?: string;
  status: string;
  details: string;
  isSecurityViolation: boolean;
}

export interface WorkflowProcessResponse {
  workflowId: string;
  status: WorkflowStatus;
  currentStage: WorkflowStage;
  procurementRequestId?: string;
  requestNumber?: string;
  estimatedTotal?: number;
  executionSummary?: string;
  clarificationPrompt?: string;
  errors: string[];
  plan: WorkflowPlan;
  auditTrail: WorkflowAuditEntry[];
  updatedAt: string;
}

// ─── Vendor DTOs ───────────────────────────────────────────────────────────

export interface VendorResponse {
  id: string;
  name: string;
  contactEmail: string;
  contactPhone?: string;
  address?: string;
  category: string;
  status: VendorStatus;
  rating?: number;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateVendorDto {
  name: string;
  contactEmail: string;
  contactPhone?: string;
  address?: string;
  category: string;
  notes?: string;
}

export type UpdateVendorDto = CreateVendorDto;
