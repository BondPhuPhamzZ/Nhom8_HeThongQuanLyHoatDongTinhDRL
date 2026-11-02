# StudentActivityManagement.Web

Ứng dụng Web ASP.NET Core MVC (.NET 8) của US-29. Web **không truy cập database**: mọi chức năng đi qua RESTful API `StudentActivityManagement.API` (`/api/v1`), giống ứng dụng Mobile sau này.

## Chạy

Cần chạy API trước (xem `../StudentActivityManagement.API/README.md`). Từ thư mục gốc repository, mở **hai terminal**:

Terminal 1 — API và Swagger:

```powershell
dotnet run --project .\StudentActivityManagement.API --launch-profile http
```

Terminal 2 — giao diện Web:

```powershell
dotnet run --project .\StudentActivityManagement.Web --launch-profile http
```

Mở `http://localhost:5220` để dùng Web; `http://localhost:5210` là Swagger của API. Trong Visual Studio, mở solution ở gốc repository, chọn **Configure Startup Projects → Multiple startup projects**, đặt cả API và Web thành **Start**, rồi chạy F5.

Địa chỉ API cấu hình ở `appsettings.json` → `Api:BaseUrl`.

Tài khoản demo (seed Development): `SV210002` sinh viên, `SV210001` lớp trưởng, `HSSV001` Phòng QL HSSV, `ADMIN001` quản trị hệ thống; mật khẩu xem README của API.

## Chức năng hiện có

| Vai trò | Trang | API sử dụng |
|---|---|---|
| Sinh viên, Lớp trưởng | Danh sách hoạt động phù hợp (tìm kiếm, lọc cơ sở, phân trang); chi tiết; đăng ký, hủy, đăng ký lại; check-in từng buổi bằng mã (US-07); nộp, xem, tải và xóa minh chứng tham gia (US-08) | `GET /student-activities`, `GET /student-activities/{id}`, `POST .../register`, `DELETE .../registration`, `GET .../check-in-status`, `POST .../sessions/{sessionId}/check-in`, `GET/POST .../evidences`, `GET .../evidences/{id}/file`, `DELETE .../evidences/{id}` |
| Sinh viên, Lớp trưởng | "Lịch sử & điểm": lọc theo kỳ đánh giá/trạng thái đăng ký; xem buổi, điểm danh và quyết định điểm đã duyệt (US-10) | `/student/reports/evaluation-periods`, `/student/reports/activity-history` |
| Sinh viên, Lớp trưởng | "Điểm & gợi ý": điểm đã duyệt, mục tiêu kỳ, khoảng thiếu và hoạt động còn mở phù hợp với eligibility (US-11) | `/student/reports/points-and-recommendations` |
| Sinh viên, Lớp trưởng | "Trao đổi lớp": xem, tìm/lọc chủ đề; thành viên đặt câu hỏi/thảo luận; lớp trưởng đăng thông báo nhắc lớp (tự ghim), ghim/bỏ ghim và chọn câu trả lời chính thức để khép câu hỏi (US-09/13/14) | `/class-space/threads`, `/class-space/threads/{id}`, `.../replies`, `.../pin`, `.../answer` |
| Sinh viên, Lớp trưởng | "Thông báo": sinh viên nhận tin hoạt động mới phù hợp và nhắc lịch; lớp trưởng nhận thêm tin hoạt động mới khớp lớp; lọc tất cả/chưa đọc và đánh dấu đã đọc (US-06/12) | `GET /notifications`, `POST /notifications/{id}/read` |
| Lớp trưởng | Trong chủ đề của lớp: chuyển câu hỏi lên Phòng QL HSSV. Trang câu hỏi đã chuyển: lọc trạng thái, xem/rút yêu cầu đang chờ, chuyển phản hồi chính thức vào thread để cả lớp đọc một lần; cả lớp thấy trạng thái và phản hồi trong chủ đề (US-15/16) | `POST /class-space/threads/{id}/escalations`, `GET /class-space/escalations`, `POST .../withdraw`, `POST .../relay` |
| Phòng QL HSSV, SystemAdmin | "Câu hỏi lớp": xem hộp thư theo trạng thái, trả lời chính thức câu hỏi đang chờ; lớp trưởng xem được phản hồi (US-20) | `GET /admin/class-escalations`, `POST /admin/class-escalations/{id}/answer` |
| Phòng QL HSSV, SystemAdmin | Danh sách hoạt động theo trạng thái; tạo/sửa với nhiều buổi, quy tắc đối tượng và yêu cầu cần chuẩn bị; chuyển trạng thái theo vòng đời (nút và lựa chọn lấy từ `allowedNextStatuses`); xóa hoạt động nháp chưa có đăng ký; phát/đổi mã check-in từng buổi và xem số lượt check-in (US-07); trang "Người tham gia": lọc, điểm danh thủ công/hủy điểm danh, xem và duyệt minh chứng, tải file (US-21) | `/admin/activities`, `/admin/activities/{id}/check-in-codes`, `/admin/activities/{id}/participants`, `.../check-ins`, `.../evidences/{evidenceId}/review`, `/admin/classes` |
| Phòng QL HSSV, SystemAdmin | Trang chi tiết hoạt động: cấu hình phương thức/cửa sổ check-in và tối đa ba mốc nhắc trước mỗi buổi (US-26). Phương thức bị khóa sau lượt check-in đầu tiên; cấu hình ẩn thao tác sửa khi hoạt động kết thúc/hủy | `GET/PUT /admin/activities/{id}/check-in-config`, `GET/PUT /admin/activities/{id}/reminder-schedule` |
| Phòng QL HSSV, SystemAdmin | "Sinh viên": danh sách lọc theo mã/tên/email, khóa, cơ sở, lớp; thêm, sửa, xóa sinh viên (US-17) | `/admin/students` |
| Phòng QL HSSV, SystemAdmin | "Lớp": danh sách lớp kèm sĩ số và lớp trưởng; thêm lớp; danh sách sinh viên của lớp; chỉ định lớp trưởng (US-18) | `/admin/classes`, `PUT /admin/classes/{id}/monitor/{studentId}` |
| SystemAdmin | "Tài khoản": danh sách mọi tài khoản lọc theo vai trò/trạng thái; tạo tài khoản cán bộ; đổi HSSV ↔ SystemAdmin; khóa/mở khóa (US-25) | `/admin/accounts` |
| Phòng QL HSSV, SystemAdmin | "Kỳ đánh giá": tạo kỳ theo ngày Việt Nam, khóa/mở lại kỳ. Bảng ghi điểm trên hoạt động đã kết thúc: xem điều kiện từng sinh viên, ghi điểm người đã chọn hoặc tất cả, chuyển quyết định hợp lệ kèm lý do và xem lịch sử (US-22/23) | `/admin/evaluation-periods`, `/admin/activities/{id}/scoring`, `/admin/point-records/{id}/transition`, `/admin/point-records/{id}/history` |
| Phòng QL HSSV, SystemAdmin | "Báo cáo": lọc theo kỳ/cơ sở/khóa/lớp; chỉ số tổng hợp; chia theo lớp, khóa hoặc cơ sở; hoạt động trong kỳ; danh sách sinh viên nguy cơ thiếu điểm (US-24) | `/admin/reports/overview`, `/admin/reports/activities`, `/admin/reports/at-risk-students` |

## Phạm vi tích hợp US-29

| Vai trò | Trang bắt đầu sau đăng nhập | Phạm vi thao tác |
|---|---|---|
| Sinh viên | Hoạt động dành cho tôi | Đăng ký, check-in, nộp minh chứng, xem lịch sử và điểm/gợi ý, trao đổi lớp, đọc thông báo |
| Lớp trưởng | Hoạt động dành cho tôi | Toàn bộ luồng sinh viên; đăng thông báo ghim, quản lý câu hỏi/đáp án trong lớp, chuyển câu hỏi lên phòng và relay phản hồi chính thức |
| Phòng QL HSSV | Quản lý hoạt động | Quản lý sinh viên/lớp/hoạt động, cấu hình check-in và nhắc lịch, kiểm tra minh chứng, ghi/chỉnh quyết định điểm, báo cáo, trả lời câu hỏi lớp |
| SystemAdmin | Quản lý hoạt động | Luồng cán bộ và quản trị tài khoản/phân quyền |

Các trang dùng chính sách Web theo vai trò, còn dữ liệu và luật nghiệp vụ được xử lý qua API v1 bằng `ApiClient`; Web không kết nối trực tiếp cơ sở dữ liệu. `Home/Index` điều hướng cán bộ tới quản lý hoạt động và sinh viên/lớp trưởng tới danh sách hoạt động.

## Cách tổ chức

- `Api/ApiClient.cs`: typed `HttpClient` cho API; đọc `ApiResponseDto` khi thành công và ProblemDetails (`detail`, `errorCode`, `errors`) khi lỗi, nên mọi trang hiển thị lỗi giống nhau.
- `Api/BearerTokenHandler.cs`: gắn JWT của người dùng vào mỗi request. JWT nằm trong cookie đăng nhập (được ASP.NET Core mã hóa bằng Data Protection), không lưu ở JavaScript.
- `Infrastructure/ApiUnauthorizedFilter.cs`: khi API trả `401` (token hết hạn, tài khoản bị khóa hoặc đổi quyền) thì đăng xuất khỏi Web và quay về trang đăng nhập.
- `Infrastructure/VnTime.cs`: API dùng UTC; Web hiển thị và nhận giờ Việt Nam (UTC+7), gửi lên API kèm `+07:00`.
- Nội dung môn học được dùng trực tiếp: **Layout** (`Views/Shared/_Layout.cshtml`), **Partial View** (`_Flash`, `_Pagination`, `_ActivityCard`), **View Component** (`UserPanel` trên thanh điều hướng; `StudentCheckIn` và `CheckInCodes` tự gọi API để hiển thị khối check-in), **Tag Helper** tự viết (`<status-badge>`, `<vn-time>`) cùng các Tag Helper có sẵn (`asp-for`, `asp-action`, `asp-validation-summary`).
