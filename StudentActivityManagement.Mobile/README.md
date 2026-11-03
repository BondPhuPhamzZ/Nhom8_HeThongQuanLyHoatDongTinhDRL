# Nhóm 8 — ứng dụng Flutter

Đây là khung giao diện Mobile cho **sinh viên và lớp trưởng**. Các thẻ trên trang đầu chỉ mô tả phạm vi, chưa phải tính năng API đã hoàn thành.

## Kiến trúc

- `lib/app/`: khởi tạo ứng dụng và điều hướng.
- `lib/core/config/`: địa chỉ REST API theo môi trường.
- `lib/core/theme/`: token giao diện đồng bộ với `system_design/design.md` và bản Web.
- `lib/features/<feature>/presentation/`: màn hình theo tính năng. Khi triển khai, thêm `data/` (DTO, API client/repository) và `domain/` chỉ khi nghiệp vụ cần tách.

Thứ tự triển khai US-30: đăng nhập JWT → danh sách/chi tiết hoạt động → đăng ký/hủy → check-in/minh chứng → thông báo → lịch sử/điểm → lớp trưởng. API ASP.NET Core là nguồn dữ liệu chuẩn cho cả Web và Mobile. **Không dùng Firestore làm CSDL thứ hai** cho tài khoản, hoạt động, đăng ký hoặc điểm; cách đó sẽ tạo hai nguồn sự thật và làm sai lệch phân quyền/transaction. Firebase Cloud Messaging có thể thêm sau cho push notification, với API là nơi quyết định người nhận và lưu thông báo.

## Chạy

Ví dụ trên Android emulator, khi API HTTP đang nghe cổng 5210:

```powershell
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5210
```

Máy thật dùng địa chỉ LAN của máy chạy API, cần API bind tới địa chỉ đó và cấu hình HTTP/HTTPS phù hợp. Không commit token, mật khẩu hoặc địa chỉ mạng cá nhân. Hiện `API_BASE_URL` chỉ là cấu hình; chưa có luồng gọi API hay lưu phiên đăng nhập.
