import type { VendorEvaluationResponseDto, EvaluationCriterionType } from '../../types/api';
import styles from './CriterionScoreModal.module.css';

interface CriterionScoreModalProps {
  evaluation: VendorEvaluationResponseDto | null;
  vendorName?: string;
  isOpen: boolean;
  onClose: () => void;
}

const CRITERION_LABEL_MAP: Record<EvaluationCriterionType, string> = {
  PRICE: '💰 Cost & Pricing Competitiveness',
  DELIVERY_TIME: '⏱️ Delivery Speed & Lead Time',
  RELIABILITY: '🛡️ Historical Reliability & Track Record',
  COMPLIANCE: '📋 Compliance & Quality Standards',
};

export default function CriterionScoreModal({
  evaluation,
  vendorName,
  isOpen,
  onClose,
}: CriterionScoreModalProps) {
  if (!isOpen || !evaluation) return null;

  const getProgressColorClass = (score: number) => {
    if (score >= 80) return styles.progressBarSuccess;
    if (score >= 60) return styles.progressBar;
    if (score >= 40) return styles.progressBarWarning;
    return styles.progressBarDanger;
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.header}>
          <div className={styles.headerLeft}>
            <span
              className={`${styles.rankBadge} ${
                evaluation.rank === 1 ? styles.rankBadgeRank1 : ''
              }`}
            >
              Rank #{evaluation.rank}
            </span>
            <h3>{vendorName || `Vendor ${evaluation.vendorId.slice(0, 8)}`}</h3>
          </div>
          <button type="button" className={styles.closeButton} onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        <div className={styles.body}>
          <div className={styles.overallScoreCard}>
            <div>
              <div className={styles.scoreLabel}>Evaluation Score</div>
              <div style={{ fontSize: '0.8rem', color: '#166534', marginTop: '0.2rem' }}>
                {evaluation.generatedByAgent ? '🤖 Calculated by AI Scoring Engine' : 'Manual Evaluation'}
              </div>
            </div>
            <div>
              <span className={styles.scoreValue}>{evaluation.overallScore.toFixed(1)}</span>
              <span className={styles.scoreOutOf}> / 100</span>
            </div>
          </div>

          <div>
            <div className={styles.sectionTitle}>Criterion Breakdown</div>
            <div className={styles.criteriaList}>
              {evaluation.criterionScores && evaluation.criterionScores.length > 0 ? (
                evaluation.criterionScores.map((c) => {
                  const label = CRITERION_LABEL_MAP[c.criterionName] || c.criterionName;
                  const weightPct = Math.round(c.weight * 100);
                  const colorClass = getProgressColorClass(c.score);

                  return (
                    <div key={c.id || c.criterionName} className={styles.criterionRow}>
                      <div className={styles.criterionHeader}>
                        <div>
                          <span className={styles.criterionName}>{label}</span>
                          <span className={styles.criterionWeight}>Weight: {weightPct}%</span>
                        </div>
                        <span className={styles.criterionScore}>{c.score.toFixed(1)} / 100</span>
                      </div>
                      <div className={styles.progressBarContainer}>
                        <div
                          className={`${styles.progressBar} ${colorClass}`}
                          style={{ width: `${Math.min(Math.max(c.score, 0), 100)}%` }}
                        />
                      </div>
                    </div>
                  );
                })
              ) : (
                <div style={{ color: '#64748b', fontSize: '0.85rem' }}>No individual criterion scores available.</div>
              )}
            </div>
          </div>

          {evaluation.reasoning && (
            <div>
              <div className={styles.sectionTitle}>AI Justification & Reasoning</div>
              <div className={styles.reasoningCard}>
                <p className={styles.reasoningText}>{evaluation.reasoning}</p>
              </div>
            </div>
          )}

          <div>
            <div className={styles.sectionTitle}>Risk Analysis & Flags</div>
            {evaluation.riskFlags && evaluation.riskFlags.length > 0 ? (
              <div className={styles.risksList}>
                {evaluation.riskFlags.map((risk, index) => (
                  <div key={index} className={styles.riskItem}>
                    <span>⚠️</span>
                    <span>{risk}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div className={styles.noRisks}>
                <span>✓</span>
                <span>No risk factors identified for this vendor candidate.</span>
              </div>
            )}
          </div>
        </div>

        <div className={styles.footer}>
          <button type="button" className={styles.closeBtn} onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
