# StudentActivityManagement.Web

Ứng dụng Web ASP.NET Core MVC (.NET 8) của US-29. Web **không truy cập database**: mọi chức năng đi qua RESTful API `StudentActivityManagement.API` (`/api/v1`), giống ứng dụng Mobile sau này.

## Chạy

Cần chạy API trước (xem `../StudentActivityManagement.API/README.md`), rồi tại thư mục gốc repository:

```powershell
dotnet run --project .\StudentActivityManagement.API --launch-profile http   # http://localhost:5210
dotnet run --project .\StudentActivityManagement.Web --launch-profile http   # http://localhost:5220
```

Địa chỉ API cấu hình ở `appsettings.json` → `Api:BaseUrl`.

Tài khoản demo (seed Development): `SV210002` sinh viên, `SV210001` lớp trưởng, `HSSV001` Phòng QL HSSV, `ADMIN001` quản trị hệ thống; mật khẩu xem README của API.

## Chức năng hiện có

| Vai trò | Trang | API sử dụng |
|---|---|---|
| Sinh viên, Lớp trưởng | Danh sách hoạt động phù hợp (tìm kiếm, lọc cơ sở, phân trang); chi tiết; đăng ký, hủy, đăng ký lại; check-in từng buổi bằng mã (US-07) | `GET /student-activities`, `GET /student-activities/{id}`, `POST .../register`, `DELETE .../registration`, `GET .../check-in-status`, `POST .../sessions/{sessionId}/check-in` |
| Phòng QL HSSV, SystemAdmin | Danh sách hoạt động theo trạng thái; tạo/sửa với nhiều buổi, quy tắc đối tượng và yêu cầu cần chuẩn bị; chuyển trạng thái theo vòng đời (nút và lựa chọn lấy từ `allowedNextStatuses`); xóa hoạt động nháp chưa có đăng ký; phát/đổi mã check-in từng buổi và xem số lượt check-in (US-07); trang "Người tham gia": lọc, điểm danh thủ công/hủy điểm danh, xem và duyệt minh chứng, tải file (US-21) | `/admin/activities`, `/admin/activities/{id}/check-in-codes`, `/admin/activities/{id}/participants`, `.../check-ins`, `.../evidences/{evidenceId}/review`, `/admin/classes` |
| Phòng QL HSSV, SystemAdmin | "Sinh viên": danh sách lọc theo mã/tên/email, khóa, cơ sở, lớp; thêm, sửa, xóa sinh viên (US-17) | `/admin/students` |
| Phòng QL HSSV, SystemAdmin | "Lớp": danh sách lớp kèm sĩ số và lớp trưởng; thêm lớp; danh sách sinh viên của lớp; chỉ định lớp trưởng (US-18) | `/admin/classes`, `PUT /admin/classes/{id}/monitor/{studentId}` |
| SystemAdmin | "Tài khoản": danh sách mọi tài khoản lọc theo vai trò/trạng thái; tạo tài khoản cán bộ; đổi HSSV ↔ SystemAdmin; khóa/mở khóa (US-25) | `/admin/accounts` |

## Cách tổ chức

- `Api/ApiClient.cs`: typed `HttpClient` cho API; đọc `ApiResponseDto` khi thành công và ProblemDetails (`detail`, `errorCode`, `errors`) khi lỗi, nên mọi trang hiển thị lỗi giống nhau.
- `Api/BearerTokenHandler.cs`: gắn JWT của người dùng vào mỗi request. JWT nằm trong cookie đăng nhập (được ASP.NET Core mã hóa bằng Data Protection), không lưu ở JavaScript.
- `Infrastructure/ApiUnauthorizedFilter.cs`: khi API trả `401` (token hết hạn, tài khoản bị khóa hoặc đổi quyền) thì đăng xuất khỏi Web và quay về trang đăng nhập.
- `Infrastructure/VnTime.cs`: API dùng UTC; Web hiển thị và nhận giờ Việt Nam (UTC+7), gửi lên API kèm `+07:00`.
- Nội dung môn học được dùng trực tiếp: **Layout** (`Views/Shared/_Layout.cshtml`), **Partial View** (`_Flash`, `_Pagination`, `_ActivityCard`), **View Component** (`UserPanel` trên thanh điều hướng; `StudentCheckIn` và `CheckInCodes` tự gọi API để hiển thị khối check-in), **Tag Helper** tự viết (`<status-badge>`, `<vn-time>`) cùng các Tag Helper có sẵn (`asp-for`, `asp-action`, `asp-validation-summary`).
