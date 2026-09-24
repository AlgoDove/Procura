import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getProcurementRequests } from '../api/endpoints';
import type { RequestStatus } from '../types/api';
import styles from './DashboardPage.module.css';

const STATUS_COLORS: Record<RequestStatus, string> = {
  DRAFT: '#888',
  SUBMITTED: '#2980b9',
  UNDER_EVALUATION: '#8e44ad',
  PENDING_APPROVAL: '#e67e22',
  APPROVED: '#27ae60',
  REJECTED: '#c0392b',
  REVISION_REQUESTED: '#d35400',
  COMPLETED: '#1abc9c',
};

export default function DashboardPage() {
  const { user } = useAuth();
  const { data: requests = [], isLoading } = useQuery({
    queryKey: ['procurement-requests'],
    queryFn: getProcurementRequests,
  });

  // Count by status from loaded data — no invented metrics
  const countByStatus = requests.reduce<Partial<Record<RequestStatus, number>>>((acc, r) => {
    acc[r.status] = (acc[r.status] ?? 0) + 1;
    return acc;
  }, {});

  const needsAttention = requests.filter(
    (r) => r.status === 'REVISION_REQUESTED' || r.status === 'DRAFT'
  );

  const recent = [...requests]
    .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
    .slice(0, 5);

  return (
    <div>
      <h1 className={styles.heading}>Welcome, {user?.email}</h1>
      <p className={styles.sub}>Role: {user?.role}</p>

      {isLoading ? (
        <p>Loading…</p>
      ) : (
        <>
          {/* Status summary */}
          <section className={styles.section}>
            <h2>Request status summary</h2>
            <div className={styles.chips}>
              {(Object.entries(countByStatus) as [RequestStatus, number][]).map(([status, count]) => (
                <span
                  key={status}
                  className={styles.chip}
                  style={{ borderColor: STATUS_COLORS[status] }}
                >
                  <span style={{ color: STATUS_COLORS[status] }}>{status}</span>
                  <strong>{count}</strong>
                </span>
              ))}
              {requests.length === 0 && <span className={styles.empty}>No requests yet.</span>}
            </div>
          </section>

          {/* Needs attention */}
          {needsAttention.length > 0 && (
            <section className={styles.section}>
              <h2>Needs attention</h2>
              <ul className={styles.list}>
                {needsAttention.map((r) => (
                  <li key={r.id} className={styles.listItem}>
                    <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                      {r.requestNumber} — {r.title}
                    </Link>
                    <span
                      className={styles.badge}
                      style={{ background: STATUS_COLORS[r.status] }}
                    >
                      {r.status}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {/* Recent requests */}
          <section className={styles.section}>
            <h2>
              Recent requests &nbsp;
              <Link to="/requests" className={styles.viewAll}>View all →</Link>
            </h2>
            {recent.length === 0 ? (
              <p className={styles.empty}>
                No requests yet.{' '}
                <Link to="/requests/new">Create your first request</Link>
              </p>
            ) : (
              <ul className={styles.list}>
                {recent.map((r) => (
                  <li key={r.id} className={styles.listItem}>
                    <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                      {r.requestNumber} — {r.title}
                    </Link>
                    <span
                      className={styles.badge}
                      style={{ background: STATUS_COLORS[r.status] }}
                    >
                      {r.status}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}
