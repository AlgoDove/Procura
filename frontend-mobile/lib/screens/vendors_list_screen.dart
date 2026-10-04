import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/auth_provider.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import 'vendor_detail_screen.dart';
import 'create_vendor_screen.dart';

class VendorsListScreen extends StatefulWidget {
  const VendorsListScreen({super.key});

  @override
  State<VendorsListScreen> createState() => _VendorsListScreenState();
}

class _VendorsListScreenState extends State<VendorsListScreen> {
  List<VendorResponse> _vendors = [];
  bool _loading = true;
  String? _error;
  String _search = '';
  String _statusFilter = '';
  String _categoryFilter = '';

  static const _statusColors = <String, Color>{
    'ACTIVE': Color(0xFF27AE60),
    'INACTIVE': Colors.grey,
  };

  @override
  void initState() {
    super.initState();
    _loadVendors();
  }

  Future<void> _loadVendors() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final response = await ApiClient.dio.get<List<dynamic>>('/api/vendors');
      final data = (response.data ?? [])
          .map((e) => VendorResponse.fromJson(e as Map<String, dynamic>))
          .toList();
      if (!mounted) return;
      setState(() {
        _vendors = data;
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load vendors.';
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

  List<String> get _categories {
    final set = _vendors.map((v) => v.category).toSet().toList();
    set.sort();
    return set;
  }

  List<VendorResponse> get _filtered {
    return _vendors.where((v) {
      final searchLower = _search.toLowerCase();
      final matchSearch = _search.isEmpty ||
          v.name.toLowerCase().contains(searchLower) ||
          v.contactPerson.toLowerCase().contains(searchLower) ||
          v.category.toLowerCase().contains(searchLower);
      final matchStatus = _statusFilter.isEmpty || v.status == _statusFilter;
      final matchCategory =
          _categoryFilter.isEmpty || v.category == _categoryFilter;
      return matchSearch && matchStatus && matchCategory;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final canManage = auth.user?.role == 'PROCUREMENT_OFFICER' ||
        auth.user?.role == 'MANAGER' ||
        auth.user?.role == 'ADMIN';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Vendors'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadVendors,
            tooltip: 'Refresh',
          ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list),
            tooltip: 'Filter by status',
            onSelected: (v) => setState(() => _statusFilter = v),
            itemBuilder: (_) => const [
              PopupMenuItem(value: '', child: Text('All statuses')),
              PopupMenuItem(value: 'ACTIVE', child: Text('Active')),
              PopupMenuItem(value: 'INACTIVE', child: Text('Inactive')),
            ],
          ),
          if (_categories.isNotEmpty)
            PopupMenuButton<String>(
              icon: const Icon(Icons.category_outlined),
              tooltip: 'Filter by category',
              onSelected: (v) => setState(() => _categoryFilter = v),
              itemBuilder: (_) => [
                const PopupMenuItem(value: '', child: Text('All categories')),
                ..._categories.map(
                  (c) => PopupMenuItem(value: c, child: Text(c)),
                ),
              ],
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
                hintText: 'Search vendors by name, contact, or category...',
                prefixIcon: const Icon(Icons.search, color: Colors.black45),
                filled: true,
                fillColor: Colors.grey[100],
                contentPadding:
                    const EdgeInsets.symmetric(vertical: 0, horizontal: 16),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide.none,
                ),
              ),
              onChanged: (v) => setState(() => _search = v),
            ),
          ),
          if (_statusFilter.isNotEmpty || _categoryFilter.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
              child: Wrap(
                spacing: 8,
                children: [
                  if (_statusFilter.isNotEmpty)
                    Chip(
                      label: Text('Status: $_statusFilter'),
                      onDeleted: () => setState(() => _statusFilter = ''),
                      backgroundColor: Colors.blue[50],
                    ),
                  if (_categoryFilter.isNotEmpty)
                    Chip(
                      label: Text('Category: $_categoryFilter'),
                      onDeleted: () => setState(() => _categoryFilter = ''),
                      backgroundColor: Colors.purple[50],
                    ),
                ],
              ),
            ),
          Expanded(
            child: _buildBody(canManage),
          ),
        ],
      ),
      floatingActionButton: canManage
          ? FloatingActionButton.extended(
              onPressed: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => const CreateVendorScreen(),
                  ),
                );
                _loadVendors();
              },
              icon: const Icon(Icons.add),
              label: const Text('New Vendor'),
            )
          : null,
    );
  }

  Widget _buildBody(bool canManage) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 16),
            Text(_error!,
                style: const TextStyle(color: Colors.red, fontSize: 16)),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _loadVendors,
              child: const Text('Retry'),
            ),
          ],
        ),
      );
    }

    if (_filtered.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.storefront_outlined, size: 64, color: Colors.grey[400]),
            const SizedBox(height: 16),
            const Text(
              'No vendors found',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: Colors.black87,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              canManage && _vendors.isEmpty
                  ? 'Add your first vendor to get started.'
                  : 'Try changing your search or filter criteria.',
              style: const TextStyle(color: Colors.black54),
            ),
            if (canManage && _vendors.isEmpty) ...[
              const SizedBox(height: 16),
              ElevatedButton.icon(
                onPressed: () async {
                  await Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => const CreateVendorScreen(),
                    ),
                  );
                  _loadVendors();
                },
                icon: const Icon(Icons.add),
                label: const Text('Add First Vendor'),
              ),
            ],
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadVendors,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        itemCount: _filtered.length,
        separatorBuilder: (_, _) => const SizedBox(height: 8),
        itemBuilder: (_, i) {
          final v = _filtered[i];
          final color = _statusColors[v.status] ?? Colors.grey;

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
                  MaterialPageRoute(
                    builder: (_) => VendorDetailScreen(vendorId: v.id),
                  ),
                );
                _loadVendors();
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
                            v.name,
                            style: const TextStyle(
                              fontWeight: FontWeight.w600,
                              fontSize: 16,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: color,
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            v.status,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: Colors.grey[100],
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Text(
                            v.category,
                            style: TextStyle(
                              color: Colors.grey[800],
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Icon(Icons.star, size: 14, color: Colors.amber[700]),
                        const SizedBox(width: 2),
                        Text(
                          v.rating.toStringAsFixed(1),
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: Colors.grey[800],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${v.contactPerson} • ${v.email}',
                      style: TextStyle(color: Colors.grey[600], fontSize: 13),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
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
