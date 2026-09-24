import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/auth_provider.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import '../services/auth_service.dart';
import 'edit_request_screen.dart';
import 'ai_workflow_screen.dart';

class RequestDetailScreen extends StatefulWidget {
  final String requestId;
  const RequestDetailScreen({super.key, required this.requestId});

  @override
  State<RequestDetailScreen> createState() => _RequestDetailScreenState();
}

class _RequestDetailScreenState extends State<RequestDetailScreen> {
  ProcurementRequestResponse? _request;
  bool _loading = true;
  String? _error;
  String? _actionError;
  bool _confirmDelete = false;

  static const _statusColors = <String, Color>{
    'DRAFT': Colors.grey,
    'SUBMITTED': Color(0xFF2980B9),
    'UNDER_EVALUATION': Color(0xFF8E44AD),
    'PENDING_APPROVAL': Color(0xFFE67E22),
    'APPROVED': Color(0xFF27AE60),
    'REJECTED': Color(0xFFC0392B),
    'REVISION_REQUESTED': Color(0xFFD35400),
    'COMPLETED': Color(0xFF1ABC9C),
  };

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final r = await ApiClient.dio.get<Map<String, dynamic>>('/api/procurement-requests/${widget.requestId}');
      setState(() { _request = ProcurementRequestResponse.fromJson(r.data!); });
    } catch (_) {
      setState(() { _error = 'Request not found or access denied.'; });
    } finally {
      setState(() { _loading = false; });
    }
  }

  Future<void> _submit() async {
    try {
      await ApiClient.dio.post('/api/procurement-requests/${widget.requestId}/submit');
      _load();
    } catch (e) {
      setState(() { _actionError = 'Failed to submit request.'; });
    }
  }

  Future<void> _updateStatus(String newStatus) async {
    try {
      await ApiClient.dio.post('/api/procurement-requests/${widget.requestId}/status?newStatus=$newStatus');
      _load();
    } catch (e) {
      setState(() { _actionError = 'Status update failed.'; });
    }
  }

  Future<void> _delete() async {
    try {
      await ApiClient.dio.delete('/api/procurement-requests/${widget.requestId}');
      if (!mounted) return;
      Navigator.pop(context);
    } catch (e) {
      setState(() { _actionError = 'Delete failed.'; _confirmDelete = false; });
    }
  }

  List<Map<String, String>> _transitions(String status, String role) {
    final t = <Map<String, String>>[];
    if ((role == 'PROCUREMENT_OFFICER' || role == 'ADMIN') && status == 'SUBMITTED') {
      t.add({'label': 'Under Evaluation', 'status': 'UNDER_EVALUATION'});
    }
    if ((role == 'PROCUREMENT_OFFICER' || role == 'ADMIN') && status == 'UNDER_EVALUATION') {
      t.add({'label': 'Pending Approval', 'status': 'PENDING_APPROVAL'});
    }
    if (role == 'ADMIN' && status == 'PENDING_APPROVAL') {
      t.add({'label': 'Approve', 'status': 'APPROVED'});
      t.add({'label': 'Reject', 'status': 'REJECTED'});
      t.add({'label': 'Request Revision', 'status': 'REVISION_REQUESTED'});
    }
    if ((role == 'PROCUREMENT_OFFICER' || role == 'ADMIN') && status == 'APPROVED') {
      t.add({'label': 'Complete', 'status': 'COMPLETED'});
    }
    if (role == 'EMPLOYEE' && status == 'REVISION_REQUESTED') {
      t.add({'label': 'Return to Draft', 'status': 'DRAFT'});
    }
    return t;
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final user = auth.user;

    return Scaffold(
      appBar: AppBar(
        title: Text(_request?.requestNumber ?? 'Request Detail'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: Colors.red)))
              : _buildBody(user),
    );
  }

  Widget _buildBody(AuthUser? user) {
    final r = _request!;
    final isDraft = r.status == 'DRAFT';
    final isOwner = user?.userId == r.requesterId;
    final isEmployee = user?.role == 'EMPLOYEE';
    final role = user?.role ?? '';
    final transitions = _transitions(r.status, role);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Status chip
        Row(
          children: [
            Expanded(child: Text(r.title, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold))),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: _statusColors[r.status] ?? Colors.grey,
                borderRadius: BorderRadius.circular(14),
              ),
              child: Text(r.status, style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold)),
            ),
          ],
        ),
        if (_actionError != null) ...[
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(color: Colors.red[50], border: Border.all(color: Colors.red), borderRadius: BorderRadius.circular(4)),
            child: Text(_actionError!, style: const TextStyle(color: Colors.red)),
          ),
        ],

        // Actions
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 4,
          children: [
            if (isEmployee && isOwner && isDraft) ...[
              OutlinedButton(
                onPressed: () async {
                  await Navigator.push(context, MaterialPageRoute(builder: (_) => EditRequestScreen(requestId: r.id)));
                  _load();
                },
                child: const Text('Edit'),
              ),
              ElevatedButton(onPressed: _submit, child: const Text('Submit')),
              OutlinedButton(
                onPressed: () async {
                  await Navigator.push(context, MaterialPageRoute(builder: (_) => AiWorkflowScreen(requestId: r.id)));
                  _load();
                },
                style: OutlinedButton.styleFrom(foregroundColor: const Color(0xFF6C3483)),
                child: const Text('🤖 AI Assistant'),
              ),
            ],
            ...transitions.map((t) => OutlinedButton(
              onPressed: () => _updateStatus(t['status']!),
              child: Text(t['label']!),
            )),
            if ((isEmployee && isOwner && isDraft) || role == 'ADMIN')
              TextButton(
                onPressed: () {
                  if (_confirmDelete) {
                    _delete();
                  } else {
                    setState(() => _confirmDelete = true);
                  }
                },
                style: TextButton.styleFrom(foregroundColor: Colors.red),
                child: Text(_confirmDelete ? 'Confirm delete?' : 'Delete'),
              ),
            if (_confirmDelete)
              TextButton(onPressed: () => setState(() => _confirmDelete = false), child: const Text('Cancel')),
          ],
        ),

        const Divider(),
        _detail('Request Number', r.requestNumber),
        _detail('Priority', r.priority),
        _detail('Required By', DateTime.parse(r.requiredByDate).toLocal().toString().split(' ')[0]),
        _detail('Estimated Total', '\$${r.estimatedTotal.toStringAsFixed(2)}'),
        _detail('Created', r.createdAt.split('T')[0]),
        _detail('Updated', r.updatedAt.split('T')[0]),
        const SizedBox(height: 8),
        _section('Description', r.description),
        _section('Justification', r.justification),

        // Items
        const SizedBox(height: 8),
        const Text('Items', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        const SizedBox(height: 4),
        ...r.items.map((item) => Card(
          margin: const EdgeInsets.symmetric(vertical: 4),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.itemName, style: const TextStyle(fontWeight: FontWeight.w600)),
                if (item.description.isNotEmpty) Text(item.description, style: const TextStyle(color: Colors.black54, fontSize: 13)),
                const SizedBox(height: 4),
                Text('Qty: ${item.quantity} ${item.unit}  ·  Unit price: \$${item.estimatedUnitPrice.toStringAsFixed(2)}', style: const TextStyle(fontSize: 13)),
              ],
            ),
          ),
        )),
      ],
    );
  }

  Widget _detail(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 3),
    child: Row(
      children: [
        SizedBox(width: 140, child: Text(label, style: const TextStyle(color: Colors.black54, fontSize: 13))),
        Expanded(child: Text(value, style: const TextStyle(fontSize: 13))),
      ],
    ),
  );

  Widget _section(String title, String body) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: const TextStyle(fontWeight: FontWeight.w600, color: Colors.black54, fontSize: 13)),
        const SizedBox(height: 2),
        Text(body, style: const TextStyle(fontSize: 14)),
        const SizedBox(height: 6),
      ],
    ),
  );
}
