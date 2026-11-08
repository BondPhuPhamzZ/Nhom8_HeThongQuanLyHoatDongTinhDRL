import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../constants/app_colors.dart';
import '../../constants/app_styles.dart';
import '../../utils/checkin_qr.dart';

/// US-35: camera view that returns the first valid check-in QR ([CheckInQr]) and ignores any other QR code.
class QrScanScreen extends StatefulWidget {
  const QrScanScreen({super.key, this.title = 'Quét QR check-in'});

  final String title;

  @override
  State<QrScanScreen> createState() => _QrScanScreenState();
}

class _QrScanScreenState extends State<QrScanScreen> {
  final _controller = MobileScannerController(detectionSpeed: DetectionSpeed.noDuplicates, formats: const [BarcodeFormat.qrCode]);
  bool _done = false;
  String? _hint;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_done) return;
    for (final barcode in capture.barcodes) {
      final qr = CheckInQr.parse(barcode.rawValue);
      if (qr != null) {
        _done = true;
        Navigator.pop(context, qr);
        return;
      }
    }
    setState(() => _hint = 'Đây không phải mã QR check-in của hệ thống. Hãy quét mã QR ban tổ chức trình chiếu.');
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(widget.title)),
        backgroundColor: Colors.black,
        body: Stack(
          children: [
            MobileScanner(
              controller: _controller,
              onDetect: _onDetect,
              errorBuilder: (context, error) => _Message(
                icon: LucideIcons.cameraOff,
                text: error.errorCode == MobileScannerErrorCode.permissionDenied
                    ? 'Ứng dụng chưa được cấp quyền camera. Hãy cho phép camera trong cài đặt, hoặc nhập mã bằng tay.'
                    : 'Không mở được camera trên thiết bị này. Bạn có thể nhập mã bằng tay.',
              ),
            ),
            Center(
              child: Container(
                width: 240,
                height: 240,
                decoration: BoxDecoration(
                  border: Border.all(color: AppColors.accent, width: 3),
                  borderRadius: BorderRadius.circular(AppStyles.radius),
                ),
              ),
            ),
            Positioned(
              left: 16,
              right: 16,
              bottom: 24,
              child: Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.7), borderRadius: BorderRadius.circular(AppStyles.radius)),
                child: Text(
                  _hint ?? 'Đưa mã QR ban tổ chức trình chiếu vào khung. Mã chỉ dùng được trong khung giờ check-in của buổi.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.white),
                ),
              ),
            ),
          ],
        ),
      );
}

class _Message extends StatelessWidget {
  const _Message({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, color: AppColors.white, size: 40),
              const SizedBox(height: 12),
              Text(text, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.white)),
            ],
          ),
        ),
      );
}
