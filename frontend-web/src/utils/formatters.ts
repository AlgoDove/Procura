import type { Priority, RequestStatus, SystemRole } from '../types/api';

const STATUS_MAP: Record<string, string> = {
  DRAFT: 'Draft',
  SUBMITTED: 'Submitted',
  UNDER_EVALUATION: 'Under Evaluation',
  PENDING_APPROVAL: 'Pending Approval',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
  REVISION_REQUESTED: 'Revision Requested',
  COMPLETED: 'Completed',
};

const PRIORITY_MAP: Record<string, string> = {
  LOW: 'Low',
  MEDIUM: 'Medium',
  HIGH: 'High',
  URGENT: 'Urgent',
};

const ROLE_MAP: Record<string, string> = {
  EMPLOYEE: 'Employee',
  PROCUREMENT_OFFICER: 'Procurement Officer',
  ADMIN: 'Admin',
  MANAGER: 'Manager',
};

export function formatStatus(status: RequestStatus | string): string {
  if (!status) return '';
  return STATUS_MAP[status] ?? status.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatPriority(priority: Priority | string): string {
  if (!priority) return '';
  return PRIORITY_MAP[priority] ?? priority;
}

export function formatRole(role: SystemRole | string): string {
  if (!role) return '';
  return ROLE_MAP[role] ?? role.replace(/_/g, ' ');
}

export function formatPrice(price: number | null | undefined): string {
  if (price == null || price <= 0) {
    return 'Pending Quote';
  }
  return `$${price.toFixed(2)}`;
}

export function formatTotal(total: number | null | undefined): string {
  if (total == null || total <= 0) {
    return 'TBD';
  }
  return `$${total.toFixed(2)}`;
}

export function formatDate(isoString: string | null | undefined): string {
  if (!isoString) return '—';
  try {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString();
  } catch {
    return '—';
  }
}

export function formatDateTime(isoString: string | null | undefined): string {
  if (!isoString) return '—';
  try {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleString();
  } catch {
    return '—';
  }
}
