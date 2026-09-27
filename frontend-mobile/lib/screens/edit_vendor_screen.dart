import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../models/api_models.dart';

class EditVendorScreen extends StatefulWidget {
  final String vendorId;
  const EditVendorScreen({super.key, required this.vendorId});

  @override
  State<EditVendorScreen> createState() => _EditVendorScreenState();
}

class _EditVendorScreenState extends State<EditVendorScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _contactPersonController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _addressController = TextEditingController();
  final _categoryController = TextEditingController();
  final _ratingController = TextEditingController();

  bool _initialLoading = true;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadVendor();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _contactPersonController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _addressController.dispose();
    _categoryController.dispose();
    _ratingController.dispose();
    super.dispose();
  }

  Future<void> _loadVendor() async {
    setState(() {
      _initialLoading = true;
      _error = null;
    });

    try {
      final r = await ApiClient.dio
          .get<Map<String, dynamic>>('/api/vendors/${widget.vendorId}');
      final v = VendorResponse.fromJson(r.data!);
      if (!mounted) return;
      _nameController.text = v.name;
      _contactPersonController.text = v.contactPerson;
      _emailController.text = v.email;
      _phoneController.text = v.phoneNumber;
      _addressController.text = v.address ?? '';
      _categoryController.text = v.category;
      _ratingController.text = v.rating.toString();
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Failed to load vendor details.';
        });
      }
    } finally {
      if (mounted) {
        setState(() {
          _initialLoading = false;
        });
      }
    }
  }

  String _extractErrorMessage(dynamic e) {
    if (e is DioException) {
      if (e.response?.data is Map) {
        final map = e.response!.data as Map;
        if (map['message'] != null && map['message'].toString().isNotEmpty) {
          return map['message'].toString();
        }
        if (map['errors'] is Map) {
          final errMap = map['errors'] as Map;
          final messages = <String>[];
          for (final val in errMap.values) {
            if (val is List) {
              messages.addAll(val.map((x) => x.toString()));
            } else if (val != null) {
              messages.add(val.toString());
            }
          }
          if (messages.isNotEmpty) return messages.join('\n');
        }
        if (map['errors'] is List && (map['errors'] as List).isNotEmpty) {
          return (map['errors'] as List).map((x) => x.toString()).join('\n');
        }
        if (map['title'] != null && map['title'].toString().isNotEmpty) {
          return map['title'].toString();
        }
      }
      if (e.type == DioExceptionType.connectionTimeout ||
          e.type == DioExceptionType.sendTimeout ||
          e.type == DioExceptionType.receiveTimeout) {
        return 'Connection timed out. Please check your network connection.';
      }
      if (e.type == DioExceptionType.connectionError) {
        return 'Could not connect to backend server. Please verify the server is running.';
      }
    }
    return 'Failed to update vendor. Please check your input and try again.';
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    final ratingVal = double.tryParse(_ratingController.text.trim()) ?? 0.0;
    if (ratingVal < 0 || ratingVal > 5) {
      setState(() {
        _error = 'Rating must be between 0 and 5.';
      });
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final dto = UpdateVendorDto(
        name: _nameController.text.trim(),
        contactPerson: _contactPersonController.text.trim(),
        email: _emailController.text.trim(),
        phoneNumber: _phoneController.text.trim(),
        address: _addressController.text.trim().isNotEmpty
            ? _addressController.text.trim()
            : null,
        category: _categoryController.text.trim(),
        rating: ratingVal,
      );

      await ApiClient.dio.put<Map<String, dynamic>>(
        '/api/vendors/${widget.vendorId}',
        data: dto.toJson(),
      );

      if (!mounted) return;
      Navigator.pop(context, true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = _extractErrorMessage(e);
        });
      }
    } finally {
      if (mounted) {
        setState(() {
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Edit Vendor'),
      ),
      body: _initialLoading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (_error != null) ...[
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Colors.red[50],
                          border: Border.all(color: Colors.red.shade300),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          _error!,
                          style: const TextStyle(color: Colors.red),
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],

                    Card(
                      elevation: 0,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                        side: BorderSide(color: Colors.grey.shade200),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            TextFormField(
                              controller: _nameController,
                              decoration: const InputDecoration(
                                labelText: 'Vendor Name *',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) => v == null || v.trim().isEmpty
                                  ? 'Vendor name is required'
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _categoryController,
                              decoration: const InputDecoration(
                                labelText: 'Category *',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) => v == null || v.trim().isEmpty
                                  ? 'Category is required'
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _contactPersonController,
                              decoration: const InputDecoration(
                                labelText: 'Contact Person *',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) => v == null || v.trim().isEmpty
                                  ? 'Contact person is required'
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _emailController,
                              keyboardType: TextInputType.emailAddress,
                              decoration: const InputDecoration(
                                labelText: 'Contact Email *',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) {
                                if (v == null || v.trim().isEmpty) {
                                  return 'Contact email is required';
                                }
                                if (!RegExp(r'^[^@]+@[^@]+\.[^@]+')
                                    .hasMatch(v.trim())) {
                                  return 'Please enter a valid email address';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _phoneController,
                              keyboardType: TextInputType.phone,
                              decoration: const InputDecoration(
                                labelText: 'Contact Phone *',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) => v == null || v.trim().isEmpty
                                  ? 'Contact phone is required'
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _ratingController,
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                      decimal: true),
                              decoration: const InputDecoration(
                                labelText: 'Rating (0 - 5)',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) {
                                if (v != null && v.trim().isNotEmpty) {
                                  final n = double.tryParse(v.trim());
                                  if (n == null || n < 0 || n > 5) {
                                    return 'Rating must be a number between 0 and 5';
                                  }
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _addressController,
                              maxLines: 2,
                              decoration: const InputDecoration(
                                labelText: 'Address (Optional)',
                                border: OutlineInputBorder(),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),

                    const SizedBox(height: 24),

                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton(
                            onPressed:
                                _saving ? null : () => Navigator.pop(context),
                            child: const Text('Cancel'),
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: ElevatedButton(
                            onPressed: _saving ? null : _save,
                            child: _saving
                                ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      color: Colors.white,
                                    ),
                                  )
                                : const Text('Save Changes'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}
