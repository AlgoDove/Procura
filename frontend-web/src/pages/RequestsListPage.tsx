import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getProcurementRequests } from '../api/endpoints';
import type { Priority, RequestStatus } from '../types/api';
import {
  formatStatus,
  formatPriority,
  formatTotal,
  formatDate,
} from '../utils/formatters';
import styles from './RequestsListPage.module.css';

const STATUS_COLORS: Record<string, string> = {
  DRAFT: '#475569',
  SUBMITTED: '#0369a1',
  UNDER_EVALUATION: '#6d28d9',
  PENDING_APPROVAL: '#b45309',
  APPROVED: '#15803d',
  REJECTED: '#b91c1c',
  REVISION_REQUESTED: '#c2410c',
  COMPLETED: '#0f766e',
};

const STATUS_OPTIONS: RequestStatus[] = [
  'DRAFT',
  'SUBMITTED',
  'UNDER_EVALUATION',
  'PENDING_APPROVAL',
  'APPROVED',
  'REJECTED',
  'REVISION_REQUESTED',
  'COMPLETED',
];

const PRIORITY_OPTIONS: Priority[] = ['LOW', 'MEDIUM', 'HIGH', 'URGENT'];

export default function RequestsListPage() {
  const { user } = useAuth();
  const { data: requests = [], isLoading, error } = useQuery({
    queryKey: ['procurement-requests'],
    queryFn: getProcurementRequests,
  });

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [priorityFilter, setPriorityFilter] = useState<string>('');

  const isEmployee = user?.role === 'EMPLOYEE';
  const canCreate = isEmployee;

  const filtered = requests.filter((r) => {
    const matchSearch =
      !search ||
      r.title.toLowerCase().includes(search.toLowerCase()) ||
      r.requestNumber.toLowerCase().includes(search.toLowerCase());
    const matchStatus = !statusFilter || r.status === statusFilter;
    const matchPriority = !priorityFilter || r.priority === priorityFilter;
    return matchSearch && matchStatus && matchPriority;
  });

  const getSubtitle = () => {
    if (isEmployee) return 'Track and manage your procurement requests.';
    if (user?.role === 'PROCUREMENT_OFFICER')
      return 'Review, evaluate, and progress submitted procurement requests.';
    if (user?.role === 'ADMIN')
      return 'Oversee procurement requests and process pending approvals.';
    return 'Manage procurement requests.';
  };

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <div>
          <h1 className={styles.heading}>
            {isEmployee ? 'My Procurement Requests' : 'Procurement Requests'}
          </h1>
          <p className={styles.description}>{getSubtitle()}</p>
        </div>
        {canCreate && (
          <Link to="/requests/new" className={styles.btnPrimary}>
            + New Request
          </Link>
        )}
      </div>

      <div className={styles.filters}>
        <input
          type="search"
          placeholder="Search by title or request number…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className={styles.searchInput}
          aria-label="Search requests"
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className={styles.select}
          aria-label="Filter by status"
        >
          <option value="">All Statuses</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {formatStatus(s)}
            </option>
          ))}
        </select>
        <select
          value={priorityFilter}
          onChange={(e) => setPriorityFilter(e.target.value)}
          className={styles.select}
          aria-label="Filter by priority"
        >
          <option value="">All Priorities</option>
          {PRIORITY_OPTIONS.map((p) => (
            <option key={p} value={p}>
              {formatPriority(p)}
            </option>
          ))}
        </select>
      </div>

      {isLoading && <div className={styles.state}>Loading procurement requests…</div>}
      {error && (
        <div className={styles.errorState}>
          Failed to load procurement requests. Please check your connection and try again.
        </div>
      )}

      {!isLoading && !error && filtered.length === 0 && (
        <div className={styles.emptyState}>
          <p>No procurement requests found matching the current filters.</p>
          {canCreate && requests.length === 0 && (
            <Link to="/requests/new" className={styles.btnPrimary}>
              Create your first request
            </Link>
          )}
        </div>
      )}

      {!isLoading && filtered.length > 0 && (
        <div className={styles.tableCard}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th className={styles.colReqNum}>Request Number</th>
                <th className={styles.colTitle}>Title</th>
                <th className={styles.colPriority}>Priority</th>
                <th className={styles.colStatus}>Status</th>
                <th className={styles.colNumeric}>Estimated Total</th>
                <th className={styles.colDate}>Required By</th>
                <th className={styles.colDateLast}>Last Updated</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((r) => (
                <tr key={r.id}>
                  <td className={styles.colReqNum}>
                    <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                      {r.requestNumber}
                    </Link>
                  </td>
                  <td className={styles.colTitle}>
                    <Link to={`/requests/${r.id}`} className={styles.reqLink}>
                      {r.title}
                    </Link>
                  </td>
                  <td className={styles.colPriority}>
                    <span className={styles.priorityBadge}>
                      {formatPriority(r.priority)}
                    </span>
                  </td>
                  <td className={styles.colStatus}>
                    <span
                      className={styles.badge}
                      style={{ background: STATUS_COLORS[r.status] ?? '#475569' }}
                    >
                      {formatStatus(r.status)}
                    </span>
                  </td>
                  <td className={styles.colNumeric}>{formatTotal(r.estimatedTotal)}</td>
                  <td className={styles.colDate}>{formatDate(r.requiredByDate)}</td>
                  <td className={styles.colDateLast}>{formatDate(r.updatedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
