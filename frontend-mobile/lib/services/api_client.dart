import 'package:dio/dio.dart';
import '../config.dart';
import '../services/auth_service.dart';

class ApiClient {
  static final Dio _dio = _createDio();

  static Dio _createDio() {
    final dio = Dio(BaseOptions(
      baseUrl: apiUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30),
      headers: {'Content-Type': 'application/json'},
    ));

    // Attach JWT token to all requests
    dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await AuthService.getToken();
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
      onError: (error, handler) async {
        if (error.response?.statusCode == 401) {
          await AuthService.clearToken();
          // Callers check auth state; navigation handled by provider listener
        }
        handler.next(error);
      },
    ));

    return dio;
  }

  static Dio get dio => _dio;
}
