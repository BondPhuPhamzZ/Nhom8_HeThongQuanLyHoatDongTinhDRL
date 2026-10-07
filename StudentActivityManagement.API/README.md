# Student Activity Management API

Backend RESTful API cho hệ thống quản lý hoạt động và tính điểm rèn luyện. API được thiết kế để Web và Mobile dùng chung dữ liệu, phân quyền và nghiệp vụ.

> Project hiện tại là **backend API**, chưa có giao diện Web/Mobile. Khi chạy, trình duyệt hiển thị Swagger với danh sách endpoint là đúng thiết kế ở giai đoạn này.

## Chạy nhanh

Yêu cầu: .NET SDK 8.

```powershell
cd StudentActivityManagement.API
dotnet restore
dotnet user-secrets set "JwtSettings:Secret" "Nhom8-local-secret-thay-doi-truoc-khi-demo-2026"
dotnet run --launch-profile http
```

Mở [http://localhost:5210](http://localhost:5210). Swagger được đặt tại trang gốc khi chạy ở môi trường Development.

Tài khoản dữ liệu mẫu:

| Quyền | Tên đăng nhập | Mật khẩu |
|---|---|---|
| Admin | `ADMIN001` | `Admin123@` |
| Lớp trưởng | `SV210001` | `123456` |
| Sinh viên | `SV210002` | `123456` |

Để thử API có phân quyền:

1. Mở `POST /api/auth/login`, chọn **Try it out** và đăng nhập.
2. Sao chép giá trị `data.token` trong response.
3. Chọn nút **Authorize** ở đầu Swagger, nhập `Bearer <token>`.
4. Sinh viên thử nhóm `/api/StudentActivities`; Admin thử `/api/admin/Students`, `/api/admin/Classes` và `/api/admin/Activities`.

## Cấu trúc project

- `Controllers`, `Services`, `DTOs`, `Models`, `Data`: các lớp chính của RESTful API.
- [`StudentActivityManagement.API.http`](StudentActivityManagement.API.http): request mẫu dùng trong Visual Studio, Rider hoặc VS Code REST Client.
- `scripts/setup-local.ps1`: cấu hình JWT secret an toàn cho máy local.

Backlog, Sprint, báo cáo, kịch bản demo và hướng dẫn thành viên được quản lý ngoài repository để Git chỉ chứa phần mềm cần build/chạy.

## Kiểm tra trước khi push hoặc báo cáo

```powershell
dotnet restore
dotnet build
dotnet test
```

Solution hiện chưa có test project nên `dotnet test` chủ yếu xác nhận solution tải và build được. Automated tests và EF Core migrations là hạng mục phải bổ sung trong Sprint tiếp theo.

## Lưu ý

- Không commit JWT secret, database `.db`, thư mục `bin`, `obj` hoặc file `.user`.
- Project hiện dùng SQLite và `EnsureCreated`; không sửa trực tiếp database rồi đưa file `.db` lên Git.
- Swagger chỉ bật trong Development.
- Web UI và Mobile UI sẽ là client riêng, gọi cùng API này qua HTTP/JSON.
