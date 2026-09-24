import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createProcurementRequest } from '../api/endpoints';
import type { CreateProcurementRequestDto, Priority, ProcurementRequestItemInput } from '../types/api';
import ItemsEditor from '../components/ItemsEditor';
import styles from './RequestFormPage.module.css';

// Tomorrow's date as the minimum allowed value for RequiredByDate
function tomorrowIso(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().split('T')[0];
}

export default function CreateRequestPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [justification, setJustification] = useState('');
  const [priority, setPriority] = useState<Priority>('MEDIUM');
  const [requiredByDate, setRequiredByDate] = useState('');
  const [items, setItems] = useState<ProcurementRequestItemInput[]>([
    { itemName: '', description: '', quantity: 1, unit: '', estimatedUnitPrice: 0 },
  ]);
  const [formError, setFormError] = useState<string | null>(null);

  const { mutate, isPending } = useMutation({
    mutationFn: (dto: CreateProcurementRequestDto) => createProcurementRequest(dto),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['procurement-requests'] });
      navigate(`/requests/${data.id}`);
    },
    onError: (err: unknown) => {
      const axiosErr = err as { response?: { data?: { message?: string; errors?: unknown } } };
      setFormError(
        axiosErr.response?.data?.message ?? 'Failed to create request. Please check your input.'
      );
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (items.length === 0) {
      setFormError('At least one item is required.');
      return;
    }
    if (items.some((it) => !it.itemName.trim())) {
      setFormError('All items must have a name.');
      return;
    }

    // Send items WITHOUT database IDs — backend always clears and rebuilds
    const dto: CreateProcurementRequestDto = {
      title,
      description,
      justification,
      priority,
      requiredByDate: new Date(requiredByDate).toISOString(),
      items: items.map(({ itemName, description: d, quantity, unit, estimatedUnitPrice }) => ({
        itemName,
        description: d,
        quantity,
        unit,
        estimatedUnitPrice,
      })),
    };

    mutate(dto);
  };

  return (
    <div className={styles.container}>
      <h1 className={styles.heading}>New Procurement Request</h1>
      <form onSubmit={handleSubmit} noValidate>
        <div className={styles.card}>
          <div className={styles.field}>
            <label htmlFor="title">Title *</label>
            <input
              id="title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              maxLength={150}
              required
            />
          </div>
          <div className={styles.field}>
            <label htmlFor="description">Description *</label>
            <textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              required
            />
          </div>
          <div className={styles.field}>
            <label htmlFor="justification">Justification *</label>
            <textarea
              id="justification"
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              rows={3}
              required
            />
          </div>
          <div className={styles.row}>
            <div className={styles.field}>
              <label htmlFor="priority">Priority *</label>
              <select
                id="priority"
                value={priority}
                onChange={(e) => setPriority(e.target.value as Priority)}
              >
                <option value="LOW">Low</option>
                <option value="MEDIUM">Medium</option>
                <option value="HIGH">High</option>
                <option value="URGENT">Urgent</option>
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="requiredByDate">Required by date *</label>
              <input
                id="requiredByDate"
                type="date"
                value={requiredByDate}
                min={tomorrowIso()}
                onChange={(e) => setRequiredByDate(e.target.value)}
                required
              />
            </div>
          </div>
        </div>

        <ItemsEditor items={items} onChange={setItems} />

        {formError && <p className={styles.error}>{formError}</p>}

        <div className={styles.actions}>
          <button type="button" onClick={() => navigate(-1)} className={styles.btnSecondary}>
            Cancel
          </button>
          <button type="submit" className={styles.btnPrimary} disabled={isPending}>
            {isPending ? 'Creating…' : 'Create Draft'}
          </button>
        </div>
      </form>
    </div>
  );
}
