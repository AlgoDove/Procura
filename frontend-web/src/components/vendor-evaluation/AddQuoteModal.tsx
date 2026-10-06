import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getSelectedVendorsForRequest, getVendors } from '../../api/endpoints';
import type { CreateVendorQuoteDto } from '../../types/api';
import styles from './AddQuoteModal.module.css';

interface AddQuoteModalProps {
  procurementRequestId: string;
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (dto: CreateVendorQuoteDto) => Promise<void>;
  isSubmitting: boolean;
}

export default function AddQuoteModal({
  procurementRequestId,
  isOpen,
  onClose,
  onSubmit,
  isSubmitting,
}: AddQuoteModalProps) {
  const [selectedVendorId, setSelectedVendorId] = useState('');
  const [vendorName, setVendorName] = useState('');
  const [quotedPrice, setQuotedPrice] = useState<number | ''>('');
  const [estimatedDeliveryDays, setEstimatedDeliveryDays] = useState<number | ''>('');
  const [reliabilityRating, setReliabilityRating] = useState<number | ''>(90);
  const [isComplianceApproved, setIsComplianceApproved] = useState(true);
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);

  const { data: selectedVendors, isLoading: isLoadingVendors } = useQuery({
    queryKey: ['selectedVendors', procurementRequestId],
    queryFn: () => getSelectedVendorsForRequest(procurementRequestId),
    enabled: isOpen && !!procurementRequestId,
  });

  const { data: allActiveVendors, isLoading: isLoadingAllVendors } = useQuery({
    queryKey: ['vendors', 'ACTIVE'],
    queryFn: () => getVendors({ status: 'ACTIVE' }),
    enabled: isOpen,
  });

  const hasSpecificShortlist = !!(selectedVendors && selectedVendors.length > 0);
  const availableVendors = hasSpecificShortlist ? selectedVendors! : (allActiveVendors ?? []);
  const isLoading = isLoadingVendors || isLoadingAllVendors;

  useEffect(() => {
    if (selectedVendorId && availableVendors) {
      const v = availableVendors.find((item) => item.id === selectedVendorId);
      if (v) {
        setVendorName(v.name);
        if (v.rating) {
          // Convert 1-5 rating to 0-100 percentage if needed
          const score = v.rating <= 5 ? v.rating * 20 : v.rating;
          setReliabilityRating(score);
        }
      }
    } else if (!selectedVendorId) {
      setVendorName('');
    }
  }, [selectedVendorId, availableVendors]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!selectedVendorId) {
      setError('Please select a vendor from the list.');
      return;
    }

    if (!vendorName.trim()) {
      setError('Vendor name is required.');
      return;
    }

    if (quotedPrice === '' || quotedPrice < 0) {
      setError('Please provide a valid quoted price.');
      return;
    }

    if (estimatedDeliveryDays === '' || estimatedDeliveryDays < 0) {
      setError('Please provide valid delivery days.');
      return;
    }

    try {
      await onSubmit({
        procurementRequestId,
        vendorId: selectedVendorId,
        vendorName: vendorName.trim(),
        quotedPrice: Number(quotedPrice),
        estimatedDeliveryDays: Number(estimatedDeliveryDays),
        reliabilityRating: Number(reliabilityRating || 80),
        isComplianceApproved,
        notes: notes.trim() || undefined,
      });
      onClose();
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setError(axiosErr.response?.data?.message ?? 'Failed to submit quote.');
    }
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.header}>
          <h3>Add Candidate Vendor Quote</h3>
          <button type="button" className={styles.closeButton} onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className={styles.form}>
          {error && <div className={styles.errorMessage}>{error}</div>}

          <div className={styles.formGroup}>
            <label htmlFor="vendorSelect">
              {hasSpecificShortlist ? 'Select Vendor (Assigned to this Request) *' : 'Select Vendor (Active Vendors) *'}
            </label>
            <select
              id="vendorSelect"
              className={styles.select}
              value={selectedVendorId}
              onChange={(e) => setSelectedVendorId(e.target.value)}
              required
            >
              <option value="">
                {isLoading
                  ? '-- Loading vendors... --'
                  : availableVendors.length > 0
                  ? hasSpecificShortlist
                    ? '-- Choose an assigned vendor for this request --'
                    : '-- Choose an active vendor --'
                  : '-- No active vendors found in directory --'}
              </option>
              {availableVendors.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name} ({v.category}) - Rating: {v.rating ?? 'N/A'}/5
                </option>
              ))}
            </select>
            {availableVendors.length === 0 && !isLoading && (
              <p style={{ fontSize: '0.8rem', color: '#e53e3e', marginTop: '0.25rem' }}>
                Note: No active vendors have been created in the Vendors directory yet.
              </p>
            )}
            {!hasSpecificShortlist && availableVendors.length > 0 && !isLoading && (
              <p style={{ fontSize: '0.8rem', color: '#0284c7', marginTop: '0.25rem' }}>
                ℹ️ Showing active vendors from the vendor directory.
              </p>
            )}
          </div>

          <div className={styles.formGroup}>
            <label htmlFor="vendorName">Vendor Name *</label>
            <input
              id="vendorName"
              type="text"
              className={styles.input}
              value={vendorName}
              onChange={(e) => setVendorName(e.target.value)}
              placeholder="Select a vendor above"
              required
              readOnly={!!selectedVendorId}
            />
          </div>

          <div className={styles.grid2}>
            <div className={styles.formGroup}>
              <label htmlFor="quotedPrice">Quoted Price ($) *</label>
              <input
                id="quotedPrice"
                type="number"
                step="0.01"
                min="0"
                className={styles.input}
                value={quotedPrice}
                onChange={(e) => setQuotedPrice(e.target.value === '' ? '' : Number(e.target.value))}
                placeholder="0.00"
                required
              />
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="estimatedDeliveryDays">Lead Time (Days) *</label>
              <input
                id="estimatedDeliveryDays"
                type="number"
                min="0"
                className={styles.input}
                value={estimatedDeliveryDays}
                onChange={(e) => setEstimatedDeliveryDays(e.target.value === '' ? '' : Number(e.target.value))}
                placeholder="e.g. 5"
                required
              />
            </div>
          </div>

          <div className={styles.grid2}>
            <div className={styles.formGroup}>
              <label htmlFor="reliabilityRating">Reliability Score (0 - 100)</label>
              <input
                id="reliabilityRating"
                type="number"
                min="0"
                max="100"
                className={styles.input}
                value={reliabilityRating}
                onChange={(e) => setReliabilityRating(e.target.value === '' ? '' : Number(e.target.value))}
                placeholder="90"
              />
            </div>

            <div className={styles.checkboxGroup} style={{ alignSelf: 'center', marginTop: '1.2rem' }}>
              <input
                id="complianceApproved"
                type="checkbox"
                checked={isComplianceApproved}
                onChange={(e) => setIsComplianceApproved(e.target.checked)}
              />
              <label htmlFor="complianceApproved">Compliance Approved</label>
            </div>
          </div>

          <div className={styles.formGroup}>
            <label htmlFor="quoteNotes">Notes & Constraints (Optional)</label>
            <textarea
              id="quoteNotes"
              rows={2}
              className={styles.textarea}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Any specific delivery terms, warranty info, or risk notes"
            />
          </div>

          <div className={styles.footer}>
            <button type="button" className={styles.cancelBtn} onClick={onClose} disabled={isSubmitting}>
              Cancel
            </button>
            <button type="submit" className={styles.submitBtn} disabled={isSubmitting}>
              {isSubmitting ? 'Submitting…' : 'Add Quote'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
