import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import 'widgets/items_form.dart';

class EditRequestScreen extends StatefulWidget {
  final String requestId;
  const EditRequestScreen({super.key, required this.requestId});

  @override
  State<EditRequestScreen> createState() => _EditRequestScreenState();
}

class _EditRequestScreenState extends State<EditRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _descController = TextEditingController();
  final _justController = TextEditingController();
  String _priority = 'MEDIUM';
  DateTime? _requiredByDate;
  List<ProcurementRequestItemInput> _items = [];
  bool _loading = true;
  bool _saving = false;
  String? _error;
  ProcurementRequestResponse? _request;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await ApiClient.dio.get<Map<String, dynamic>>('/api/procurement-requests/${widget.requestId}');
      final req = ProcurementRequestResponse.fromJson(r.data!);
      setState(() {
        _request = req;
        _titleController.text = req.title;
        _descController.text = req.description;
        _justController.text = req.justification;
        _priority = req.priority;
        _requiredByDate = DateTime.parse(req.requiredByDate).toLocal();
        // Strip IDs — backend clears and rebuilds on PUT
        _items = req.items.map((i) => ProcurementRequestItemInput(
          itemName: i.itemName,
          description: i.description,
          quantity: i.quantity,
          unit: i.unit,
          estimatedUnitPrice: i.estimatedUnitPrice,
        )).toList();
        _loading = false;
      });
    } catch (_) {
      setState(() { _loading = false; _error = 'Failed to load request.'; });
    }
  }

  Future<void> _pickDate() async {
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final picked = await showDatePicker(
      context: context,
      initialDate: _requiredByDate ?? tomorrow,
      firstDate: tomorrow,
      lastDate: DateTime.now().add(const Duration(days: 3 * 365)),
    );
    if (picked != null) setState(() => _requiredByDate = picked);
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_requiredByDate == null) { setState(() => _error = 'Required by date is required.'); return; }
    if (_items.isEmpty) { setState(() => _error = 'At least one item is required.'); return; }

    setState(() { _saving = true; _error = null; });
    try {
      await ApiClient.dio.put(
        '/api/procurement-requests/${widget.requestId}',
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
      final statusCode = (e as dynamic)?.response?.statusCode as int?;
      setState(() {
        _error = statusCode == 409
            ? 'Only DRAFT requests can be edited.'
            : 'Failed to update request.';
      });
    } finally {
      if (mounted) setState(() { _saving = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    if (_request?.status != 'DRAFT') {
      return Scaffold(
        appBar: AppBar(title: const Text('Edit Request')),
        body: Center(child: Text('Only DRAFT requests can be edited.\nThis request is ${_request?.status}.')),
      );
    }

    return Scaffold(
      appBar: AppBar(title: Text('Edit ${_request?.requestNumber ?? "Request"}')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextFormField(controller: _titleController, decoration: const InputDecoration(labelText: 'Title *', border: OutlineInputBorder()), maxLength: 150, validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null),
            const SizedBox(height: 12),
            TextFormField(controller: _descController, decoration: const InputDecoration(labelText: 'Description *', border: OutlineInputBorder()), maxLines: 3, validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null),
            const SizedBox(height: 12),
            TextFormField(controller: _justController, decoration: const InputDecoration(labelText: 'Justification *', border: OutlineInputBorder()), maxLines: 3, validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null),
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
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Required by date *'),
              subtitle: Text(_requiredByDate == null ? 'Tap to select' : _requiredByDate!.toLocal().toString().split(' ')[0]),
              trailing: const Icon(Icons.calendar_today),
              onTap: _pickDate,
            ),
            const Divider(),
            ItemsForm(items: _items, onChanged: (v) => setState(() => _items = v)),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: const TextStyle(color: Colors.red)),
            ],
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _saving ? null : _save,
              child: _saving ? const CircularProgressIndicator(color: Colors.white) : const Text('Save Changes'),
            ),
          ],
        ),
      ),
    );
  }
}
