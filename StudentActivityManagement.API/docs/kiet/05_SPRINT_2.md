# Sprint 2 dành cho Kiệt

Thời gian dự kiến: 05/10/2026 đến 18/10/2026.

## Task của Kiệt

### US-05 Hủy đăng ký

- Thêm endpoint hủy đăng ký của chính sinh viên.
- Chỉ cho hủy trước khi đóng đăng ký.
- Không xóa bản ghi. Chuyển trạng thái `Cancelled`.
- Giảm số lượng đúng một lần và xử lý request lặp.

### US-27 Refresh token và thu hồi phiên

- Thiết kế bảng refresh token hoặc session.
- Lưu hash của refresh token, không lưu token thô.
- Rotate token khi refresh.
- Revoke khi logout hoặc đổi mật khẩu.

### US-28 Khóa tài khoản và đặt lại mật khẩu

- Bổ sung trạng thái tài khoản.
- Login phải từ chối tài khoản bị khóa.
- Luồng reset không trả mật khẩu qua API.

## Việc phải bàn giao cho Phú

- DTO và API contract trước khi code Controller.
- Migration và mô tả schema.
- Test cho hủy lặp, refresh token dùng lại và tài khoản bị khóa.
- Cập nhật `StudentActivityManagement.API.http` hoặc collection test.
- Ghi rõ breaking change trong Pull Request.

## Không tự làm trong branch này

- Không đổi cấu trúc Activity Session hoặc Audience Rule do Phú phụ trách.
- Không sửa route của toàn project nếu chưa thống nhất US-40.
- Không merge migration của hai branch bằng cách xóa migration của người khác.
