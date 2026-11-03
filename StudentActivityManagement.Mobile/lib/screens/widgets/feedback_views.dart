import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';

/// Empty state: icon, descriptive text and an optional action (design: "Icon-based composition").
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.message, this.icon = LucideIcons.inbox, this.actionLabel, this.onAction});

  final String message;
  final IconData icon;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 32),
        decoration: BoxDecoration(
          color: AppColors.white,
          borderRadius: BorderRadius.circular(AppStyles.radius),
          border: Border.all(color: const Color(0xFFC8D1E0)),
        ),
        child: Column(
          children: [
            Icon(icon, size: 36, color: const Color(0xFFADB5BD)),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.muted)),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 12),
              OutlinedButton(onPressed: onAction, child: Text(actionLabel!)),
            ],
          ],
        ),
      );
}

/// Error with a retry button, used when a screen cannot load its data.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => EmptyState(
        icon: LucideIcons.circleAlert,
        message: message,
        actionLabel: 'Thử lại',
        onAction: onRetry,
      );
}

/// Loading placeholder: blocks matching the content, pulsing (design: skeletons, no circular spinners).
class SkeletonList extends StatefulWidget {
  const SkeletonList({super.key, this.count = 4, this.height = 96});

  final int count;
  final double height;

  @override
  State<SkeletonList> createState() => _SkeletonListState();
}

class _SkeletonListState extends State<SkeletonList> with SingleTickerProviderStateMixin {
  late final AnimationController _controller =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 900))..repeat(reverse: true);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => FadeTransition(
        opacity: Tween(begin: 0.45, end: 1.0).animate(_controller),
        child: Column(
          children: [
            for (var i = 0; i < widget.count; i++)
              Container(
                height: widget.height,
                margin: const EdgeInsets.only(bottom: 12),
                decoration: BoxDecoration(color: AppColors.skeleton, borderRadius: BorderRadius.circular(AppStyles.radius)),
              ),
          ],
        ),
      );
}

/// Entry animation: fade + rise 16px over 480ms; [index] staggers list items by 100ms (capped).
class EntryAnimation extends StatelessWidget {
  const EntryAnimation({super.key, required this.child, this.index = 0});

  final Widget child;
  final int index;

  @override
  Widget build(BuildContext context) {
    if (MediaQuery.of(context).disableAnimations) return child;
    final delay = AppStyles.staggerStep * index.clamp(0, 5);
    final total = AppStyles.entryDuration + delay;
    final start = delay.inMilliseconds / total.inMilliseconds;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: total,
      curve: Interval(start, 1, curve: Curves.easeOut),
      builder: (context, value, child) => Opacity(
        opacity: value,
        child: Transform.translate(offset: Offset(0, 16 * (1 - value)), child: child),
      ),
      child: child,
    );
  }
}

/// Filled button that shows a progress label instead of a spinner while [busy].
class BusyButton extends StatelessWidget {
  const BusyButton({super.key, required this.label, required this.onPressed, this.busy = false, this.icon, this.outlined = false});

  final String label;
  final VoidCallback? onPressed;
  final bool busy;
  final IconData? icon;
  final bool outlined;

  @override
  Widget build(BuildContext context) {
    final text = Text(busy ? 'Đang xử lý...' : label);
    final action = busy ? null : onPressed;
    if (outlined) {
      return icon == null
          ? OutlinedButton(onPressed: action, child: text)
          : OutlinedButton.icon(onPressed: action, icon: Icon(icon, size: 18), label: text);
    }
    return icon == null
        ? FilledButton(onPressed: action, child: text)
        : FilledButton.icon(onPressed: action, icon: Icon(icon, size: 18), label: text);
  }
}

/// Section heading used inside detail screens.
class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key, this.trailing});

  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 16, bottom: 8),
        child: Row(
          children: [
            Expanded(child: Text(text, style: Theme.of(context).textTheme.titleMedium)),
            ?trailing,
          ],
        ),
      );
}

/// Shows an API message in a floating snackbar; red for errors.
void showMessage(BuildContext context, String message, {bool error = false}) {
  if (message.isEmpty) return;
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: error ? AppColors.danger : AppColors.charcoal,
    ));
}
