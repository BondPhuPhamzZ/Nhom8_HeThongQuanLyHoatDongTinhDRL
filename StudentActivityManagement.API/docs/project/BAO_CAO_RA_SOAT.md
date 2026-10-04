# Báo cáo rà soát Case Study Product Backlog và mã nguồn

## Kết luận

Backlog ban đầu đã nhận diện đúng bốn actor và các luồng chính, nhưng chưa đủ chi tiết để triển khai trọn vẹn Case Study theo kiến trúc RESTful API dùng chung cho Web và Mobile. Mã nguồn Sprint 1 build thành công và đã có nền tảng Controller, Service, DTO, EF Core, JWT, Swagger. Tuy nhiên, trước đợt rà soát này có một số lỗi phân quyền, đồng thời đăng ký hoạt động chưa an toàn khi nhiều request chạy cùng lúc.

Bản backlog hiệu chỉnh nằm tại `docs/project/Nhom8_ProductBacklog_Revised.xlsx`. Bản này giữ 26 story gốc, bổ sung 14 story và thêm các sheet Traceability, Acceptance Criteria, Sprint Plan, API Map và Definition of Done.

## Đối chiếu Case Study với backlog cũ

Các phần backlog cũ đã có:

- Bốn actor: Sinh viên, Lớp trưởng, Phòng QL HSSV và Quản trị hệ thống.
- Xem hoạt động, đăng ký, check-in, minh chứng, thông báo, trao đổi lớp, duyệt điểm và báo cáo.
- Quản lý sinh viên, lớp, lớp trưởng, hoạt động, tài khoản và cấu hình.

Các phần còn thiếu hoặc quá chung:

- Một hoạt động có nhiều buổi, cơ sở hoặc địa điểm khác nhau.
- Đối tượng tham gia có cấu trúc theo cơ sở, khóa, lớp, nhóm; phân biệt bắt buộc và khuyến khích.
- Vòng đời đăng ký, trạng thái hủy và tính nhất quán của số chỗ khi có request đồng thời.
- Cửa sổ check-in, QR hoặc mã dùng một lần, chống check-in lặp.
- Metadata, loại file, dung lượng và quyền truy cập minh chứng.
- Kỳ đánh giá, quy tắc cộng điểm và lịch sử cấp, hủy, điều chỉnh điểm.
- Trạng thái đã đọc, delivery log và retry thông báo cho Web hoặc Mobile.
- Refresh token, thu hồi phiên, khóa tài khoản và đặt lại mật khẩu.
- Ngưỡng thiếu điểm, rule gợi ý, export báo cáo, audit log và yêu cầu chất lượng API.

## Lỗi và rủi ro đã phát hiện trong tài liệu

- Tiêu đề sheet Product Backlog ghi nhầm `E-COMMERCE PROJECT`.
- Case Study mô tả Mobile Application cho `shop giao hàng`, không đúng đề tài quản lý điểm rèn luyện.
- Công thức tổng point Sprint 1 chỉ cộng `C7:C11`, bỏ hai story cuối. Tổng đúng là 23 points, không phải 17.
- Sprint Planning ghi SQL Server trong khi project hiện tại dùng SQLite.
- Sprint Planning giao Kiệt 12 points và Phú 11 points. Kế hoạch mới giao leader các phần có ảnh hưởng kiến trúc và phụ thuộc chéo nhiều hơn ở các Sprint sau.

## RESTful API khác MVC đã học như thế nào

Trong ASP.NET Core MVC truyền thống, Controller thường nhận request rồi trả về View HTML. View và Controller thuộc cùng một web application. Mobile không thể tái sử dụng View Razor đó.

Trong project này, ASP.NET Core đóng vai trò backend API. Controller trả JSON và HTTP status code. Web client và Mobile client là hai ứng dụng riêng, cùng gọi một API và không truy cập database trực tiếp.

Luồng chuẩn:

`Web hoặc Mobile -> HTTP request -> API Controller -> Service -> EF Core -> Database -> JSON response`

Các lớp hiện tại đã đi đúng hướng cơ bản:

- `Controllers`: nhận HTTP request, kiểm tra quyền và chuyển việc cho service.
- `Services`: chứa nghiệp vụ như đăng nhập, đăng ký hoạt động và CRUD.
- `DTOs`: định nghĩa dữ liệu request và response, tránh trả entity trực tiếp.
- `Models` và `AppDbContext`: mô hình dữ liệu và truy cập database.
- JWT: Web và Mobile gửi cùng access token trong header `Authorization`.

Điểm cần phát triển tiếp là tách domain rõ hơn, chuẩn hóa route `/api/v1`, thêm migration, integration test, refresh token, file storage abstraction và các module còn thiếu trong backlog.

## Lỗi mã nguồn đã sửa

- Cho phép cả `Student` và `Monitor` dùng API dành cho sinh viên.
- Khi gán lớp trưởng, cập nhật đồng bộ `Role` và `IsClassMonitor`; gỡ vai trò của lớp trưởng cũ.
- Lọc danh sách sinh viên để không lẫn tài khoản Admin.
- Chặn API xem, sửa hoặc xóa “sinh viên” tác động nhầm lên tài khoản Admin; role lớp trưởng chỉ đổi qua API chỉ định lớp trưởng.
- Không cho CRUD sinh viên sửa trực tiếp điểm tích lũy; điểm phải đi qua luồng xét duyệt và lịch sử điểm.
- Kiểm tra lớp tồn tại khi đăng ký hoặc tạo/cập nhật sinh viên; không cho chuyển lớp khi sinh viên vẫn là lớp trưởng.
- Tạo lớp và chỉ định lớp trưởng thành hai bước rõ ràng để tránh quan hệ lớp–lớp trưởng không nhất quán.
- Chuẩn hóa page index và giới hạn page size tối đa 100.
- Chỉ trả hoạt động chưa kết thúc cho danh sách sắp tới.
- Chặn sinh viên truy cập hoạt động Draft, Cancelled hoặc đã kết thúc bằng cách đoán ID.
- Đăng ký hoạt động dùng transaction và câu lệnh tăng số lượng có điều kiện để hạn chế race condition.
- Kiểm tra tài khoản đăng ký phải là sinh viên hoặc lớp trưởng.
- Chặn sức chứa mới nhỏ hơn số người đã đăng ký.
- Kiểm tra thời gian đóng đăng ký không sau thời gian bắt đầu hoạt động.
- Chỉ chấp nhận status hợp lệ.
- Chặn xóa sinh viên đã có lịch sử đăng ký để không làm sai bộ đếm người tham gia.
- Thay SHA-256 với salt cố định bằng PBKDF2 với salt ngẫu nhiên; vẫn đọc được hash cũ và tự nâng cấp sau lần đăng nhập hợp lệ.
- Bỏ toàn bộ JWT secret fallback khỏi cấu hình và mã nguồn; yêu cầu cấu hình bằng user-secrets hoặc biến môi trường.
- Swagger chỉ bật ở Development; CORS production lấy danh sách origin từ cấu hình.
- Sửa chuỗi tiếng Việt bị lỗi encoding và thay file HTTP mẫu cũ.

## Kiểm thử đã thực hiện

- `dotnet build`: 0 warning, 0 error.
- Đăng nhập thành công với Student, Monitor và Admin.
- Student và Monitor đều lấy được danh sách hoạt động.
- Status hoạt động không hợp lệ trả HTTP 400.
- Khi sức chứa bằng 1, lượt đăng ký đầu thành công và lượt tiếp theo bị từ chối HTTP 400.
- API tra cứu và xóa sinh viên đều trả HTTP 404 khi ID thuộc tài khoản Admin.
- Student gọi endpoint Admin trả HTTP 403; Student không thể xem hoạt động Draft bằng ID.
- Swagger root, đăng nhập ba role, đổi trạng thái hợp lệ và đăng ký bằng tài khoản Monitor đều hoạt động.
- `dotnet format --verify-no-changes`: đạt, không còn lỗi định dạng.

## Việc chưa nên đánh dấu Done

- Chưa có project test tự động trong solution.
- Database đang dùng `EnsureCreated`, chưa có EF Core migration chính thức.
- Chưa version route theo `/api/v1` như API Map đích.
- Chưa có refresh token, revoke token, rate limiting và account lockout.
- Mô hình `TargetAudience` vẫn là chuỗi, chưa đủ để kiểm tra eligibility.
- Chưa có các module từ Sprint 2 đến Sprint 6.

## Thứ tự tiếp theo

1. Chốt Sprint 1 bằng integration test và migration đầu tiên.
2. Thực hiện Sprint 2 theo `Sprint Plan`, ưu tiên activity session, audience rule và vòng đời đăng ký.
3. Mỗi thay đổi database phải cập nhật hướng dẫn Kiệt và ghi rõ migration trong Pull Request.
4. Chỉ chuyển story sang Done khi đạt toàn bộ sheet Definition of Done.
