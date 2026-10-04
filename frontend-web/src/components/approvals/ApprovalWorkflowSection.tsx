import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../../context/AuthContext';
import {
  getApprovalWorkflowByRequestId,
  initializeApprovalWorkflow,
  transitionApprovalWorkflow,
  approveWorkflow,
  rejectWorkflow,
  requestWorkflowRevision,
} from '../../api/endpoints';
import type { WorkflowState } from '../../types/approval';
import { formatDateTime } from '../../utils/formatters';
import AuditTrailModal from './AuditTrailModal';
import styles from './ApprovalWorkflowSection.module.css';

interface ApprovalWorkflowSectionProps {
  requestId: string;
}

export default function ApprovalWorkflowSection({ requestId }: ApprovalWorkflowSectionProps) {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const [showAuditModal, setShowAuditModal] = useState(false);
  const [modalAction, setModalAction] = useState<'APPROVE' | 'REJECT' | 'REVISION_REQUESTED' | null>(null);
  const [comments, setComments] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const isManager = user?.role === 'MANAGER';
  const isOfficer = user?.role === 'PROCUREMENT_OFFICER';
  const isAdmin = user?.role === 'ADMIN';

  const {
    data: workflow,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['approval-workflow', requestId],
    queryFn: () => getApprovalWorkflowByRequestId(requestId),
    retry: false,
  });

  const invalidateQueries = () => {
    queryClient.invalidateQueries({ queryKey: ['approval-workflow', requestId] });
    queryClient.invalidateQueries({ queryKey: ['procurement-request', requestId] });
    queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
    queryClient.invalidateQueries({ queryKey: ['pending-approvals'] });
  };

  const initMutation = useMutation({
    mutationFn: () => initializeApprovalWorkflow(requestId),
    onSuccess: () => {
      setActionError(null);
      invalidateQueries();
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Failed to initialize approval workflow.');
    },
  });

  const transitionMutation = useMutation({
    mutationFn: (targetStatus: WorkflowState) =>
      transitionApprovalWorkflow(workflow!.id, targetStatus),
    onSuccess: () => {
      setActionError(null);
      invalidateQueries();
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Transition failed.');
    },
  });

  const decisionMutation = useMutation({
    mutationFn: async () => {
      if (!workflow) return;
      if (modalAction === 'APPROVE') {
        return approveWorkflow(workflow.id, comments || undefined);
      } else if (modalAction === 'REJECT') {
        return rejectWorkflow(workflow.id, comments);
      } else if (modalAction === 'REVISION_REQUESTED') {
        return requestWorkflowRevision(workflow.id, comments);
      }
    },
    onSuccess: () => {
      setActionError(null);
      setModalAction(null);
      setComments('');
      invalidateQueries();
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setActionError(axiosErr.response?.data?.message ?? 'Action failed.');
    },
  });

  const handleOpenModal = (action: 'APPROVE' | 'REJECT' | 'REVISION_REQUESTED') => {
    setActionError(null);
    setComments('');
    setModalAction(action);
  };

  const handleCloseModal = () => {
    if (!decisionMutation.isPending) {
      setModalAction(null);
      setComments('');
    }
  };

  if (isLoading) {
    return <div className={styles.card}>Loading approval workflow details…</div>;
  }

  if (error || !workflow) {
    return (
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h2 className={styles.cardTitle}>Approval Workflow</h2>
        </div>
        <p style={{ color: '#64748b', fontSize: '0.9rem', marginBottom: '1rem' }}>
          No formal approval workflow has been initialized for this procurement request yet.
        </p>
        {(isOfficer || isAdmin || isManager) && (
          <button
            type="button"
            className={styles.btnSecondary}
            onClick={() => initMutation.mutate()}
            disabled={initMutation.isPending}
          >
            {initMutation.isPending ? 'Initializing…' : 'Initialize Approval Workflow'}
          </button>
        )}
        {actionError && <div className={styles.errorBanner} style={{ marginTop: '1rem' }}>{actionError}</div>}
      </div>
    );
  }

  const currentStatus = workflow.currentStatus;
  const isPendingManager = currentStatus === 'WAITING_MANAGER_APPROVAL';
  const isApproved = currentStatus === 'APPROVED';
  const isRejected = currentStatus === 'REJECTED';
  const isRevision = currentStatus === 'REVISION_REQUESTED';
  const isEvaluation = currentStatus === 'UNDER_VENDOR_EVALUATION';

  // Stepper state calculations
  const step1Done = currentStatus !== 'DRAFT';
  const step2Done = isPendingManager || isApproved || isRejected || isRevision;
  const step3Done = isApproved || isRejected || isRevision;

  return (
    <div className={styles.container}>
      {actionError && <div className={styles.errorBanner}>{actionError}</div>}

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h2 className={styles.cardTitle}>
            <span>Approval Lifecycle</span>
            <span
              style={{
                fontSize: '0.8rem',
                fontWeight: 600,
                padding: '0.2rem 0.6rem',
                borderRadius: '9999px',
                background: isApproved ? '#dcfce7' : isRejected ? '#fee2e2' : isPendingManager ? '#fef3c7' : '#f1f5f9',
                color: isApproved ? '#15803d' : isRejected ? '#b91c1c' : isPendingManager ? '#b45309' : '#475569',
              }}
            >
              {currentStatus.replace(/_/g, ' ')}
            </span>
          </h2>
          <button
            type="button"
            className={styles.btnSecondary}
            onClick={() => setShowAuditModal(true)}
          >
            View Audit Trail
          </button>
        </div>

        {/* Stepper visual */}
        <div className={styles.stepper}>
          <div className={`${styles.stepItem} ${step1Done ? styles.stepCompleted : styles.stepActive}`}>
            <div className={styles.stepCircle}>{step1Done ? '✓' : '1'}</div>
            <div className={styles.stepLabel}>Request Draft</div>
          </div>

          <div
            className={`${styles.stepItem} ${
              step2Done
                ? styles.stepCompleted
                : isEvaluation
                ? styles.stepActive
                : ''
            }`}
          >
            <div className={styles.stepCircle}>{step2Done ? '✓' : '2'}</div>
            <div className={styles.stepLabel}>Vendor Evaluation</div>
          </div>

          <div
            className={`${styles.stepItem} ${
              step3Done
                ? isRejected
                  ? styles.stepRejected
                  : styles.stepCompleted
                : isPendingManager
                ? styles.stepActive
                : ''
            }`}
          >
            <div className={styles.stepCircle}>
              {step3Done ? (isRejected ? '✗' : '✓') : '3'}
            </div>
            <div className={styles.stepLabel}>Manager Review</div>
          </div>

          <div
            className={`${styles.stepItem} ${
              isApproved
                ? styles.stepCompleted
                : isRejected
                ? styles.stepRejected
                : isRevision
                ? styles.stepActive
                : ''
            }`}
          >
            <div className={styles.stepCircle}>
              {isApproved ? '✓' : isRejected ? '✗' : '4'}
            </div>
            <div className={styles.stepLabel}>
              {isApproved ? 'Approved' : isRejected ? 'Rejected' : isRevision ? 'Revision Req.' : 'Finalized'}
            </div>
          </div>
        </div>

        {/* Component 3 AI Executive Recommendation Summary */}
        {workflow.vendorRecommendationSummary && (
          <div className={styles.aiBriefCard}>
            <div className={styles.aiHeader}>
              <h3 className={styles.aiTitle}>
                <span>🤖 AI Executive Evaluation Summary</span>
              </h3>
              {workflow.vendorRecommendationSummary.topScore !== undefined && (
                <span className={styles.aiScoreBadge}>
                  Top Score: {workflow.vendorRecommendationSummary.topScore.toFixed(1)} / 100
                </span>
              )}
            </div>

            <p className={styles.aiSummaryText}>
              {workflow.vendorRecommendationSummary.recommendationSummary}
            </p>

            {workflow.vendorRecommendationSummary.rankedEvaluations?.length > 0 && (
              <div className={styles.vendorList}>
                {workflow.vendorRecommendationSummary.rankedEvaluations.map((e) => (
                  <div key={e.id} className={styles.vendorItem}>
                    <div className={styles.vendorRank}>
                      <span className={styles.rankBadge}>#{e.rank}</span>
                      <div>
                        <strong>Overall Score: {e.overallScore.toFixed(1)}</strong>
                        <div className={styles.vendorReasoning}>{e.reasoning}</div>
                      </div>
                    </div>
                    {e.riskFlags && e.riskFlags.length > 0 && (
                      <span className={styles.riskFlag}>
                        {e.riskFlags.join(', ')}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Manager Action Panel */}
        {(isManager || isAdmin) && isPendingManager && (
          <div className={styles.actionPanel}>
            <h4 className={styles.actionPanelTitle}>Manager Review Required</h4>
            <p className={styles.actionPanelNotice}>
              You have the authority to make an approval decision on this procurement request.
            </p>
            <div className={styles.actionButtons}>
              <button
                type="button"
                className={styles.btnApprove}
                onClick={() => handleOpenModal('APPROVE')}
              >
                ✓ Approve Request
              </button>
              <button
                type="button"
                className={styles.btnRevision}
                onClick={() => handleOpenModal('REVISION_REQUESTED')}
              >
                Request Revision
              </button>
              <button
                type="button"
                className={styles.btnReject}
                onClick={() => handleOpenModal('REJECT')}
              >
                ✗ Reject Request
              </button>
            </div>
          </div>
        )}

        {/* PO Transition Progression */}
        {(isOfficer || isAdmin) && isEvaluation && (
          <div className={styles.actionPanel}>
            <h4 className={styles.actionPanelTitle}>Procurement Officer Progression</h4>
            <p className={styles.actionPanelNotice}>
              Vendor evaluation is in progress. Once candidate quotes and scoring are validated, submit this workflow to the Manager for approval.
            </p>
            <button
              type="button"
              className={styles.btnApprove}
              onClick={() => transitionMutation.mutate('WAITING_MANAGER_APPROVAL')}
              disabled={transitionMutation.isPending}
            >
              {transitionMutation.isPending ? 'Advancing…' : 'Submit for Manager Approval'}
            </button>
          </div>
        )}

        {/* Decision History */}
        {workflow.decisions && workflow.decisions.length > 0 && (
          <div>
            <h4 style={{ fontSize: '0.95rem', color: '#1e3a5f', margin: '1rem 0 0.5rem 0' }}>
              Decision History
            </h4>
            <div className={styles.historyList}>
              {workflow.decisions.map((d) => (
                <div key={d.id} className={styles.historyItem}>
                  <div className={styles.historyMeta}>
                    <strong>{d.managerName} — {d.decision}</strong>
                    <span>{formatDateTime(d.createdAt)}</span>
                  </div>
                  <div className={styles.historyComment}>
                    {d.comments ? d.comments : <em>No comments recorded.</em>}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Decision Modal */}
      {modalAction && (
        <div className={styles.modalOverlay} onClick={handleCloseModal}>
          <div className={styles.decisionModal} onClick={(e) => e.stopPropagation()}>
            <h3 className={styles.modalTitle}>
              {modalAction === 'APPROVE' && 'Approve Procurement Request'}
              {modalAction === 'REJECT' && 'Reject Procurement Request'}
              {modalAction === 'REVISION_REQUESTED' && 'Request Revision'}
            </h3>
            <p className={styles.modalDesc}>
              {modalAction === 'APPROVE' &&
                'Optionally provide comments or stipulations for your approval.'}
              {modalAction === 'REJECT' &&
                'Please provide a mandatory justification for rejecting this request.'}
              {modalAction === 'REVISION_REQUESTED' &&
                'Please provide clear instructions on what revisions the requester must make.'}
            </p>

            <textarea
              className={styles.textarea}
              placeholder={
                modalAction === 'APPROVE'
                  ? 'Add optional approval notes…'
                  : 'Enter mandatory explanation / required adjustments…'
              }
              value={comments}
              onChange={(e) => setComments(e.target.value)}
              required={modalAction !== 'APPROVE'}
            />

            <div className={styles.modalActions}>
              <button
                type="button"
                className={styles.btnSecondary}
                onClick={handleCloseModal}
                disabled={decisionMutation.isPending}
              >
                Cancel
              </button>
              <button
                type="button"
                className={
                  modalAction === 'APPROVE'
                    ? styles.btnApprove
                    : modalAction === 'REJECT'
                    ? styles.btnReject
                    : styles.btnRevision
                }
                onClick={() => decisionMutation.mutate()}
                disabled={
                  decisionMutation.isPending ||
                  (modalAction !== 'APPROVE' && !comments.trim())
                }
              >
                {decisionMutation.isPending ? 'Submitting…' : `Confirm ${modalAction.replace('_', ' ')}`}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Audit Trail Modal */}
      {showAuditModal && (
        <AuditTrailModal workflowId={workflow.id} onClose={() => setShowAuditModal(false)} />
      )}
    </div>
  );
}
