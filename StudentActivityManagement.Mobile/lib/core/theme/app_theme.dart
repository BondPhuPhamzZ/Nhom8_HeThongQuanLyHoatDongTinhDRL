import 'package:flutter/material.dart';

abstract final class AppTheme {
  static const primary = Color(0xFF007BFF);
  static const charcoal = Color(0xFF343A40);
  static const surface = Color(0xFFF8F9FA);
  static const success = Color(0xFF28A745);
  static const warning = Color(0xFFFFC107);

  static ThemeData get light => ThemeData(
    useMaterial3: true,
    scaffoldBackgroundColor: surface,
    colorScheme: ColorScheme.fromSeed(
      seedColor: primary,
      primary: primary,
      surface: Colors.white,
      onSurface: charcoal,
    ),
    appBarTheme: const AppBarTheme(
      backgroundColor: charcoal,
      foregroundColor: Colors.white,
      centerTitle: false,
    ),
    cardTheme: CardThemeData(
      color: Colors.white,
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
    ),
    inputDecorationTheme: InputDecorationTheme(
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(4)),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(4),
        borderSide: const BorderSide(color: primary, width: 2),
      ),
    ),
  );
}
