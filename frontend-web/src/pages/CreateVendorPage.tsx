import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createVendor } from '../api/endpoints';
import type { CreateVendorDto } from '../types/api';
import styles from './RequestFormPage.module.css';

export default function CreateVendorPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [name, setName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [contactPhone, setContactPhone] = useState('');
  const [address, setAddress] = useState('');
  const [category, setCategory] = useState('');
  const [notes, setNotes] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const { mutate, isPending } = useMutation({
    mutationFn: (dto: CreateVendorDto) => createVendor(dto),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['vendors'] });
      navigate(`/vendors/${data.id}`);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      setFormError(axiosErr.response?.data?.message ?? 'Failed to create vendor.');
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    mutate({
      name,
      contactEmail,
      contactPhone: contactPhone || undefined,
      address: address || undefined,
      category,
      notes: notes || undefined,
    });
  };

  return (
    <div className={styles.container}>
      <h1 className={styles.heading}>New Vendor</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.card}>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="name">Vendor name *</label>
              <input id="name" type="text" value={name} onChange={(e) => setName(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="category">Category *</label>
              <input id="category" type="text" value={category} onChange={(e) => setCategory(e.target.value)} required placeholder="e.g. Electronics, Office Supplies" />
            </div>
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="contactEmail">Contact email *</label>
              <input id="contactEmail" type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="contactPhone">Contact phone</label>
              <input id="contactPhone" type="text" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} />
            </div>
          </div>
          <div className={styles.field}>
            <label htmlFor="address">Address</label>
            <input id="address" type="text" value={address} onChange={(e) => setAddress(e.target.value)} />
          </div>
          <div className={styles.field}>
            <label htmlFor="notes">Notes</label>
            <textarea id="notes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        </div>

        {formError && <p className={styles.error}>{formError}</p>}
        <div className={styles.actions}>
          <button type="button" onClick={() => navigate('/vendors')} className={styles.btnSecondary}>Cancel</button>
          <button type="submit" className={styles.btnPrimary} disabled={isPending}>
            {isPending ? 'Creating…' : 'Create Vendor'}
          </button>
        </div>
      </form>
    </div>
  );
}
