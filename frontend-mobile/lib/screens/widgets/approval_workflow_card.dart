import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../providers/auth_provider.dart';
import '../../services/api_client.dart';
import '../../models/api_models.dart';
import '../../utils/formatters.dart';

class ApprovalWorkflowCard extends StatefulWidget {
  final String requestId;
  final VoidCallback? onStatusChanged;

  const ApprovalWorkflowCard({
    super.key,
    required this.requestId,
    this.onStatusChanged,
  });

  @override
  State<ApprovalWorkflowCard> createState() => _ApprovalWorkflowCardState();
}

class _ApprovalWorkflowCardState extends State<ApprovalWorkflowCard> {
  ApprovalWorkflowResponse? _workflow;
  bool _loading = true;
  String? _error;
  String? _actionError;
  bool _isProcessing = false;

  static const _statusColors = <String, Color>{
    'DRAFT': Colors.grey,
    'UNDER_VENDOR_EVALUATION': Color(0xFF6C3483),
    'WAITING_MANAGER_APPROVAL': Color(0xFFB45309),
    'APPROVED': Color(0xFF15803D),
    'REJECTED': Color(0xFFB91C1C),
    'REVISION_REQUESTED': Color(0xFFC2410C),
  };

  @override
  void initState() {
    super.initState();
    _loadWorkflow();
  }

  Future<void> _loadWorkflow() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final res = await ApiClient.dio.get<Map<String, dynamic>>(
        '/api/approval-workflows/procurement-request/${widget.requestId}',
      );
      if (mounted) {
        setState(() {
          _workflow = ApprovalWorkflowResponse.fromJson(res.data!);
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _workflow = null;
          _error = 'No workflow found';
        });
      }
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
        });
      }
    }
  }

  Future<void> _initializeWorkflow() async {
    setState(() {
      _isProcessing = true;
      _actionError = null;
    });
    try {
      await ApiClient.dio.post(
        '/api/approval-workflows/initialize',
        data: {'procurementRequestId': widget.requestId},
      );
      await _loadWorkflow();
      widget.onStatusChanged?.call();
    } catch (e) {
      setState(() {
        _actionError = 'Failed to initialize approval workflow.';
      });
    } finally {
      setState(() {
        _isProcessing = false;
      });
    }
  }

  Future<void> _transitionWorkflow(String targetStatus) async {
    if (_workflow == null) return;
    setState(() {
      _isProcessing = true;
      _actionError = null;
    });
    try {
      await ApiClient.dio.post(
        '/api/approval-workflows/${_workflow!.id}/transition',
        data: {'targetStatus': targetStatus},
      );
      await _loadWorkflow();
      widget.onStatusChanged?.call();
    } catch (e) {
      setState(() {
        _actionError = 'Failed to transition workflow.';
      });
    } finally {
      setState(() {
        _isProcessing = false;
      });
    }
  }

  Future<void> _submitDecision(String action, String? comments) async {
    if (_workflow == null) return;
    setState(() {
      _isProcessing = true;
      _actionError = null;
    });
    try {
      String path;
      if (action == 'APPROVE') {
        path = '/api/approval-workflows/${_workflow!.id}/approve';
      } else if (action == 'REJECT') {
        path = '/api/approval-workflows/${_workflow!.id}/reject';
      } else {
        path = '/api/approval-workflows/${_workflow!.id}/request-revision';
      }

      await ApiClient.dio.post(
        path,
        data: comments != null && comments.isNotEmpty ? {'comments': comments} : null,
      );
      await _loadWorkflow();
      widget.onStatusChanged?.call();
    } catch (e) {
      setState(() {
        _actionError = 'Decision submission failed.';
      });
    } finally {
      setState(() {
        _isProcessing = false;
      });
    }
  }

  void _showDecisionDialog(String action) {
    final isApproval = action == 'APPROVE';
    final controller = TextEditingController();

    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(
          isApproval
              ? 'Approve Request'
              : (action == 'REJECT' ? 'Reject Request' : 'Request Revision'),
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              isApproval
                  ? 'Optionally provide notes or stipulations.'
                  : 'Please provide mandatory comments/justification.',
              style: const TextStyle(fontSize: 13, color: Colors.black54),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              maxLines: 3,
              decoration: InputDecoration(
                hintText: isApproval
                    ? 'Optional approval comments...'
                    : 'Mandatory justification / changes...',
                border: const OutlineInputBorder(),
                filled: true,
                fillColor: Colors.grey[50],
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: isApproval
                  ? const Color(0xFF15803D)
                  : (action == 'REJECT' ? const Color(0xFFB91C1C) : const Color(0xFFC2410C)),
              foregroundColor: Colors.white,
            ),
            onPressed: () {
              final text = controller.text.trim();
              if (!isApproval && text.isEmpty) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Comments are required for this action.')),
                );
                return;
              }
              Navigator.pop(ctx);
              _submitDecision(action, text);
            },
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
  }

  void _showAuditTrailBottomSheet() async {
    if (_workflow == null) return;
    try {
      final res = await ApiClient.dio.get<Map<String, dynamic>>(
        '/api/approval-workflows/${_workflow!.id}/audit-trail',
      );
      final audit = WorkflowAuditTrailResponse.fromJson(res.data!);

      if (!mounted) return;
      showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.white,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
        ),
        builder: (ctx) => DraggableScrollableSheet(
          initialChildSize: 0.65,
          maxChildSize: 0.9,
          minChildSize: 0.4,
          expand: false,
          builder: (_, scrollController) => Column(
            children: [
              Container(
                padding: const EdgeInsets.all(16),
                decoration: const BoxDecoration(
                  border: Border(bottom: BorderSide(color: Color(0xFFE2E8F0))),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Workflow Audit Trail',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF1E3A5F),
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.pop(ctx),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: ListView(
                  controller: scrollController,
                  padding: const EdgeInsets.all(16),
                  children: [
                    const Text(
                      'Decisions History',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                    ),
                    const SizedBox(height: 8),
                    if (audit.decisions.isEmpty)
                      const Text('No manager decisions recorded yet.', style: TextStyle(color: Colors.grey))
                    else
                      ...audit.decisions.map((d) => Card(
                            margin: const EdgeInsets.only(bottom: 8),
                            child: ListTile(
                              title: Text('${d.managerName} — ${d.decision}'),
                              subtitle: Text(
                                '${d.comments ?? 'No comment'}\n${formatDate(d.createdAt)}',
                              ),
                              isThreeLine: true,
                            ),
                          )),
                    const SizedBox(height: 16),
                    const Text(
                      'AI Agent Executions',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                    ),
                    const SizedBox(height: 8),
                    if (audit.agentExecutions.isEmpty)
                      const Text('No autonomous executions recorded.', style: TextStyle(color: Colors.grey))
                    else
                      ...audit.agentExecutions.map((a) => Card(
                            margin: const EdgeInsets.only(bottom: 8),
                            child: ListTile(
                              title: Text(a.agentName),
                              subtitle: Text(
                                '${a.executionStatus} • Started: ${formatDate(a.startedAt)}\n${a.outputSummary ?? ''}',
                              ),
                              isThreeLine: true,
                            ),
                          )),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Failed to load audit trail.')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final isManager = user?.role == 'MANAGER';
    final isOfficer = user?.role == 'PROCUREMENT_OFFICER';
    final isAdmin = user?.role == 'ADMIN';

    if (_loading) {
      return const Card(
        margin: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Center(child: CircularProgressIndicator()),
        ),
      );
    }

    if (_workflow == null) {
      return Card(
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Approval Workflow',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF1E3A5F),
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'No formal approval workflow has been initialized for this procurement request yet.',
                style: TextStyle(color: Colors.black54, fontSize: 13),
              ),
              const SizedBox(height: 12),
              ElevatedButton.icon(
                onPressed: _isProcessing ? null : _initializeWorkflow,
                icon: const Icon(Icons.play_arrow),
                label: Text(_isProcessing ? 'Initializing...' : 'Initialize Approval Workflow'),
              ),
            ],
          ),
        ),
      );
    }

    final status = _workflow!.currentStatus;
    final statusColor = _statusColors[status] ?? Colors.grey;
    final isPendingManager = status == 'WAITING_MANAGER_APPROVAL';
    final isEvaluation = status == 'UNDER_VENDOR_EVALUATION';
    final eval = _workflow!.vendorRecommendationSummary;

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Header
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Approval Lifecycle',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF1E3A5F),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: statusColor.withOpacity(0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    formatStatus(status),
                    style: TextStyle(
                      color: statusColor,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            if (_actionError != null)
              Container(
                margin: const EdgeInsets.only(bottom: 12),
                padding: const EdgeInsets.all(8),
                color: Colors.red[50],
                child: Text(_actionError!, style: const TextStyle(color: Colors.red, fontSize: 13)),
              ),

            // Stepper indicator
            _buildStepper(status),
            const SizedBox(height: 16),

            // AI Executive Recommendation Brief
            if (eval != null) ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFFAF5FF),
                  border: Border.all(color: const Color(0xFFE9D5FF)),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text(
                          '🤖 AI Evaluation Summary',
                          style: TextStyle(
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF6C3483),
                            fontSize: 13,
                          ),
                        ),
                        if (eval.topScore != null)
                          Text(
                            'Score: ${eval.topScore!.toStringAsFixed(1)} / 100',
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF6C3483),
                              fontSize: 12,
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      eval.recommendationSummary,
                      style: const TextStyle(fontSize: 12, color: Colors.black87),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Manager Review Action Controls
            if ((isManager || isAdmin) && isPendingManager) ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: Colors.amber[50],
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.amber[300]!),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Action Required: Manager Approval Pending',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                    ),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFF15803D),
                              foregroundColor: Colors.white,
                              padding: const EdgeInsets.symmetric(vertical: 10),
                            ),
                            onPressed: _isProcessing ? null : () => _showDecisionDialog('APPROVE'),
                            child: const Text('Approve', style: TextStyle(fontSize: 12)),
                          ),
                        ),
                        const SizedBox(width: 6),
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFFC2410C),
                              foregroundColor: Colors.white,
                              padding: const EdgeInsets.symmetric(vertical: 10),
                            ),
                            onPressed: _isProcessing ? null : () => _showDecisionDialog('REVISION_REQUESTED'),
                            child: const Text('Revision', style: TextStyle(fontSize: 12)),
                          ),
                        ),
                        const SizedBox(width: 6),
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFFB91C1C),
                              foregroundColor: Colors.white,
                              padding: const EdgeInsets.symmetric(vertical: 10),
                            ),
                            onPressed: _isProcessing ? null : () => _showDecisionDialog('REJECT'),
                            child: const Text('Reject', style: TextStyle(fontSize: 12)),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
            ],

            // Procurement Officer Transition Button
            if ((isOfficer || isAdmin) && isEvaluation) ...[
              ElevatedButton.icon(
                icon: const Icon(Icons.arrow_forward, size: 16),
                label: const Text('Submit for Manager Approval'),
                onPressed: _isProcessing
                    ? null
                    : () => _transitionWorkflow('WAITING_MANAGER_APPROVAL'),
              ),
              const SizedBox(height: 12),
            ],

            // Decisions History
            if (_workflow!.decisions.isNotEmpty) ...[
              const Text(
                'Recent Decision Notes',
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: Color(0xFF1E3A5F)),
              ),
              const SizedBox(height: 6),
              ..._workflow!.decisions.map((d) => Container(
                    margin: const EdgeInsets.only(bottom: 6),
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: Colors.grey[100],
                      borderRadius: BorderRadius.circular(6),
                      border: const Border(left: BorderSide(color: Color(0xFF1E3A5F), width: 3)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${d.managerName} — ${d.decision}',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
                        ),
                        if (d.comments != null && d.comments!.isNotEmpty)
                          Text(d.comments!, style: const TextStyle(fontSize: 12, color: Colors.black87)),
                      ],
                    ),
                  )),
            ],

            // Full Audit Trail Button
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                icon: const Icon(Icons.history, size: 16),
                label: const Text('View Full Audit Trail', style: TextStyle(fontSize: 12)),
                onPressed: _showAuditTrailBottomSheet,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStepper(String currentStatus) {
    final step1Done = currentStatus != 'DRAFT';
    final step2Done = currentStatus == 'WAITING_MANAGER_APPROVAL' ||
        currentStatus == 'APPROVED' ||
        currentStatus == 'REJECTED' ||
        currentStatus == 'REVISION_REQUESTED';
    final step3Done = currentStatus == 'APPROVED' ||
        currentStatus == 'REJECTED' ||
        currentStatus == 'REVISION_REQUESTED';

    return Row(
      children: [
        _stepCircle('1', 'Draft', step1Done, currentStatus == 'DRAFT'),
        _stepDivider(step1Done),
        _stepCircle('2', 'Eval', step2Done, currentStatus == 'UNDER_VENDOR_EVALUATION'),
        _stepDivider(step2Done),
        _stepCircle('3', 'Manager', step3Done, currentStatus == 'WAITING_MANAGER_APPROVAL'),
        _stepDivider(step3Done),
        _stepCircle(
          currentStatus == 'APPROVED' ? '✓' : (currentStatus == 'REJECTED' ? '✗' : '4'),
          'Decision',
          step3Done,
          false,
        ),
      ],
    );
  }

  Widget _stepCircle(String label, String title, bool done, bool active) {
    Color bg = Colors.grey[200]!;
    Color text = Colors.grey[600]!;
    if (done) {
      bg = const Color(0xFF15803D);
      text = Colors.white;
    } else if (active) {
      bg = const Color(0xFF1E3A5F);
      text = Colors.white;
    }

    return Column(
      children: [
        CircleAvatar(
          radius: 12,
          backgroundColor: bg,
          child: Text(label, style: TextStyle(color: text, fontSize: 11, fontWeight: FontWeight.bold)),
        ),
        const SizedBox(height: 2),
        Text(title, style: const TextStyle(fontSize: 10, color: Colors.black54)),
      ],
    );
  }

  Widget _stepDivider(bool done) {
    return Expanded(
      child: Container(
        height: 2,
        color: done ? const Color(0xFF15803D) : Colors.grey[300],
      ),
    );
  }
}
