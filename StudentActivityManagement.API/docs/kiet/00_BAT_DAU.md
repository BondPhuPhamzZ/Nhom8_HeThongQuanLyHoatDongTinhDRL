# Bắt đầu sau khi pull code

## Phần mềm cần có

- Git.
- .NET SDK 8.
- IDE: Visual Studio 2022, Rider hoặc VS Code.
- Postman tùy chọn. Swagger đã có sẵn khi chạy Development.

## Lần đầu clone

```powershell
git clone <URL_REPOSITORY>
cd Nhom8_ActivitiesManagement\StudentActivityManagement.API
dotnet restore
dotnet user-secrets set "JwtSettings:Secret" "thay-bang-chuoi-bi-mat-it-nhat-32-ky-tu"
dotnet build
dotnet run
```

Mở địa chỉ được in trong terminal. Với launch profile hiện tại, Swagger thường ở `https://localhost:7025` hoặc `http://localhost:5210`.

## Tài khoản seed

Chỉ dùng ở máy local:

- Admin: `ADMIN001` / `Admin123@`
- Lớp trưởng: `SV210001` / `123456`
- Sinh viên: `SV210002` / `123456`

Không dùng các mật khẩu này ở môi trường demo công khai hoặc production.

## Sau mỗi lần pull

```powershell
git status
git pull --rebase origin main
dotnet restore
dotnet build
```

Sau đó đọc Pull Request hoặc tin nhắn bàn giao để biết có migration mới hay thay đổi secret, port hoặc API contract hay không.
