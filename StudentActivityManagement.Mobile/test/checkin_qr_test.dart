import 'package:flutter_test/flutter_test.dart';
import 'package:nhom8_qlhd_tinh_drl/utils/checkin_qr.dart';

/// US-35: the payload must match the Web's CheckInQr.Payload exactly; anything else is ignored by the scanner.
void main() {
  test('reads the Web payload', () {
    final qr = CheckInQr.parse('nhom8-checkin:v1:12:34:AB7KQ9');
    expect(qr, isNotNull);
    expect(qr!.activityId, 12);
    expect(qr.sessionId, 34);
    expect(qr.code, 'AB7KQ9');
    expect(qr.toString(), 'nhom8-checkin:v1:12:34:AB7KQ9');
  });

  test('upper-cases the code and trims spaces', () {
    expect(CheckInQr.parse('  nhom8-checkin:v1:1:2:ab7kq9 \n')!.code, 'AB7KQ9');
  });

  test('ignores anything that is not a check-in QR of this system', () {
    for (final raw in [
      null,
      '',
      'https://evil.example/checkin',
      'nhom8-checkin:v2:1:2:AB7KQ9',
      'other-app:v1:1:2:AB7KQ9',
      'nhom8-checkin:v1:x:2:AB7KQ9',
      'nhom8-checkin:v1:0:2:AB7KQ9',
      'nhom8-checkin:v1:1:-2:AB7KQ9',
      'nhom8-checkin:v1:1:2:AB',
      'nhom8-checkin:v1:1:2:AB7<script>',
      'nhom8-checkin:v1:1:2:AB7KQ9:extra',
    ]) {
      expect(CheckInQr.parse(raw), isNull, reason: '$raw');
    }
  });
}
