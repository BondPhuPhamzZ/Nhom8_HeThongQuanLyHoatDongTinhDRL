import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';

import 'constants/app_styles.dart';
import 'providers/auth_provider.dart';
import 'screens/auth/login_screen.dart';
import 'screens/auth/splash_screen.dart';
import 'screens/main_page.dart';
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
      ],
      child: const Nhom8App(),
    ),
  );
}

/// Lato (design system font) is fetched by google_fonts; waiting for it before the first frame keeps text
/// from being laid out with a fallback font and re-measured. Offline, the app starts with the fallback.
Future<void> _loadFonts() async {
  try {
    await GoogleFonts.pendingFonts([
      for (final weight in [FontWeight.w400, FontWeight.w500, FontWeight.w600, FontWeight.w700]) GoogleFonts.lato(fontWeight: weight),
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
    final status = context.watch<AuthProvider>().status;
    return MaterialApp(
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
        AuthStatus.signedIn => const MainPage(),
      },
    );
  }
}
