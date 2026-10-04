# Quy tắc code RESTful API

## Trách nhiệm từng lớp

- Controller: route, model binding, authorization và chuyển request sang service.
- Service: nghiệp vụ và transaction.
- DTO: contract request và response.
- Entity: dữ liệu lưu trong database.
- DbContext: mapping, index và relationship.

Không viết toàn bộ nghiệp vụ trong Controller. Không trả entity có navigation property trực tiếp ra client.

## Web và Mobile dùng chung API

API chỉ trả JSON và status code. Không trả Razor View. Web và Mobile tự xây giao diện, nhưng dùng chung rule ở backend.

## Quy ước endpoint đích

```text
GET    /api/v1/activities
GET    /api/v1/activities/{id}
POST   /api/v1/activities/{id}/registrations
DELETE /api/v1/registrations/{id}
```

Dùng danh từ số nhiều. Không đặt route kiểu `/GetActivities` hoặc `/CreateActivity`.

## Checklist trước khi giao task

- Validate input bằng DTO.
- Kiểm tra role và ownership.
- Dùng `async` cho database và I/O.
- Dùng transaction cho nhiều thay đổi phải thành công cùng nhau.
- Xử lý request đồng thời ở các thao tác liên quan sức chứa hoặc điểm.
- Không hard-code secret, URL production hoặc password.
- Cập nhật Swagger.
- Viết test cho happy path và edge case quan trọng.
- Build không warning.
