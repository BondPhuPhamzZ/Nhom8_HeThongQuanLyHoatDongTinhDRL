import 'package:flutter/material.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';

/// Shown while the stored session is checked against the API.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: AppColors.charcoal,
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const BrandMark(size: 56),
              const SizedBox(height: 16),
              Text(
                'Hoạt động & Điểm rèn luyện',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(color: AppColors.white),
              ),
            ],
          ),
        ),
      );
}

/// The yellow "RL" mark used on the Web navigation bar.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 32});

  final double size;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        alignment: Alignment.center,
        decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(AppStyles.radius)),
        child: Text('RL', style: TextStyle(color: AppColors.charcoal, fontWeight: FontWeight.w700, fontSize: size * 0.42)),
      );
}
