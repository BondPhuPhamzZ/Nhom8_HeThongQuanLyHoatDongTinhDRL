import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_colors.dart';

/// Shapes, spacing, motion and the Material theme built from the design tokens.
abstract final class AppStyles {
  static const radius = 4.0;
  static const gap = 8.0;
  static const pagePadding = EdgeInsets.symmetric(horizontal: 16, vertical: 12);
  static const entryDuration = Duration(milliseconds: 480);
  static const staggerStep = Duration(milliseconds: 100);
  static const pageTransition = Duration(milliseconds: 300);

  static final cardShape = RoundedRectangleBorder(
    borderRadius: BorderRadius.circular(radius),
    side: const BorderSide(color: AppColors.border),
  );

  /// Subtle shadow, never heavier than 0 2px 8px rgba(0,0,0,.08).
  static const cardShadow = [BoxShadow(color: Color(0x0F000000), blurRadius: 8, offset: Offset(0, 2))];

  static const mono = TextStyle(fontFamily: 'monospace', fontWeight: FontWeight.w700, letterSpacing: 4);

  static ThemeData get theme {
    final base = ThemeData(useMaterial3: true, brightness: Brightness.light);
    final textTheme = GoogleFonts.latoTextTheme(base.textTheme).apply(
      bodyColor: AppColors.charcoal,
      displayColor: AppColors.charcoal,
    );
    final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(radius));

    return base.copyWith(
      scaffoldBackgroundColor: AppColors.surface,
      colorScheme: const ColorScheme.light(
        primary: AppColors.primary,
        onPrimary: AppColors.white,
        secondary: AppColors.charcoal,
        onSecondary: AppColors.white,
        tertiary: AppColors.accent,
        error: AppColors.danger,
        surface: AppColors.white,
        onSurface: AppColors.charcoal,
        outline: AppColors.border,
      ),
      textTheme: textTheme.copyWith(
        headlineSmall: textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
        titleLarge: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700, fontSize: 20),
        titleMedium: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
        labelLarge: textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600),
        bodyMedium: textTheme.bodyMedium?.copyWith(height: 1.6, fontSize: 15),
        bodySmall: textTheme.bodySmall?.copyWith(color: AppColors.muted, letterSpacing: 0.2),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.charcoal,
        foregroundColor: AppColors.white,
        elevation: 0,
        centerTitle: false,
        titleTextStyle: GoogleFonts.lato(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.white),
      ),
      cardTheme: CardThemeData(
        color: AppColors.white,
        elevation: 0,
        margin: const EdgeInsets.only(bottom: 12),
        shape: cardShape,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: AppColors.primary,
          foregroundColor: AppColors.white,
          disabledBackgroundColor: AppColors.border,
          shape: shape,
          padding: const EdgeInsets.all(12),
          textStyle: GoogleFonts.lato(fontWeight: FontWeight.w600, fontSize: 15),
        ).copyWith(
          // Hover/press darkens ~8%, the design's tactile feedback without heavy shadows.
          overlayColor: WidgetStateProperty.all(const Color(0x14000000)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.primary,
          side: const BorderSide(color: AppColors.primary, width: 1.5),
          shape: shape,
          padding: const EdgeInsets.all(12),
          textStyle: GoogleFonts.lato(fontWeight: FontWeight.w600, fontSize: 15),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primary,
          shape: shape,
          textStyle: GoogleFonts.lato(fontWeight: FontWeight.w600),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.white,
        floatingLabelBehavior: FloatingLabelBehavior.never,
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(radius), borderSide: const BorderSide(color: AppColors.border)),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(radius), borderSide: const BorderSide(color: AppColors.border)),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(radius), borderSide: const BorderSide(color: AppColors.primary, width: 2)),
        errorBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(radius), borderSide: const BorderSide(color: AppColors.danger)),
        errorStyle: const TextStyle(color: AppColors.danger),
      ),
      chipTheme: base.chipTheme.copyWith(
        shape: shape,
        side: const BorderSide(color: AppColors.border),
        backgroundColor: AppColors.white,
        selectedColor: const Color(0x1F007BFF),
        labelStyle: GoogleFonts.lato(fontWeight: FontWeight.w500, color: AppColors.charcoal),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AppColors.white,
        indicatorColor: const Color(0x1F007BFF),
        indicatorShape: shape,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => GoogleFonts.lato(
            fontSize: 12,
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? AppColors.primary : AppColors.muted,
          ),
        ),
      ),
      snackBarTheme: SnackBarThemeData(behavior: SnackBarBehavior.floating, shape: shape),
      dividerTheme: const DividerThemeData(color: AppColors.border, thickness: 1, space: 1),
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.iOS: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.windows: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.linux: FadeSlidePageTransitionsBuilder(),
          TargetPlatform.macOS: FadeSlidePageTransitionsBuilder(),
        },
      ),
    );
  }
}

/// Page transitions: fade + slide, 300ms (design: "Page transitions: Fade + slide").
class FadeSlidePageTransitionsBuilder extends PageTransitionsBuilder {
  const FadeSlidePageTransitionsBuilder();

  @override
  Duration get transitionDuration => AppStyles.pageTransition;

  @override
  Widget buildTransitions<T>(
    PageRoute<T> route,
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) {
    final curved = CurvedAnimation(parent: animation, curve: Curves.easeOutCubic);
    return FadeTransition(
      opacity: curved,
      child: SlideTransition(
        position: Tween(begin: const Offset(0, 0.04), end: Offset.zero).animate(curved),
        child: child,
      ),
    );
  }
}
