import 'package:flutter/material.dart';
import '../../models/api_models.dart';

/// Reusable widget for editing procurement request items.
/// Items are sent WITHOUT database IDs — backend clears and rebuilds on PUT.
class ItemsForm extends StatefulWidget {
  final List<ProcurementRequestItemInput> items;
  final ValueChanged<List<ProcurementRequestItemInput>> onChanged;
  final bool disabled;

  const ItemsForm({
    super.key,
    required this.items,
    required this.onChanged,
    this.disabled = false,
  });

  @override
  State<ItemsForm> createState() => _ItemsFormState();
}

class _ItemsFormState extends State<ItemsForm> {
  void _update(int i, ProcurementRequestItemInput updated) {
    final list = List<ProcurementRequestItemInput>.from(widget.items);
    list[i] = updated;
    widget.onChanged(list);
  }

  void _add() {
    widget.onChanged([...widget.items, ProcurementRequestItemInput()]);
  }

  void _remove(int i) {
    final list = List<ProcurementRequestItemInput>.from(widget.items)..removeAt(i);
    widget.onChanged(list);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Text('Items', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const Spacer(),
            if (!widget.disabled)
              TextButton.icon(
                onPressed: _add,
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Add item'),
              ),
          ],
        ),
        if (widget.items.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('At least one item is required.', style: TextStyle(color: Colors.red)),
          ),
        ...widget.items.asMap().entries.map((entry) {
          final i = entry.key;
          final item = entry.value;
          return Card(
            margin: const EdgeInsets.symmetric(vertical: 6),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text('Item ${i + 1}', style: const TextStyle(fontWeight: FontWeight.w600, color: Colors.black54, fontSize: 13)),
                      const Spacer(),
                      if (!widget.disabled && widget.items.length > 1)
                        IconButton(
                          icon: const Icon(Icons.delete_outline, color: Colors.red),
                          onPressed: () => _remove(i),
                          padding: EdgeInsets.zero,
                          constraints: const BoxConstraints(),
                        ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  TextFormField(
                    initialValue: item.itemName,
                    decoration: const InputDecoration(labelText: 'Item name *', border: OutlineInputBorder(), isDense: true),
                    enabled: !widget.disabled,
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(
                      itemName: v, description: item.description, quantity: item.quantity,
                      unit: item.unit, estimatedUnitPrice: item.estimatedUnitPrice,
                    )),
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          initialValue: item.quantity.toString(),
                          decoration: const InputDecoration(labelText: 'Qty *', border: OutlineInputBorder(), isDense: true),
                          keyboardType: TextInputType.number,
                          enabled: !widget.disabled,
                          onChanged: (v) => _update(i, ProcurementRequestItemInput(
                            itemName: item.itemName, description: item.description,
                            quantity: int.tryParse(v) ?? 1, unit: item.unit,
                            estimatedUnitPrice: item.estimatedUnitPrice,
                          )),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: TextFormField(
                          initialValue: item.unit,
                          decoration: const InputDecoration(labelText: 'Unit', border: OutlineInputBorder(), isDense: true),
                          enabled: !widget.disabled,
                          onChanged: (v) => _update(i, ProcurementRequestItemInput(
                            itemName: item.itemName, description: item.description,
                            quantity: item.quantity, unit: v,
                            estimatedUnitPrice: item.estimatedUnitPrice,
                          )),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  TextFormField(
                    initialValue: item.estimatedUnitPrice.toString(),
                    decoration: const InputDecoration(labelText: 'Est. unit price *', border: OutlineInputBorder(), isDense: true, prefixText: '\$'),
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    enabled: !widget.disabled,
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(
                      itemName: item.itemName, description: item.description,
                      quantity: item.quantity, unit: item.unit,
                      estimatedUnitPrice: double.tryParse(v) ?? 0.0,
                    )),
                  ),
                  const SizedBox(height: 6),
                  TextFormField(
                    initialValue: item.description,
                    decoration: const InputDecoration(labelText: 'Description', border: OutlineInputBorder(), isDense: true),
                    enabled: !widget.disabled,
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(
                      itemName: item.itemName, description: v,
                      quantity: item.quantity, unit: item.unit,
                      estimatedUnitPrice: item.estimatedUnitPrice,
                    )),
                  ),
                ],
              ),
            ),
          );
        }),
        if (widget.items.isNotEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Text(
              'Estimated total: \$${widget.items.fold<double>(0.0, (sum, it) => sum + it.quantity * it.estimatedUnitPrice).toStringAsFixed(2)}',
              textAlign: TextAlign.right,
              style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF1E3A5F)),
            ),
          ),
      ],
    );
  }
}
