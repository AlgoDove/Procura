import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createProcurementRequest,
  processAiWorkflow,
  getAiWorkflow,
} from '../api/endpoints';
import type {
  CreateProcurementRequestDto,
  Priority,
  ProcurementRequestItemInput,
  WorkflowProcessResponse,
} from '../types/api';
import ItemsEditor from '../components/ItemsEditor';
import styles from './RequestFormPage.module.css';

function tomorrowIso(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().split('T')[0];
}

type CreationMethod = 'AI' | 'MANUAL';

export default function CreateRequestPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [method, setMethod] = useState<CreationMethod>('AI');

  // Manual Form State
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [justification, setJustification] = useState('');
  const [priority, setPriority] = useState<Priority>('MEDIUM');
  const [requiredByDate, setRequiredByDate] = useState('');
  const [items, setItems] = useState<ProcurementRequestItemInput[]>([
    { itemName: '', description: '', quantity: 1, unit: '', estimatedUnitPrice: 0 },
  ]);
  const [formError, setFormError] = useState<string | null>(null);

  // AI Workflow State
  const [aiObjective, setAiObjective] = useState('');
  const [clarificationAnswer, setClarificationAnswer] = useState('');
  const [aiWorkflow, setAiWorkflow] = useState<WorkflowProcessResponse | null>(null);
  const [aiError, setAiError] = useState<string | null>(null);
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

  const isWorkflowSuccessful = (status: string) =>
    status === 'COMPLETED' || status === 'STAGE_COMPLETED';

  const handleWorkflowResult = (data: WorkflowProcessResponse) => {
    setAiWorkflow(data);
    queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });

    if (isWorkflowSuccessful(data.status) && data.procurementRequestId) {
      stopPolling();
      // Redirect to newly created DRAFT request after brief delay
      setTimeout(() => {
        navigate(`/requests/${data.procurementRequestId}`);
      }, 1200);
      return;
    }

    if (data.status === 'NEEDS_USER_INPUT' || data.status === 'FAILED') {
      stopPolling();
    }
  };

  const startPolling = (workflowId: string) => {
    stopPolling();
    pollRef.current = setInterval(async () => {
      try {
        const data = await getAiWorkflow(workflowId);
        handleWorkflowResult(data);
      } catch {
        stopPolling();
      }
    }, 3000);
  };

  // Mutation for starting AI workflow (from scratch, no existing ID)
  const { mutate: startAi, isPending: isAiStarting } = useMutation({
    mutationFn: () => processAiWorkflow({ objective: aiObjective.trim() }),
    onSuccess: (data) => {
      setAiError(null);
      handleWorkflowResult(data);
      if (!isWorkflowSuccessful(data.status) && data.status !== 'NEEDS_USER_INPUT' && data.status !== 'FAILED') {
        startPolling(data.workflowId);
      }
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string; errors?: string[] } } };
      const rawMsg = axiosErr.response?.data?.message ?? '';
      if (rawMsg.includes('503') || rawMsg.includes('Gemini') || rawMsg.includes('AGENT_FAILED')) {
        setAiError('The AI service is temporarily unavailable. Please try again in a few moments.');
      } else {
        setAiError(rawMsg || 'Failed to start AI workflow. Please check your connection.');
      }
    },
  });

  // Mutation for continuing AI workflow with clarification
  const { mutate: continueAi, isPending: isAiContinuing } = useMutation({
    mutationFn: () =>
      processAiWorkflow({
        objective: clarificationAnswer.trim(),
        workflowId: aiWorkflow!.workflowId,
        existingRequestId: aiWorkflow?.procurementRequestId,
      }),
    onSuccess: (data) => {
      setAiError(null);
      setClarificationAnswer('');
      handleWorkflowResult(data);
      if (!isWorkflowSuccessful(data.status) && data.status !== 'NEEDS_USER_INPUT' && data.status !== 'FAILED') {
        startPolling(data.workflowId);
      }
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      const rawMsg = axiosErr.response?.data?.message ?? '';
      if (rawMsg.includes('503') || rawMsg.includes('Gemini') || rawMsg.includes('AGENT_FAILED')) {
        setAiError('The AI service is temporarily unavailable. Please try again in a few moments.');
      } else {
        setAiError(rawMsg || 'Failed to send answer. Please try again.');
      }
    },
  });

  // Mutation for Manual submission
  const { mutate: submitManual, isPending: isManualPending } = useMutation({
    mutationFn: (dto: CreateProcurementRequestDto) => createProcurementRequest(dto),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
      navigate(`/requests/${data.id}`);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setFormError(
        axiosErr.response?.data?.message ?? 'Failed to create request. Please check your input.'
      );
    },
  });

  const handleManualSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!title.trim()) {
      setFormError('Title is required.');
      return;
    }
    if (!description.trim()) {
      setFormError('Description is required.');
      return;
    }
    if (!justification.trim()) {
      setFormError('Justification is required.');
      return;
    }
    if (!requiredByDate) {
      setFormError('Required by date is required.');
      return;
    }
    if (items.length === 0) {
      setFormError('At least one item is required.');
      return;
    }
    if (items.some((it) => !it.itemName.trim())) {
      setFormError('All items must have a name.');
      return;
    }
    if (items.some((it) => !it.unit.trim())) {
      setFormError('All items must have a unit (e.g. Piece, Box, Hours).');
      return;
    }
    if (items.some((it) => !it.description.trim())) {
      setFormError('All items must have a description.');
      return;
    }
    if (items.some((it) => (it.quantity || 0) < 1)) {
      setFormError('Item quantity must be at least 1.');
      return;
    }
    if (items.some((it) => (it.estimatedUnitPrice || 0) < 0)) {
      setFormError('Item price cannot be negative.');
      return;
    }

    const dto: CreateProcurementRequestDto = {
      title: title.trim(),
      description: description.trim(),
      justification: justification.trim(),
      priority,
      requiredByDate: new Date(requiredByDate).toISOString(),
      items: items.map(({ itemName, description: d, quantity, unit, estimatedUnitPrice }) => ({
        itemName: itemName.trim(),
        description: d.trim(),
        quantity,
        unit: unit.trim(),
        estimatedUnitPrice,
      })),
    };

    submitManual(dto);
  };

  // Sanitize errors array from AI workflow
  const getSanitizedAiErrors = (errors: string[]) => {
    const raw = Array.from(new Set(errors));
    const isTechFailure = raw.some(
      (e) => e.includes('503') || e.includes('Gemini') || e.includes('AGENT_FAILED') || e.includes('temporarily')
    );
    if (isTechFailure) {
      return ['The AI service is temporarily unavailable. Please try again in a few moments.'];
    }
    return raw;
  };

  return (
    <div className={styles.container}>
      <h1 className={styles.heading}>New Procurement Request</h1>
      <p className={styles.subheading}>
        Choose your preferred creation method below. AI assistant creates a draft based on your description.
      </p>

      {/* Creation Method Tabs */}
      <div className={styles.methodToggle}>
        <button
          type="button"
          className={`${styles.toggleBtn} ${method === 'AI' ? styles.toggleBtnActive : ''}`}
          onClick={() => setMethod('AI')}
        >
          🤖 AI Assistant (Recommended)
        </button>
        <button
          type="button"
          className={`${styles.toggleBtn} ${method === 'MANUAL' ? styles.toggleBtnActive : ''}`}
          onClick={() => setMethod('MANUAL')}
        >
          📝 Manual Form
        </button>
      </div>

      {/* AI Assistant Mode */}
      {method === 'AI' && (
        <div className={styles.card}>
          <p className={styles.cardIntro}>
            Describe the items or services you need in plain English. The Procurement Request Agent
            will extract your requirements, validate line items, and generate a <strong>Draft</strong> request
            for your review.
          </p>

          {aiError && <div className={styles.errorBanner}>{aiError}</div>}

          {/* Workflow Status Banners */}
          {aiWorkflow && (
            <>
              {aiWorkflow.status === 'IN_PROGRESS' && (
                <div className={`${styles.statusBanner} ${styles.statusProcessing}`}>
                  <span>⚙️ Processing requirements with Procurement Request Agent…</span>
                </div>
              )}

              {isWorkflowSuccessful(aiWorkflow.status) && (
                <div className={`${styles.statusBanner} ${styles.statusSuccess}`}>
                  <span>
                    ✓ Draft request created successfully! Redirecting to draft review…
                  </span>
                </div>
              )}

              {aiWorkflow.status === 'FAILED' && (
                <div className={`${styles.statusBanner} ${styles.statusFailed}`}>
                  <span>Workflow failed.</span>
                  <button
                    type="button"
                    className={styles.btnAiAction}
                    onClick={() => startAi()}
                    style={{ padding: '0.3rem 0.8rem', fontSize: '0.8rem' }}
                  >
                    Retry
                  </button>
                </div>
              )}

              {/* Sanitized error list */}
              {aiWorkflow.errors && aiWorkflow.errors.length > 0 && (
                <div className={styles.errorBanner}>
                  <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
                    {getSanitizedAiErrors(aiWorkflow.errors).map((e, idx) => (
                      <li key={idx}>{e}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Clarification prompt if info missing */}
              {aiWorkflow.status === 'NEEDS_USER_INPUT' && aiWorkflow.clarificationPrompt && (
                <div className={styles.clarificationCard}>
                  <h3 className={styles.clarificationTitle}>AI Clarification Needed</h3>
                  <p className={styles.clarificationText}>{aiWorkflow.clarificationPrompt}</p>

                  <div className={styles.field}>
                    <label htmlFor="clarificationAnswer">Your Answer</label>
                    <textarea
                      id="clarificationAnswer"
                      rows={3}
                      value={clarificationAnswer}
                      onChange={(e) => setClarificationAnswer(e.target.value)}
                      placeholder="e.g. Needed by October 15; quantity is 5."
                      disabled={isAiContinuing}
                    />
                  </div>

                  <div className={styles.actions}>
                    <button
                      type="button"
                      className={styles.btnAiAction}
                      onClick={() => continueAi()}
                      disabled={isAiContinuing || !clarificationAnswer.trim()}
                    >
                      {isAiContinuing ? 'Sending…' : 'Send Answer'}
                    </button>
                  </div>
                </div>
              )}
            </>
          )}

          {/* Initial Prompt Input (shown before workflow starts or when starting new) */}
          {(!aiWorkflow || aiWorkflow.status === 'FAILED') && (
            <>
              <div className={styles.field}>
                <label htmlFor="aiObjective">What do you need to procure? *</label>
                <textarea
                  id="aiObjective"
                  rows={5}
                  value={aiObjective}
                  onChange={(e) => setAiObjective(e.target.value)}
                  placeholder="e.g. I need a MacBook Pro for a new software engineering employee. It should have 16GB RAM and sufficient storage for development work. Needed by end of next week."
                  disabled={isAiStarting}
                />
              </div>

              <div className={styles.actions}>
                <button
                  type="button"
                  onClick={() => navigate('/requests')}
                  className={styles.btnSecondary}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className={styles.btnAiAction}
                  onClick={() => startAi()}
                  disabled={isAiStarting || !aiObjective.trim()}
                >
                  {isAiStarting ? 'Creating Draft with AI…' : '🤖 Create Draft with AI'}
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {/* Manual Form Mode */}
      {method === 'MANUAL' && (
        <form onSubmit={handleManualSubmit} noValidate>
          <div className={styles.card}>
            <div className={styles.field}>
              <label htmlFor="title">Title *</label>
              <input
                id="title"
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="e.g. Laptops for Engineering Team"
                maxLength={150}
                required
              />
            </div>

            <div className={styles.field}>
              <label htmlFor="description">Description *</label>
              <textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Provide detailed requirements and specifications"
                rows={3}
                required
              />
            </div>

            <div className={styles.field}>
              <label htmlFor="justification">Business Justification *</label>
              <textarea
                id="justification"
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
                placeholder="Explain the business need for this procurement"
                rows={3}
                required
              />
            </div>

            <div className={styles.row}>
              <div className={styles.field}>
                <label htmlFor="priority">Priority *</label>
                <select
                  id="priority"
                  value={priority}
                  onChange={(e) => setPriority(e.target.value as Priority)}
                >
                  <option value="LOW">Low</option>
                  <option value="MEDIUM">Medium</option>
                  <option value="HIGH">High</option>
                  <option value="URGENT">Urgent</option>
                </select>
              </div>

              <div className={styles.field}>
                <label htmlFor="requiredByDate">Required by Date *</label>
                <input
                  id="requiredByDate"
                  type="date"
                  value={requiredByDate}
                  min={tomorrowIso()}
                  onChange={(e) => setRequiredByDate(e.target.value)}
                  required
                />
              </div>
            </div>
          </div>

          <ItemsEditor items={items} onChange={setItems} />

          {formError && <div className={styles.errorBanner}>{formError}</div>}

          <div className={styles.actions}>
            <button
              type="button"
              onClick={() => navigate('/requests')}
              className={styles.btnSecondary}
            >
              Cancel
            </button>
            <button
              type="submit"
              className={styles.btnPrimary}
              disabled={isManualPending}
            >
              {isManualPending ? 'Creating Draft…' : 'Create Draft'}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
