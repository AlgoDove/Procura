import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import 'widgets/items_form.dart';

class CreateRequestScreen extends StatefulWidget {
  const CreateRequestScreen({super.key});

  @override
  State<CreateRequestScreen> createState() => _CreateRequestScreenState();
}

class _CreateRequestScreenState extends State<CreateRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _descController = TextEditingController();
  final _justController = TextEditingController();
  String _priority = 'MEDIUM';
  DateTime? _requiredByDate;
  List<ProcurementRequestItemInput> _items = [ProcurementRequestItemInput()];
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _titleController.dispose();
    _descController.dispose();
    _justController.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final picked = await showDatePicker(
      context: context,
      initialDate: _requiredByDate ?? tomorrow,
      firstDate: tomorrow, // Native date picker enforces future-date constraint
      lastDate: DateTime.now().add(const Duration(days: 3 * 365)),
      helpText: 'Select required by date',
    );
    if (picked != null) setState(() => _requiredByDate = picked);
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_requiredByDate == null) {
      setState(() => _error = 'Required by date is required.');
      return;
    }
    if (_items.isEmpty) {
      setState(() => _error = 'At least one item is required.');
      return;
    }
    if (_items.any((i) => i.itemName.trim().isEmpty)) {
      setState(() => _error = 'All items must have a name.');
      return;
    }

    setState(() { _loading = true; _error = null; });
    try {
      await ApiClient.dio.post(
        '/api/procurement-requests',
        data: CreateProcurementRequestDto(
          title: _titleController.text.trim(),
          description: _descController.text.trim(),
          justification: _justController.text.trim(),
          priority: _priority,
          requiredByDate: _requiredByDate!.toUtc().toIso8601String(),
          items: _items,
        ).toJson(),
      );
      if (!mounted) return;
      Navigator.pop(context);
    } catch (e) {
      setState(() { _error = 'Failed to create request. Please check your input.'; });
    } finally {
      if (mounted) setState(() { _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('New Request')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextFormField(
              controller: _titleController,
              decoration: const InputDecoration(labelText: 'Title *', border: OutlineInputBorder()),
              maxLength: 150,
              validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _descController,
              decoration: const InputDecoration(labelText: 'Description *', border: OutlineInputBorder()),
              maxLines: 3,
              validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _justController,
              decoration: const InputDecoration(labelText: 'Justification *', border: OutlineInputBorder()),
              maxLines: 3,
              validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: _priority,
              decoration: const InputDecoration(labelText: 'Priority', border: OutlineInputBorder()),
              items: const [
                DropdownMenuItem(value: 'LOW', child: Text('Low')),
                DropdownMenuItem(value: 'MEDIUM', child: Text('Medium')),
                DropdownMenuItem(value: 'HIGH', child: Text('High')),
                DropdownMenuItem(value: 'URGENT', child: Text('Urgent')),
              ],
              onChanged: (v) => setState(() => _priority = v!),
            ),
            const SizedBox(height: 12),
            // Native date picker (ADR-007)
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Required by date *'),
              subtitle: Text(
                _requiredByDate == null
                    ? 'Tap to select a future date'
                    : _requiredByDate!.toLocal().toString().split(' ')[0],
                style: TextStyle(
                  color: _requiredByDate == null ? Colors.red : Colors.black87,
                ),
              ),
              trailing: const Icon(Icons.calendar_today),
              onTap: _pickDate,
            ),
            const Divider(),
            ItemsForm(
              items: _items,
              onChanged: (items) => setState(() => _items = items),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: const TextStyle(color: Colors.red)),
            ],
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _loading ? null : _submit,
              child: _loading ? const CircularProgressIndicator(color: Colors.white) : const Text('Create Draft'),
            ),
          ],
        ),
      ),
    );
  }
}
