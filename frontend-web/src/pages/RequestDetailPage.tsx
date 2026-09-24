import { useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import {
  getProcurementRequest,
  submitProcurementRequest,
  updateProcurementRequestStatus,
  deleteProcurementRequest,
} from '../api/endpoints';
import type { RequestStatus, SystemRole } from '../types/api';
import ItemsEditor from '../components/ItemsEditor';
import styles from './RequestDetailPage.module.css';

const STATUS_COLORS: Record<RequestStatus, string> = {
  DRAFT: '#888',
  SUBMITTED: '#2980b9',
  UNDER_EVALUATION: '#8e44ad',
  PENDING_APPROVAL: '#e67e22',
  APPROVED: '#27ae60',
  REJECTED: '#c0392b',
  REVISION_REQUESTED: '#d35400',
  COMPLETED: '#1abc9c',
};

// Allowed status transitions by role and current status
function getAllowedTransitions(
  status: RequestStatus,
  role: SystemRole
): { label: string; newStatus: RequestStatus }[] {
  const transitions: { label: string; newStatus: RequestStatus }[] = [];

  if ((role === 'PROCUREMENT_OFFICER' || role === 'ADMIN') && status === 'SUBMITTED') {
    transitions.push({ label: 'Move to Under Evaluation', newStatus: 'UNDER_EVALUATION' });
  }
  if ((role === 'PROCUREMENT_OFFICER' || role === 'ADMIN') && status === 'UNDER_EVALUATION') {
    transitions.push({ label: 'Move to Pending Approval', newStatus: 'PENDING_APPROVAL' });
  }
  if (role === 'ADMIN' && status === 'PENDING_APPROVAL') {
    transitions.push({ label: 'Approve', newStatus: 'APPROVED' });
    transitions.push({ label: 'Reject', newStatus: 'REJECTED' });
    transitions.push({ label: 'Request Revision', newStatus: 'REVISION_REQUESTED' });
  }
  if ((role === 'PROCUREMENT_OFFICER' || role === 'ADMIN') && status === 'APPROVED') {
    transitions.push({ label: 'Mark Completed', newStatus: 'COMPLETED' });
  }
  if (role === 'EMPLOYEE' && status === 'REVISION_REQUESTED') {
    transitions.push({ label: 'Return to Draft', newStatus: 'DRAFT' });
  }

  return transitions;
}

export default function RequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { data: request, isLoading, error } = useQuery({
    queryKey: ['procurement-request', id],
    queryFn: () => getProcurementRequest(id!),
    enabled: !!id,
  });

  const submitMutation = useMutation({
    mutationFn: () => submitProcurementRequest(id!),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['procurement-request', id] });
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Failed to submit request.');
    },
  });

  const statusMutation = useMutation({
    mutationFn: (newStatus: RequestStatus) => updateProcurementRequestStatus(id!, newStatus),
    onSuccess: () => {
      setActionError(null);
      queryClient.invalidateQueries({ queryKey: ['procurement-request', id] });
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Status update failed.');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteProcurementRequest(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
      navigate('/requests');
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Delete failed.');
      setConfirmDelete(false);
    },
  });

  if (isLoading) return <p className={styles.state}>Loading…</p>;
  if (error || !request) return <p className={styles.errorState}>Request not found or access denied.</p>;

  const isOwner = user?.userId === request.requesterId;
  const isEmployee = user?.role === 'EMPLOYEE';
  const isDraft = request.status === 'DRAFT';
  const canEdit = isEmployee && isOwner && isDraft;
  const canSubmit = isEmployee && isOwner && isDraft;
  const canDelete =
    (isEmployee && isOwner && isDraft) || user?.role === 'ADMIN';
  const transitions = user ? getAllowedTransitions(request.status, user.role) : [];

  return (
    <div className={styles.container}>
      {/* Header */}
      <div className={styles.header}>
        <div>
          <Link to="/requests" className={styles.back}>← All Requests</Link>
          <h1 className={styles.heading}>{request.title}</h1>
          <span className={styles.number}>{request.requestNumber}</span>
        </div>
        <span
          className={styles.statusBadge}
          style={{ background: STATUS_COLORS[request.status] ?? '#888' }}
        >
          {request.status}
        </span>
      </div>

      {actionError && <div className={styles.errorBanner}>{actionError}</div>}

      {/* Action bar */}
      <div className={styles.actions}>
        {canEdit && (
          <Link to={`/requests/${id}/edit`} className={styles.btnSecondary}>
            Edit
          </Link>
        )}
        {canSubmit && (
          <button
            className={styles.btnPrimary}
            onClick={() => submitMutation.mutate()}
            disabled={submitMutation.isPending}
          >
            {submitMutation.isPending ? 'Submitting…' : 'Submit Request'}
          </button>
        )}
        {transitions.map((t) => (
          <button
            key={t.newStatus}
            className={t.newStatus === 'REJECTED' ? styles.btnDanger : styles.btnSecondary}
            onClick={() => statusMutation.mutate(t.newStatus)}
            disabled={statusMutation.isPending}
          >
            {statusMutation.isPending ? '…' : t.label}
          </button>
        ))}
        {canDelete && !confirmDelete && (
          <button className={styles.btnDanger} onClick={() => setConfirmDelete(true)}>
            Delete
          </button>
        )}
        {confirmDelete && (
          <span className={styles.confirmDelete}>
            Are you sure?{' '}
            <button
              className={styles.btnDanger}
              onClick={() => deleteMutation.mutate()}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending ? '…' : 'Yes, delete'}
            </button>
            {' '}
            <button className={styles.btnSecondary} onClick={() => setConfirmDelete(false)}>
              Cancel
            </button>
          </span>
        )}
        {/* AI Workflow button — only for EMPLOYEE on own DRAFT */}
        {isEmployee && isOwner && isDraft && (
          <Link to={`/requests/${id}/ai`} className={styles.btnAi}>
            🤖 AI Assistant
          </Link>
        )}
      </div>

      {/* Details */}
      <div className={styles.section}>
        <h2>Details</h2>
        <dl className={styles.dl}>
          <dt>Priority</dt><dd>{request.priority}</dd>
          <dt>Required by</dt><dd>{new Date(request.requiredByDate).toLocaleDateString()}</dd>
          <dt>Created</dt><dd>{new Date(request.createdAt).toLocaleString()}</dd>
          <dt>Updated</dt><dd>{new Date(request.updatedAt).toLocaleString()}</dd>
          <dt>Estimated total</dt><dd>${request.estimatedTotal.toFixed(2)}</dd>
        </dl>
        <div className={styles.longText}>
          <strong>Description</strong>
          <p>{request.description}</p>
        </div>
        <div className={styles.longText}>
          <strong>Justification</strong>
          <p>{request.justification}</p>
        </div>
      </div>

      {/* Items (read-only) */}
      <div className={styles.section}>
        <h2>Items</h2>
        <ItemsEditor
          items={request.items.map(({ itemName, description, quantity, unit, estimatedUnitPrice }) => ({
            itemName, description, quantity, unit, estimatedUnitPrice,
          }))}
          onChange={() => {}}
          disabled
        />
      </div>
    </div>
  );
}
