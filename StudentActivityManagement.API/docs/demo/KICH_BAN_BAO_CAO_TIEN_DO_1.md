# Kịch bản báo cáo tiến độ 1

Thời lượng gợi ý: 8–10 phút. Phú trình bày kiến trúc, nghiệp vụ quan trọng và phần demo chính; Kiệt trình bày quy trình nhóm và phần việc được giao.

## Slide 1 — Giới thiệu đề tài (30–45 giây, Phú)

> Nhóm 8 thực hiện hệ thống quản lý hoạt động và tính điểm rèn luyện. Hệ thống phục vụ sinh viên, lớp trưởng, Phòng Quản lý Học sinh Sinh viên và quản trị hệ thống. Mục tiêu là quản lý xuyên suốt từ công bố hoạt động, đăng ký, check-in, minh chứng đến xét điểm và báo cáo.

## Slide 2 — Vấn đề và phạm vi (45–60 giây, Phú)

> Nếu quản lý bằng biểu mẫu rời rạc, số chỗ, minh chứng và điểm rất dễ không đồng bộ. Nhóm xây một backend tập trung để mọi rule được xử lý tại một nơi. Case Study yêu cầu Web và Mobile, vì vậy nhóm chọn RESTful API thay vì gắn nghiệp vụ trực tiếp vào Razor View.

Nêu bốn actor và các luồng đã xác định trong backlog.

## Slide 3 — Kiến trúc (60–90 giây, Phú)

> Web và Mobile là hai client riêng nhưng cùng gửi HTTP request đến ASP.NET Core API. Controller xử lý route và authorization; Service xử lý nghiệp vụ; DTO quy định dữ liệu request/response; Entity Framework Core truy cập SQLite. API trả JSON và HTTP status code, không trả Razor View.

Sơ đồ để trình chiếu:

```text
Web / Mobile -> Controller -> Service -> EF Core -> SQLite
                    |
                 JWT + Role
```

Giải thích ngắn rằng Swagger đang được dùng làm giao diện kiểm thử backend trong giai đoạn này.

## Slide 4 — Kết quả Sprint 1 (90 giây, Phú)

> Sprint 1 đã tạo nền tảng project .NET 8, cấu hình database, JWT, Swagger và phân quyền. Nhóm đã có đăng nhập, thông tin người dùng, quản lý sinh viên, lớp, lớp trưởng, hoạt động và đăng ký hoạt động. Sau rà soát, nhóm sửa thêm phân quyền Student/Monitor, bảo vệ tài khoản Admin, validate hoạt động và chống đăng ký vượt sức chứa khi có request đồng thời.

Nhấn mạnh Sprint 1 có 23 points, không phải 17 points như công thức cũ trong file planning.

## Slide 5 — Demo trực tiếp (2–3 phút, Phú)

Thứ tự demo an toàn:

1. Mở Swagger và nói rõ đây là tài liệu API tương tác.
2. Login bằng `SV210002`.
3. Authorize bằng JWT và gọi `GET /api/StudentActivities`.
4. Gọi đăng ký một hoạt động đang mở.
5. Login lại bằng `ADMIN001`.
6. Gọi danh sách sinh viên và danh sách hoạt động của Admin.
7. Chỉ ra endpoint gán lớp trưởng và các endpoint có biểu tượng khóa.

Lời chuyển:

> Cùng một endpoint và token convention này có thể được gọi từ giao diện Web hoặc ứng dụng Mobile; client không truy cập database trực tiếp.

## Slide 6 — Tổ chức nhóm và kiểm soát chất lượng (60 giây, Kiệt)

> Nhóm làm việc theo story và branch riêng. Trước Pull Request phải build, đọc diff, mô tả API contract và migration nếu có. Phú phụ trách kiến trúc và các phần nghiệp vụ có phụ thuộc lớn; em phụ trách các story độc lập theo Sprint Plan và bàn giao qua Pull Request để Phú review.

Kiệt có thể mở nhanh folder `docs/kiet` để chứng minh quy trình đã được tài liệu hóa.

## Slide 7 — Backlog hiệu chỉnh và Sprint 2 (60–90 giây, Phú)

> Sau khi trace Case Study, nhóm mở rộng backlog từ 26 lên 40 story, tổng 148 points và chia 6 Sprint. Sprint 2 tập trung vào vòng đời đăng ký, nhiều lịch tổ chức, đối tượng tham gia có cấu trúc, refresh token và quản trị tài khoản. Các hạng mục chưa làm sẽ không được báo cáo là hoàn thành.

Nêu rõ hai giới hạn hiện tại: chưa có giao diện Web/Mobile và chưa chuyển từ `EnsureCreated` sang EF Core migrations.

## Slide 8 — Kết luận (30 giây, Phú)

> Tiến độ hiện tại đã hoàn thành nền tảng backend và các luồng lõi của Sprint 1. Kiến trúc đã sẵn sàng để Web và Mobile dùng chung. Bước tiếp theo của nhóm là hoàn thiện Sprint 2, thêm migration, automated tests và bắt đầu client Web sau khi API contract ổn định.

## Câu hỏi dự phòng

### “Tại sao chạy lên không có giao diện?”

> Vì repository hiện tại là backend RESTful API. Swagger là giao diện tài liệu và kiểm thử API. Web và Mobile sẽ là client riêng gọi vào backend này.

### “RESTful khác MVC đã học thế nào?”

> MVC server-rendered thường trả View HTML. REST API trả JSON và status code, nên nhiều client có thể tái sử dụng cùng backend. Các kiến thức ASP.NET Core như Controller, routing, middleware, DI và EF Core vẫn được dùng.

### “Tại sao dùng SQLite thay vì SQL Server?”

> SQLite giúp nhóm dựng prototype và demo nhanh. Trước khi mở rộng schema, nhóm sẽ dùng EF Core migrations; provider có thể đổi sang SQL Server mà không thay đổi contract của Web/Mobile.

### “Đã xử lý đăng ký cùng lúc chưa?”

> Có. Việc tăng số người tham gia dùng transaction và cập nhật có điều kiện `CurrentParticipantsCount < MaxParticipants`, nên hai request tranh chỗ cuối không thể cùng thành công.

### “Điểm rèn luyện hiện đã hoàn chỉnh chưa?”

> Chưa. Sprint 1 mới có trường tổng điểm mẫu. Kỳ đánh giá, quy tắc điểm và lịch sử cấp/điều chỉnh điểm nằm trong các Sprint tiếp theo và đã được bổ sung vào backlog.

## Checklist trước ngày báo cáo

- Pull nhánh `main` mới nhất và build trước ít nhất một ngày.
- Cấu hình user-secret trên máy dùng để trình chiếu.
- Chạy thử toàn bộ luồng demo, ghi lại ID hoạt động sẽ đăng ký.
- Đóng ứng dụng đang chiếm port 5210.
- Chuẩn bị ảnh chụp Swagger và sơ đồ kiến trúc để dùng nếu mạng hoặc máy chiếu gặp lỗi.
- Không mở file chứa secret, token hoặc dữ liệu cá nhân thật khi trình chiếu.
