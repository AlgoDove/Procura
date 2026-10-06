// ─── Approval Workflow Domain Types ──────────────────────────────────────

export type WorkflowState =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_VENDOR_EVALUATION'
  | 'AI_RECOMMENDATION_GENERATED'
  | 'WAITING_MANAGER_APPROVAL'
  | 'APPROVED'
  | 'REJECTED'
  | 'REVISION_REQUESTED'
  | 'COMPLETED';

export type ApprovalDecisionType = 'APPROVED' | 'REJECTED' | 'REVISION_REQUESTED';

export interface ApprovalDecisionRequestDto {
  comments?: string;
}

export interface TransitionWorkflowRequestDto {
  targetStatus: WorkflowState;
}

export interface ApprovalDecisionResponseDto {
  id: string;
  managerId: string;
  managerName: string;
  decision: string;
  comments?: string;
  createdAt: string;
}

export interface NotificationResponseDto {
  id: string;
  recipientUserId: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

export interface CriterionScoreResponseDto {
  id: string;
  criterion: string;
  rawScore: number;
  weight: number;
  weightedScore: number;
  scoreExplanation?: string;
}

export interface VendorEvaluationResponseDto {
  id: string;
  procurementRequestId: string;
  vendorId: string;
  rank: number;
  overallScore: number;
  reasoning: string;
  riskFlags: string[];
  generatedByAgent: boolean;
  createdAt: string;
  updatedAt: string;
  criterionScores: CriterionScoreResponseDto[];
}

export interface ProcurementEvaluationSummaryDto {
  procurementRequestId: string;
  totalCandidatesEvaluated: number;
  topRecommendedVendorId?: string;
  topScore?: number;
  recommendationSummary: string;
  evaluatedAt: string;
  rankedEvaluations: VendorEvaluationResponseDto[];
}

export interface ApprovalWorkflowResponseDto {
  id: string;
  procurementRequestId: string;
  requestNumber: string;
  requestTitle: string;
  estimatedTotal: number;
  requesterId: string;
  requesterName: string;
  currentStatus: WorkflowState;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  decisions: ApprovalDecisionResponseDto[];
  notifications: NotificationResponseDto[];
  vendorRecommendationSummary?: ProcurementEvaluationSummaryDto;
}

export interface AIAgentExecutionResponseDto {
  id: string;
  agentName: string;
  executionOrder: number;
  executionStatus: string;
  inputSummary?: string;
  outputSummary?: string;
  validationResult?: string;
  toolExecutionMetadata?: string;
  startedAt: string;
  completedAt?: string;
}

export interface WorkflowAuditTrailDto {
  workflowId: string;
  procurementRequestId: string;
  requestNumber: string;
  requestTitle: string;
  estimatedTotal: number;
  requesterId: string;
  requesterName: string;
  currentStatus: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  decisions: ApprovalDecisionResponseDto[];
  agentExecutions: AIAgentExecutionResponseDto[];
  notifications: NotificationResponseDto[];
}

export interface DecisionSupportOutput {
  procurementRequestId: string;
  workflowId: string;
  requestNumber: string;
  requestTitle: string;
  recommendedAction: string;
  topVendorId?: string;
  topVendorName?: string;
  recommendedAmount: number;
  budgetCap: number;
  isWithinBudget: boolean;
  isComplianceVerified: boolean;
  confidenceScore: number;
  executiveBrief: string;
  riskFactors: string[];
  keyTradeoffs: string[];
  conditionsOrStipulations: string[];
  generatedByAgent: boolean;
  generatedAt: string;
}
