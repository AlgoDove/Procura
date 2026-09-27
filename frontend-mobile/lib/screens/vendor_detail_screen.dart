import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/auth_provider.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';
import 'edit_vendor_screen.dart';

class VendorDetailScreen extends StatefulWidget {
  final String vendorId;
  const VendorDetailScreen({super.key, required this.vendorId});

  @override
  State<VendorDetailScreen> createState() => _VendorDetailScreenState();
}

class _VendorDetailScreenState extends State<VendorDetailScreen> {
  VendorResponse? _vendor;
  bool _loading = true;
  String? _error;
  String? _actionError;
  bool _actionLoading = false;

  static const _statusColors = <String, Color>{
    'ACTIVE': Color(0xFF27AE60),
    'INACTIVE': Colors.grey,
  };

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _actionError = null;
    });
    try {
      final r = await ApiClient.dio
          .get<Map<String, dynamic>>('/api/vendors/${widget.vendorId}');
      if (!mounted) return;
      setState(() {
        _vendor = VendorResponse.fromJson(r.data!);
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Vendor not found or access denied.';
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

  Future<void> _deactivate() async {
    setState(() {
      _actionLoading = true;
      _actionError = null;
    });
    try {
      await ApiClient.dio
          .post('/api/vendors/${widget.vendorId}/deactivate');
      _load();
    } on DioException catch (e) {
      final msg = e.response?.statusCode == 409
          ? 'Vendor is already inactive.'
          : 'Failed to deactivate vendor.';
      setState(() {
        _actionError = msg;
      });
    } catch (_) {
      setState(() {
        _actionError = 'An unexpected error occurred.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _actionLoading = false;
        });
      }
    }
  }

  Future<void> _activate() async {
    setState(() {
      _actionLoading = true;
      _actionError = null;
    });
    try {
      await ApiClient.dio
          .post('/api/vendors/${widget.vendorId}/activate');
      _load();
    } on DioException catch (e) {
      final msg = e.response?.statusCode == 409
          ? 'Vendor is already active.'
          : 'Failed to activate vendor.';
      setState(() {
        _actionError = msg;
      });
    } catch (_) {
      setState(() {
        _actionError = 'An unexpected error occurred.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _actionLoading = false;
        });
      }
    }
  }

  String _formatDate(String isoString) {
    if (isoString.isEmpty) return '—';
    final parsed = DateTime.tryParse(isoString);
    if (parsed != null) {
      return parsed.toLocal().toString().split(' ')[0];
    }
    return isoString.contains('T') ? isoString.split('T')[0] : isoString;
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final canManage = auth.user?.role == 'PROCUREMENT_OFFICER' ||
        auth.user?.role == 'MANAGER' ||
        auth.user?.role == 'ADMIN';

    return Scaffold(
      appBar: AppBar(
        title: Text(_vendor?.name ?? 'Vendor Detail'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _load,
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.error_outline,
                          size: 48, color: Colors.red),
                      const SizedBox(height: 16),
                      Text(_error!,
                          style: const TextStyle(color: Colors.red, fontSize: 16)),
                      const SizedBox(height: 16),
                      ElevatedButton(
                        onPressed: _load,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                )
              : _buildBody(canManage),
    );
  }

  Widget _buildBody(bool canManage) {
    final v = _vendor!;
    final color = _statusColors[v.status] ?? Colors.grey;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    v.name,
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    v.category,
                    style: TextStyle(
                      fontSize: 14,
                      color: Colors.grey[700],
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
              decoration: BoxDecoration(
                color: color,
                borderRadius: BorderRadius.circular(14),
              ),
              child: Text(
                v.status,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 12,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ],
        ),

        if (_actionError != null) ...[
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: Colors.red[50],
              border: Border.all(color: Colors.red),
              borderRadius: BorderRadius.circular(6),
            ),
            child: Text(
              _actionError!,
              style: const TextStyle(color: Colors.red),
            ),
          ),
        ],

        // Management Actions
        if (canManage) ...[
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton.icon(
                onPressed: () async {
                  final result = await Navigator.push<bool>(
                    context,
                    MaterialPageRoute(
                      builder: (_) => EditVendorScreen(vendorId: v.id),
                    ),
                  );
                  if (result == true) {
                    _load();
                  }
                },
                icon: const Icon(Icons.edit, size: 18),
                label: const Text('Edit'),
              ),
              if (v.status == 'ACTIVE')
                OutlinedButton.icon(
                  onPressed: _actionLoading ? null : _deactivate,
                  icon: const Icon(Icons.block, size: 18, color: Colors.red),
                  label: const Text('Deactivate'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.red,
                    side: const BorderSide(color: Colors.red),
                  ),
                ),
              if (v.status == 'INACTIVE')
                ElevatedButton.icon(
                  onPressed: _actionLoading ? null : _activate,
                  icon: const Icon(Icons.check_circle_outline, size: 18),
                  label: const Text('Activate'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF27AE60),
                  ),
                ),
            ],
          ),
        ],

        const Divider(height: 32),

        const Text(
          'Contact & Overview',
          style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),

        Card(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: BorderSide(color: Colors.grey.shade200),
          ),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                _detailRow('Contact Person', v.contactPerson),
                _divider(),
                _detailRow('Email', v.email),
                _divider(),
                _detailRow('Phone Number', v.phoneNumber),
                _divider(),
                _detailRow('Address', v.address?.isNotEmpty == true ? v.address! : '—'),
                _divider(),
                _detailRow(
                  'Rating',
                  '${v.rating.toStringAsFixed(1)} / 5.0',
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.star, size: 16, color: Colors.amber[700]),
                      const SizedBox(width: 4),
                      Text(
                        v.rating.toStringAsFixed(1),
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
                _divider(),
                _detailRow('Created Date', _formatDate(v.createdAt)),
                _divider(),
                _detailRow('Updated Date', _formatDate(v.updatedAt)),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _divider() => const Divider(height: 16, thickness: 0.5);

  Widget _detailRow(String label, String value, {Widget? trailing}) => Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(
              label,
              style: const TextStyle(
                color: Colors.black54,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Expanded(
            child: trailing ??
                Text(
                  value,
                  style: const TextStyle(
                    fontSize: 14,
                    color: Colors.black87,
                  ),
                ),
          ),
        ],
      );
}
