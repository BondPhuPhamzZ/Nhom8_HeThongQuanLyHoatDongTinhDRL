import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_colors.dart';

/// Shapes, spacing, motion and the Material theme built from the design tokens.
abstract final class AppStyles {
  static const radius = 6.0;
  static const gap = 8.0;
  static const pagePadding = EdgeInsets.symmetric(horizontal: 16, vertical: 12);
  static const entryDuration = Duration(milliseconds: 480);
  static const staggerStep = Duration(milliseconds: 100);
  static const pageTransition = Duration(milliseconds: 300);

  static final cardShape = RoundedRectangleBorder(
    borderRadius: BorderRadius.circular(radius),
    side: const BorderSide(color: AppColors.border),
  );

  /// Linear: depth comes from hairline borders, not shadows.
  static const cardShadow = <BoxShadow>[];

  static const mono = TextStyle(fontFamily: 'monospace', fontWeight: FontWeight.w700, letterSpacing: 4);

  static ThemeData get theme {
    final base = ThemeData(useMaterial3: true, brightness: Brightness.light);
    final textTheme = GoogleFonts.interTextTheme(base.textTheme).apply(
      bodyColor: AppColors.charcoal,
      displayColor: AppColors.charcoal,
    );
    final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(radius));

    return base.copyWith(
      scaffoldBackgroundColor: AppColors.white,
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
        headlineSmall: textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.4),
        titleLarge: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700, fontSize: 21, letterSpacing: -0.3),
        // Section titles: larger and darker so every block of a screen is easy to find.
        titleMedium: textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700, fontSize: 17, letterSpacing: -0.2),
        labelLarge: textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600),
        bodyMedium: textTheme.bodyMedium?.copyWith(height: 1.55, fontSize: 15),
        bodySmall: textTheme.bodySmall?.copyWith(color: AppColors.muted),
      ),
      // White bar with a hairline under it, like the Web's top bar.
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.white,
        foregroundColor: AppColors.charcoal,
        surfaceTintColor: AppColors.white,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        shape: const Border(bottom: BorderSide(color: AppColors.border)),
        titleTextStyle: GoogleFonts.inter(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.charcoal, letterSpacing: -0.3),
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
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 15),
        ).copyWith(
          // Pressed goes one step darker (primaryDark), the Web's hover colour.
          overlayColor: WidgetStateProperty.all(const Color(0x1A1B1C22)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.primary,
          backgroundColor: AppColors.white,
          side: const BorderSide(color: AppColors.borderStrong),
          shape: shape,
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 15),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primary,
          shape: shape,
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600),
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
        selectedColor: AppColors.primarySoft,
        labelStyle: GoogleFonts.inter(fontWeight: FontWeight.w500, color: AppColors.charcoal),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          shape: WidgetStateProperty.all(shape),
          side: WidgetStateProperty.all(const BorderSide(color: AppColors.border)),
          backgroundColor: WidgetStateProperty.resolveWith(
            (states) => states.contains(WidgetState.selected) ? AppColors.primarySoft : AppColors.white,
          ),
          foregroundColor: WidgetStateProperty.resolveWith(
            (states) => states.contains(WidgetState.selected) ? AppColors.primary : AppColors.charcoal,
          ),
          textStyle: WidgetStateProperty.all(GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 14)),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AppColors.white,
        indicatorColor: AppColors.primarySoft,
        indicatorShape: shape,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => GoogleFonts.inter(
            fontSize: 12,
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? AppColors.primary : AppColors.muted,
          ),
        ),
      ),
      snackBarTheme: SnackBarThemeData(behavior: SnackBarBehavior.floating, shape: shape),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: AppColors.primary,
        foregroundColor: AppColors.white,
        elevation: 1,
        shape: shape,
        extendedTextStyle: GoogleFonts.inter(fontWeight: FontWeight.w600),
      ),
      badgeTheme: const BadgeThemeData(backgroundColor: AppColors.danger),
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
