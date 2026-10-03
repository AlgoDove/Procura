import apiClient from './client';
import type {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  CreateProcurementRequestDto,
  UpdateProcurementRequestDto,
  ProcurementRequestResponse,
  ProcessAiRequest,
  WorkflowProcessResponse,
  VendorResponse,
  CreateVendorDto,
  UpdateVendorDto,
  UserSummaryResponse,
  UpdateUserRoleRequest,
  VendorQuoteResponseDto,
  CreateVendorQuoteDto,
  EvaluateVendorsRequestDto,
  VendorEvaluationResponseDto,
  ProcurementEvaluationSummaryDto,
} from '../types/api';
import type {
  WorkflowState,
  ApprovalWorkflowResponseDto,
  WorkflowAuditTrailDto,
  NotificationResponseDto,
} from '../types/approval';

// ─── Auth ──────────────────────────────────────────────────────────────────

export const login = (data: LoginRequest) =>
  apiClient.post<AuthResponse>('/api/Auth/login', data).then((r) => r.data);

export const register = (data: RegisterRequest) =>
  apiClient.post<{ message: string }>('/api/Auth/register', data).then((r) => r.data);

// ─── Procurement Requests ──────────────────────────────────────────────────

export const getProcurementRequests = () =>
  apiClient.get<ProcurementRequestResponse[]>('/api/procurement-requests').then((r) => r.data);

export const getProcurementRequest = (id: string) =>
  apiClient.get<ProcurementRequestResponse>(`/api/procurement-requests/${id}`).then((r) => r.data);

export const createProcurementRequest = (data: CreateProcurementRequestDto) =>
  apiClient.post<ProcurementRequestResponse>('/api/procurement-requests', data).then((r) => r.data);

export const updateProcurementRequest = (id: string, data: UpdateProcurementRequestDto) =>
  apiClient.put<ProcurementRequestResponse>(`/api/procurement-requests/${id}`, data).then((r) => r.data);

export const deleteProcurementRequest = (id: string) =>
  apiClient.delete(`/api/procurement-requests/${id}`).then((r) => r.data);

export const submitProcurementRequest = (id: string) =>
  apiClient.post<{ message: string }>(`/api/procurement-requests/${id}/submit`).then((r) => r.data);

export const updateProcurementRequestStatus = (id: string, newStatus: string) =>
  apiClient
    .post<{ message: string }>(`/api/procurement-requests/${id}/status?newStatus=${newStatus}`)
    .then((r) => r.data);

// ─── AI Workflow ───────────────────────────────────────────────────────────

export const processAiWorkflow = (data: ProcessAiRequest) =>
  apiClient.post<WorkflowProcessResponse>('/api/procurement-requests/ai/process', data).then((r) => r.data);

export const getAiWorkflow = (workflowId: string) =>
  apiClient.get<WorkflowProcessResponse>(`/api/procurement-requests/ai/workflows/${workflowId}`).then((r) => r.data);

export const getWorkflowForRequest = (procurementRequestId: string) =>
  apiClient.get<WorkflowProcessResponse>(`/api/procurement-requests/${procurementRequestId}/workflow`).then((r) => r.data);

// ─── Vendors ───────────────────────────────────────────────────────────────

export const getVendors = (params?: { status?: string; category?: string }) =>
  apiClient.get<VendorResponse[]>('/api/vendors', { params }).then((r) => r.data);

export const getVendor = (id: string) =>
  apiClient.get<VendorResponse>(`/api/vendors/${id}`).then((r) => r.data);

export const createVendor = (data: CreateVendorDto) =>
  apiClient.post<VendorResponse>('/api/vendors', data).then((r) => r.data);

export const updateVendor = (id: string, data: UpdateVendorDto) =>
  apiClient.put<VendorResponse>(`/api/vendors/${id}`, data).then((r) => r.data);

export const deactivateVendor = (id: string) =>
  apiClient.post<VendorResponse>(`/api/vendors/${id}/deactivate`).then((r) => r.data);

export const activateVendor = (id: string) =>
  apiClient.post<VendorResponse>(`/api/vendors/${id}/activate`).then((r) => r.data);

// ─── Users (Admin only) ──────────────────────────────────────────────────

export const getUsers = () =>
  apiClient.get<UserSummaryResponse[]>('/api/users').then((r) => r.data);

export const updateUserRole = (id: string, role: UpdateUserRoleRequest['role']) =>
  apiClient.patch<UserSummaryResponse>(`/api/users/${id}/role`, { role }).then((r) => r.data);

// ─── Approval Workflows ──────────────────────────────────────────────────

export const getApprovalWorkflows = (status?: WorkflowState) =>
  apiClient
    .get<ApprovalWorkflowResponseDto[]>('/api/approval-workflows', {
      params: status ? { status } : undefined,
    })
    .then((r) => r.data);

export const getPendingApprovals = () =>
  apiClient.get<ApprovalWorkflowResponseDto[]>('/api/approval-workflows/pending').then((r) => r.data);

export const getApprovalWorkflowById = (id: string) =>
  apiClient.get<ApprovalWorkflowResponseDto>(`/api/approval-workflows/${id}`).then((r) => r.data);

export const getApprovalWorkflowByRequestId = (procurementRequestId: string) =>
  apiClient
    .get<ApprovalWorkflowResponseDto>(`/api/approval-workflows/procurement-request/${procurementRequestId}`)
    .then((r) => r.data);

export const initializeApprovalWorkflow = (procurementRequestId: string) =>
  apiClient
    .post<ApprovalWorkflowResponseDto>('/api/approval-workflows/initialize', { procurementRequestId })
    .then((r) => r.data);

export const transitionApprovalWorkflow = (id: string, targetStatus: WorkflowState) =>
  apiClient
    .post<ApprovalWorkflowResponseDto>(`/api/approval-workflows/${id}/transition`, { targetStatus })
    .then((r) => r.data);

export const approveWorkflow = (id: string, comments?: string) =>
  apiClient
    .post<ApprovalWorkflowResponseDto>(`/api/approval-workflows/${id}/approve`, { comments })
    .then((r) => r.data);

export const rejectWorkflow = (id: string, comments: string) =>
  apiClient
    .post<ApprovalWorkflowResponseDto>(`/api/approval-workflows/${id}/reject`, { comments })
    .then((r) => r.data);

export const requestWorkflowRevision = (id: string, comments: string) =>
  apiClient
    .post<ApprovalWorkflowResponseDto>(`/api/approval-workflows/${id}/request-revision`, { comments })
    .then((r) => r.data);

export const getWorkflowAuditTrail = (id: string) =>
  apiClient.get<WorkflowAuditTrailDto>(`/api/approval-workflows/${id}/audit-trail`).then((r) => r.data);

export const getUserNotifications = (unreadOnly?: boolean) =>
  apiClient
    .get<NotificationResponseDto[]>('/api/approval-workflows/notifications', {
      params: unreadOnly !== undefined ? { unreadOnly } : undefined,
    })
    .then((r) => r.data);

export const getUnreadNotificationCount = () =>
  apiClient.get<{ unreadCount: number }>('/api/approval-workflows/notifications/unread-count').then((r) => r.data);

export const markNotificationAsRead = (id: string) =>
  apiClient.patch(`/api/approval-workflows/notifications/${id}/read`).then((r) => r.data);

export const markAllNotificationsAsRead = () =>
  apiClient.post('/api/approval-workflows/notifications/mark-all-read').then((r) => r.data);

// ─── Vendor Evaluation & Quotes ─────────────────────────────────────────────

export const getVendorQuotes = (procurementRequestId: string) =>
  apiClient
    .get<VendorQuoteResponseDto[]>(`/api/vendor-quotes/procurement-request/${procurementRequestId}`)
    .then((r) => r.data);

export const getSelectedVendorsForRequest = (procurementRequestId: string) =>
  apiClient
    .get<VendorResponse[]>(`/api/vendor-quotes/procurement-request/${procurementRequestId}/selected-vendors`)
    .then((r) => r.data);

export const submitVendorQuote = (data: CreateVendorQuoteDto) =>
  apiClient
    .post<VendorQuoteResponseDto>('/api/vendor-quotes', data)
    .then((r) => r.data);

export const getVendorQuoteById = (id: string) =>
  apiClient
    .get<VendorQuoteResponseDto>(`/api/vendor-quotes/${id}`)
    .then((r) => r.data);

export const evaluateVendors = (data: EvaluateVendorsRequestDto) =>
  apiClient
    .post<ProcurementEvaluationSummaryDto>('/api/vendor-evaluations/evaluate', data)
    .then((r) => r.data);

export const getVendorEvaluations = (procurementRequestId: string) =>
  apiClient
    .get<VendorEvaluationResponseDto[]>(`/api/vendor-evaluations/procurement-request/${procurementRequestId}`)
    .then((r) => r.data);

export const getRecommendationSummary = (procurementRequestId: string) =>
  apiClient
    .get<ProcurementEvaluationSummaryDto>(`/api/vendor-evaluations/procurement-request/${procurementRequestId}/recommendation`)
    .then((r) => r.data);
