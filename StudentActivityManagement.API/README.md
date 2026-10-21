# Student Activity Management API

Backend RESTful API cho hệ thống quản lý hoạt động và tính điểm rèn luyện. API được thiết kế để Web và Mobile dùng chung dữ liệu, phân quyền và nghiệp vụ.

> Project hiện tại là **backend API**, chưa có giao diện Web/Mobile. Khi chạy, trình duyệt hiển thị Swagger với danh sách endpoint là đúng thiết kế ở giai đoạn này.

## Cập nhật và chạy bản mới

Yêu cầu: .NET SDK 8. Chạy các lệnh sau từ thư mục gốc của repository, nơi có file `Nhom8_ActivitiesManagement.sln`:

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
| Admin | `ADMIN001` | `Admin123@` |
| Lớp trưởng | `SV210001` | `123456` |
| Sinh viên | `SV210002` | `123456` |

Để thử API có phân quyền:

1. Mở `POST /api/v1/auth/login`, chọn **Try it out** và đăng nhập.
2. Sao chép giá trị `data.token` trong response.
3. Chọn nút **Authorize** ở đầu Swagger, nhập `Bearer <token>`.
4. Sinh viên thử nhóm `/api/v1/student-activities`; Admin thử `/api/v1/admin/students`, `/api/v1/admin/classes` và `/api/v1/admin/activities`.

Sprint 2 đã chuẩn hóa hoạt động nhiều buổi. Mỗi hoạt động có `organizerName`, danh sách `sessions` và `audienceRules`. Sinh viên chỉ thấy và đăng ký hoạt động phù hợp cơ sở, khóa hoặc lớp của mình.

## Cấu trúc project

- `Controllers`, `Services`, `DTOs`, `Models`, `Data`: các lớp chính của RESTful API.
- [`StudentActivityManagement.API.http`](StudentActivityManagement.API.http): request mẫu dùng trong Visual Studio, Rider hoặc VS Code REST Client.
- `scripts/setup-local.ps1`: cấu hình JWT secret an toàn cho máy local.
- `scripts/update-database.ps1`: restore EF tool và áp dụng migration đã commit.
- `StudentActivityManagement.API.Tests`: integration test cho JWT, phân quyền, OpenAPI, lịch nhiều buổi và đối tượng tham gia.

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
