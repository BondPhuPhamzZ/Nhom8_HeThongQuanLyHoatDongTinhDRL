import 'package:flutter/foundation.dart';

import '../services/api_client.dart';
import '../services/notification_service.dart';

/// Unread count for the badge on the notifications tab.
class NotificationProvider extends ChangeNotifier {
  NotificationProvider({NotificationService? service}) : _service = service ?? NotificationService();

  final NotificationService _service;
  int unread = 0;

  Future<void> refresh() async {
    try {
      final page = await _service.list(unreadOnly: true, pageSize: 1);
      if (page.totalCount != unread) {
        unread = page.totalCount;
        notifyListeners();
      }
    } on ApiException {
      // The badge is a hint; screens show their own errors.
    }
  }

  void clear() {
    unread = 0;
    notifyListeners();
  }
}
