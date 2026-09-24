import type { ProcurementRequestItemInput } from '../types/api';
import styles from './ItemsEditor.module.css';

interface Props {
  items: ProcurementRequestItemInput[];
  onChange: (items: ProcurementRequestItemInput[]) => void;
  disabled?: boolean;
}

const emptyItem = (): ProcurementRequestItemInput => ({
  itemName: '',
  description: '',
  quantity: 1,
  unit: '',
  estimatedUnitPrice: 0,
});

export default function ItemsEditor({ items, onChange, disabled }: Props) {
  const update = (index: number, patch: Partial<ProcurementRequestItemInput>) => {
    onChange(items.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  };

  const addItem = () => onChange([...items, emptyItem()]);

  const removeItem = (index: number) =>
    onChange(items.filter((_, i) => i !== index));

  return (
    <div className={styles.wrapper}>
      <div className={styles.header}>
        <strong>Items</strong>
        <small>({items.length} item{items.length !== 1 ? 's' : ''})</small>
        {!disabled && (
          <button type="button" onClick={addItem} className={styles.addBtn}>
            + Add item
          </button>
        )}
      </div>

      {items.length === 0 && (
        <p className={styles.empty}>At least one item is required.</p>
      )}

      {items.map((item, i) => (
        <div key={i} className={styles.itemCard}>
          <div className={styles.itemHeader}>
            <span className={styles.itemNum}>Item {i + 1}</span>
            {!disabled && items.length > 1 && (
              <button
                type="button"
                onClick={() => removeItem(i)}
                className={styles.removeBtn}
              >
                Remove
              </button>
            )}
          </div>
          <div className={styles.grid}>
            <div className={styles.field}>
              <label>Item name *</label>
              <input
                type="text"
                value={item.itemName}
                onChange={(e) => update(i, { itemName: e.target.value })}
                maxLength={150}
                required
                disabled={disabled}
              />
            </div>
            <div className={styles.field}>
              <label>Unit</label>
              <input
                type="text"
                value={item.unit}
                onChange={(e) => update(i, { unit: e.target.value })}
                maxLength={30}
                placeholder="e.g. pcs, kg, box"
                disabled={disabled}
              />
            </div>
            <div className={styles.field}>
              <label>Quantity *</label>
              <input
                type="number"
                value={item.quantity}
                min={1}
                onChange={(e) => update(i, { quantity: parseInt(e.target.value) || 1 })}
                required
                disabled={disabled}
              />
            </div>
            <div className={styles.field}>
              <label>Est. unit price ($) *</label>
              <input
                type="number"
                value={item.estimatedUnitPrice}
                min={0}
                step="0.01"
                onChange={(e) => update(i, { estimatedUnitPrice: parseFloat(e.target.value) || 0 })}
                required
                disabled={disabled}
              />
            </div>
            <div className={`${styles.field} ${styles.fullWidth}`}>
              <label>Description</label>
              <input
                type="text"
                value={item.description}
                onChange={(e) => update(i, { description: e.target.value })}
                disabled={disabled}
              />
            </div>
          </div>
        </div>
      ))}

      {items.length > 0 && (
        <div className={styles.total}>
          Estimated total: $
          {items
            .reduce((sum, it) => sum + (it.quantity || 0) * (it.estimatedUnitPrice || 0), 0)
            .toFixed(2)}
        </div>
      )}
    </div>
  );
}
