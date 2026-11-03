import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/constants/app_colors.dart';
import 'package:nhom8_qlhd_tinh_drl/screens/widgets/feedback_views.dart';
import 'package:nhom8_qlhd_tinh_drl/screens/widgets/status_badge.dart';

/// Shared widgets render with plain Material (no google_fonts download in tests).
void main() {
  Future<void> pump(WidgetTester tester, Widget child) =>
      tester.pumpWidget(MaterialApp(home: Scaffold(body: Center(child: child))));

  testWidgets('StatusBadge uses the Web labels and design colours', (tester) async {
    await pump(tester, const StatusBadge('Attended'));
    expect(find.text('Đã tham gia'), findsOneWidget);
    final box = tester.widget<Container>(find.byType(Container)).decoration as BoxDecoration;
    expect(box.color, AppColors.primary);
    expect(box.borderRadius, BorderRadius.circular(4));
  });

  testWidgets('StatusBadge accepts a custom label', (tester) async {
    await pump(tester, const StatusBadge('Pending', label: 'Đã chuyển P.QL HSSV'));
    expect(find.text('Đã chuyển P.QL HSSV'), findsOneWidget);
  });

  testWidgets('EmptyState shows its message and runs its action', (tester) async {
    var tapped = false;
    await pump(tester, EmptyState(message: 'Chưa có hoạt động', actionLabel: 'Xem hoạt động', onAction: () => tapped = true));
    expect(find.text('Chưa có hoạt động'), findsOneWidget);
    await tester.tap(find.text('Xem hoạt động'));
    expect(tapped, isTrue);
  });

  testWidgets('BusyButton disables itself and shows progress text instead of a spinner', (tester) async {
    await pump(tester, BusyButton(label: 'Đăng ký', busy: true, onPressed: () {}));
    expect(find.text('Đang xử lý...'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);
  });

  testWidgets('EntryAnimation settles fully visible', (tester) async {
    await pump(tester, const EntryAnimation(index: 2, child: Text('Nội dung')));
    await tester.pumpAndSettle();
    expect(tester.widget<Opacity>(find.byType(Opacity)).opacity, 1);
  });
}
