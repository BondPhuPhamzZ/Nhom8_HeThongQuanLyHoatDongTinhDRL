# Nhóm 8 — ứng dụng Mobile (Flutter, US-30)

Ứng dụng Mobile dành cho **sinh viên và lớp trưởng**, dùng chung RESTful API `StudentActivityManagement.API` với bản Web. API ASP.NET Core là nguồn dữ liệu duy nhất (SQLite qua EF Core); Mobile không có cơ sở dữ liệu riêng và không dùng Firestore. Cán bộ Phòng QL HSSV và quản trị viên dùng bản Web. Nếu đăng nhập bằng tài khoản cán bộ, ứng dụng từ chối và hướng dẫn chuyển sang Web.

## Chức năng

| Tab / màn hình | Story | API |
|---|---|---|
| Đăng nhập, giữ phiên, đăng xuất, đổi địa chỉ API | US-01 | `POST /auth/login`, `GET /auth/me` |
| Hoạt động: sắp diễn ra / của tôi, tìm kiếm, lọc cơ sở, phân trang | US-02 | `GET /student-activities` |
| Chi tiết: lịch các buổi, đối tượng, điểm, hạn đăng ký; đăng ký, hủy | US-03, US-04, US-05 | `GET /student-activities/{id}`, `POST .../register`, `DELETE .../registration` |
| Check-in từng buổi bằng mã trong khung giờ check-in | US-07 | `GET .../check-in-status`, `POST .../sessions/{id}/check-in` |
| Minh chứng: ảnh, PDF (tối đa 5 MB) hoặc mã xác nhận; xem trạng thái duyệt; xóa khi còn chờ | US-08 | `GET/POST .../evidences`, `DELETE .../evidences/{id}` |
| Điểm: điểm đã duyệt, mục tiêu, còn thiếu, gợi ý hoạt động; lịch sử theo kỳ | US-10, US-11 | `GET /student/reports/evaluation-periods`, `.../activity-history`, `.../points-and-recommendations` |
| Lớp: chủ đề của lớp, đặt câu hỏi, thảo luận, trả lời, xóa bài của mình | US-09 | `/class-space/threads...` |
| Lớp trưởng: thông báo, chuyển tiếp hoạt động vào lớp, ghim, chọn câu trả lời | US-13, US-14 | `POST /class-space/threads` (Announcement), `PATCH .../pin`, `POST .../replies/{id}/answer` |
| Lớp trưởng: chuyển câu hỏi lên Phòng QL HSSV, rút lại, chuyển phản hồi chính thức vào lớp | US-15, US-16 | `POST .../escalations`, `GET /class-space/escalations`, `.../withdraw`, `.../relay` |
| Thông báo: nhắc lịch, hoạt động mới của bạn và của lớp; đánh dấu đã đọc; số chưa đọc trên thanh điều hướng | US-06, US-12 | `GET /notifications`, `POST /notifications/{id}/read` |

Mọi quy tắc nghiệp vụ (đối tượng tham gia, sức chứa, khung giờ check-in, quyền lớp trưởng, trạng thái điểm) nằm ở API. Mobile chỉ ẩn hoặc hiện nút theo đúng điều kiện Web đang dùng và hiển thị thông báo lỗi mà API trả về.

## Cấu trúc (theo project mẫu `anime_wibu`)

```
lib/
  main.dart                 khởi tạo, Provider, chọn màn hình theo trạng thái đăng nhập
  constants/                app_colors, app_styles (theme), api_config
  models/                   DTO của API: activity, participation, student_report, class_space, ...
  services/                 api_client (JWT, ApiResponseDto, ProblemDetails), preference_service và mỗi nhóm API một service
  providers/                auth_provider (phiên đăng nhập, tự đăng xuất khi 401), notification_provider (số chưa đọc)
  screens/
    auth/                   splash, login
    main_page.dart          5 tab: Hoạt động, Điểm, Lớp, Thông báo, Tài khoản
    tabs/                   activities_tab, points_tab, class_tab, notifications_tab, profile_tab
    activity/               chi tiết hoạt động, check-in và minh chứng
    class_space/            chi tiết chủ đề, đăng bài, câu hỏi đã chuyển lên
    widgets/                status_badge, activity_card, feedback_views (empty state, skeleton, animation)
  utils/vn_time.dart        API dùng UTC; app hiển thị giờ Việt Nam (UTC+7) như bản Web
test/                       kiểm thử model/JSON, quy tắc đăng ký, giờ Việt Nam và widget dùng chung
```

## Giao diện

Mobile dùng chung token với Web theo `system_design/design.md`:

- **Màu:** `#007BFF` cho primary, `#343A40` cho thanh tiêu đề, `#F8F9FA` cho nền, `#FFC107` cho accent; nhãn trạng thái cùng màu và chữ với Web.
- **Chữ và hình khối:** font Lato, bo góc 4px, bóng nhẹ.
- **Biểu tượng:** Lucide (`lucide_icons_flutter`), không dùng emoji.
- **Chuyển động:** nội dung hiện dần và dịch lên 16px trong 480ms, danh sách hiện lần lượt cách nhau 100ms. Khi chuyển trang thì mờ dần kết hợp trượt (300ms). Hệ thống bật giảm chuyển động thì tắt animation.
- **Trạng thái tải và rỗng:** dùng skeleton thay vòng xoay; trạng thái rỗng có biểu tượng, mô tả và nút hành động.

## Chạy

1. Chạy API: trong `StudentActivityManagement.API`, chạy `dotnet run --launch-profile http`. API nghe ở `http://localhost:5210`.
2. Cài package và chạy app:

```powershell
flutter pub get
flutter run                                              # Android emulator: tự dùng http://10.0.2.2:5210
flutter run -d chrome                                    # web: http://localhost:5210
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5210   # máy thật: địa chỉ LAN của máy chạy API
```

- **Đổi địa chỉ API:** bấm dòng `API: ...` ở màn hình đăng nhập (địa chỉ được lưu lại), hoặc truyền `--dart-define=API_BASE_URL=...`.
- **HTTP trên Android:** `AndroidManifest.xml` đã khai báo quyền `INTERNET` và cho phép HTTP (`usesCleartextTraffic`) để gọi API cục bộ khi làm đồ án.
- **Tài khoản demo** (seed Development): `SV210002` là sinh viên, `SV210001` là lớp trưởng lớp 21DTH1, mật khẩu `123456`.

> **Lưu ý đường dẫn trên Windows:** Flutter build không chạy được trong thư mục có ký tự `&` (ví dụ `D:\nhom8_hethongquanlyhoatdong&tinhdrl\...`), vì `cmd.exe` coi `&` là dấu nối lệnh khi build native assets. Hãy clone repo vào đường dẫn không có `&`, hoặc tạo junction tới thư mục này:
> `New-Item -ItemType Junction -Path D:\Nhom8Mobile -Target "<đường dẫn tới StudentActivityManagement.Mobile>"`, rồi chạy Flutter trong `D:\Nhom8Mobile`.

## Kiểm tra

```powershell
flutter analyze
flutter test
```

CI (`.github/workflows/ci.yml`, job `mobile`) chạy `flutter analyze` và `flutter test` trên Flutter 3.41.9.
