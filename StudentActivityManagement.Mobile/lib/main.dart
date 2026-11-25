import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';

import 'constants/app_styles.dart';
import 'providers/auth_provider.dart';
import 'providers/notification_provider.dart';
import 'screens/auth/login_screen.dart';
import 'screens/auth/splash_screen.dart';
import 'screens/main_page.dart';
import 'screens/profile/change_password_screen.dart';
import 'services/preference_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await PreferenceService.instance.init();
  await initializeDateFormatting('vi');
  await _loadFonts();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthProvider()..restore()),
        ChangeNotifierProvider(create: (_) => NotificationProvider()),
      ],
      child: const Nhom8App(),
    ),
  );
}

/// Inter (the free substitute for Linear's sans) is fetched by google_fonts; waiting for it before the first frame keeps text
/// from being laid out with a fallback font and re-measured. Offline, the app starts with the fallback.
Future<void> _loadFonts() async {
  try {
    await GoogleFonts.pendingFonts([
      for (final weight in [FontWeight.w400, FontWeight.w500, FontWeight.w600, FontWeight.w700]) GoogleFonts.inter(fontWeight: weight),
    ]).timeout(const Duration(seconds: 6));
  } on Object {
    // Font download failed or timed out: keep going with the platform font.
  }
}

/// US-30: Mobile app for students and class monitors on the shared REST API.
class Nhom8App extends StatelessWidget {
  const Nhom8App({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final status = auth.status;
    return MaterialApp(
      key: ValueKey((status, auth.requiresPasswordChange)),
      title: 'Hoạt động & Điểm rèn luyện',
      debugShowCheckedModeBanner: false,
      theme: AppStyles.theme,
      locale: const Locale('vi'),
      supportedLocales: const [Locale('vi'), Locale('en')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: switch (status) {
        AuthStatus.unknown => const SplashScreen(),
        AuthStatus.signedOut => const LoginScreen(),
        AuthStatus.signedIn when auth.requiresPasswordChange => const _PasswordChangeGate(),
        AuthStatus.signedIn => const MainPage(),
      },
    );
  }
}

class _PasswordChangeGate extends StatefulWidget {
  const _PasswordChangeGate();

  @override
  State<_PasswordChangeGate> createState() => _PasswordChangeGateState();
}

class _PasswordChangeGateState extends State<_PasswordChangeGate> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const ChangePasswordScreen(requiredChange: true)),
        (_) => false,
      );
    });
  }

  @override
  Widget build(BuildContext context) => const SplashScreen();
}
