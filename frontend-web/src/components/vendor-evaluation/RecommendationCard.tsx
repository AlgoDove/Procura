import type {
  ProcurementEvaluationSummaryDto,
  VendorQuoteResponseDto,
  RequestStatus,
  SystemRole,
} from '../../types/api';
import { formatPrice } from '../../utils/formatters';
import styles from './RecommendationCard.module.css';

interface RecommendationCardProps {
  summary: ProcurementEvaluationSummaryDto | null;
  topQuote?: VendorQuoteResponseDto;
  requestStatus: RequestStatus;
  userRole?: SystemRole;
  onSubmitForApproval: () => Promise<void>;
  isSubmitting: boolean;
  onViewBreakdown: () => void;
}

export default function RecommendationCard({
  summary,
  topQuote,
  requestStatus,
  userRole,
  onSubmitForApproval,
  isSubmitting,
  onViewBreakdown,
}: RecommendationCardProps) {
  if (!summary || !summary.topRecommendedVendorId || summary.rankedEvaluations.length === 0) {
    return null;
  }

  const topEval = summary.rankedEvaluations[0];
  let vendorDisplayName = topQuote?.vendorName;
  if (!vendorDisplayName) {
    const nameMatch = topEval.reasoning?.match(/^([A-Za-z0-9\s&.-]+?)\s+(?:achieved|offered|scored|was ranked)/i);
    vendorDisplayName = nameMatch ? nameMatch[1].trim() : `Vendor ${topEval.vendorId.slice(0, 8)}`;
  }

  // Fallback metric parsing if topQuote is not loaded in current scope
  let displayPrice = topQuote ? formatPrice(topQuote.quotedPrice) : '—';
  if (displayPrice === '—' && topEval.reasoning) {
    const priceMatch = topEval.reasoning.match(/\$([0-9,]+(?:\.[0-9]{2})?)/);
    if (priceMatch) displayPrice = `$${priceMatch[1]}`;
  }

  let displayLeadTime = topQuote ? `${topQuote.estimatedDeliveryDays} Days` : '—';
  if (displayLeadTime === '—' && topEval.reasoning) {
    const daysMatch = topEval.reasoning.match(/([0-9]+)\s*day\(s\)/i);
    if (daysMatch) displayLeadTime = `${daysMatch[1]} Days`;
  }

  let displayReliability = topQuote
    ? `${topQuote.reliabilityRating > 5 ? (topQuote.reliabilityRating / 20).toFixed(1) : topQuote.reliabilityRating.toFixed(1)} / 5.0`
    : '—';
  if (displayReliability === '—' && topEval.reasoning) {
    const relMatch = topEval.reasoning.match(/reliability rating of ([0-9]+(?:\.[0-9]+)?)\/100/i);
    if (relMatch) {
      const parsedScore = parseFloat(relMatch[1]);
      displayReliability = `${(parsedScore / 20).toFixed(1)} / 5.0`;
    }
  }

  const canSubmit =
    requestStatus === 'UNDER_EVALUATION' &&
    (userRole === 'PROCUREMENT_OFFICER' || userRole === 'MANAGER' || userRole === 'ADMIN');

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div>
          <div className={styles.badgeGroup}>
            <span className={styles.trophyBadge}>🏆 Top Recommended Vendor</span>
            <span className={styles.aiBadge}>
              {topEval.generatedByAgent ? 'AI Evaluated' : 'Deterministic Evaluation'}
            </span>
          </div>
          <h3 className={styles.vendorName}>{vendorDisplayName}</h3>
        </div>

        <div className={styles.scoreBox}>
          <span className={styles.scoreValue}>{topEval.overallScore.toFixed(1)}</span>
          <span className={styles.scoreLabel}>Overall Score / 100</span>
        </div>
      </div>

      <div className={styles.metricsGrid}>
        <div className={styles.metricItem}>
          <span className={styles.metricLabel}>Quoted Price</span>
          <span className={styles.metricValue}>{displayPrice}</span>
          <span className={styles.metricSub}>Rank #1 for overall value</span>
        </div>

        <div className={styles.metricItem}>
          <span className={styles.metricLabel}>Delivery Lead Time</span>
          <span className={styles.metricValue}>{displayLeadTime}</span>
          <span className={styles.metricSub}>Meets project schedule</span>
        </div>

        <div className={styles.metricItem}>
          <span className={styles.metricLabel}>Reliability Score</span>
          <span className={styles.metricValue}>{displayReliability}</span>
          <span className={styles.metricSub}>Historical vendor track record</span>
        </div>

        <div className={styles.metricItem}>
          <span className={styles.metricLabel}>Candidates Evaluated</span>
          <span className={styles.metricValue}>{summary.totalCandidatesEvaluated} Vendors</span>
          <span className={styles.metricSub}>
            {topEval.generatedByAgent ? 'AI-assisted scoring' : 'Deterministic scoring'}
          </span>
        </div>
      </div>

      {topEval.reasoning && (
        <div className={styles.justificationSection}>
          <div className={styles.justificationTitle}>
            <span>🤖</span>
            <span>Executive Recommendation Justification</span>
          </div>
          <p className={styles.justificationText}>{topEval.reasoning}</p>
        </div>
      )}

      <div className={styles.actionsBar}>
        <div>
          <button
            type="button"
            style={{
              background: '#f1f5f9',
              border: '1px solid #cbd5e1',
              padding: '0.5rem 1rem',
              borderRadius: '6px',
              fontSize: '0.85rem',
              fontWeight: 600,
              color: '#334155',
              cursor: 'pointer',
            }}
            onClick={onViewBreakdown}
          >
            📊 View Full Scoring Breakdown
          </button>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          {requestStatus === 'PENDING_APPROVAL' && (
            <span className={styles.statusNote}>
              <span>✓</span> Recommendation already submitted. Awaiting Manager decision.
            </span>
          )}

          {requestStatus === 'APPROVED' && (
            <span className={styles.statusNote} style={{ color: '#16a34a' }}>
              <span>✓</span> Request & Recommendation Approved!
            </span>
          )}

          {canSubmit && (
            <button
              type="button"
              className={styles.submitBtn}
              onClick={onSubmitForApproval}
              disabled={isSubmitting}
            >
              <span>🚀</span>
              <span>{isSubmitting ? 'Submitting…' : 'Submit Recommendation for Approval'}</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
