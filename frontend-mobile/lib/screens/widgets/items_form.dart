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

  Widget _buildField({
    required String label,
    required String initialValue,
    required ValueChanged<String> onChanged,
    TextInputType keyboardType = TextInputType.text,
    String? prefixText,
    String? hintText,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13, color: Colors.black87)),
        const SizedBox(height: 4),
        TextFormField(
          initialValue: initialValue,
          keyboardType: keyboardType,
          enabled: !widget.disabled,
          style: const TextStyle(fontSize: 14),
          decoration: InputDecoration(
            prefixText: prefixText,
            hintText: hintText,
            hintStyle: const TextStyle(color: Colors.black38),
            filled: true,
            fillColor: widget.disabled ? Colors.grey[200] : Colors.grey[50],
            contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(6), borderSide: const BorderSide(color: Colors.black26)),
            enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(6), borderSide: const BorderSide(color: Colors.black26)),
            focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(6), borderSide: const BorderSide(color: Color(0xFF1E3A5F), width: 2)),
          ),
          onChanged: onChanged,
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            const Text('Items', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Color(0xFF1E3A5F))),
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
            elevation: 0,
            margin: const EdgeInsets.symmetric(vertical: 8),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8), side: BorderSide(color: Colors.grey.shade300)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Text('Item ${i + 1}', style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.black87, fontSize: 16)),
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
                  const SizedBox(height: 12),
                  _buildField(
                    label: 'Item Name *',
                    initialValue: item.itemName,
                    hintText: 'e.g. Dell Latitude 5420',
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(itemName: v, description: item.description, quantity: item.quantity, unit: item.unit, estimatedUnitPrice: item.estimatedUnitPrice)),
                  ),
                  const SizedBox(height: 12),
                  _buildField(
                    label: 'Quantity *',
                    initialValue: item.quantity > 0 ? item.quantity.toString() : '',
                    hintText: '1',
                    keyboardType: TextInputType.number,
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(itemName: item.itemName, description: item.description, quantity: int.tryParse(v) ?? 1, unit: item.unit, estimatedUnitPrice: item.estimatedUnitPrice)),
                  ),
                  const SizedBox(height: 12),
                  _buildField(
                    label: 'Unit *',
                    initialValue: item.unit,
                    hintText: 'e.g. pcs, box, kg',
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(itemName: item.itemName, description: item.description, quantity: item.quantity, unit: v, estimatedUnitPrice: item.estimatedUnitPrice)),
                  ),
                  const SizedBox(height: 12),
                  _buildField(
                    label: 'Estimated Unit Price *',
                    initialValue: item.estimatedUnitPrice > 0 ? item.estimatedUnitPrice.toString() : '',
                    hintText: '0.00',
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    prefixText: '\$ ',
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(itemName: item.itemName, description: item.description, quantity: item.quantity, unit: item.unit, estimatedUnitPrice: double.tryParse(v) ?? 0.0)),
                  ),
                  const SizedBox(height: 12),
                  _buildField(
                    label: 'Description *',
                    initialValue: item.description,
                    hintText: 'e.g. 16GB RAM, 512GB SSD',
                    onChanged: (v) => _update(i, ProcurementRequestItemInput(itemName: item.itemName, description: v, quantity: item.quantity, unit: item.unit, estimatedUnitPrice: item.estimatedUnitPrice)),
                  ),
                ],
              ),
            ),
          );
        }),
        if (widget.items.isNotEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              'Estimated total: \$${widget.items.fold<double>(0.0, (sum, it) => sum + it.quantity * it.estimatedUnitPrice).toStringAsFixed(2)}',
              textAlign: TextAlign.right,
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF1E3A5F)),
            ),
          ),
      ],
    );
  }
}
