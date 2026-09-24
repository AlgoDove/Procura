import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/auth_provider.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
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
      setState(() { _requests = data; });
    } catch (_) {
      setState(() { _error = 'Failed to load requests.'; });
    } finally {
      setState(() { _loading = false; });
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
    final canCreate = auth.user?.role == 'EMPLOYEE' ||
        auth.user?.role == 'PROCUREMENT_OFFICER' ||
        auth.user?.role == 'ADMIN';

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
              ..._statusColors.keys.map((s) => PopupMenuItem(value: s, child: Text(s))),
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
        children: [
          Padding(
            padding: const EdgeInsets.all(8),
            child: TextField(
              decoration: const InputDecoration(
                hintText: 'Search by title or number…',
                prefixIcon: Icon(Icons.search),
                border: OutlineInputBorder(),
                contentPadding: EdgeInsets.symmetric(vertical: 8, horizontal: 12),
              ),
              onChanged: (v) => setState(() => _search = v),
            ),
          ),
          if (_statusFilter.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Row(
                children: [
                  Chip(
                    label: Text('Status: $_statusFilter'),
                    onDeleted: () => setState(() => _statusFilter = ''),
                  ),
                ],
              ),
            ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                    ? Center(child: Text(_error!, style: const TextStyle(color: Colors.red)))
                    : _filtered.isEmpty
                        ? const Center(child: Text('No requests found.'))
                        : RefreshIndicator(
                            onRefresh: _loadRequests,
                            child: ListView.builder(
                              itemCount: _filtered.length,
                              itemBuilder: (_, i) {
                                final r = _filtered[i];
                                return ListTile(
                                  title: Text(r.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                                  subtitle: Text('${r.requestNumber} · ${r.priority}'),
                                  trailing: Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                    decoration: BoxDecoration(
                                      color: _statusColors[r.status] ?? Colors.grey,
                                      borderRadius: BorderRadius.circular(12),
                                    ),
                                    child: Text(
                                      r.status,
                                      style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
                                    ),
                                  ),
                                  onTap: () => Navigator.push(
                                    context,
                                    MaterialPageRoute(builder: (_) => RequestDetailScreen(requestId: r.id)),
                                  ),
                                );
                              },
                            ),
                          ),
          ),
        ],
      ),
      floatingActionButton: canCreate
          ? FloatingActionButton(
              onPressed: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const CreateRequestScreen()),
                );
                _loadRequests();
              },
              tooltip: 'New Request',
              child: const Icon(Icons.add),
            )
          : null,
    );
  }
}
