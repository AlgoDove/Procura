import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthUser {
  final String userId;
  final String email;
  final String role;

  AuthUser({required this.userId, required this.email, required this.role});
}

class AuthService {
  static const _storage = FlutterSecureStorage();
  static const _tokenKey = 'token';

  static Future<void> saveToken(String token) async {
    await _storage.write(key: _tokenKey, value: token);
  }

  static Future<String?> getToken() async {
    return _storage.read(key: _tokenKey);
  }

  static Future<void> clearToken() async {
    await _storage.delete(key: _tokenKey);
  }

  /// Decode JWT payload without signature verification (client-side only)
  static AuthUser? decodeToken(String token) {
    try {
      final parts = token.split('.');
      if (parts.length != 3) return null;

      // Base64Url decode the payload segment
      String payload = parts[1];
      // Pad to multiple of 4
      payload += '=' * ((4 - payload.length % 4) % 4);
      final decoded = utf8.decode(base64Url.decode(payload));
      final Map<String, dynamic> claims = json.decode(decoded) as Map<String, dynamic>;

      // Check expiry
      final exp = claims['exp'] as int?;
      if (exp != null) {
        final expiry = DateTime.fromMillisecondsSinceEpoch(exp * 1000);
        if (expiry.isBefore(DateTime.now())) return null;
      }

      return AuthUser(
        userId: claims['sub'] as String,
        email: claims['email'] as String,
        role: claims['role'] as String,
      );
    } catch (_) {
      return null;
    }
  }

  static Future<AuthUser?> getCurrentUser() async {
    final token = await getToken();
    if (token == null) return null;
    return decodeToken(token);
  }
}
