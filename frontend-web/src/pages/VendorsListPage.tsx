import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getVendors, activateVendor, deactivateVendor } from '../api/endpoints';
import { useAuth } from '../context/AuthContext';
import styles from './VendorsPage.module.css';

export default function VendorsListPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [categoryFilter, setCategoryFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: vendors = [], isLoading, error } = useQuery({
    queryKey: ['vendors'],
    queryFn: () => getVendors(),
  });

  const canManage =
    user?.role === 'PROCUREMENT_OFFICER' || user?.role === 'ADMIN';

  const activateMutation = useMutation({
    mutationFn: (id: string) => activateVendor(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vendors'] }),
    onError: () => setActionError('Failed to activate vendor.'),
  });

  const deactivateMutation = useMutation({
    mutationFn: (id: string) => deactivateVendor(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vendors'] }),
    onError: () => setActionError('Failed to deactivate vendor.'),
  });

  const categories = [...new Set(vendors.map((v) => v.category))].sort();

  const filtered = vendors.filter((v) => {
    const matchCat = !categoryFilter || v.category === categoryFilter;
    const matchStatus = !statusFilter || v.status === statusFilter;
    return matchCat && matchStatus;
  });

  return (
    <div>
      <div className={styles.toolbar}>
        <h1 className={styles.heading}>Vendors</h1>
        {canManage && (
          <Link to="/vendors/new" className={styles.btnPrimary}>
            + New Vendor
          </Link>
        )}
      </div>

      <div className={styles.filters}>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className={styles.select}
        >
          <option value="">All statuses</option>
          <option value="ACTIVE">Active</option>
          <option value="INACTIVE">Inactive</option>
        </select>
        <select
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
          className={styles.select}
        >
          <option value="">All categories</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
      </div>

      {actionError && <div className={styles.errorBanner}>{actionError}</div>}

      {isLoading && <p className={styles.state}>Loading…</p>}
      {error && <p className={styles.errorState}>Failed to load vendors.</p>}

      {!isLoading && filtered.length === 0 && (
        <div className={styles.emptyState}>
          <p>No vendors found.</p>
          {canManage && vendors.length === 0 && (
            <Link to="/vendors/new" className={styles.btnPrimary}>Add first vendor</Link>
          )}
        </div>
      )}

      {!isLoading && filtered.length > 0 && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Name</th>
              <th>Category</th>
              <th>Contact Email</th>
              <th>Status</th>
              <th>Rating</th>
              {canManage && <th>Actions</th>}
            </tr>
          </thead>
          <tbody>
            {filtered.map((v) => (
              <tr key={v.id}>
                <td>
                  <Link to={`/vendors/${v.id}`} className={styles.link}>{v.name}</Link>
                </td>
                <td>{v.category}</td>
                <td>{v.contactEmail}</td>
                <td>
                  <span
                    className={styles.badge}
                    style={{ background: v.status === 'ACTIVE' ? '#27ae60' : '#888' }}
                  >
                    {v.status}
                  </span>
                </td>
                <td>{v.rating != null ? v.rating.toFixed(1) : '—'}</td>
                {canManage && (
                  <td className={styles.actionsCell}>
                    <Link to={`/vendors/${v.id}/edit`} className={styles.btnSmall}>Edit</Link>
                    {v.status === 'ACTIVE' ? (
                      <button
                        className={`${styles.btnSmall} ${styles.btnDanger}`}
                        onClick={() => deactivateMutation.mutate(v.id)}
                        disabled={deactivateMutation.isPending}
                      >
                        Deactivate
                      </button>
                    ) : (
                      <button
                        className={`${styles.btnSmall} ${styles.btnSuccess}`}
                        onClick={() => activateMutation.mutate(v.id)}
                        disabled={activateMutation.isPending}
                      >
                        Activate
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
