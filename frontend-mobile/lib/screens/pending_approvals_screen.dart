import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import '../utils/formatters.dart';
import 'request_detail_screen.dart';

class PendingApprovalsScreen extends StatefulWidget {
  const PendingApprovalsScreen({super.key});

  @override
  State<PendingApprovalsScreen> createState() => _PendingApprovalsScreenState();
}

class _PendingApprovalsScreenState extends State<PendingApprovalsScreen> {
  List<ApprovalWorkflowResponse> _workflows = [];
  bool _loading = true;
  String? _error;
  String _search = '';

  @override
  void initState() {
    super.initState();
    _loadPending();
  }

  Future<void> _loadPending() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final res = await ApiClient.dio.get<List<dynamic>>('/api/approval-workflows/pending');
      final data = res.data!
          .map((e) => ApprovalWorkflowResponse.fromJson(e as Map<String, dynamic>))
          .toList();
      if (!mounted) return;
      setState(() {
        _workflows = data;
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load pending approvals. Ensure you have managerial permissions.';
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

  List<ApprovalWorkflowResponse> get _filtered {
    if (_search.isEmpty) return _workflows;
    final q = _search.toLowerCase();
    return _workflows.where((w) {
      return w.requestNumber.toLowerCase().contains(q) ||
          w.requestTitle.toLowerCase().contains(q) ||
          w.requesterName.toLowerCase().contains(q);
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Pending Approvals'),
        backgroundColor: const Color(0xFF1E3A5F),
        foregroundColor: Colors.white,
      ),
      body: Column(
        children: [
          Container(
            color: Colors.white,
            padding: const EdgeInsets.all(16),
            child: TextField(
              decoration: InputDecoration(
                hintText: 'Search pending approvals...',
                prefixIcon: const Icon(Icons.search, color: Colors.black45),
                filled: true,
                fillColor: Colors.grey[100],
                contentPadding: const EdgeInsets.symmetric(vertical: 0, horizontal: 16),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide.none,
                ),
              ),
              onChanged: (v) => setState(() => _search = v),
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _loadPending,
              child: _buildBody(),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!, textAlign: TextAlign.center, style: const TextStyle(color: Colors.red)),
              const SizedBox(height: 12),
              ElevatedButton(onPressed: _loadPending, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    final list = _filtered;
    if (list.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(32),
          child: Text(
            'No procurement requests currently awaiting your approval.',
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.black54, fontSize: 14),
          ),
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.all(12),
      itemCount: list.length,
      itemBuilder: (ctx, i) {
        final w = list[i];
        final eval = w.vendorRecommendationSummary;

        return Card(
          margin: const EdgeInsets.only(bottom: 12),
          elevation: 2,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      w.requestNumber,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                        color: Color(0xFF1E3A5F),
                      ),
                    ),
                    Text(
                      formatTotal(w.estimatedTotal),
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                        color: Color(0xFF15803D),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  w.requestTitle,
                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                ),
                const SizedBox(height: 4),
                Text(
                  'Requester: ${w.requesterName} • ${formatDate(w.createdAt)}',
                  style: const TextStyle(fontSize: 12, color: Colors.black54),
                ),
                if (eval != null && eval.topScore != null) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFAF5FF),
                      border: Border.all(color: const Color(0xFFE9D5FF)),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      'AI Top Vendor Score: ${eval.topScore!.toStringAsFixed(1)}/100',
                      style: const TextStyle(
                        fontSize: 11,
                        color: Color(0xFF6C3483),
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerRight,
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1E3A5F),
                      foregroundColor: Colors.white,
                    ),
                    icon: const Icon(Icons.rate_review, size: 16),
                    label: const Text('Review & Decide'),
                    onPressed: () async {
                      await Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => RequestDetailScreen(requestId: w.procurementRequestId),
                        ),
                      );
                      _loadPending();
                    },
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
