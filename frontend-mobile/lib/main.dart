import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'providers/auth_provider.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/requests_list_screen.dart';
import 'screens/request_detail_screen.dart';
import 'screens/create_request_screen.dart';
import 'screens/edit_request_screen.dart';
import 'screens/ai_workflow_screen.dart';

void main() {
  runApp(
    ChangeNotifierProvider(
      create: (_) => AuthProvider(),
      child: const ProcuraApp(),
    ),
  );
}

class ProcuraApp extends StatelessWidget {
  const ProcuraApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Procura',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF1E3A5F)),
        useMaterial3: true,
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF1E3A5F),
          foregroundColor: Colors.white,
          elevation: 2,
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF1E3A5F),
            foregroundColor: Colors.white,
          ),
        ),
      ),
      home: const _RootRouter(),
      onGenerateRoute: (settings) {
        switch (settings.name) {
          case '/login':
            return MaterialPageRoute(builder: (_) => const LoginScreen());
          case '/register':
            return MaterialPageRoute(builder: (_) => const RegisterScreen());
          case '/requests':
            return MaterialPageRoute(builder: (_) => const RequestsListScreen());
          case '/requests/create':
            return MaterialPageRoute(builder: (_) => const CreateRequestScreen());
          default:
            if (settings.name?.startsWith('/requests/') == true) {
              final parts = settings.name!.split('/');
              final id = parts[2];
              if (parts.length == 4 && parts[3] == 'edit') {
                return MaterialPageRoute(
                  builder: (_) => EditRequestScreen(requestId: id),
                );
              }
              if (parts.length == 4 && parts[3] == 'ai') {
                return MaterialPageRoute(
                  builder: (_) => AiWorkflowScreen(requestId: id),
                );
              }
              return MaterialPageRoute(
                builder: (_) => RequestDetailScreen(requestId: id),
              );
            }
            return null;
        }
      },
    );
  }
}

class _RootRouter extends StatelessWidget {
  const _RootRouter();

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    if (auth.isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (!auth.isAuthenticated) {
      return const LoginScreen();
    }

    return const RequestsListScreen();
  }
}
