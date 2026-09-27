import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getVendor, updateVendor } from '../api/endpoints';
import type { UpdateVendorDto } from '../types/api';
import styles from './RequestFormPage.module.css';

export default function EditVendorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: vendor, isLoading } = useQuery({
    queryKey: ['vendor', id],
    queryFn: () => getVendor(id!),
    enabled: !!id,
  });

  const [name, setName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [address, setAddress] = useState('');
  const [category, setCategory] = useState('');
  const [notes, setNotes] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!vendor) return;
    setName(vendor.name);
    setContactEmail(vendor.contactEmail);
    setContactPhone(vendor.contactPhone ?? '');
    setAddress(vendor.address ?? '');
    setCategory(vendor.category);
    setNotes(vendor.notes ?? '');
  }, [vendor]);

  const { mutate, isPending } = useMutation({
    mutationFn: (dto: UpdateVendorDto) => updateVendor(id!, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vendors'] });
      queryClient.invalidateQueries({ queryKey: ['vendor', id] });
      navigate(`/vendors/${id}`);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setFormError(axiosErr.response?.data?.message ?? 'Failed to update vendor.');
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    mutate({ name, contactEmail, contactPhone: contactPhone || undefined, address: address || undefined, category, notes: notes || undefined });
  };

  if (isLoading) return <p>Loading…</p>;

  return (
    <div className={styles.container}>
      <h1 className={styles.heading}>Edit Vendor</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.card}>
          <div className={styles.row}>
            <div className={styles.field}>
              <label>Vendor name *</label>
              <input type="text" value={name} onChange={(e) => setName(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label>Category *</label>
              <input type="text" value={category} onChange={(e) => setCategory(e.target.value)} required />
            </div>
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label>Contact email *</label>
              <input type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label>Contact phone</label>
              <input type="text" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} />
            </div>
          </div>
          <div className={styles.field}>
            <label>Address</label>
            <input type="text" value={address} onChange={(e) => setAddress(e.target.value)} />
          </div>
          <div className={styles.field}>
            <label>Notes</label>
            <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        </div>
        {formError && <p className={styles.error}>{formError}</p>}
        <div className={styles.actions}>
          <button type="button" onClick={() => navigate(`/vendors/${id}`)} className={styles.btnSecondary}>Cancel</button>
          <button type="submit" className={styles.btnPrimary} disabled={isPending}>{isPending ? 'Saving…' : 'Save Changes'}</button>
        </div>
      </form>
    </div>
  );
}
