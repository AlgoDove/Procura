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
  const [contactPerson, setContactPerson] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [address, setAddress] = useState('');
  const [category, setCategory] = useState('');
  const [rating, setRating] = useState(0);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!vendor) return;
    setName(vendor.name);
    setContactPerson(vendor.contactPerson);
    setEmail(vendor.email);
    setPhoneNumber(vendor.phoneNumber);
    setAddress(vendor.address ?? '');
    setCategory(vendor.category);
    setRating(vendor.rating);
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
    mutate({ name, contactPerson, email, phoneNumber, address: address || undefined, category, rating });
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
              <label>Contact person *</label>
              <input type="text" value={contactPerson} onChange={(e) => setContactPerson(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label>Contact email *</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label>Contact phone *</label>
              <input type="text" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="rating">Rating</label>
              <input id="rating" type="number" min={0} max={5} step={0.1} value={rating} onChange={(e) => setRating(Number(e.target.value))} />
            </div>
          </div>
          <div className={styles.field}>
            <label>Address</label>
            <input type="text" value={address} onChange={(e) => setAddress(e.target.value)} />
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
