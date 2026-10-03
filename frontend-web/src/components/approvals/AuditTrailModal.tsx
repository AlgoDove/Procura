import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getWorkflowAuditTrail } from '../../api/endpoints';
import { formatDateTime } from '../../utils/formatters';
import styles from './AuditTrailModal.module.css';

interface AuditTrailModalProps {
  workflowId: string;
  onClose: () => void;
}

export default function AuditTrailModal({ workflowId, onClose }: AuditTrailModalProps) {
  const [activeTab, setActiveTab] = useState<'decisions' | 'agents' | 'notifications'>('decisions');

  const { data: auditTrail, isLoading, error } = useQuery({
    queryKey: ['workflow-audit-trail', workflowId],
    queryFn: () => getWorkflowAuditTrail(workflowId),
  });

  const getDecisionBadge = (decision: string) => {
    switch (decision.toUpperCase()) {
      case 'APPROVED':
        return `${styles.badge} ${styles.badgeApproved}`;
      case 'REJECTED':
        return `${styles.badge} ${styles.badgeRejected}`;
      case 'REVISION_REQUESTED':
        return `${styles.badge} ${styles.badgeRevision}`;
      default:
        return styles.badge;
    }
  };

  return (
    <div className={styles.backdrop} onClick={onClose} role="dialog" aria-modal="true">
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.header}>
          <h2 className={styles.headerTitle}>Workflow Audit Trail</h2>
          <button className={styles.closeBtn} onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>

        <div className={styles.tabs}>
          <button
            className={`${styles.tab} ${activeTab === 'decisions' ? styles.activeTab : ''}`}
            onClick={() => setActiveTab('decisions')}
          >
            Decisions ({auditTrail?.decisions.length ?? 0})
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'agents' ? styles.activeTab : ''}`}
            onClick={() => setActiveTab('agents')}
          >
            AI Agent Logs ({auditTrail?.agentExecutions.length ?? 0})
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'notifications' ? styles.activeTab : ''}`}
            onClick={() => setActiveTab('notifications')}
          >
            Notifications ({auditTrail?.notifications.length ?? 0})
          </button>
        </div>

        <div className={styles.content}>
          {isLoading && <div className={styles.state}>Loading comprehensive audit trail…</div>}
          {error && <div className={styles.errorState}>Failed to load audit trail records.</div>}

          {!isLoading && !error && auditTrail && (
            <>
              {activeTab === 'decisions' && (
                <div className={styles.timeline}>
                  {auditTrail.decisions.length === 0 ? (
                    <div className={styles.emptyNotice}>No managerial decisions recorded yet.</div>
                  ) : (
                    auditTrail.decisions.map((d) => (
                      <div key={d.id} className={styles.timelineItem}>
                        <div className={styles.timelineDot} />
                        <div className={styles.itemHeader}>
                          <div className={styles.itemTitle}>
                            <span>{d.managerName}</span>
                            <span className={getDecisionBadge(d.decision)}>{d.decision}</span>
                          </div>
                          <span className={styles.itemTimestamp}>{formatDateTime(d.createdAt)}</span>
                        </div>
                        <div className={styles.itemBody}>
                          {d.comments ? d.comments : <em>No comments provided.</em>}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              )}

              {activeTab === 'agents' && (
                <div className={styles.timeline}>
                  {auditTrail.agentExecutions.length === 0 ? (
                    <div className={styles.emptyNotice}>No autonomous agent executions recorded.</div>
                  ) : (
                    auditTrail.agentExecutions.map((a) => (
                      <div key={a.id} className={styles.timelineItem}>
                        <div className={styles.timelineDot} />
                        <div className={styles.itemHeader}>
                          <div className={styles.itemTitle}>
                            <span>{a.agentName}</span>
                            <span className={`${styles.badge} ${styles.badgeAgent}`}>
                              {a.executionStatus}
                            </span>
                          </div>
                          <span className={styles.itemTimestamp}>{formatDateTime(a.startedAt)}</span>
                        </div>
                        <div className={styles.itemBody}>
                          {a.outputSummary && <p>{a.outputSummary}</p>}
                          {a.toolExecutionMetadata && (
                            <div className={styles.metadataBox}>
                              <strong>Tools Executed:</strong>
                              <div>{a.toolExecutionMetadata}</div>
                            </div>
                          )}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              )}

              {activeTab === 'notifications' && (
                <div className={styles.timeline}>
                  {auditTrail.notifications.length === 0 ? (
                    <div className={styles.emptyNotice}>No workflow notifications generated.</div>
                  ) : (
                    auditTrail.notifications.map((n) => (
                      <div key={n.id} className={styles.timelineItem}>
                        <div className={styles.timelineDot} />
                        <div className={styles.itemHeader}>
                          <div className={styles.itemTitle}>
                            <span>{n.title}</span>
                            <span className={styles.badge}>{n.type}</span>
                          </div>
                          <span className={styles.itemTimestamp}>{formatDateTime(n.createdAt)}</span>
                        </div>
                        <div className={styles.itemBody}>
                          <p>{n.message}</p>
                          <small style={{ color: n.isRead ? '#15803d' : '#b45309' }}>
                            {n.isRead ? '✓ Read' : '● Unread'}
                          </small>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
