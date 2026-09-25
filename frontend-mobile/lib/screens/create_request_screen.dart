import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import 'widgets/items_form.dart';
import 'request_detail_screen.dart';

enum CreationMethod { ai, manual }

const _terminalStatuses = {
  'COMPLETED',
  'STAGE_COMPLETED',
  'FAILED',
  'APPROVED',
  'REJECTED',
  'WAITING_FOR_HUMAN_APPROVAL',
};

class CreateRequestScreen extends StatefulWidget {
  const CreateRequestScreen({super.key});

  @override
  State<CreateRequestScreen> createState() => _CreateRequestScreenState();
}

class _CreateRequestScreenState extends State<CreateRequestScreen> {
  CreationMethod _method = CreationMethod.ai;

  // AI Creation State
  final _aiObjectiveController = TextEditingController();
  final _aiClarificationController = TextEditingController();
  WorkflowProcessResponse? _aiWorkflow;
  bool _aiLoading = false;
  String? _aiError;
  Timer? _aiPollTimer;

  // Manual Form State
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _descController = TextEditingController();
  final _justController = TextEditingController();
  String _priority = 'MEDIUM';
  DateTime? _requiredByDate;
  List<ProcurementRequestItemInput> _items = [ProcurementRequestItemInput()];
  bool _manualLoading = false;
  String? _manualError;

  @override
  void dispose() {
    _aiObjectiveController.dispose();
    _aiClarificationController.dispose();
    _aiPollTimer?.cancel();
    _titleController.dispose();
    _descController.dispose();
    _justController.dispose();
    super.dispose();
  }

  bool _isDraftCreated(WorkflowProcessResponse? w) {
    if (w == null) return false;
    return (w.status == 'COMPLETED' || w.status == 'STAGE_COMPLETED') &&
        w.procurementRequestId != null &&
        w.procurementRequestId!.isNotEmpty;
  }

  void _navigateToDetail(String requestId) {
    if (!mounted) return;
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(
        builder: (_) => RequestDetailScreen(requestId: requestId),
      ),
    );
  }

  void _scheduleNavigationIfComplete(WorkflowProcessResponse data) {
    if (_isDraftCreated(data)) {
      Future.delayed(const Duration(milliseconds: 1200), () {
        if (mounted && _isDraftCreated(_aiWorkflow)) {
          _navigateToDetail(data.procurementRequestId!);
        }
      });
    }
  }

  void _stopAiPolling() {
    _aiPollTimer?.cancel();
    _aiPollTimer = null;
  }

  void _startAiPolling(String workflowId) {
    _stopAiPolling();
    _aiPollTimer = Timer.periodic(const Duration(seconds: 3), (_) async {
      try {
        final r = await ApiClient.dio.get<Map<String, dynamic>>(
          '/api/procurement-requests/ai/workflows/$workflowId',
        );
        final data = WorkflowProcessResponse.fromJson(r.data!);
        if (!mounted) return;
        setState(() => _aiWorkflow = data);
        if (_isDraftCreated(data)) {
          _stopAiPolling();
          _scheduleNavigationIfComplete(data);
        } else if (_terminalStatuses.contains(data.status) || data.status == 'NEEDS_USER_INPUT') {
          _stopAiPolling();
        }
      } catch (_) {
        _stopAiPolling();
      }
    });
  }

  String _sanitizeAiError(String raw) {
    if (raw.contains('503') ||
        raw.contains('Gemini') ||
        raw.contains('AGENT_FAILED') ||
        raw.contains('temporarily')) {
      return 'The AI service is temporarily unavailable. Please try again in a few moments.';
    }
    return raw;
  }

  String _extractErrorMessage(dynamic e) {
    if (e is DioException) {
      if (e.response?.data is Map) {
        final map = e.response!.data as Map;
        if (map['message'] != null && map['message'].toString().isNotEmpty) {
          return _sanitizeAiError(map['message'].toString());
        }
        if (map['errors'] is List && (map['errors'] as List).isNotEmpty) {
          return (map['errors'] as List).map((x) => x.toString()).join('\n');
        }
      }
      if (e.type == DioExceptionType.connectionTimeout ||
          e.type == DioExceptionType.sendTimeout ||
          e.type == DioExceptionType.receiveTimeout) {
        return 'Connection timed out. Please check your backend connection.';
      }
      if (e.type == DioExceptionType.connectionError) {
        return 'Could not connect to backend server. Please verify the server is running.';
      }
    }
    return _sanitizeAiError(e.toString());
  }

  Future<void> _startAiCreation() async {
    final text = _aiObjectiveController.text.trim();
    if (text.isEmpty) return;

    setState(() {
      _aiLoading = true;
      _aiError = null;
    });

    try {
      final r = await ApiClient.dio.post<Map<String, dynamic>>(
        '/api/procurement-requests/ai/process',
        data: ProcessAiRequest(objective: text).toJson(),
      );
      final data = WorkflowProcessResponse.fromJson(r.data!);
      if (!mounted) return;
      setState(() => _aiWorkflow = data);

      if (_isDraftCreated(data)) {
        _stopAiPolling();
        _scheduleNavigationIfComplete(data);
      } else if (!_terminalStatuses.contains(data.status) && data.status != 'NEEDS_USER_INPUT') {
        _startAiPolling(data.workflowId);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _aiError = _extractErrorMessage(e);
        });
      }
    } finally {
      if (mounted) setState(() => _aiLoading = false);
    }
  }

  Future<void> _sendClarification() async {
    final answer = _aiClarificationController.text.trim();
    if (answer.isEmpty || _aiWorkflow == null) return;

    setState(() {
      _aiLoading = true;
      _aiError = null;
    });

    try {
      final r = await ApiClient.dio.post<Map<String, dynamic>>(
        '/api/procurement-requests/ai/process',
        data: ProcessAiRequest(
          objective: answer,
          workflowId: _aiWorkflow!.workflowId,
          existingRequestId: _aiWorkflow?.procurementRequestId,
        ).toJson(),
      );
      final data = WorkflowProcessResponse.fromJson(r.data!);
      if (!mounted) return;
      setState(() {
        _aiWorkflow = data;
        _aiClarificationController.clear();
      });

      if (_isDraftCreated(data)) {
        _stopAiPolling();
        _scheduleNavigationIfComplete(data);
      } else if (!_terminalStatuses.contains(data.status) && data.status != 'NEEDS_USER_INPUT') {
        _startAiPolling(data.workflowId);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _aiError = _extractErrorMessage(e);
        });
      }
    } finally {
      if (mounted) setState(() => _aiLoading = false);
    }
  }

  Future<void> _pickDate() async {
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final picked = await showDatePicker(
      context: context,
      initialDate: _requiredByDate ?? tomorrow,
      firstDate: tomorrow,
      lastDate: DateTime.now().add(const Duration(days: 3 * 365)),
      helpText: 'Select required by date',
    );
    if (picked != null) setState(() => _requiredByDate = picked);
  }

  Future<void> _submitManual() async {
    if (!_formKey.currentState!.validate()) return;
    if (_requiredByDate == null) {
      setState(() => _manualError = 'Required by date is required.');
      return;
    }
    if (_items.isEmpty) {
      setState(() => _manualError = 'At least one item is required.');
      return;
    }
    if (_items.any((i) => i.itemName.trim().isEmpty)) {
      setState(() => _manualError = 'All items must have a name.');
      return;
    }

    setState(() {
      _manualLoading = true;
      _manualError = null;
    });

    try {
      final r = await ApiClient.dio.post<Map<String, dynamic>>(
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
      final created = ProcurementRequestResponse.fromJson(r.data!);
      Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (_) => RequestDetailScreen(requestId: created.id),
        ),
      );
    } catch (_) {
      setState(() {
        _manualError = 'Failed to create request. Please check your connection and try again.';
      });
    } finally {
      if (mounted) setState(() => _manualLoading = false);
    }
  }

  Widget _buildField({
    required String label,
    required TextEditingController controller,
    String? hint,
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
            hintText: hint,
            hintStyle: const TextStyle(color: Colors.black38),
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
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        title: const Text('New Procurement Request'),
        backgroundColor: Colors.white,
        foregroundColor: Colors.black87,
        elevation: 1,
      ),
      body: Column(
        children: [
          // Segmented Toggle for Creation Method
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
            color: Colors.grey[100],
            child: SizedBox(
              width: double.infinity,
              child: SegmentedButton<CreationMethod>(
                segments: const [
                  ButtonSegment<CreationMethod>(
                    value: CreationMethod.ai,
                    icon: Icon(Icons.auto_awesome),
                    label: Text('AI Assistant'),
                  ),
                  ButtonSegment<CreationMethod>(
                    value: CreationMethod.manual,
                    icon: Icon(Icons.edit_note),
                    label: Text('Manual Request'),
                  ),
                ],
                selected: {_method},
                onSelectionChanged: (newSet) {
                  setState(() => _method = newSet.first);
                },
              ),
            ),
          ),
          Expanded(
            child: _method == CreationMethod.ai ? _buildAiView() : _buildManualView(),
          ),
        ],
      ),
    );
  }

  Widget _buildAiView() {
    final isInProgress = _aiWorkflow != null &&
        !_terminalStatuses.contains(_aiWorkflow!.status) &&
        _aiWorkflow!.status != 'NEEDS_USER_INPUT';
    final isCompleted = _isDraftCreated(_aiWorkflow);
    final needsInput = _aiWorkflow?.status == 'NEEDS_USER_INPUT';

    final hasWorkflowErrors = _aiWorkflow != null &&
        (_aiWorkflow!.status == 'FAILED' || _aiWorkflow!.errors.isNotEmpty);
    final rawWorkflowErrors = _aiWorkflow?.errors ?? [];
    final isTechFailure = rawWorkflowErrors.any((e) =>
        e.contains('503') ||
        e.contains('Gemini') ||
        e.contains('AGENT_FAILED') ||
        e.contains('temporarily'));
    final displayErrors = isTechFailure
        ? ['The AI service is temporarily unavailable. Please try again in a few moments.']
        : rawWorkflowErrors.toSet().toList();

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        const Text(
          'Describe what you need in normal language',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Colors.black87),
        ),
        const SizedBox(height: 6),
        const Text(
          'The Procurement Request Agent will extract your items, validate requirements, and create a Draft request for your review.',
          style: TextStyle(color: Colors.black54, fontSize: 13, height: 1.4),
        ),
        const SizedBox(height: 16),

        if (_aiError != null) ...[
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Colors.red[50],
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.red.shade200),
            ),
            child: Row(
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 20),
                const SizedBox(width: 8),
                Expanded(child: Text(_aiError!, style: const TextStyle(color: Colors.red, fontSize: 13))),
                TextButton(
                  onPressed: _aiLoading ? null : _startAiCreation,
                  child: const Text('Retry', style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],

        if (hasWorkflowErrors && !isCompleted) ...[
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.red[50],
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.red.shade200),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Icon(Icons.error_outline, color: Colors.red, size: 20),
                    SizedBox(width: 8),
                    Text(
                      'AI Workflow Failed',
                      style: TextStyle(fontWeight: FontWeight.bold, color: Colors.red),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                ...displayErrors.map(
                  (e) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Text('• $e', style: const TextStyle(color: Colors.red, fontSize: 13)),
                  ),
                ),
                const SizedBox(height: 10),
                ElevatedButton.icon(
                  onPressed: _aiLoading ? null : _startAiCreation,
                  icon: const Icon(Icons.refresh, size: 16),
                  label: const Text('Retry'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.red,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],

        if (isInProgress) ...[
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.blue[50],
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.blue.shade200),
            ),
            child: const Row(
              children: [
                SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
                SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'Processing requirements with Procurement Request Agent...',
                    style: TextStyle(color: Color(0xFF1E3A5F), fontWeight: FontWeight.w500),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],

        if (needsInput && _aiWorkflow?.clarificationPrompt != null) ...[
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.amber[50],
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.amber.shade300),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Icon(Icons.help_outline, color: Color(0xFFD97706), size: 20),
                    SizedBox(width: 8),
                    Text(
                      'AI Clarification Needed',
                      style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF92400E)),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  _aiWorkflow!.clarificationPrompt!,
                  style: const TextStyle(color: Color(0xFF78350F), fontSize: 14),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _aiClarificationController,
                  maxLines: 2,
                  decoration: InputDecoration(
                    hintText: 'Provide your answer...',
                    filled: true,
                    fillColor: Colors.white,
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                  ),
                ),
                const SizedBox(height: 12),
                ElevatedButton(
                  onPressed: _aiLoading ? null : _sendClarification,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF6C3483),
                    foregroundColor: Colors.white,
                  ),
                  child: _aiLoading
                      ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                      : const Text('Send Clarification'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],

        if (isCompleted && _aiWorkflow?.procurementRequestId != null) ...[
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.green[50],
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.green.shade300),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Icon(Icons.check_circle, color: Color(0xFF16A34A), size: 20),
                    SizedBox(width: 8),
                    Text(
                      'Draft Created Successfully',
                      style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF166534)),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  'Draft Request: ${_aiWorkflow!.requestNumber ?? _aiWorkflow!.procurementRequestId}',
                  style: const TextStyle(fontWeight: FontWeight.w600, color: Color(0xFF1E293B)),
                ),
                const SizedBox(height: 4),
                const Text(
                  'The AI created a DRAFT request. Review the items and submit when ready. Redirecting…',
                  style: TextStyle(color: Colors.black54, fontSize: 13),
                ),
                const SizedBox(height: 14),
                ElevatedButton(
                  onPressed: () => _navigateToDetail(_aiWorkflow!.procurementRequestId!),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF1E3A5F),
                    foregroundColor: Colors.white,
                  ),
                  child: const Text('Review Created Draft →'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],

        if (!isCompleted) ...[
          TextField(
            controller: _aiObjectiveController,
            maxLines: 5,
            enabled: !_aiLoading,
            decoration: InputDecoration(
              hintText: 'e.g. 5 ThinkPad laptops with 16GB RAM for the new engineering hires, needed by next Friday.',
              hintStyle: const TextStyle(color: Colors.black38),
              filled: true,
              fillColor: Colors.grey[50],
              contentPadding: const EdgeInsets.all(16),
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Colors.black26)),
              focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Color(0xFF6C3483), width: 2)),
            ),
          ),
          const SizedBox(height: 20),
          ElevatedButton.icon(
            onPressed: _aiLoading ? null : _startAiCreation,
            icon: const Icon(Icons.auto_awesome),
            label: _aiLoading
                ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                : const Text('Create Draft with AI', style: TextStyle(fontSize: 16)),
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF6C3483),
              foregroundColor: Colors.white,
              padding: const EdgeInsets.symmetric(vertical: 16),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
          ),
        ],
      ],
    );
  }

  Widget _buildManualView() {
    return Form(
      key: _formKey,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          _buildField(
            label: 'Title *',
            controller: _titleController,
            hint: 'e.g. Office supplies for Q3',
            maxLength: 150,
          ),
          const SizedBox(height: 16),
          _buildField(
            label: 'Description *',
            controller: _descController,
            hint: 'Explain what is being requested in detail',
            maxLines: 3,
          ),
          const SizedBox(height: 16),
          _buildField(
            label: 'Justification *',
            controller: _justController,
            hint: 'Business justification for the request',
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
                      _requiredByDate == null ? 'Tap to select a future date' : _requiredByDate!.toLocal().toString().split(' ')[0],
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
            onChanged: (items) => setState(() => _items = items),
          ),
          if (_manualError != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: Colors.red[50], borderRadius: BorderRadius.circular(8), border: Border.all(color: Colors.red.shade200)),
              child: Text(_manualError!, style: const TextStyle(color: Colors.red)),
            ),
          ],
          const SizedBox(height: 32),
          ElevatedButton(
            onPressed: _manualLoading ? null : _submitManual,
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF1E3A5F),
              foregroundColor: Colors.white,
              padding: const EdgeInsets.symmetric(vertical: 16),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            child: _manualLoading
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                : const Text('Save Draft', style: TextStyle(fontSize: 16)),
          ),
          const SizedBox(height: 32),
        ],
      ),
    );
  }
}
