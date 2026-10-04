# Database và cập nhật schema

## Trạng thái hiện tại

Project dùng SQLite và gọi `EnsureCreated`. Cách này phù hợp dựng prototype nhưng không quản lý lịch sử thay đổi schema. Không tự sửa database bằng SQLite Browser rồi commit file `.db`.

Trong Sprint 2, nhóm phải chuyển sang EF Core Migrations trước khi thêm các bảng mới.

## Quy trình đích sau khi có migrations

Tạo migration:

```powershell
dotnet ef migrations add TenMigrationRoRang
dotnet ef database update
```

Sau khi pull migration của người khác:

```powershell
dotnet restore
dotnet ef database update
dotnet build
```

## Khi Pull Request có thay đổi database

Tác giả phải ghi:

- Tên migration.
- Bảng, cột, index, foreign key thay đổi.
- Có mất dữ liệu hay không.
- Lệnh apply và rollback nếu cần.
- Seed data có thay đổi hay không.

## Quy tắc model

- Không dùng chuỗi tự do cho dữ liệu cần query hoặc kiểm tra rule, ví dụ TargetAudience.
- Có unique index cho mã sinh viên, email và các khóa nghiệp vụ.
- Dùng UTC cho thời gian lưu trong database.
- Không lưu file lớn trực tiếp trong SQLite. Chỉ lưu metadata và URL hoặc storage key.
- Điểm đã duyệt phải có lịch sử thay đổi, không chỉ ghi đè một tổng điểm.
