import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getVendorQuotes,
  submitVendorQuote,
  evaluateVendors,
  getVendorEvaluations,
  getRecommendationSummary,
  updateProcurementRequestStatus,
  getWorkflowForRequest,
  processAiWorkflow,
  getApprovalWorkflowByRequestId,
  transitionApprovalWorkflow,
} from '../../api/endpoints';
import type {
  RequestStatus,
  SystemRole,
  CreateVendorQuoteDto,
  VendorEvaluationResponseDto,
  CriterionWeightConfigDto,
} from '../../types/api';
import AddQuoteModal from './AddQuoteModal';
import CriterionScoreModal from './CriterionScoreModal';
import VendorComparisonTable from './VendorComparisonTable';
import RecommendationCard from './RecommendationCard';
import styles from './VendorEvaluationSection.module.css';

interface EvaluationPreset {
  id: string;
  label: string;
  weights: CriterionWeightConfigDto[];
}

const EVALUATION_PRESETS: EvaluationPreset[] = [
  {
    id: 'BALANCED',
    label: '⚖️ Balanced (Price 35%, Delivery 25%, Reliability 25%, Compliance 15%)',
    weights: [
      { criterion: 'PRICE', weight: 0.35 },
      { criterion: 'DELIVERY_TIME', weight: 0.25 },
      { criterion: 'RELIABILITY', weight: 0.25 },
      { criterion: 'COMPLIANCE', weight: 0.15 },
    ],
  },
  {
    id: 'PRICE_FIRST',
    label: '💰 Cost-Focused (Price 55%, Delivery 15%, Reliability 15%, Compliance 15%)',
    weights: [
      { criterion: 'PRICE', weight: 0.55 },
      { criterion: 'DELIVERY_TIME', weight: 0.15 },
      { criterion: 'RELIABILITY', weight: 0.15 },
      { criterion: 'COMPLIANCE', weight: 0.15 },
    ],
  },
  {
    id: 'SPEED_FIRST',
    label: '⚡ Fast Delivery (Delivery 55%, Price 15%, Reliability 15%, Compliance 15%)',
    weights: [
      { criterion: 'PRICE', weight: 0.15 },
      { criterion: 'DELIVERY_TIME', weight: 0.55 },
      { criterion: 'RELIABILITY', weight: 0.15 },
      { criterion: 'COMPLIANCE', weight: 0.15 },
    ],
  },
  {
    id: 'QUALITY_FIRST',
    label: '🛡️ High Reliability (Reliability 50%, Price 20%, Delivery 15%, Compliance 15%)',
    weights: [
      { criterion: 'PRICE', weight: 0.20 },
      { criterion: 'DELIVERY_TIME', weight: 0.15 },
      { criterion: 'RELIABILITY', weight: 0.50 },
      { criterion: 'COMPLIANCE', weight: 0.15 },
    ],
  },
];

function sanitizeErrorString(errStr: string) {
  if (
    errStr.includes('503') ||
    errStr.includes('Gemini') ||
    errStr.includes('AGENT_FAILED') ||
    errStr.includes('temporarily')
  ) {
    return 'The AI service is temporarily unavailable. Please try again in a few moments.';
  }
  return errStr;
}

interface VendorEvaluationSectionProps {
  procurementRequestId: string;
  requestStatus: RequestStatus;
  userRole?: SystemRole;
  estimatedBudget?: number;
  requiredDeliveryDays?: number;
}

export default function VendorEvaluationSection({
  procurementRequestId,
  requestStatus,
  userRole,
  estimatedBudget,
  requiredDeliveryDays,
}: VendorEvaluationSectionProps) {
  const queryClient = useQueryClient();

  const [selectedStrategy, setSelectedStrategy] = useState<string>('BALANCED');
  const [isAddQuoteOpen, setIsAddQuoteOpen] = useState(false);
  const [selectedEvaluation, setSelectedEvaluation] = useState<VendorEvaluationResponseDto | null>(null);
  const [selectedVendorName, setSelectedVendorName] = useState<string>('');
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [clarificationAnswer, setClarificationAnswer] = useState('');
  const [pendingClarification, setPendingClarification] = useState<{
    workflowId: string;
    prompt: string;
  } | null>(null);
  const [aiObjective, setAiObjective] = useState('');

    // Helper — add near the top of the component, or inline where needed
  const buildObjectiveWithPriority = (rawObjective: string) => {
    const preset = EVALUATION_PRESETS.find((p) => p.id === selectedStrategy);
    const priorityLabel = preset?.label ?? 'Balanced';
    return `${rawObjective} (Evaluation priority: ${priorityLabel})`;
  };

  // 1. Fetch submitted quotes
  const { data: quotes = [] } = useQuery({
    queryKey: ['vendor-quotes', procurementRequestId],
    queryFn: () => getVendorQuotes(procurementRequestId),
    enabled: !!procurementRequestId,
  });

  // 2. Fetch evaluation summary
  const { data: recommendationSummary } = useQuery({
    queryKey: ['vendor-recommendation', procurementRequestId],
    queryFn: () => getRecommendationSummary(procurementRequestId),
    enabled: !!procurementRequestId,
    retry: false, // 404 is normal if not yet evaluated
  });

  // 3. Fetch detailed evaluations
  const { data: evaluations = [] } = useQuery({
    queryKey: ['vendor-evaluations', procurementRequestId],
    queryFn: () => getVendorEvaluations(procurementRequestId),
    enabled: !!procurementRequestId,
    retry: false,
  });
  

  // Mutation: Submit Quote
  const addQuoteMutation = useMutation({
    mutationFn: (dto: CreateVendorQuoteDto) => submitVendorQuote(dto),
    onSuccess: () => {
      setFeedbackMessage('Vendor quote added successfully!');
      queryClient.invalidateQueries({ queryKey: ['vendor-quotes', procurementRequestId] });
      setIsAddQuoteOpen(false);
      setTimeout(() => setFeedbackMessage(null), 4000);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setErrorMessage(axiosErr.response?.data?.message ?? 'Failed to add quote.');
    },
  });

  // Mutation: Run Evaluation
  const evaluateMutation = useMutation({
    mutationFn: () => {
      const preset = EVALUATION_PRESETS.find((p) => p.id === selectedStrategy);
      return evaluateVendors({
        procurementRequestId,
        estimatedBudget,
        requiredDeliveryDays,
        customWeights: preset?.weights,
      });
    },
    onSuccess: (data) => {
      setFeedbackMessage(`Evaluated ${data.totalCandidatesEvaluated} candidate(s). Top recommendation generated!`);
      queryClient.invalidateQueries({ queryKey: ['vendor-recommendation', procurementRequestId] });
      queryClient.invalidateQueries({ queryKey: ['vendor-evaluations', procurementRequestId] });
      queryClient.invalidateQueries({ queryKey: ['procurement-request', procurementRequestId] });
      setTimeout(() => setFeedbackMessage(null), 5000);
    },
    onError: (err: unknown) => {
      const axiosErr = err as {
        response?: {
          status?: number;
          data?: { message?: string; detail?: string; title?: string };
        };
      };
      const status = axiosErr.response?.status;
      const detail =
        axiosErr.response?.data?.detail ??
        axiosErr.response?.data?.message ??
        axiosErr.response?.data?.title;

      if (status === 403) {
        setErrorMessage('Access Denied: Your user role is not authorized to run vendor evaluations.');
      } else {
        setErrorMessage(detail ? `Evaluation failed: ${detail}` : 'Failed to run evaluation.');
      }
    },
  });

  // Mutation: Run AI Evaluation (via the real agent/orchestrator workflow)
  const runAiEvaluationMutation = useMutation({
    mutationFn: async (objective: string) => {
      let workflowId: string | undefined;
      try {
        const workflow = await getWorkflowForRequest(procurementRequestId);
        workflowId = workflow?.workflowId;
      } catch {
        // No existing workflow yet; orchestrator will initialize automatically
      }

      return processAiWorkflow({
        objective,
        existingRequestId: procurementRequestId,
        workflowId,
      });
    },
    onSuccess: async (data) => {
      setAiObjective('');
      if (data.status === 'STAGE_COMPLETED' || data.status === 'COMPLETED') {
        setPendingClarification(null);
        setFeedbackMessage('AI evaluation completed. Recommendation generated!');
        await queryClient.invalidateQueries({ queryKey: ['vendor-recommendation', procurementRequestId] });
        await queryClient.invalidateQueries({ queryKey: ['vendor-evaluations', procurementRequestId] });
        setTimeout(() => setFeedbackMessage(null), 5000);
      } else if (data.status === 'NEEDS_USER_INPUT') {
        setPendingClarification({
          workflowId: data.workflowId,
          prompt: data.clarificationPrompt ?? 'The AI needs more information to evaluate vendors.',
        });
      } else if (data.status === 'FAILED') {
        setErrorMessage(sanitizeErrorString(data.errors?.[0] ?? 'AI evaluation failed.'));
      }
    },
    onError: (err: unknown) => {
      if (err instanceof Error && err.message.includes('evaluation stage')) {
        setErrorMessage(err.message);
        return;
      }
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
      if (axiosErr.response?.status === 404) {
        setErrorMessage('No AI workflow found for this request. It may need to go through Vendor Selection first.');
      } else {
        setErrorMessage(sanitizeErrorString(axiosErr.response?.data?.message ?? 'Failed to run AI evaluation.'));
      }
    },
  });

  // Mutation: Continue AI Evaluation (after user clarification)
  const continueAiEvaluationMutation = useMutation({
    mutationFn: () => {
      if (!pendingClarification) throw new Error('No pending clarification.');
      return processAiWorkflow({
        objective: clarificationAnswer,
        workflowId: pendingClarification.workflowId,
        existingRequestId: procurementRequestId,
      });
    },
    onSuccess: async (data) => {
      setClarificationAnswer('');
      if (data.status === 'STAGE_COMPLETED' || data.status === 'COMPLETED') {
        setPendingClarification(null);
        setFeedbackMessage('AI evaluation completed. Recommendation generated!');
        await queryClient.invalidateQueries({ queryKey: ['vendor-recommendation', procurementRequestId] });
        await queryClient.invalidateQueries({ queryKey: ['vendor-evaluations', procurementRequestId] });
        setTimeout(() => setFeedbackMessage(null), 5000);
      } else if (data.status === 'NEEDS_USER_INPUT') {
        setPendingClarification({
          workflowId: data.workflowId,
          prompt: data.clarificationPrompt ?? 'The AI needs more information.',
        });
      } else if (data.status === 'FAILED') {
        setPendingClarification(null);
        setErrorMessage(sanitizeErrorString(data.errors?.[0] ?? 'AI evaluation failed.'));
      }
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setErrorMessage(sanitizeErrorString(axiosErr.response?.data?.message ?? 'Failed to continue AI evaluation.'));
    },
  });

  // Mutation: Submit Recommendation for Approval
  const submitForApprovalMutation = useMutation({
    mutationFn: async () => {
      await updateProcurementRequestStatus(procurementRequestId, 'PENDING_APPROVAL');
      try {
        const wf = await getApprovalWorkflowByRequestId(procurementRequestId);
        if (wf && wf.currentStatus !== 'WAITING_MANAGER_APPROVAL' && wf.currentStatus !== 'APPROVED') {
          await transitionApprovalWorkflow(wf.id, 'WAITING_MANAGER_APPROVAL');
        }
      } catch {
        // Safe fallback if approval workflow is not yet initialized or already transitioned
      }
    },
    onSuccess: () => {
      setFeedbackMessage('Recommendation submitted for manager approval!');
      queryClient.invalidateQueries({ queryKey: ['procurement-request', procurementRequestId] });
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
      queryClient.invalidateQueries({ queryKey: ['approval-workflow', procurementRequestId] });
      setTimeout(() => setFeedbackMessage(null), 5000);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setErrorMessage(axiosErr.response?.data?.message ?? 'Failed to submit recommendation for approval.');
    },
  });

  const canManageQuotes =
    userRole === 'PROCUREMENT_OFFICER' || userRole === 'MANAGER' || userRole === 'ADMIN';

  const topQuote = quotes.find(
    (q) => q.vendorId.toLowerCase() === recommendationSummary?.topRecommendedVendorId?.toLowerCase()
  );

  return (
    <div className={styles.container}>
      <div className={styles.sectionHeader}>
        <h3 className={styles.sectionTitle}>
          <span>⚖️</span>
          <span>Vendor Evaluation & Recommendation</span>
        </h3>

        {canManageQuotes && (
          <div className={styles.actionButtons}>
            <div className={styles.strategySelectWrapper}>
              <label htmlFor="strategy-select" className={styles.strategyLabel}>
                Priority:
              </label>
              <select
                id="strategy-select"
                className={styles.strategySelect}
                value={selectedStrategy}
                onChange={(e) => setSelectedStrategy(e.target.value)}
                disabled={evaluateMutation.isPending}
              >
                {EVALUATION_PRESETS.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.label}
                  </option>
                ))}
              </select>
            </div>

            <button
              type="button"
              className={styles.addQuoteBtn}
              onClick={() => setIsAddQuoteOpen(true)}
            >
              <span>+</span> Add Candidate Quote
            </button>

            <button
              type="button"
              className={styles.addQuoteBtn}
              onClick={() => evaluateMutation.mutate()}
              disabled={evaluateMutation.isPending || quotes.length === 0}
            >
              <span>📊</span>
              {evaluateMutation.isPending ? 'Evaluating…' : 'Run Deterministic Evaluation'}
            </button>

              <div className={styles.aiObjectiveWrapper}>
                <input
                  type="text"
                  className={styles.aiObjectiveInput}
                  value={aiObjective}
                  onChange={(e) => setAiObjective(e.target.value)}
                  placeholder="Ask the AI — e.g. 'evaluate the quotes' or 'show current recommendation'"
                  disabled={runAiEvaluationMutation.isPending || quotes.length === 0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && aiObjective.trim() && !runAiEvaluationMutation.isPending) {
                      runAiEvaluationMutation.mutate(buildObjectiveWithPriority(aiObjective.trim()));
                    }
                  }}
                />
                <button
                  type="button"
                  className={styles.evaluateBtn}
                  onClick={() => runAiEvaluationMutation.mutate(buildObjectiveWithPriority(aiObjective.trim()))}
                  disabled={runAiEvaluationMutation.isPending || quotes.length === 0 || !aiObjective.trim()}
                >
                  <span>🤖</span>
                  {runAiEvaluationMutation.isPending ? 'AI Working…' : 'Ask AI'}
                </button>
              </div>
          </div>
        )}
      </div>

      {feedbackMessage && (
        <div className={styles.feedbackBanner}>
          <span>✓ {feedbackMessage}</span>
          <button
            type="button"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#166534' }}
            onClick={() => setFeedbackMessage(null)}
          >
            ✕
          </button>
        </div>
      )}

      {errorMessage && (
        <div className={styles.errorBanner}>
          <span>⚠️ {errorMessage}</span>
          <button
            type="button"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#991b1b', float: 'right' }}
            onClick={() => setErrorMessage(null)}
          >
            ✕
          </button>
          {errorMessage.includes('temporarily unavailable') && (
            <div style={{ marginTop: '0.6rem' }}>
              <button
                type="button"
                className={styles.addQuoteBtn}
                onClick={() => runAiEvaluationMutation.mutate(aiObjective.trim() || 'Evaluate the submitted vendor quotes and generate a ranked recommendation.')}
                disabled={runAiEvaluationMutation.isPending}
              >
                Retry AI Evaluation
              </button>
            </div>
          )}
        </div>
      )}

      {pendingClarification && (
        <div className={styles.clarificationCard}>
          <h4 className={styles.clarificationTitle}>🤖 AI Needs Clarification</h4>
          <p className={styles.clarificationPrompt}>{pendingClarification.prompt}</p>
          <div className={styles.clarificationField}>
            <textarea
              className={styles.clarificationTextarea}
              rows={3}
              value={clarificationAnswer}
              onChange={(e) => setClarificationAnswer(e.target.value)}
              placeholder="Provide the requested information…"
              disabled={continueAiEvaluationMutation.isPending}
            />
          </div>
          <div className={styles.clarificationActions}>
            <button
              type="button"
              className={styles.evaluateBtn}
              onClick={() => continueAiEvaluationMutation.mutate()}
              disabled={continueAiEvaluationMutation.isPending || !clarificationAnswer.trim()}
            >
              {continueAiEvaluationMutation.isPending ? 'Sending…' : 'Send Answer'}
            </button>
          </div>
        </div>
      )}

      {/* Top Recommendation Highlight Card */}
      {recommendationSummary && (
        <RecommendationCard
          summary={recommendationSummary}
          topQuote={topQuote}
          requestStatus={requestStatus}
          userRole={userRole}
          onSubmitForApproval={async () => {
            await submitForApprovalMutation.mutateAsync();
          }}
          isSubmitting={submitForApprovalMutation.isPending}
          onViewBreakdown={() => {
            if (recommendationSummary.rankedEvaluations.length > 0) {
              setSelectedEvaluation(recommendationSummary.rankedEvaluations[0]);
              setSelectedVendorName(topQuote?.vendorName || '');
            }
          }}
        />
      )}

      {/* Quotes & Comparison Matrix Table (Officers / Managers / Admins only) */}
      {canManageQuotes && (
        <VendorComparisonTable
          quotes={quotes}
          evaluations={evaluations}
          estimatedBudget={estimatedBudget}
          requiredDeliveryDays={requiredDeliveryDays}
          onSelectEvaluation={(evalItem, qName) => {
            setSelectedEvaluation(evalItem);
            setSelectedVendorName(qName);
          }}
        />
      )}

      {/* Add Quote Modal */}
      <AddQuoteModal
        procurementRequestId={procurementRequestId}
        isOpen={isAddQuoteOpen}
        onClose={() => setIsAddQuoteOpen(false)}
        onSubmit={async (dto) => {
          await addQuoteMutation.mutateAsync(dto);
        }}
        isSubmitting={addQuoteMutation.isPending}
      />

      {/* Criterion Breakdown Modal */}
      <CriterionScoreModal
        evaluation={selectedEvaluation}
        vendorName={selectedVendorName}
        isOpen={!!selectedEvaluation}
        onClose={() => setSelectedEvaluation(null)}
      />
    </div>
  );
}
