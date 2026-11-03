import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../models/activity.dart';
import '../../utils/vn_time.dart';
import 'status_badge.dart';

/// Activity summary card, the Mobile counterpart of the Web's `_ActivityCard` partial.
class ActivityCard extends StatelessWidget {
  const ActivityCard({super.key, required this.activity, required this.onTap});

  final Activity activity;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final first = activity.orderedSessions.isEmpty ? null : activity.orderedSessions.first;
    final type = activity.eligibility?.participationType;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  StatusBadge(activity.status),
                  if (type != null) ...[const SizedBox(width: 6), StatusBadge(type)],
                  const Spacer(),
                  PointsChip(activity.trainingPoints),
                ],
              ),
              const SizedBox(height: 10),
              Text(activity.name, style: Theme.of(context).textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
              const SizedBox(height: 2),
              Text(activity.organizerName, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
              const SizedBox(height: 10),
              _Meta(icon: LucideIcons.clock, text: first == null ? VnTime.dateTime(activity.startTime) : '${VnTime.dateTime(first.startTime)} · ${first.campus}'),
              _Meta(
                icon: LucideIcons.users,
                text: '${activity.sessions.length} buổi · còn ${activity.remainingSlots}/${activity.maxParticipants} chỗ',
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  if (activity.myRegistrationStatus != null &&
                      activity.myRegistrationStatus != 'Cancelled') ...[
                    StatusBadge(activity.myRegistrationStatus!),
                    const SizedBox(width: 8),
                  ],
                  Expanded(
                    child: Text('Hạn đăng ký ${VnTime.dateTime(activity.registrationCloseTime)}',
                        style: const TextStyle(color: AppColors.muted, fontSize: 12), overflow: TextOverflow.ellipsis),
                  ),
                  const Icon(LucideIcons.chevronRight, size: 18, color: AppColors.muted),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.text});
  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 4),
        child: Row(
          children: [
            Icon(icon, size: 15, color: AppColors.muted),
            const SizedBox(width: 6),
            Expanded(child: Text(text, style: const TextStyle(fontSize: 13), overflow: TextOverflow.ellipsis)),
          ],
        ),
      );
}

/// Bordered white box for a group of facts on detail screens.
class InfoPanel extends StatelessWidget {
  const InfoPanel({super.key, required this.children, this.padding = const EdgeInsets.all(14)});

  final List<Widget> children;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: padding,
        decoration: BoxDecoration(
          color: AppColors.white,
          borderRadius: BorderRadius.circular(AppStyles.radius),
          border: Border.all(color: AppColors.border),
          boxShadow: AppStyles.cardShadow,
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: children),
      );
}
