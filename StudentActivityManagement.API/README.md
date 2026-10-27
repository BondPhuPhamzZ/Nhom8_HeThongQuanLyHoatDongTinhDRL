# Student Activity Management API

Backend RESTful API cho hệ thống quản lý hoạt động và tính điểm rèn luyện. API được thiết kế để Web và Mobile dùng chung dữ liệu, phân quyền và nghiệp vụ.

> Project hiện tại là **backend API**, chưa có giao diện Web/Mobile. Khi chạy, trình duyệt hiển thị Swagger với danh sách endpoint là đúng thiết kế ở giai đoạn này.

## Cập nhật và chạy bản mới

Yêu cầu: .NET SDK 8. Chạy các lệnh sau từ thư mục gốc của repository, nơi có file `Nhom8_ActivitiesManagement.sln`:

> Chỉ dùng `Nhom8_ActivitiesManagement.sln` ở gốc repository: file này chứa cả API và project test. Nếu thư mục cha (ngoài Git) còn một file `.sln` cùng tên do Visual Studio tạo lúc khởi tạo, file đó chỉ có project API, thiếu project test và không được theo dõi trong Git, nên không mở file đó.

```powershell
git switch main
git pull --ff-only origin main
dotnet restore .\Nhom8_ActivitiesManagement.sln
cd .\StudentActivityManagement.API
dotnet tool restore
pwsh -NoProfile -File .\scripts\setup-local.ps1
pwsh -NoProfile -File .\scripts\update-database.ps1
dotnet run --launch-profile http
```

Máy mới hoặc chưa có database chỉ dùng lệnh ở trên. Nếu máy đang giữ database Sprint 1 cũ chưa có migration history, thay riêng lệnh cập nhật database bằng lệnh dưới đây đúng một lần:

```powershell
pwsh -NoProfile -File .\scripts\update-database.ps1 -ResetLegacyDatabase
```

Script đổi tên database cũ thành bản `.bak` trước khi tạo database theo migration. Những lần chạy sau không dùng lại `-ResetLegacyDatabase`.

Mở [http://localhost:5210](http://localhost:5210). Swagger được đặt tại trang gốc khi chạy ở môi trường Development.

Tài khoản dữ liệu mẫu:

| Quyền | Tên đăng nhập | Mật khẩu |
|---|---|---|
| Quản trị viên hệ thống (`SystemAdmin`) | `ADMIN001` | `Admin123@` |
| Phòng Quản lý HSSV (`HSSV`) | `HSSV001` | `Hssv123@` |
| Lớp trưởng (`Monitor`) | `SV210001` | `123456` |
| Sinh viên (`Student`) | `SV210002` | `123456` |

`HSSV001` được bổ sung tự động khi chạy ở môi trường Development, kể cả trên database đã có dữ liệu từ trước.

Để thử API có phân quyền:

1. Mở `POST /api/v1/auth/login`, chọn **Try it out** và đăng nhập.
2. Sao chép giá trị `data.token` trong response.
3. Chọn nút **Authorize** ở đầu Swagger, nhập `Bearer <token>`.
4. Sinh viên/lớp trưởng thử nhóm `/api/v1/student-activities`; HSSV hoặc SystemAdmin thử `/api/v1/admin/students`, `/api/v1/admin/classes` và `/api/v1/admin/activities`; chỉ SystemAdmin dùng `/api/v1/admin/accounts`.
5. Sinh viên có thể hủy đăng ký trước hạn qua DELETE /api/v1/student-activities/{activityId}/registration (US-05: idempotent, hoàn capacity và bảo lưu lịch sử).

## Phân quyền (US-25)

| Nhóm endpoint | Student | Monitor | HSSV | SystemAdmin |
|---|---|---|---|---|
| `/api/v1/auth/me` | ✓ | ✓ | ✓ | ✓ |
| `/api/v1/student-activities/*` | ✓ | ✓ | 403 | 403 |
| `/api/v1/admin/students`, `/classes`, `/activities` | 403 | 403 | ✓ | ✓ |
| `/api/v1/admin/accounts/*` | 403 | 403 | 403 | ✓ |

- Tài khoản sinh viên do Phòng QL HSSV tạo qua `POST /api/v1/admin/students`. Endpoint tự đăng ký `POST /api/v1/auth/register` đã được gỡ (breaking change).
- API tài khoản chỉ tạo tài khoản cán bộ (`HSSV`, `SystemAdmin`) và chỉ đổi vai trò giữa hai role này. Lớp trưởng (`Monitor`) chỉ được chỉ định qua `PUT /api/v1/admin/classes/{id}/monitor/{studentId}`.
- Không thể tự đổi vai trò, tự khóa tài khoản của mình, hoặc làm hệ thống không còn `SystemAdmin` đang hoạt động.
- Khóa tài khoản hoặc đổi vai trò (kể cả chỉ định lớp trưởng mới) có hiệu lực ngay: token cũ nhận `401`, client phải đăng nhập lại. Tài khoản bị khóa đăng nhập nhận `403` với `errorCode = account_disabled`.

## Quy ước dữ liệu hoạt động (Sprint 3)

- **Thời gian:** mọi giá trị thời gian gửi lên phải theo ISO 8601 và có múi giờ (`2026-11-02T08:00:00+07:00` hoặc `2026-11-02T01:00:00Z`); thiếu múi giờ nhận `400 validation_failed` (breaking change). API lưu và luôn trả về UTC dạng `...Z`; client tự đổi sang giờ Việt Nam khi hiển thị.
- **Sửa hoạt động:** gửi kèm `id` của buổi cũ để giữ nguyên buổi đó (check-in sẽ gắn theo id buổi); buổi không có `id` được tạo mới; buổi cũ không có trong request bị xóa. `id` của hoạt động khác nhận `400`.
- **Đăng ký lại:** sinh viên đã hủy có thể đăng ký lại nếu còn hạn và còn chỗ; hệ thống dùng lại bản ghi cũ. Endpoint sinh viên trả thêm `myRegistrationStatus` (`Registered`, `Cancelled`, ... hoặc `null`).
- **Hoạt động đang diễn ra:** sinh viên thấy cả hoạt động `Published` và `Ongoing` (để check-in); chỉ `Published` mới đăng ký được.

Sprint 2 đã chuẩn hóa hoạt động nhiều buổi. Mỗi hoạt động có `organizerName`, danh sách `sessions` và `audienceRules`. Sinh viên chỉ thấy và đăng ký hoạt động phù hợp cơ sở, khóa hoặc lớp của mình.

## Cấu trúc project

- `Controllers`, `Services`, `DTOs`, `Models`, `Data`: các lớp chính của RESTful API.
- [`StudentActivityManagement.API.http`](StudentActivityManagement.API.http): request mẫu dùng trong Visual Studio, Rider hoặc VS Code REST Client.
- `scripts/setup-local.ps1`: cấu hình JWT secret an toàn cho máy local.
- `scripts/update-database.ps1`: restore EF tool và áp dụng migration đã commit.
- `StudentActivityManagement.API.Tests`: integration test cho JWT, phân quyền, OpenAPI, lịch nhiều buổi, đối tượng tham gia, hủy đăng ký và quản lý tài khoản.

Backlog, Sprint, báo cáo, kịch bản demo và hướng dẫn thành viên được quản lý ngoài repository để Git chỉ chứa phần mềm cần build/chạy.

## Kiểm tra trước khi push hoặc báo cáo

```powershell
dotnet restore ..\Nhom8_ActivitiesManagement.sln
dotnet build ..\Nhom8_ActivitiesManagement.sln --no-restore -m:1
dotnet test ..\Nhom8_ActivitiesManagement.sln --no-build --no-restore -m:1
```

## Lưu ý

- Không commit JWT secret, database `.db`, thư mục `bin`, `obj` hoặc file `.user`.
- Script `scripts/setup-local.ps1` sinh khóa ngẫu nhiên và lưu bằng .NET user-secrets ngoài repository; script không in giá trị secret.
- Project dùng SQLite và EF Core migrations; không sửa trực tiếp database rồi đưa file `.db` lên Git.
- Swagger chỉ bật trong Development.
- Web UI và Mobile UI sẽ là client riêng, gọi cùng API này qua HTTP/JSON.
