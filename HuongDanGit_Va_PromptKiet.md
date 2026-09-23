# HƯỚNG DẪN LÀM VIỆC NHÓM TRÊN GITHUB & PROMPT CHO KIỆT

## 1. QUY TRÌNH GITHUB (TRÁNH CONFLICT 100%)
Vì cả 2 cùng code chung một project (cùng sửa chung file DbContext, Program.cs), nếu code đè lên nhánh main chắc chắn sẽ nát project. 

**Tuyệt chiêu:** Mỗi người tạo một NHÁNH (Branch) riêng!

### Quy trình chuẩn dành cho Phú và Kiệt:
**Bước 1:** Phú tạo project Base (theo hướng dẫn setup), đưa lên Github ở nhánh main.
**Bước 2:** Kiệt clone code về.
**Bước 3 (Cực kỳ quan trọng):** 
- Phú tạo nhánh riêng tên là eature/phu-api
- Kiệt tạo nhánh riêng tên là eature/kiet-api
*(Lệnh git: git checkout -b tên_nhánh_của_bạn)*

**Bước 4:** Ai code xong tính năng nào, thì Add và Commit lên nhánh của người đó.
- git add .
- git commit -m "Phu hoan thanh API Dang ky hoat dong"
- git push origin feature/phu-api (Đẩy lên nhánh của Phú, KHÔNG ẢNH HƯỞNG GÌ TỚI KIỆT).

**Bước 5 (Gộp code - Merge):** 
Cuối tuần (21-Sep), hai bạn ngồi cùng nhau, lên Github bấm tạo **Pull Request** gộp nhánh của Phú và nhánh của Kiệt vào main. Nếu có file nào bị đỏ (Conflict), hai bạn xem dòng nào cần giữ lại, dòng nào bỏ đi là xong.

---

## 2. PROMPT DÀNH CHO KIỆT (ĐỂ CODE KHỚP VỚI PHÚ)

**Phú hãy copy và gửi nguyên đoạn tin nhắn này cho Kiệt thả vào AI (ChatGPT/Claude/Gemini) để nó sinh code chuẩn nhé:**

> "Tôi đang làm backend ASP.NET Core Web API .NET 8. Bạn cùng nhóm của tôi (Phú) sẽ làm các API về (Lấy danh sách hoạt động, Đăng ký tham gia, Quản lý lớp trưởng). Phần của tôi (Kiệt) ở Sprint 1 bao gồm: 
> 1. Thiết lập Database Schema bằng Entity Framework Core (Code-First) theo các thực thể: User, Class, Student, Activity, ActivityRegistration.
> 2. Viết API Authentication (Đăng nhập) sinh ra JWT Token phân quyền (Student, Admin).
> 3. Viết API CRUD Quản lý sinh viên (Admin).
> 4. Viết API CRUD Quản lý hoạt động (Admin).
>
> Hãy giúp tôi:
> - Định nghĩa các Models chuẩn nhất (có Data Annotations).
> - Cấu hình AppDbContext và connection string.
> - Cấu hình JWT trong Program.cs.
> - Viết chi tiết AuthController và ActivitiesController (phần của Admin).
> Xin lưu ý code phải tách biệt Controller, sử dụng DTO, và code thật clean để bạn Phú có thể gọi các Models này ra dùng cho phần API Đăng ký của bạn ấy mà không bị lỗi."

