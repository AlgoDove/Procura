import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getPendingApprovals } from '../../api/endpoints';
import { formatTotal, formatDate } from '../../utils/formatters';
import styles from './PendingApprovalsPage.module.css';

export default function PendingApprovalsPage() {
  const [search, setSearch] = useState('');

  const { data: workflows = [], isLoading, error } = useQuery({
    queryKey: ['pending-approvals'],
    queryFn: getPendingApprovals,
  });

  const filtered = workflows.filter((w) => {
    if (!search) return true;
    const query = search.toLowerCase();
    return (
      w.requestNumber.toLowerCase().includes(query) ||
      w.requestTitle.toLowerCase().includes(query) ||
      w.requesterName.toLowerCase().includes(query)
    );
  });

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <div>
          <h1 className={styles.heading}>
            Pending Approvals
            {workflows.length > 0 && (
              <span className={styles.badgePending}>{workflows.length} Action Needed</span>
            )}
          </h1>
          <p className={styles.description}>
            Review procurement requests and vendor evaluation recommendations awaiting your managerial approval decision.
          </p>
        </div>
      </div>

      <div className={styles.filters}>
        <input
          type="search"
          placeholder="Search by request number, title, or requester…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className={styles.searchInput}
          aria-label="Search pending approvals"
        />
      </div>

      {isLoading && <div className={styles.state}>Loading pending approval requests…</div>}
      {error && (
        <div className={styles.errorState}>
          Failed to load pending approvals. Please ensure you have appropriate managerial permissions.
        </div>
      )}

      {!isLoading && !error && filtered.length === 0 && (
        <div className={styles.emptyState}>
          <p>
            {search
              ? 'No pending approval requests match your search criteria.'
              : 'You have no procurement requests currently awaiting your approval.'}
          </p>
        </div>
      )}

      {!isLoading && filtered.length > 0 && (
        <div className={styles.tableCard}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Request Number</th>
                <th>Title</th>
                <th>Requester</th>
                <th>Top Recommended Vendor</th>
                <th className={styles.colNumeric}>Estimated Total</th>
                <th>Submitted Date</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((w) => (
                <tr key={w.id}>
                  <td>
                    <Link to={`/requests/${w.procurementRequestId}`} className={styles.reqLink}>
                      {w.requestNumber}
                    </Link>
                  </td>
                  <td>
                    <Link to={`/requests/${w.procurementRequestId}`} className={styles.reqLink}>
                      {w.requestTitle}
                    </Link>
                  </td>
                  <td>{w.requesterName}</td>
                  <td>
                    {w.vendorRecommendationSummary?.topScore !== undefined ? (
                      <span className={styles.badgeTopVendor}>
                        Score: {w.vendorRecommendationSummary.topScore.toFixed(1)}
                      </span>
                    ) : (
                      <span style={{ color: '#94a3b8' }}>—</span>
                    )}
                  </td>
                  <td className={styles.colNumeric}>{formatTotal(w.estimatedTotal)}</td>
                  <td>{formatDate(w.createdAt)}</td>
                  <td>
                    <Link
                      to={`/requests/${w.procurementRequestId}`}
                      className={styles.btnAction}
                    >
                      Review & Decide →
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
