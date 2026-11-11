import 'package:flutter/material.dart';

/// Light reading of system_design/DESIGN-linear.app.md, the same tokens as the Web's site.css:
/// white canvas, one lavender-blue accent, hairline light-blue borders.
abstract final class AppColors {
  static const primary = Color(0xFF5E6AD2);
  static const primaryDark = Color(0xFF4551C4);
  static const primarySoft = Color(0xFFEEF0FC);

  /// Ink: titles and body text (the name is kept from the earlier palette).
  static const charcoal = Color(0xFF1B1C22);
  static const white = Color(0xFFFFFFFF);
  static const surface = Color(0xFFF7F8FB);
  static const success = Color(0xFF27A644);
  static const accent = Color(0xFFF4B73F);
  static const danger = Color(0xFFD9364A);
  static const info = Color(0xFF2F80C4);
  static const border = Color(0xFFD9DEF5);
  static const borderStrong = Color(0xFFA9B1EA);
  static const muted = Color(0xFF5D6170);
  static const skeleton = Color(0xFFECEFF8);
}
