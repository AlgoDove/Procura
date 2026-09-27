import { useState, useRef, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { processAiWorkflow, getAiWorkflow } from '../api/endpoints';
import type { WorkflowProcessResponse, WorkflowStatus, StepStatus } from '../types/api';
import { formatStatus, formatTotal } from '../utils/formatters';
import styles from './AiWorkflowPage.module.css';

const TERMINAL_STATUSES: WorkflowStatus[] = [
  'COMPLETED', 'STAGE_COMPLETED', 'FAILED', 'APPROVED', 'REJECTED', 'WAITING_FOR_HUMAN_APPROVAL',
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
  NOT_STARTED: '#94a3b8',
  PENDING: '#d97706',
  IN_PROGRESS: '#0284c7',
  COMPLETED: '#16a34a',
  FAILED: '#dc2626',
  SKIPPED: '#94a3b8',
};

export default function AiWorkflowPage() {
  const { id: requestId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [objective, setObjective] = useState('');
  const [clarificationAnswer, setClarificationAnswer] = useState('');
  const [workflow, setWorkflow] = useState<WorkflowProcessResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const stopPolling = () => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  };

  useEffect(() => {
    return () => stopPolling();
  }, []);

  const handleWorkflowUpdate = (data: WorkflowProcessResponse) => {
    setWorkflow(data);
    queryClient.invalidateQueries({ queryKey: ['procurement-request', requestId] });
    queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });

    if (TERMINAL_STATUSES.includes(data.status) || data.status === 'NEEDS_USER_INPUT') {
      stopPolling();
    }
  };

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

  const sanitizeErrorString = (errStr: string) => {
    if (
      errStr.includes('503') ||
      errStr.includes('Gemini') ||
      errStr.includes('AGENT_FAILED') ||
      errStr.includes('temporarily')
    ) {
      return 'The AI service is temporarily unavailable. Please try again in a few moments.';
    }
    return errStr;
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
      const raw = axiosErr.response?.data?.message ?? 'Failed to start AI workflow.';
      setError(sanitizeErrorString(raw));
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
      const raw = axiosErr.response?.data?.message ?? 'Failed to continue workflow.';
      setError(sanitizeErrorString(raw));
    },
  });

  const isInProgress =
    workflow &&
    !TERMINAL_STATUSES.includes(workflow.status) &&
    workflow.status !== 'NEEDS_USER_INPUT';

  const rawErrors = workflow?.errors ? Array.from(new Set(workflow.errors)) : [];
  const isAiServiceError = rawErrors.some(
    (e) => e.includes('503') || e.includes('Gemini') || e.includes('AGENT_FAILED') || e.includes('temporarily')
  );
  const displayErrors = isAiServiceError
    ? ['The AI service is temporarily unavailable. Please try again in a few moments.']
    : rawErrors;

  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <Link to={`/requests/${requestId}`} className={styles.back}>
          ← Back to Request
        </Link>
        <h1 className={styles.heading}>🤖 AI Procurement Assistant</h1>
      </div>

      {/* Start form — shown before workflow begins */}
      {!workflow && (
        <div className={styles.card}>
          <p className={styles.intro}>
            Describe what you need to procure or update on this request. The Procurement Request Agent
            will extract the details, validate line items, and update your draft procurement request.
          </p>
          <div className={styles.field}>
            <label htmlFor="objective">What do you need to procure or update?</label>
            <textarea
              id="objective"
              rows={4}
              value={objective}
              onChange={(e) => setObjective(e.target.value)}
              placeholder="e.g. Add 5 wireless mice and 2 external monitors to this request, needed by next month."
              disabled={isStarting}
            />
          </div>
          {error && <p className={styles.error}>{error}</p>}
          <div className={styles.actions}>
            <button
              type="button"
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
            <strong>Status:</strong> {formatStatus(workflow.status)}
            {isInProgress && <span className={styles.spinner}> ⟳ Processing…</span>}
          </div>

          {/* Created / updated request info */}
          {workflow.procurementRequestId && (
            <div className={styles.card}>
              <strong>Draft request:</strong>{' '}
              <Link to={`/requests/${workflow.procurementRequestId}`} className={styles.reqLink}>
                {workflow.requestNumber ?? workflow.procurementRequestId}
              </Link>
              {workflow.estimatedTotal != null && (
                <span> — Estimated Total: {formatTotal(workflow.estimatedTotal)}</span>
              )}
              <p className={styles.note}>
                ⚠ The AI has updated this <strong>DRAFT</strong> request. You must review the details
                and <strong>explicitly submit it</strong> when satisfied.
              </p>
            </div>
          )}

          {/* Clarification prompt */}
          {workflow.status === 'NEEDS_USER_INPUT' && workflow.clarificationPrompt && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>AI Needs Clarification</h2>
              <p className={styles.clarificationPrompt}>{workflow.clarificationPrompt}</p>
              <div className={styles.field}>
                <label htmlFor="clarification">Your Answer</label>
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
                  type="button"
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

          {/* Sanitized Errors */}
          {displayErrors.length > 0 && (
            <div className={`${styles.card} ${styles.errorCard}`}>
              <h2 className={styles.cardTitle}>Status Notice</h2>
              <ul>
                {displayErrors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
              {isAiServiceError && (
                <div style={{ marginTop: '0.75rem' }}>
                  <button
                    type="button"
                    className={styles.btnPrimary}
                    onClick={() => startWorkflow()}
                    disabled={isStarting}
                    style={{ fontSize: '0.85rem', padding: '0.35rem 0.85rem' }}
                  >
                    Retry Workflow
                  </button>
                </div>
              )}
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
                        <span className={styles.stepAgent}>{step.agentName}</span>
                        <span
                          className={styles.stepStatus}
                          style={{ color: STEP_STATUS_COLOR[step.status] }}
                        >
                          {formatStatus(step.status)}
                        </span>
                      </div>
                      <p className={styles.stepObjective}>{step.objective}</p>
                      {step.outcomeSummary && (
                        <p className={styles.stepOutcome}>✓ {step.outcomeSummary}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Audit trail */}
          {workflow.auditTrail?.length > 0 && (
            <div className={styles.card}>
              <h2 className={styles.cardTitle}>Audit Trail</h2>
              <div className={styles.auditList}>
                {workflow.auditTrail.map((entry) => (
                  <div key={entry.id} className={styles.auditRow}>
                    <span className={styles.auditTime}>
                      {entry.timestamp ? entry.timestamp.split('T')[1]?.split('.')[0] : ''}
                    </span>
                    <span className={styles.auditActor}>{entry.actor}</span>
                    <span className={styles.auditAction}>{entry.action}</span>
                    <span className={styles.auditDetails}>{entry.details}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
