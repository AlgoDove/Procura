import 'package:flutter/foundation.dart';
import '../services/auth_service.dart';

class AuthProvider extends ChangeNotifier {
  AuthUser? _user;
  bool _isLoading = true;

  AuthUser? get user => _user;
  bool get isLoading => _isLoading;
  bool get isAuthenticated => _user != null;

  AuthProvider() {
    _init();
  }

  Future<void> _init() async {
    _user = await AuthService.getCurrentUser();
    _isLoading = false;
    notifyListeners();
  }

  Future<void> loginWithToken(String token) async {
    await AuthService.saveToken(token);
    _user = AuthService.decodeToken(token);
    notifyListeners();
  }

  Future<void> logout() async {
    await AuthService.clearToken();
    _user = null;
    notifyListeners();
  }
}
