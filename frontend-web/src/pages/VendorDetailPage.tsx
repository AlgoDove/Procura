import { useParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getVendor, activateVendor, deactivateVendor } from '../api/endpoints';
import { formatDate, formatStatus } from '../utils/formatters';
import styles from './RequestDetailPage.module.css';
import vendorStyles from './VendorDetailPage.module.css';

const VENDOR_STATUS_COLORS: Record<string, string> = {
  ACTIVE: '#16a34a',
  INACTIVE: '#64748b',
};

function RatingStars({ rating }: { rating: number }) {
  const rounded = Math.round(Math.max(0, Math.min(5, rating)));
  return (
    <span className={vendorStyles.rating} aria-label={`Rating ${rating.toFixed(1)} out of 5`}>
      <span className={vendorStyles.stars} aria-hidden="true">
        {'★'.repeat(rounded)}
        <span className={vendorStyles.starsEmpty}>{'★'.repeat(5 - rounded)}</span>
      </span>
      <span className={vendorStyles.ratingValue}>{rating.toFixed(1)} / 5</span>
    </span>
  );
}

export default function VendorDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const { data: vendor, isLoading, error } = useQuery({
    queryKey: ['vendor', id],
    queryFn: () => getVendor(id!),
    enabled: !!id,
  });

  const canManage =
    user?.role === 'PROCUREMENT_OFFICER' ||
    user?.role === 'MANAGER' ||
    user?.role === 'ADMIN';

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['vendor', id] });
    queryClient.invalidateQueries({ queryKey: ['vendors'] });
  };

  const activateMutation = useMutation({
    mutationFn: () => activateVendor(id!),
    onSuccess: invalidate,
  });

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateVendor(id!),
    onSuccess: invalidate,
  });

  if (isLoading) return <div className={styles.container}><p className={styles.state}>Loading vendor…</p></div>;
  if (error || !vendor) {
    return (
      <div className={styles.container}>
        <Link to="/vendors" className={styles.backLink}>← All Vendors</Link>
        <p className={styles.errorState}>Vendor not found.</p>
      </div>
    );
  }

  const mutationError = activateMutation.isError || deactivateMutation.isError;

  return (
    <div className={styles.container}>
      <Link to="/vendors" className={styles.backLink}>← All Vendors</Link>

      {mutationError && (
        <div className={styles.bannerError}>Failed to update vendor status. Please try again.</div>
      )}

      {/* Header card */}
      <div className={styles.headerCard}>
        <div className={styles.headerTop}>
          <div>
            <div className={vendorStyles.metaRow}>
              <span className={vendorStyles.categoryChip}>{vendor.category}</span>
              <span className={styles.createdDate}>Added {formatDate(vendor.createdAt)}</span>
            </div>
            <h1 className={styles.title}>{vendor.name}</h1>
          </div>
          <span
            className={styles.statusBadge}
            style={{ background: VENDOR_STATUS_COLORS[vendor.status] ?? '#64748b' }}
          >
            {formatStatus(vendor.status)}
          </span>
        </div>

        {canManage && (
          <div className={styles.actionBar}>
            <Link to={`/vendors/${id}/edit`} className={styles.btnSecondary}>
              Edit Vendor
            </Link>
            {vendor.status === 'ACTIVE' && (
              <button
                type="button"
                className={styles.btnDanger}
                onClick={() => deactivateMutation.mutate()}
                disabled={deactivateMutation.isPending}
              >
                {deactivateMutation.isPending ? 'Deactivating…' : 'Deactivate'}
              </button>
            )}
            {vendor.status === 'INACTIVE' && (
              <button
                type="button"
                className={styles.btnApprove}
                onClick={() => activateMutation.mutate()}
                disabled={activateMutation.isPending}
              >
                {activateMutation.isPending ? 'Activating…' : 'Activate'}
              </button>
            )}
          </div>
        )}
      </div>

      {/* Contact information */}
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Contact Information</h2>
        <div className={styles.grid}>
          <div className={styles.gridItem}>
            <label>Contact Person</label>
            <span>{vendor.contactPerson}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Email</label>
            <span>
              <a href={`mailto:${vendor.email}`} className={vendorStyles.contactLink}>{vendor.email}</a>
            </span>
          </div>
          <div className={styles.gridItem}>
            <label>Phone</label>
            <span>
              <a href={`tel:${vendor.phoneNumber}`} className={vendorStyles.contactLink}>{vendor.phoneNumber}</a>
            </span>
          </div>
        </div>
        <div className={styles.textAreaBlock}>
          <label>Address</label>
          <p>{vendor.address || '—'}</p>
        </div>
      </section>

      {/* Vendor details */}
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Vendor Details</h2>
        <div className={styles.grid}>
          <div className={styles.gridItem}>
            <label>Category</label>
            <span>{vendor.category}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Rating</label>
            <span><RatingStars rating={vendor.rating} /></span>
          </div>
          <div className={styles.gridItem}>
            <label>Created</label>
            <span>{formatDate(vendor.createdAt)}</span>
          </div>
          <div className={styles.gridItem}>
            <label>Last Updated</label>
            <span>{formatDate(vendor.updatedAt)}</span>
          </div>
        </div>
      </section>
    </div>
  );
}
