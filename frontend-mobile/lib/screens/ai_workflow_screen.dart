import 'dart:async';
import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';

const _terminalStatuses = {
  'COMPLETED', 'FAILED', 'APPROVED', 'REJECTED', 'WAITING_FOR_HUMAN_APPROVAL',
};

class AiWorkflowScreen extends StatefulWidget {
  final String requestId;
  const AiWorkflowScreen({super.key, required this.requestId});

  @override
  State<AiWorkflowScreen> createState() => _AiWorkflowScreenState();
}

class _AiWorkflowScreenState extends State<AiWorkflowScreen> {
  final _objectiveController = TextEditingController();
  final _clarificationController = TextEditingController();
  WorkflowProcessResponse? _workflow;
  bool _starting = false;
  bool _continuing = false;
  String? _error;
  Timer? _pollTimer;

  @override
  void dispose() {
    _objectiveController.dispose();
    _clarificationController.dispose();
    _pollTimer?.cancel();
    super.dispose();
  }

  void _stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  void _startPolling(String workflowId) {
    _stopPolling();
    _pollTimer = Timer.periodic(const Duration(seconds: 3), (_) async {
      try {
        final r = await ApiClient.dio.get<Map<String, dynamic>>(
          '/api/procurement-requests/ai/workflows/$workflowId',
        );
        final data = WorkflowProcessResponse.fromJson(r.data!);
        if (!mounted) return;
        setState(() => _workflow = data);
        if (_terminalStatuses.contains(data.status) || data.status == 'NEEDS_USER_INPUT') {
          _stopPolling();
        }
      } catch (_) {
        _stopPolling();
      }
    });
  }

  Future<void> _startWorkflow() async {
    if (_objectiveController.text.trim().isEmpty) return;
    setState(() { _starting = true; _error = null; });
    try {
      final r = await ApiClient.dio.post<Map<String, dynamic>>(
        '/api/procurement-requests/ai/process',
        data: ProcessAiRequest(
          objective: _objectiveController.text.trim(),
          existingRequestId: widget.requestId,
        ).toJson(),
      );
      final data = WorkflowProcessResponse.fromJson(r.data!);
      setState(() => _workflow = data);
      if (!_terminalStatuses.contains(data.status) && data.status != 'NEEDS_USER_INPUT') {
        _startPolling(data.workflowId);
      }
    } catch (_) {
      setState(() => _error = 'Failed to start AI workflow.');
    } finally {
      if (mounted) setState(() => _starting = false);
    }
  }

  Future<void> _continueWorkflow() async {
    if (_clarificationController.text.trim().isEmpty) return;
    setState(() { _continuing = true; _error = null; });
    try {
      // NEEDS_USER_INPUT continuation: same endpoint, same workflowId, new objective = clarification answer
      final r = await ApiClient.dio.post<Map<String, dynamic>>(
        '/api/procurement-requests/ai/process',
        data: ProcessAiRequest(
          objective: _clarificationController.text.trim(),
          workflowId: _workflow!.workflowId,
          existingRequestId: _workflow?.procurementRequestId ?? widget.requestId,
        ).toJson(),
      );
      final data = WorkflowProcessResponse.fromJson(r.data!);
      setState(() {
        _workflow = data;
        _clarificationController.clear();
      });
      if (!_terminalStatuses.contains(data.status) && data.status != 'NEEDS_USER_INPUT') {
        _startPolling(data.workflowId);
      }
    } catch (_) {
      setState(() => _error = 'Failed to continue workflow.');
    } finally {
      if (mounted) setState(() => _continuing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('🤖 AI Assistant')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_workflow == null) ...[
            const Text('Describe what you need to procure:', style: TextStyle(fontWeight: FontWeight.w600)),
            const SizedBox(height: 8),
            TextField(
              controller: _objectiveController,
              maxLines: 4,
              decoration: const InputDecoration(
                hintText: 'e.g. 10 laptops, Core i7, 16GB RAM, needed by end of month.',
                border: OutlineInputBorder(),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: const TextStyle(color: Colors.red)),
            ],
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: _starting ? null : _startWorkflow,
              child: _starting ? const CircularProgressIndicator(color: Colors.white) : const Text('Start AI Workflow'),
            ),
          ] else ...[
            // Status
            _statusBanner(),
            const SizedBox(height: 12),

            // Created request info
            if (_workflow!.procurementRequestId != null) ...[
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Draft created: ${_workflow!.requestNumber ?? _workflow!.procurementRequestId}',
                          style: const TextStyle(fontWeight: FontWeight.w600)),
                      if (_workflow!.estimatedTotal != null)
                        Text('Est. total: \$${_workflow!.estimatedTotal!.toStringAsFixed(2)}'),
                      const SizedBox(height: 4),
                      const Text(
                        '⚠ The AI has created a DRAFT request. You must review it and explicitly submit it.',
                        style: TextStyle(color: Colors.red, fontSize: 12),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 8),
            ],

            // Clarification prompt
            if (_workflow!.status == 'NEEDS_USER_INPUT' && _workflow!.clarificationPrompt != null) ...[
              Card(
                color: const Color(0xFFFEF9E7),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('AI needs clarification:', style: TextStyle(fontWeight: FontWeight.bold)),
                      const SizedBox(height: 6),
                      Text(_workflow!.clarificationPrompt!),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _clarificationController,
                        maxLines: 3,
                        decoration: const InputDecoration(labelText: 'Your answer', border: OutlineInputBorder()),
                      ),
                      if (_error != null) ...[
                        const SizedBox(height: 4),
                        Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 12)),
                      ],
                      const SizedBox(height: 8),
                      ElevatedButton(
                        onPressed: _continuing ? null : _continueWorkflow,
                        child: _continuing ? const CircularProgressIndicator(color: Colors.white) : const Text('Send Answer'),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 8),
            ],

            // Summary
            if (_workflow!.executionSummary != null) ...[
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Execution Summary', style: TextStyle(fontWeight: FontWeight.bold)),
                      const SizedBox(height: 4),
                      Text(_workflow!.executionSummary!),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 8),
            ],

            // Errors
            if (_workflow!.errors.isNotEmpty) ...[
              Card(
                color: Colors.red[50],
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Errors', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.red)),
                      ..._workflow!.errors.map((e) => Text('• $e', style: const TextStyle(color: Colors.red))),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 8),
            ],

            // Plan steps
            if (_workflow!.plan.steps.isNotEmpty) ...[
              const Text('Workflow Plan', style: TextStyle(fontWeight: FontWeight.bold)),
              const SizedBox(height: 4),
              ..._workflow!.plan.steps.map((step) => ListTile(
                contentPadding: EdgeInsets.zero,
                leading: _stepIcon(step.status),
                title: Text(step.agentName, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                subtitle: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(step.objective, style: const TextStyle(fontSize: 12)),
                    if (step.outcomeSummary != null)
                      Text(step.outcomeSummary!, style: const TextStyle(fontSize: 12, color: Color(0xFF27AE60))),
                  ],
                ),
                trailing: Text(step.status, style: TextStyle(fontSize: 11, color: _stepColor(step.status))),
              )),
              const SizedBox(height: 8),
            ],

            // Audit trail (compact)
            if (_workflow!.auditTrail.isNotEmpty) ...[
              const Text('Audit Trail', style: TextStyle(fontWeight: FontWeight.bold)),
              const SizedBox(height: 4),
              SizedBox(
                height: 200,
                child: ListView.builder(
                  itemCount: _workflow!.auditTrail.length,
                  itemBuilder: (_, i) {
                    final e = _workflow!.auditTrail[i];
                    return Padding(
                      padding: const EdgeInsets.symmetric(vertical: 1),
                      child: Row(
                        children: [
                          Text(e.timestamp.split('T')[1].split('.')[0], style: const TextStyle(fontSize: 10, color: Colors.black45, fontFamily: 'monospace')),
                          const SizedBox(width: 4),
                          Expanded(child: Text('${e.actor}: ${e.action}', style: const TextStyle(fontSize: 11))),
                        ],
                      ),
                    );
                  },
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }

  Widget _statusBanner() {
    Color bg;
    switch (_workflow!.status) {
      case 'IN_PROGRESS': bg = const Color(0xFFEBF5FB); break;
      case 'NEEDS_USER_INPUT': bg = const Color(0xFFFEF9E7); break;
      case 'COMPLETED': bg = const Color(0xFFEAFAF1); break;
      case 'FAILED': bg = Colors.red[50]!; break;
      default: bg = Colors.grey[100]!;
    }
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(6)),
      child: Row(
        children: [
          Text('Status: ${_workflow!.status}', style: const TextStyle(fontWeight: FontWeight.w600)),
          if (_workflow!.status == 'IN_PROGRESS') ...[
            const SizedBox(width: 8),
            const SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 2)),
          ],
        ],
      ),
    );
  }

  Widget _stepIcon(String status) {
    switch (status) {
      case 'COMPLETED': return const Icon(Icons.check_circle, color: Color(0xFF27AE60));
      case 'IN_PROGRESS': return const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2));
      case 'FAILED': return const Icon(Icons.cancel, color: Colors.red);
      default: return const Icon(Icons.radio_button_unchecked, color: Colors.grey);
    }
  }

  Color _stepColor(String status) {
    switch (status) {
      case 'COMPLETED': return const Color(0xFF27AE60);
      case 'IN_PROGRESS': return const Color(0xFF2980B9);
      case 'FAILED': return Colors.red;
      default: return Colors.grey;
    }
  }
}
