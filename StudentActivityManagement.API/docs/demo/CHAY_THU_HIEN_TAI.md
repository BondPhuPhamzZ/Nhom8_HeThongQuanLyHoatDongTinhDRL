# Hướng dẫn chạy thử ở thời điểm hiện tại

## Vì sao không thấy giao diện?

Project hiện tại là ASP.NET Core Web API. Nó không chứa Razor View, HTML hay ứng dụng Mobile, vì vậy màn hình có các nhóm đường dẫn như `/api/auth/login` và `/api/admin/activities` chính là **Swagger UI**—giao diện để đọc và gọi thử API.

Kiến trúc dự kiến:

```text
Web UI ---------\
                 -> REST API -> Service -> EF Core -> Database
Mobile App -----/
```

Web và Mobile sẽ được xây riêng nhưng dùng chung REST API này.

## Chạy bằng terminal

Mở PowerShell tại thư mục `StudentActivityManagement.API`:

```powershell
dotnet --version
dotnet restore
dotnet user-secrets set "JwtSettings:Secret" "Nhom8-local-secret-thay-doi-truoc-khi-demo-2026"
dotnet run --launch-profile http
```

Khi terminal hiện `Now listening on: http://localhost:5210`, mở:

```text
http://localhost:5210
```

Giữ terminal đang chạy. Nhấn `Ctrl+C` khi muốn dừng.

## Chạy bằng Visual Studio

1. Mở file solution `Nhom8_ActivitiesManagement.sln`.
2. Chọn `StudentActivityManagement.API` làm Startup Project.
3. Mở terminal trong Visual Studio và cấu hình user-secret như phần trên một lần duy nhất.
4. Chọn profile `http` rồi nhấn `F5` hoặc `Ctrl+F5`.
5. Trình duyệt mở Swagger tại `http://localhost:5210`.

## Demo đăng nhập và phân quyền

Trong Swagger, mở `POST /api/auth/login`, chọn **Try it out** và dùng một trong các body sau.

Sinh viên:

```json
{
  "usernameOrEmail": "SV210002",
  "password": "123456"
}
```

Admin:

```json
{
  "usernameOrEmail": "ADMIN001",
  "password": "Admin123@"
}
```

Sau khi Execute:

1. Sao chép `data.token`.
2. Nhấn **Authorize** ở đầu trang.
3. Nhập `Bearer `, một dấu cách, rồi dán token.
4. Đăng nhập sinh viên để gọi `GET /api/StudentActivities`.
5. Đăng nhập Admin để gọi `GET /api/admin/Students` và `GET /api/admin/Activities`.

## Nếu chạy không được

- Lỗi thiếu `JwtSettings:Secret`: chạy lại lệnh `dotnet user-secrets set`.
- Port 5210 đang bận: đóng tiến trình cũ hoặc chạy `dotnet run --urls http://localhost:5220` rồi mở port 5220.
- Database cũ sai schema: không xóa ngay nếu có dữ liệu cần giữ. Sao lưu `student_activity.db`, sau đó mới tạo lại database local.
- HTTP 401: chưa gửi token hoặc token sai/hết hạn.
- HTTP 403: token hợp lệ nhưng tài khoản không có đúng role.
- Swagger không xuất hiện khi Production: đây là chủ ý bảo mật; chạy bằng profile Development.
