import { useState, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { processAiWorkflow, getAiWorkflow } from '../api/endpoints';
import type { WorkflowProcessResponse, WorkflowStatus, StepStatus } from '../types/api';
import styles from './AiWorkflowPage.module.css';

const TERMINAL_STATUSES: WorkflowStatus[] = [
  'COMPLETED', 'FAILED', 'APPROVED', 'REJECTED', 'WAITING_FOR_HUMAN_APPROVAL',
];

const STEP_STATUS_ICON: Record<StepStatus, string> = {
  NOT_STARTED: '○',
  PENDING: '◌',
  IN_PROGRESS: '⟳',
  COMPLETED: '✓',
  FAILED: '✗',
  SKIPPED: '—',
};

const STEP_STATUS_COLOR: Record<StepStatus, string> = {
  NOT_STARTED: '#bbb',
  PENDING: '#e67e22',
  IN_PROGRESS: '#2980b9',
  COMPLETED: '#27ae60',
  FAILED: '#c0392b',
  SKIPPED: '#bbb',
};

export default function AiWorkflowPage() {
  const { id: requestId } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [objective, setObjective] = useState('');
  const [clarificationAnswer, setClarificationAnswer] = useState('');
  const [workflow, setWorkflow] = useState<WorkflowProcessResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Polling interval ref so we can clear it
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const stopPolling = () => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  };

  const handleWorkflowUpdate = (data: WorkflowProcessResponse) => {
    setWorkflow(data);
    queryClient.invalidateQueries({ queryKey: ['procurement-request', requestId] });
    queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });

    // Stop polling on terminal state or when user input is needed
    if (TERMINAL_STATUSES.includes(data.status) || data.status === 'NEEDS_USER_INPUT') {
      stopPolling();
    }
  };

  // Poll for workflow status when we have a workflowId and it's in-progress
  const startPolling = (workflowId: string) => {
    stopPolling();
    pollRef.current = setInterval(async () => {
      try {
        const data = await getAiWorkflow(workflowId);
        handleWorkflowUpdate(data);
      } catch {
        stopPolling();
      }
    }, 3000);
  };

  const { mutate: startWorkflow, isPending: isStarting } = useMutation({
    mutationFn: () =>
      processAiWorkflow({
        objective,
        existingRequestId: requestId,
      }),
    onSuccess: (data) => {
      setError(null);
      handleWorkflowUpdate(data);
      if (!TERMINAL_STATUSES.includes(data.status) && data.status !== 'NEEDS_USER_INPUT') {
        startPolling(data.workflowId);
      }
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setError(axiosErr.response?.data?.message ?? 'Failed to start AI workflow.');
    },
  });

  const { mutate: continueWorkflow, isPending: isContinuing } = useMutation({
    mutationFn: () =>
      processAiWorkflow({
        objective: clarificationAnswer,
        workflowId: workflow!.workflowId,
        existingRequestId: workflow?.procurementRequestId ?? requestId,
      }),
    onSuccess: (data) => {
      setError(null);
      setClarificationAnswer('');
      handleWorkflowUpdate(data);
      if (!TERMINAL_STATUSES.includes(data.status) && data.status !== 'NEEDS_USER_INPUT') {
        startPolling(data.workflowId);
      }
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setError(axiosErr.response?.data?.message ?? 'Failed to continue workflow.');
    },
  });

  const isInProgress =
    workflow &&
    !TERMINAL_STATUSES.includes(workflow.status) &&
    workflow.status !== 'NEEDS_USER_INPUT';

  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <Link to={`/requests/${requestId}`} className={styles.back}>← Back to Request</Link>
        <h1 className={styles.heading}>🤖 AI Procurement Assistant</h1>
      </div>

      {/* Start form — shown before workflow begins */}
      {!workflow && (
        <div className={styles.card}>
          <p className={styles.intro}>
            Describe what you need to procure. The AI will extract the details, validate them,
            and create a draft procurement request on your behalf.
          </p>
          <div className={styles.field}>
            <label htmlFor="objective">What do you need to procure?</label>
            <textarea
              id="objective"
              rows={4}
              value={objective}
              onChange={(e) => setObjective(e.target.value)}
              placeholder="e.g. 10 laptop computers, Core i7, 16GB RAM, for the finance team. Needed by end of month."
              disabled={isStarting}
            />
          </div>
          {error && <p className={styles.error}>{error}</p>}
          <div className={styles.actions}>
            <button
              className={styles.btnPrimary}
              onClick={() => startWorkflow()}
              disabled={isStarting || !objective.trim()}
            >
              {isStarting ? 'Starting AI workflow…' : 'Start AI Workflow'}
            </button>
          </div>
        </div>
      )}

      {/* Workflow result */}
      {workflow && (
        <>
          {/* Status banner */}
          <div className={`${styles.statusBanner} ${styles[`status_${workflow.status}`] ?? ''}`}>
            <strong>Status:</strong> {workflow.status}
            {isInProgress && <span className={styles.spinner}> ⟳ Processing…</span>}
          </div>

          {/* Created request info */}
          {workflow.procurementRequestId && (
            <div className={styles.card}>
              <strong>Draft request created:</strong>{' '}
              <Link to={`/requests/${workflow.procurementRequestId}`} className={styles.reqLink}>
                {workflow.requestNumber ?? workflow.procurementRequestId}
              </Link>
              {workflow.estimatedTotal != null && (
                <span> — Estimated total: ${workflow.estimatedTotal.toFixed(2)}</span>
              )}
              <p className={styles.note}>
                ⚠ The AI has created a <strong>DRAFT</strong> request. You must review it and{' '}
                <strong>explicitly submit it</strong> when you are satisfied.
              </p>
            </div>
          )}

          {/* Clarification prompt */}
          {workflow.status === 'NEEDS_USER_INPUT' && workflow.clarificationPrompt && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>AI needs clarification</h2>
              <p className={styles.clarificationPrompt}>{workflow.clarificationPrompt}</p>
              <div className={styles.field}>
                <label htmlFor="clarification">Your answer</label>
                <textarea
                  id="clarification"
                  rows={3}
                  value={clarificationAnswer}
                  onChange={(e) => setClarificationAnswer(e.target.value)}
                  placeholder="Provide the requested information…"
                  disabled={isContinuing}
                />
              </div>
              {error && <p className={styles.error}>{error}</p>}
              <div className={styles.actions}>
                <button
                  className={styles.btnPrimary}
                  onClick={() => continueWorkflow()}
                  disabled={isContinuing || !clarificationAnswer.trim()}
                >
                  {isContinuing ? 'Sending…' : 'Send Answer'}
                </button>
              </div>
            </div>
          )}

          {/* Execution summary */}
          {workflow.executionSummary && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>Execution Summary</h2>
              <p className={styles.summary}>{workflow.executionSummary}</p>
            </div>
          )}

          {/* Errors */}
          {workflow.errors && workflow.errors.length > 0 && (
            <div className={`${styles.card} ${styles.errorCard}`}>
              <h2 className={styles.cardTitle}>Errors</h2>
              <ul>
                {workflow.errors.map((e, i) => <li key={i}>{e}</li>)}
              </ul>
            </div>
          )}

          {/* Workflow Plan */}
          {workflow.plan?.steps?.length > 0 && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>Workflow Plan</h2>
              <div className={styles.planSteps}>
                {workflow.plan.steps.map((step) => (
                  <div key={step.stepNumber} className={styles.planStep}>
                    <span
                      className={styles.stepIcon}
                      style={{ color: STEP_STATUS_COLOR[step.status] }}
                    >
                      {STEP_STATUS_ICON[step.status] ?? '○'}
                    </span>
                    <div className={styles.stepBody}>
                      <div className={styles.stepHeader}>
                        <span className={styles.stepName}>{step.agentName}</span>
                        <span
                          className={styles.stepStatus}
                          style={{ color: STEP_STATUS_COLOR[step.status] }}
                        >
                          {step.status}
                        </span>
                      </div>
                      <p className={styles.stepObjective}>{step.objective}</p>
                      {step.outcomeSummary && (
                        <p className={styles.stepOutcome}>{step.outcomeSummary}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Audit Trail */}
          {workflow.auditTrail?.length > 0 && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>Audit Trail</h2>
              <div className={styles.audit}>
                {workflow.auditTrail.map((entry) => (
                  <div
                    key={entry.id}
                    className={`${styles.auditEntry} ${entry.isSecurityViolation ? styles.secViolation : ''}`}
                  >
                    <span className={styles.auditTime}>
                      {new Date(entry.timestamp).toLocaleTimeString()}
                    </span>
                    <span className={styles.auditStage}>[{entry.stage}]</span>
                    <span className={styles.auditActor}>{entry.actor}</span>
                    <span className={styles.auditAction}>{entry.action}</span>
                    {entry.toolName && (
                      <span className={styles.auditTool}>({entry.toolName})</span>
                    )}
                    <span
                      className={styles.auditStatus}
                      style={{ color: entry.status === 'COMPLETED' ? '#27ae60' : '#c0392b' }}
                    >
                      {entry.status}
                    </span>
                    {entry.isSecurityViolation && (
                      <span className={styles.secLabel}>⚠ SECURITY</span>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Navigate to request when completed */}
          {workflow.procurementRequestId && workflow.status !== 'IN_PROGRESS' && (
            <div className={styles.actions}>
              <button
                className={styles.btnSecondary}
                onClick={() => navigate(`/requests/${workflow.procurementRequestId}`)}
              >
                Review Request →
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
