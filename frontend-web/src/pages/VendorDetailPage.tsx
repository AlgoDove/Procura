import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getVendor, activateVendor, deactivateVendor } from '../api/endpoints';
import styles from './RequestDetailPage.module.css';

export default function VendorDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: vendor, isLoading, error } = useQuery({
    queryKey: ['vendor', id],
    queryFn: () => getVendor(id!),
    enabled: !!id,
  });

  const canManage = user?.role === 'PROCUREMENT_OFFICER' || user?.role === 'ADMIN';

  const activateMutation = useMutation({
    mutationFn: () => activateVendor(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vendor', id] }),
  });

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateVendor(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vendor', id] }),
  });

  if (isLoading) return <p className={styles.state}>Loading…</p>;
  if (error || !vendor) return <p className={styles.errorState}>Vendor not found.</p>;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <Link to="/vendors" className={styles.back}>← All Vendors</Link>
          <h1 className={styles.heading}>{vendor.name}</h1>
          <span style={{ color: '#888', fontSize: '0.85rem' }}>{vendor.category}</span>
        </div>
        <span
          className={styles.statusBadge}
          style={{ background: vendor.status === 'ACTIVE' ? '#27ae60' : '#888' }}
        >
          {vendor.status}
        </span>
      </div>

      <div className={styles.actions}>
        {canManage && (
          <button
            onClick={() => navigate(`/vendors/${id}/edit`)}
            className={styles.btnSecondary}
          >
            Edit
          </button>
        )}
        {canManage && vendor.status === 'ACTIVE' && (
          <button
            className={styles.btnDanger}
            onClick={() => deactivateMutation.mutate()}
            disabled={deactivateMutation.isPending}
          >
            Deactivate
          </button>
        )}
        {canManage && vendor.status === 'INACTIVE' && (
          <button
            className={styles.btnPrimary}
            onClick={() => activateMutation.mutate()}
            disabled={activateMutation.isPending}
          >
            Activate
          </button>
        )}
      </div>

      <div className={styles.section}>
        <h2>Contact</h2>
        <dl className={styles.dl}>
          <dt>Email</dt><dd>{vendor.contactEmail}</dd>
          <dt>Phone</dt><dd>{vendor.contactPhone ?? '—'}</dd>
          <dt>Address</dt><dd>{vendor.address ?? '—'}</dd>
          <dt>Rating</dt><dd>{vendor.rating != null ? vendor.rating.toFixed(1) : '—'}</dd>
          <dt>Created</dt><dd>{new Date(vendor.createdAt).toLocaleDateString()}</dd>
          <dt>Updated</dt><dd>{new Date(vendor.updatedAt).toLocaleDateString()}</dd>
        </dl>
        {vendor.notes && (
          <div className={styles.longText}>
            <strong>Notes</strong>
            <p>{vendor.notes}</p>
          </div>
        )}
      </div>
    </div>
  );
}
