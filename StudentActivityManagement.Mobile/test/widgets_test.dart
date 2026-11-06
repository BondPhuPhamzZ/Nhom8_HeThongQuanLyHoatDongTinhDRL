import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/constants/app_colors.dart';
import 'package:nhom8_qlhd_tinh_drl/models/assistant.dart';
import 'package:nhom8_qlhd_tinh_drl/screens/assistant/assistant_screen.dart';
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

  testWidgets('ChatBubble shows the answer, its activity cards and what was looked up', (tester) async {
    int? opened;
    final message = ChatMessage.assistant(AssistantReply.fromJson({
      'reply': 'Bạn có thể tham gia Hiến máu.',
      'activities': [
        {'id': 12, 'activityName': 'Hiến máu', 'trainingPoints': 15, 'campus': 'Cơ sở 1', 'myRegistrationStatus': 'Registered'},
      ],
      'toolsUsed': ['search_activities'],
    }));
    await pump(tester, ChatBubble(message: message, onOpenActivity: (id) => opened = id));
    await tester.pumpAndSettle();

    expect(find.text('Bạn có thể tham gia Hiến máu.'), findsOneWidget);
    expect(find.text('+15 điểm'), findsOneWidget);
    expect(find.text('Đã đăng ký'), findsOneWidget);
    expect(find.text('Đã tra cứu: hoạt động phù hợp'), findsOneWidget);
    await tester.tap(find.byType(AssistantActivityTile));
    expect(opened, 12);
  });

  testWidgets('ChatBubble draws the student on primary and errors in red', (tester) async {
    await pump(tester, ChatBubble(message: ChatMessage.user('Mình còn thiếu bao nhiêu điểm?')));
    await tester.pumpAndSettle();
    BoxDecoration bubble() => tester.widgetList<Container>(find.byType(Container)).map((item) => item.decoration).whereType<BoxDecoration>().first;
    expect(bubble().color, AppColors.primary);

    await pump(tester, ChatBubble(message: ChatMessage.error('Trợ lý đang quá tải.')));
    await tester.pumpAndSettle();
    expect((bubble().border as Border).top.color, AppColors.danger);
    expect(tester.widget<Text>(find.text('Trợ lý đang quá tải.')).style?.color, AppColors.danger);
  });
}
