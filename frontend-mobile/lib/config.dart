// API URL is injected via --dart-define=API_URL=...
// Default is 10.0.2.2:5071 for Android emulator (maps to host machine localhost)
const String apiUrl = String.fromEnvironment(
  'API_URL',
  defaultValue: 'http://10.0.2.2:5071',
);
