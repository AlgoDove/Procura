import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getProcurementRequests } from '../api/endpoints';
import type { Priority, RequestStatus } from '../types/api';
import styles from './RequestsListPage.module.css';

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

const PRIORITY_LABELS: Record<Priority, string> = {
  LOW: '⬇ Low',
  MEDIUM: '➡ Medium',
  HIGH: '⬆ High',
  URGENT: '🔴 Urgent',
};

export default function RequestsListPage() {
  const { user } = useAuth();
  const { data: requests = [], isLoading, error } = useQuery({
    queryKey: ['procurement-requests'],
    queryFn: getProcurementRequests,
  });

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [priorityFilter, setPriorityFilter] = useState<string>('');

  const canCreate = user?.role === 'EMPLOYEE' || user?.role === 'PROCUREMENT_OFFICER' || user?.role === 'ADMIN';

  const filtered = requests.filter((r) => {
    const matchSearch =
      !search ||
      r.title.toLowerCase().includes(search.toLowerCase()) ||
      r.requestNumber.toLowerCase().includes(search.toLowerCase());
    const matchStatus = !statusFilter || r.status === statusFilter;
    const matchPriority = !priorityFilter || r.priority === priorityFilter;
    return matchSearch && matchStatus && matchPriority;
  });

  return (
    <div>
      <div className={styles.toolbar}>
        <h1 className={styles.heading}>Procurement Requests</h1>
        {canCreate && (
          <Link to="/requests/new" className={styles.btnPrimary}>
            + New Request
          </Link>
        )}
      </div>

      <div className={styles.filters}>
        <input
          type="search"
          placeholder="Search by title or number…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className={styles.searchInput}
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className={styles.select}
        >
          <option value="">All statuses</option>
          {(['DRAFT','SUBMITTED','UNDER_EVALUATION','PENDING_APPROVAL','APPROVED','REJECTED','REVISION_REQUESTED','COMPLETED'] as RequestStatus[]).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <select
          value={priorityFilter}
          onChange={(e) => setPriorityFilter(e.target.value)}
          className={styles.select}
        >
          <option value="">All priorities</option>
          {(['LOW','MEDIUM','HIGH','URGENT'] as Priority[]).map((p) => (
            <option key={p} value={p}>{p}</option>
          ))}
        </select>
      </div>

      {isLoading && <p className={styles.state}>Loading…</p>}
      {error && <p className={styles.errorState}>Failed to load requests. Please try again.</p>}

      {!isLoading && !error && filtered.length === 0 && (
        <div className={styles.emptyState}>
          <p>No requests found.</p>
          {canCreate && requests.length === 0 && (
            <Link to="/requests/new" className={styles.btnPrimary}>
              Create your first request
            </Link>
          )}
        </div>
      )}

      {!isLoading && filtered.length > 0 && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Number</th>
              <th>Title</th>
              <th>Priority</th>
              <th>Status</th>
              <th>Estimated Total</th>
              <th>Required By</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((r) => (
              <tr key={r.id}>
                <td>
                  <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                    {r.requestNumber}
                  </Link>
                </td>
                <td>
                  <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                    {r.title}
                  </Link>
                </td>
                <td>{PRIORITY_LABELS[r.priority] ?? r.priority}</td>
                <td>
                  <span
                    className={styles.badge}
                    style={{ background: STATUS_COLORS[r.status] ?? '#888' }}
                  >
                    {r.status}
                  </span>
                </td>
                <td>${r.estimatedTotal.toFixed(2)}</td>
                <td>{new Date(r.requiredByDate).toLocaleDateString()}</td>
                <td>{new Date(r.updatedAt).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
