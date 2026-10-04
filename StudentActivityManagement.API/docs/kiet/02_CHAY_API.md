# Chạy và kiểm tra API

## Cấu hình secret

Project không lưu JWT secret trong Git.

```powershell
dotnet user-secrets set "JwtSettings:Secret" "chuoi-bi-mat-it-nhat-32-ky-tu"
```

Có thể dùng biến môi trường trong CI hoặc container:

```powershell
$env:JwtSettings__Secret="chuoi-bi-mat-it-nhat-32-ky-tu"
```

## Chạy local

```powershell
dotnet restore
dotnet build
dotnet run
```

Swagger chỉ được bật ở môi trường Development.

## Test JWT trong Swagger

1. Gọi `POST /api/auth/login`.
2. Copy `data.token`.
3. Bấm Authorize và nhập `Bearer <token>`.
4. Gọi endpoint theo đúng role.

## HTTP status cần dùng

- `200`: đọc hoặc cập nhật thành công.
- `201`: tạo mới thành công.
- `400`: dữ liệu request sai.
- `401`: chưa đăng nhập hoặc token hết hạn.
- `403`: đã đăng nhập nhưng sai quyền.
- `404`: không tìm thấy resource.
- `409`: xung đột nghiệp vụ như đăng ký trùng hoặc hết chỗ do request cạnh tranh.

Hiện một số endpoint cũ vẫn trả `400` cho xung đột nghiệp vụ. Khi thực hiện US-40 sẽ chuẩn hóa toàn bộ.
