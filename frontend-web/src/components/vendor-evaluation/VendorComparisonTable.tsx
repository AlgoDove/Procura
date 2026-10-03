import { useMemo } from 'react';
import type {
  VendorQuoteResponseDto,
  VendorEvaluationResponseDto,
} from '../../types/api';
import { formatPrice } from '../../utils/formatters';
import styles from './VendorComparisonTable.module.css';

interface VendorComparisonTableProps {
  quotes: VendorQuoteResponseDto[];
  evaluations: VendorEvaluationResponseDto[];
  estimatedBudget?: number;
  requiredDeliveryDays?: number;
  onSelectEvaluation: (evalItem: VendorEvaluationResponseDto, quoteName: string) => void;
}

export default function VendorComparisonTable({
  quotes,
  evaluations,
  estimatedBudget,
  requiredDeliveryDays,
  onSelectEvaluation,
}: VendorComparisonTableProps) {
  // Map evaluations by vendorId for fast lookup
  const evaluationMap = useMemo(() => {
    const map = new Map<string, VendorEvaluationResponseDto>();
    for (const ev of evaluations) {
      map.set(ev.vendorId.toLowerCase(), ev);
    }
    return map;
  }, [evaluations]);

  // Merge quotes and evaluations, sorted by rank (if evaluated) or by price
  const mergedRows = useMemo(() => {
    return [...quotes].sort((a, b) => {
      const evalA = evaluationMap.get(a.vendorId.toLowerCase());
      const evalB = evaluationMap.get(b.vendorId.toLowerCase());
      if (evalA && evalB) {
        return evalA.rank - evalB.rank;
      }
      if (evalA) return -1;
      if (evalB) return 1;
      return a.quotedPrice - b.quotedPrice;
    });
  }, [quotes, evaluationMap]);

  if (quotes.length === 0) {
    return (
      <div className={styles.tableWrapper}>
        <div className={styles.emptyState}>
          <div className={styles.emptyIcon}>📦</div>
          <div style={{ fontWeight: 600, color: '#334155' }}>No candidate quotes submitted yet</div>
          <div style={{ fontSize: '0.85rem', marginTop: '0.25rem' }}>
            Click &quot;Add Candidate Quote&quot; to enter quotes from suppliers.
          </div>
        </div>
      </div>
    );
  }

  const getRankBadgeClass = (rank?: number) => {
    if (rank === 1) return styles.rankBadge1;
    if (rank === 2) return styles.rankBadge2;
    if (rank === 3) return styles.rankBadge3;
    return '';
  };

  const getLeadTimePill = (days: number) => {
    if (!requiredDeliveryDays || requiredDeliveryDays <= 0) return null;
    if (days <= requiredDeliveryDays - 2) {
      return <span className={`${styles.leadTimePill} ${styles.leadTimeGood}`}>✓ Fast</span>;
    }
    if (days <= requiredDeliveryDays) {
      return <span className={`${styles.leadTimePill} ${styles.leadTimeGood}`}>On Time</span>;
    }
    return <span className={`${styles.leadTimePill} ${styles.leadTimeLate}`}>⚠️ Exceeds by {days - requiredDeliveryDays}d</span>;
  };

  return (
    <div className={styles.tableWrapper}>
      <div className={styles.tableHeaderBar}>
        <h4 className={styles.tableTitle}>
          Candidate Vendor Quotes & Comparison
          <span className={styles.countBadge}>{quotes.length} Quotes</span>
        </h4>
      </div>

      <div className={styles.tableResponsive}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Rank</th>
              <th>Candidate Vendor</th>
              <th>Quoted Price</th>
              <th>Lead Time</th>
              <th>Reliability</th>
              <th>Compliance</th>
              <th>Score</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {mergedRows.map((q) => {
              const evalData = evaluationMap.get(q.vendorId.toLowerCase());
              const isRank1 = evalData?.rank === 1;

              // Price variance calculation
              const variance = estimatedBudget && estimatedBudget > 0 ? q.quotedPrice - estimatedBudget : null;

              return (
                <tr key={q.id} className={isRank1 ? styles.rank1Row : undefined}>
                  <td>
                    {evalData ? (
                      <span className={`${styles.rankBadge} ${getRankBadgeClass(evalData.rank)}`}>
                        {isRank1 ? '🏆 #1' : `#${evalData.rank}`}
                      </span>
                    ) : (
                      <span className={styles.rankBadge}>—</span>
                    )}
                  </td>

                  <td>
                    <div className={styles.vendorCell}>
                      <span>{q.vendorName}</span>
                      {q.notes && <span className={styles.vendorNotes}>{q.notes}</span>}
                    </div>
                  </td>

                  <td>
                    <div className={styles.priceValue}>{formatPrice(q.quotedPrice)}</div>
                    {variance !== null && (
                      <div className={variance <= 0 ? styles.varianceUnder : styles.varianceOver}>
                        {variance <= 0
                          ? `-${formatPrice(Math.abs(variance))} (under budget)`
                          : `+${formatPrice(variance)} (over budget)`}
                      </div>
                    )}
                  </td>

                  <td>
                    <div className={styles.deliveryDays}>{q.estimatedDeliveryDays} Days</div>
                    {getLeadTimePill(q.estimatedDeliveryDays)}
                  </td>

                  <td>
                    <div className={styles.reliabilityBar}>
                      <span>⭐️</span>
                      <span>{q.reliabilityRating > 5 ? (q.reliabilityRating / 20).toFixed(1) : q.reliabilityRating.toFixed(1)}</span>
                      <span style={{ fontSize: '0.75rem', color: '#64748b' }}>/ 5</span>
                    </div>
                  </td>

                  <td>
                    <span
                      className={`${styles.compliancePill} ${
                        q.isComplianceApproved ? styles.complianceApproved : styles.compliancePending
                      }`}
                    >
                      {q.isComplianceApproved ? '✓ Approved' : 'Pending'}
                    </span>
                  </td>

                  <td>
                    {evalData ? (
                      <span
                        className={`${styles.scorePill} ${isRank1 ? styles.scorePillRank1 : ''}`}
                      >
                        {evalData.overallScore.toFixed(1)} / 100
                      </span>
                    ) : (
                      <span style={{ color: '#94a3b8', fontSize: '0.85rem' }}>Not evaluated</span>
                    )}
                  </td>

                  <td>
                    {evalData ? (
                      <button
                        type="button"
                        className={styles.viewDetailsBtn}
                        onClick={() => onSelectEvaluation(evalData, q.vendorName)}
                      >
                        View Breakdown
                      </button>
                    ) : (
                      <span style={{ color: '#94a3b8', fontSize: '0.8rem' }}>Run eval to score</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
