import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getProcurementRequest, updateProcurementRequest } from '../api/endpoints';
import type { Priority, ProcurementRequestItemInput, UpdateProcurementRequestDto } from '../types/api';
import ItemsEditor from '../components/ItemsEditor';
import { formatStatus } from '../utils/formatters';
import styles from './RequestFormPage.module.css';

function tomorrowIso(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().split('T')[0];
}

export default function EditRequestPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: request, isLoading, error } = useQuery({
    queryKey: ['procurement-request', id],
    queryFn: () => getProcurementRequest(id!),
    enabled: !!id,
  });

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [justification, setJustification] = useState('');
  const [priority, setPriority] = useState<Priority>('MEDIUM');
  const [requiredByDate, setRequiredByDate] = useState('');
  const [items, setItems] = useState<ProcurementRequestItemInput[]>([]);
  const [formError, setFormError] = useState<string | null>(null);

  // Populate form when request loads
  useEffect(() => {
    if (!request) return;
    setTitle(request.title);
    setDescription(request.description);
    setJustification(request.justification);
    setPriority(request.priority);
    setRequiredByDate(request.requiredByDate.split('T')[0]);
    // Strip IDs — backend clears and rebuilds items on PUT
    setItems(
      request.items.map(({ itemName, description: d, quantity, unit, estimatedUnitPrice }) => ({
        itemName,
        description: d,
        quantity,
        unit,
        estimatedUnitPrice,
      }))
    );
  }, [request]);

  const { mutate, isPending } = useMutation({
    mutationFn: (dto: UpdateProcurementRequestDto) => updateProcurementRequest(id!, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
      queryClient.invalidateQueries({ queryKey: ['procurement-request', id] });
      navigate(`/requests/${id}`);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
      if (axiosErr.response?.status === 409) {
        setFormError('Only DRAFT requests can be edited.');
      } else if (axiosErr.response?.status === 403) {
        setFormError('You do not have permission to edit this request.');
      } else {
        setFormError(axiosErr.response?.data?.message ?? 'Failed to update request.');
      }
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    if (items.length === 0) { setFormError('At least one item is required.'); return; }
    if (items.some((it) => !it.itemName.trim())) { setFormError('All items must have a name.'); return; }
    mutate({
      title,
      description,
      justification,
      priority,
      requiredByDate: new Date(requiredByDate).toISOString(),
      items,
    });
  };

  if (isLoading) return <p>Loading…</p>;
  if (error || !request) return <p style={{ color: 'red' }}>Request not found.</p>;
  if (request.status !== 'DRAFT') {
    return (
      <div>
        <h1 className={styles.heading}>Cannot Edit</h1>
        <p>Only DRAFT requests can be edited. This request is <strong>{formatStatus(request.status)}</strong>.</p>
        <button onClick={() => navigate(`/requests/${id}`)} className={styles.btnSecondary}>
          ← Back
        </button>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <h1 className={styles.heading}>Edit Request — {request.requestNumber}</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.card}>
          <div className={styles.field}>
            <label htmlFor="title">Title *</label>
            <input id="title" type="text" value={title} onChange={(e) => setTitle(e.target.value)} maxLength={150} required />
          </div>
          <div className={styles.field}>
            <label htmlFor="description">Description *</label>
            <textarea id="description" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} required />
          </div>
          <div className={styles.field}>
            <label htmlFor="justification">Justification *</label>
            <textarea id="justification" value={justification} onChange={(e) => setJustification(e.target.value)} rows={3} required />
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="priority">Priority *</label>
              <select id="priority" value={priority} onChange={(e) => setPriority(e.target.value as Priority)}>
                <option value="LOW">Low</option>
                <option value="MEDIUM">Medium</option>
                <option value="HIGH">High</option>
                <option value="URGENT">Urgent</option>
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="requiredByDate">Required by date *</label>
              <input id="requiredByDate" type="date" value={requiredByDate} min={tomorrowIso()} onChange={(e) => setRequiredByDate(e.target.value)} required />
            </div>
          </div>
        </div>

        <ItemsEditor items={items} onChange={setItems} />

        {formError && <p className={styles.error}>{formError}</p>}
        <div className={styles.actions}>
          <button type="button" onClick={() => navigate(`/requests/${id}`)} className={styles.btnSecondary}>Cancel</button>
          <button type="submit" className={styles.btnPrimary} disabled={isPending}>{isPending ? 'Saving…' : 'Save Changes'}</button>
        </div>
      </form>
    </div>
  );
}
