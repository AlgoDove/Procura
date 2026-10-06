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
  const [contactPerson, setContactPerson] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [address, setAddress] = useState('');
  const [category, setCategory] = useState('');
  const [rating, setRating] = useState(0);
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

    const trimmedName = name.trim();
    if (!trimmedName) {
      setFormError('Vendor name is required.');
      return;
    }

    const trimmedCategory = category.trim();
    if (!trimmedCategory) {
      setFormError('Category is required.');
      return;
    }

    const trimmedContactPerson = contactPerson.trim();
    if (!trimmedContactPerson) {
      setFormError('Contact person is required.');
      return;
    }

    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setFormError('Contact email is required.');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setFormError('Please enter a valid contact email address.');
      return;
    }

    const trimmedPhoneNumber = phoneNumber.trim();
    if (!trimmedPhoneNumber) {
      setFormError('Contact phone is required.');
      return;
    }

    mutate({
      name: trimmedName,
      contactPerson: trimmedContactPerson,
      email: trimmedEmail,
      phoneNumber: trimmedPhoneNumber,
      address: address.trim() || undefined,
      category: trimmedCategory,
      rating,
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
              <input id="name" type="text" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Acme Supplies Ltd" required />
            </div>
            <div className={styles.field}>
              <label htmlFor="category">Category *</label>
              <input id="category" type="text" value={category} onChange={(e) => setCategory(e.target.value)} required placeholder="e.g. Electronics, Office Supplies" />
            </div>
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="contactPerson">Contact person *</label>
              <input id="contactPerson" type="text" value={contactPerson} onChange={(e) => setContactPerson(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="email">Contact email *</label>
              <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="e.g. contact@acme.com" required />
            </div>
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="phoneNumber">Contact phone *</label>
              <input id="phoneNumber" type="text" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="e.g. +1 555-0199" required />
            </div>
            <div className={styles.field}>
              <label htmlFor="rating">Rating</label>
              <input id="rating" type="number" min={0} max={5} step={0.1} value={rating} onChange={(e) => setRating(Number(e.target.value))} />
            </div>
          </div>
          <div className={styles.field}>
            <label htmlFor="address">Address</label>
            <input id="address" type="text" value={address} onChange={(e) => setAddress(e.target.value)} placeholder="e.g. 123 Business Way, Suite 100" />
          </div>
        </div>

        {formError && <div className={styles.errorBanner} role="alert">{formError}</div>}
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
