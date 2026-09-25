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
import type { RequestStatus } from '../types/api';
import {
  formatStatus,
  formatPriority,
  formatPrice,
  formatTotal,
  formatDate,
  formatDateTime,
} from '../utils/formatters';
import styles from './RequestDetailPage.module.css';

const STATUS_COLORS: Record<string, string> = {
  DRAFT: '#475569',
  SUBMITTED: '#0369a1',
  UNDER_EVALUATION: '#6d28d9',
  PENDING_APPROVAL: '#b45309',
  APPROVED: '#15803d',
  REJECTED: '#b91c1c',
  REVISION_REQUESTED: '#c2410c',
  COMPLETED: '#0f766e',
};

export default function RequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [actionError, setActionError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [confirmAction, setConfirmAction] = useState<RequestStatus | null>(null);

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
      setConfirmAction(null);
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
      setActionError(axiosErr.response?.data?.message ?? 'Failed to delete request.');
      setConfirmDelete(false);
    },
  });

  if (isLoading) return <div className={styles.state}>Loading request details…</div>;
  if (error || !request) {
    return (
      <div className={styles.container}>
        <div className={styles.errorState}>
          Request not found or access denied.
        </div>
        <Link to="/requests" className={styles.backLink}>← Back to Requests</Link>
      </div>
    );
  }

  const isOwner = user?.userId === request.requesterId;
  const isEmployee = user?.role === 'EMPLOYEE';
  const isOfficer = user?.role === 'PROCUREMENT_OFFICER';
  const isAdmin = user?.role === 'ADMIN';
  const isDraft = request.status === 'DRAFT';

  // Role-appropriate permissions — employee-owned draft management
  const canEdit = isEmployee && isOwner && isDraft;
  const canSubmit = isEmployee && isOwner && isDraft;
  const canUseAi = isEmployee && isOwner && isDraft;
  // Delete is strictly allowed ONLY when status is DRAFT and user is the employee owner
  const canDelete = isEmployee && isOwner && isDraft;

  // Status transitions
  const canBeginEvaluation = (isOfficer || isAdmin) && request.status === 'SUBMITTED';
  const canSubmitForApproval = isOfficer && request.status === 'UNDER_EVALUATION';
  const canApprove = isAdmin && request.status === 'PENDING_APPROVAL';
  const canReturnToDraft = isEmployee && isOwner && request.status === 'REVISION_REQUESTED';
  const canComplete = (isOfficer || isAdmin) && request.status === 'APPROVED';

  return (
    <div className={styles.container}>
      <Link to="/requests" className={styles.backLink}>
        ← Back to {isEmployee ? 'My Requests' : 'All Requests'}
      </Link>

      {/* Revision requested banner */}
      {request.status === 'REVISION_REQUESTED' && (
        <div className={styles.bannerRevision}>
          <strong>Revision Requested:</strong> An administrator has requested revisions on this request.
          {isOwner && ' Click "Return to Draft" below to unlock editing, update requirements, and resubmit.'}
        </div>
      )}

      {/* Draft notice banner */}
      {isDraft && (
        <div className={styles.bannerDraft}>
          <strong>Draft Request:</strong> This request is currently in <strong>Draft</strong> status.
          {isOwner ? ' Review the items and click "Submit Request" to send it to the procurement team.' : ''}
        </div>
      )}

      {actionError && <div className={styles.bannerError}>{actionError}</div>}

      {/* Header card with status and action bar */}
      <div className={styles.headerCard}>
        <div className={styles.headerTop}>
          <div>
            <div className={styles.requestMeta}>
              <span className={styles.requestNumber}>{request.requestNumber}</span>
              <span className={styles.createdDate}>
                Created {formatDateTime(request.createdAt)}
              </span>
            </div>
            <h1 className={styles.title}>{request.title}</h1>
          </div>
          <span
            className={styles.statusBadge}
            style={{ background: STATUS_COLORS[request.status] ?? '#64748b' }}
          >
            {formatStatus(request.status)}
          </span>
        </div>

        {/* Action bar — only valid role/status actions */}
        <div className={styles.actionBar}>
          {/* Employee Draft Actions */}
          {canSubmit && (
            <button
              type="button"
              className={styles.btnPrimary}
              onClick={() => submitMutation.mutate()}
              disabled={submitMutation.isPending}
            >
              {submitMutation.isPending ? 'Submitting…' : 'Submit Request'}
            </button>
          )}

          {canEdit && (
            <Link to={`/requests/${id}/edit`} className={styles.btnSecondary}>
              Edit Request
            </Link>
          )}

          {canUseAi && (
            <Link to={`/requests/${id}/ai`} className={styles.btnAi}>
              🤖 AI Assistant
            </Link>
          )}

          {/* Revision return to draft */}
          {canReturnToDraft && (
            <button
              type="button"
              className={styles.btnReturnToDraft}
              onClick={() => statusMutation.mutate('DRAFT')}
              disabled={statusMutation.isPending}
            >
              {statusMutation.isPending ? 'Updating…' : 'Return to Draft for Revision'}
            </button>
          )}

          {/* PO / Admin Progression Actions */}
          {canBeginEvaluation && (
            <button
              type="button"
              className={styles.btnPrimary}
              onClick={() => statusMutation.mutate('UNDER_EVALUATION')}
              disabled={statusMutation.isPending}
            >
              {statusMutation.isPending ? 'Updating…' : 'Move to Under Evaluation'}
            </button>
          )}

          {canSubmitForApproval && (
            <button
              type="button"
              className={styles.btnPrimary}
              onClick={() => statusMutation.mutate('PENDING_APPROVAL')}
              disabled={statusMutation.isPending}
            >
              {statusMutation.isPending ? 'Updating…' : 'Submit for Approval'}
            </button>
          )}

          {/* Admin Approval Actions */}
          {canApprove && !confirmAction && (
            <>
              <button
                type="button"
                className={styles.btnApprove}
                onClick={() => statusMutation.mutate('APPROVED')}
                disabled={statusMutation.isPending}
              >
                {statusMutation.isPending ? 'Approving…' : '✓ Approve Request'}
              </button>

              <button
                type="button"
                className={styles.btnRevision}
                onClick={() => setConfirmAction('REVISION_REQUESTED')}
                disabled={statusMutation.isPending}
              >
                Request Revision
              </button>

              <button
                type="button"
                className={styles.btnReject}
                onClick={() => setConfirmAction('REJECTED')}
                disabled={statusMutation.isPending}
              >
                Reject Request
              </button>
            </>
          )}

          {/* Confirmation for Reject or Request Revision */}
          {confirmAction && (
            <span className={styles.confirmBox}>
              Confirm {confirmAction === 'REJECTED' ? 'rejection' : 'revision request'}?
              <button
                type="button"
                className={confirmAction === 'REJECTED' ? styles.btnReject : styles.btnRevision}
                onClick={() => statusMutation.mutate(confirmAction)}
                disabled={statusMutation.isPending}
              >
                Yes, {confirmAction === 'REJECTED' ? 'Reject' : 'Request Revision'}
              </button>
              <button
                type="button"
                className={styles.btnSecondary}
                onClick={() => setConfirmAction(null)}
              >
                Cancel
              </button>
            </span>
          )}

          {canComplete && (
            <button
              type="button"
              className={styles.btnComplete}
              onClick={() => statusMutation.mutate('COMPLETED')}
              disabled={statusMutation.isPending}
            >
              {statusMutation.isPending ? 'Completing…' : 'Mark Completed'}
            </button>
          )}

          {/* Delete action (strictly DRAFT only) */}
          {canDelete && !confirmDelete && (
            <button
              type="button"
              className={styles.btnDanger}
              onClick={() => setConfirmDelete(true)}
            >
              Delete Draft
            </button>
          )}

          {confirmDelete && (
            <span className={styles.confirmBox}>
              Permanently delete this draft?
              <button
                type="button"
                className={styles.btnDanger}
                onClick={() => deleteMutation.mutate()}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Deleting…' : 'Yes, Delete'}
              </button>
              <button
                type="button"
                className={styles.btnSecondary}
                onClick={() => setConfirmDelete(false)}
              >
                Cancel
              </button>
            </span>
          )}
        </div>
      </div>

      {/* Summary section */}
      <div className={styles.section}>
        <h2 className={styles.sectionTitle}>Request Summary</h2>
        <div className={styles.grid}>
          <div className={styles.gridItem}>
            <label>Priority</label>
            <span>{formatPriority(request.priority)}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Required by Date</label>
            <span>{formatDate(request.requiredByDate)}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Estimated Total</label>
            <span>{formatTotal(request.estimatedTotal)}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Last Updated</label>
            <span>{formatDateTime(request.updatedAt)}</span>
          </div>
        </div>

        <div className={styles.textAreaBlock}>
          <label>Description</label>
          <p>{request.description}</p>
        </div>

        <div className={styles.textAreaBlock}>
          <label>Business Justification</label>
          <p>{request.justification}</p>
        </div>
      </div>

      {/* Items Section */}
      <div className={styles.section}>
        <h2 className={styles.sectionTitle}>
          Requested Items ({request.items.length})
        </h2>
        <table className={styles.itemsTable}>
          <thead>
            <tr>
              <th>Item Name</th>
              <th>Description / Specifications</th>
              <th>Qty</th>
              <th>Unit</th>
              <th>Estimated Unit Price</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {request.items.map((item) => {
              const hasPrice = item.estimatedUnitPrice > 0;
              const itemTotal = hasPrice ? `$${(item.quantity * item.estimatedUnitPrice).toFixed(2)}` : 'TBD';

              return (
                <tr key={item.id}>
                  <td><strong>{item.itemName}</strong></td>
                  <td>{item.description || '—'}</td>
                  <td>{item.quantity}</td>
                  <td>{item.unit || 'Piece'}</td>
                  <td>{formatPrice(item.estimatedUnitPrice)}</td>
                  <td>{itemTotal}</td>
                </tr>
              );
            })}
            <tr className={styles.totalRow}>
              <td colSpan={5} style={{ textAlign: 'right' }}>
                Estimated Total:
              </td>
              <td>{formatTotal(request.estimatedTotal)}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  );
}
