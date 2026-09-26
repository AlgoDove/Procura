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
} from '../types/api';

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

