import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/auth_provider.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import '../utils/formatters.dart';
import 'request_detail_screen.dart';
import 'create_request_screen.dart';

class RequestsListScreen extends StatefulWidget {
  const RequestsListScreen({super.key});

  @override
  State<RequestsListScreen> createState() => _RequestsListScreenState();
}

class _RequestsListScreenState extends State<RequestsListScreen> {
  List<ProcurementRequestResponse> _requests = [];
  bool _loading = true;
  String? _error;
  String _search = '';
  String _statusFilter = '';

  static const _statusColors = {
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
    _loadRequests();
  }

  Future<void> _loadRequests() async {
    setState(() { _loading = true; _error = null; });
    try {
      final response = await ApiClient.dio.get<List<dynamic>>('/api/procurement-requests');
      final data = response.data!
          .map((e) => ProcurementRequestResponse.fromJson(e as Map<String, dynamic>))
          .toList();
      if (!mounted) return;
      setState(() { _requests = data; });
    } catch (_) {
      if (mounted) setState(() { _error = 'Failed to load requests.'; });
    } finally {
      if (mounted) setState(() { _loading = false; });
    }
  }

  List<ProcurementRequestResponse> get _filtered {
    return _requests.where((r) {
      final matchSearch = _search.isEmpty ||
          r.title.toLowerCase().contains(_search.toLowerCase()) ||
          r.requestNumber.toLowerCase().contains(_search.toLowerCase());
      final matchStatus = _statusFilter.isEmpty || r.status == _statusFilter;
      return matchSearch && matchStatus;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final canCreate = auth.user?.role == 'EMPLOYEE';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Procurement Requests'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadRequests,
            tooltip: 'Refresh',
          ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list),
            tooltip: 'Filter by status',
            onSelected: (v) => setState(() => _statusFilter = v),
            itemBuilder: (_) => [
              const PopupMenuItem(value: '', child: Text('All statuses')),
              ..._statusColors.keys.map((s) => PopupMenuItem(value: s, child: Text(formatStatus(s)))),
            ],
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () => context.read<AuthProvider>().logout(),
          ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            color: Colors.white,
            padding: const EdgeInsets.all(16),
            child: TextField(
              decoration: InputDecoration(
                hintText: 'Search by title or number...',
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
          if (_statusFilter.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
              child: Row(
                children: [
                  Chip(
                    label: Text('Status: ${formatStatus(_statusFilter)}'),
                    onDeleted: () => setState(() => _statusFilter = ''),
                    backgroundColor: Colors.blue[50],
                  ),
                ],
              ),
            ),
          Expanded(
            child: _buildBody(),
          ),
        ],
      ),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const CreateRequestScreen()),
                );
                _loadRequests();
              },
              icon: const Icon(Icons.add),
              label: const Text('New Request'),
            )
          : null,
    );
  }

  Widget _buildBody() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 16),
            Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 16)),
            const SizedBox(height: 16),
            ElevatedButton(onPressed: _loadRequests, child: const Text('Retry')),
          ],
        ),
      );
    }
    
    if (_filtered.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.inbox_outlined, size: 64, color: Colors.grey[400]),
            const SizedBox(height: 16),
            const Text('No requests found', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.black87)),
            const SizedBox(height: 8),
            const Text('Create your first request to get started.', style: TextStyle(color: Colors.black54)),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadRequests,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        itemCount: _filtered.length,
        separatorBuilder: (_, _) => const SizedBox(height: 8),
        itemBuilder: (_, i) {
          final r = _filtered[i];
          final color = _statusColors[r.status] ?? Colors.grey;
          
          return Card(
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
              side: BorderSide(color: Colors.grey.shade200),
            ),
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => RequestDetailScreen(requestId: r.id)),
                );
                _loadRequests();
              },
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(
                            r.title, 
                            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: color,
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            formatStatus(r.status),
                            style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text('${r.requestNumber} • Priority: ${formatStatus(r.priority)} • Total: ${formatTotal(r.estimatedTotal)}', style: TextStyle(color: Colors.grey[600], fontSize: 13)),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
