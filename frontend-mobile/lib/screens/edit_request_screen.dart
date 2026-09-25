import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
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
    } on DioException catch (e) {
      final statusCode = e.response?.statusCode;
      setState(() {
        _error = statusCode == 409
            ? 'Only DRAFT requests can be edited.'
            : 'Failed to update request. Please check your connection.';
      });
    } catch (e) {
      setState(() { _error = 'An unexpected error occurred.'; });
    } finally {
      if (mounted) setState(() { _saving = false; });
    }
  }

  Widget _buildField({
    required String label,
    required TextEditingController controller,
    int maxLines = 1,
    int? maxLength,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.w600, color: Colors.black87)),
        const SizedBox(height: 6),
        TextFormField(
          controller: controller,
          maxLines: maxLines,
          maxLength: maxLength,
          style: const TextStyle(color: Colors.black87),
          decoration: InputDecoration(
            filled: true,
            fillColor: Colors.grey[50],
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Colors.black26)),
            enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Colors.black26)),
            focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Color(0xFF1E3A5F), width: 2)),
          ),
          validator: (v) => (v == null || v.trim().isEmpty) ? 'Required' : null,
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Scaffold(backgroundColor: Colors.white, body: Center(child: CircularProgressIndicator()));
    if (_request?.status != 'DRAFT') {
      return Scaffold(
        backgroundColor: Colors.white,
        appBar: AppBar(title: const Text('Edit Request')),
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.lock_outline, size: 64, color: Colors.grey),
              const SizedBox(height: 16),
              Text('This request is ${_request?.status}.', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
              const SizedBox(height: 8),
              const Text('Only DRAFT requests can be edited.', style: TextStyle(color: Colors.black54)),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        title: Text('Edit ${_request?.requestNumber ?? "Request"}'),
        backgroundColor: Colors.white,
        foregroundColor: Colors.black87,
        elevation: 1,
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            _buildField(
              label: 'Title *',
              controller: _titleController,
              maxLength: 150,
            ),
            const SizedBox(height: 16),
            _buildField(
              label: 'Description *',
              controller: _descController,
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            _buildField(
              label: 'Justification *',
              controller: _justController,
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            
            const Text('Priority', style: TextStyle(fontWeight: FontWeight.w600, color: Colors.black87)),
            const SizedBox(height: 6),
            DropdownButtonFormField<String>(
              initialValue: _priority,
              decoration: InputDecoration(
                filled: true,
                fillColor: Colors.grey[50],
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Colors.black26)),
                enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Colors.black26)),
                focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Color(0xFF1E3A5F), width: 2)),
              ),
              items: const [
                DropdownMenuItem(value: 'LOW', child: Text('Low')),
                DropdownMenuItem(value: 'MEDIUM', child: Text('Medium')),
                DropdownMenuItem(value: 'HIGH', child: Text('High')),
                DropdownMenuItem(value: 'URGENT', child: Text('Urgent')),
              ],
              onChanged: (v) => setState(() => _priority = v!),
            ),
            const SizedBox(height: 24),
            
            const Text('Required by date *', style: TextStyle(fontWeight: FontWeight.w600, color: Colors.black87)),
            const SizedBox(height: 6),
            InkWell(
              onTap: _pickDate,
              borderRadius: BorderRadius.circular(8),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
                decoration: BoxDecoration(
                  color: Colors.grey[50],
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: _requiredByDate == null ? Colors.black26 : const Color(0xFF1E3A5F)),
                ),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        _requiredByDate == null ? 'Tap to select a date' : _requiredByDate!.toLocal().toString().split(' ')[0],
                        style: TextStyle(color: _requiredByDate == null ? Colors.black45 : Colors.black87, fontSize: 16),
                      ),
                    ),
                    const Icon(Icons.calendar_today, color: Color(0xFF1E3A5F), size: 20),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 32),
            
            ItemsForm(
              items: _items,
              onChanged: (v) => setState(() => _items = v),
            ),
            
            if (_error != null) ...[
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: Colors.red[50], borderRadius: BorderRadius.circular(8), border: Border.all(color: Colors.red.shade200)),
                child: Text(_error!, style: const TextStyle(color: Colors.red)),
              ),
            ],
            
            const SizedBox(height: 32),
            ElevatedButton(
              onPressed: _saving ? null : _save,
              style: ElevatedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 16),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              ),
              child: _saving 
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)) 
                  : const Text('Save Changes', style: TextStyle(fontSize: 16)),
            ),
            const SizedBox(height: 32),
          ],
        ),
      ),
    );
  }
}
