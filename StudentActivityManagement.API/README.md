# Student Activity Management API

Backend RESTful API cho hệ thống quản lý hoạt động và tính điểm rèn luyện. API được thiết kế để Web và Mobile dùng chung dữ liệu, phân quyền và nghiệp vụ.

> Chạy riêng project API sẽ mở Swagger để kiểm thử endpoint. Giao diện Web MVC đã nằm trong project `../StudentActivityManagement.Web` và gọi API này qua HTTP/JSON; ứng dụng Mobile chưa được triển khai.

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

Máy mới hoặc chưa có database chỉ dùng lệnh ở trên. Nếu API dừng ở `Database.Migrate()` với lỗi `table "Activities" already exists`, database Sprint 1 cũ có bảng nhưng chưa có `__EFMigrationsHistory`. **Dừng API/Visual Studio**, rồi thay riêng lệnh cập nhật database bằng lệnh dưới đây đúng một lần:

```powershell
pwsh -NoProfile -File .\scripts\update-database.ps1 -ResetLegacyDatabase
```

Script giữ nguyên database cũ cùng các file SQLite phụ (`-wal`, `-shm`, `-journal`) dưới tên `.legacy-*.bak*`, rồi tạo database mới theo migration. Dữ liệu cũ nằm trong bản sao lưu, **không tự chuyển sang DB mới**. Những lần chạy sau không dùng lại `-ResetLegacyDatabase`.

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
- **Vòng đời hoạt động:**
  - `Draft → Published | Cancelled`
  - `Published → Ongoing | Cancelled | Draft` (về `Draft` chỉ khi không còn sinh viên đăng ký)
  - `Ongoing → Completed | Cancelled`
  - `Completed`, `Cancelled` là trạng thái cuối, không sửa được nữa.

  Hoạt động mới chỉ tạo ở `Draft` hoặc `Published`. Chuyển sai nhận `409 business_rule_violation`. Response cho cán bộ có `allowedNextStatuses` để client chỉ hiện bước hợp lệ.
- **Xóa hoạt động:** chỉ xóa được hoạt động `Draft` chưa từng có bản ghi đăng ký. Trường hợp khác nhận `409`; hãy chuyển sang `Cancelled` để giữ lịch sử tham gia.
- **Yêu cầu chuẩn bị:** trường `requirements` (tối đa 2000 ký tự) trong tạo/sửa và chi tiết hoạt động.
- **Không xóa buổi đã có dữ liệu:** khi sửa hoạt động, bỏ một buổi đã có check-in hoặc minh chứng nhận `409`.

### Thay đổi status code (breaking change, Sprint 3)

| Trường hợp | Trước | Nay |
|---|---|---|
| Đăng ký ngoài thời gian đăng ký | `400` | `409 business_rule_violation` |
| `POST /api/v1/admin/classes` thành công | `200` | `201 Created` |
| Xóa hoạt động không phải `Draft`, hoặc nhảy trạng thái sai vòng đời | `200` | `409 business_rule_violation` |

### Nền check-in và minh chứng (Sprint 3)

Migration `Sprint3CheckInAndEvidence` thêm bốn bảng và hai cột vào `ActivitySessions` (`CheckInCode`, `CheckInCodeGeneratedAt`):

| Bảng | Mục đích | Story |
|---|---|---|
| `ActivityCheckIns` | Mỗi đăng ký tối đa một check-in cho mỗi buổi; ghi phương thức và cán bộ điểm danh hộ | US-07, US-21 |
| `ActivityCheckInConfigs` | Phương thức (`Code`/`Manual`) và cửa sổ check-in của hoạt động. Không có dòng thì dùng mặc định `Code`, mở trước 15 phút, đóng sau 30 phút | US-26 |
| `ActivityReminderOffsets` | Các mốc nhắc (phút trước mỗi buổi). Mặc định `[1440]`; Sprint 3 chỉ lưu | US-26, US-06 |
| `ActivityEvidences` | Minh chứng file (`Image`, `Document`) hoặc `ConfirmationCode`, trạng thái duyệt `Pending`/`Accepted`/`Rejected` | US-08, US-21 |

Dịch vụ dùng chung (`Services/Storage`, `Services/Settings`):

- `IEvidenceFileStorage`: lưu file ngoài `wwwroot` theo `EvidenceStorage:RootPath` (mặc định `App_Data/evidence`, đã gitignore), tên GUID do server sinh.
- `IEvidenceFileValidator`: chỉ nhận `.jpg/.jpeg/.png` (ảnh) và `.pdf` (tài liệu), tối đa 5 MB, kiểm tra chữ ký nhị phân.
- `ICheckInConfigProvider`: cấu hình check-in đang có hiệu lực (dòng đã lưu hoặc mặc định trong `SettingDefaults`).

Giới hạn khác cấu hình ở section `EvidenceStorage` của `appsettings.json`: 5 minh chứng cho mỗi đăng ký, hạn nộp 7 ngày sau buổi cuối.

### Check-in bằng mã (US-07)

| Endpoint | Quyền | Mô tả |
|---|---|---|
| `GET /api/v1/admin/activities/{id}/check-in-codes` | HSSV, SystemAdmin | Mã, cửa sổ check-in và số lượt đã check-in của từng buổi |
| `POST /api/v1/admin/activities/{id}/sessions/{sessionId}/check-in-code` | HSSV, SystemAdmin | Phát hoặc đổi mã 6 ký tự (không có 0/O/1/I/L). Mã cũ hết hiệu lực ngay |
| `GET /api/v1/student-activities/{id}/check-in-status` | Student, Monitor | Phương thức, cửa sổ và thời điểm đã check-in của từng buổi; `canCheckIn` cho biết có thể check-in ngay lúc này |
| `POST /api/v1/student-activities/{id}/sessions/{sessionId}/check-in` | Student, Monitor | Body `{ "code": "..." }`, không phân biệt hoa thường |

Quy tắc check-in:

- Sinh viên phải có đăng ký chưa hủy.
- Hoạt động ở trạng thái `Published` hoặc `Ongoing`, dùng phương thức `Code`.
- Thời điểm check-in nằm trong cửa sổ của buổi (mặc định từ 15 phút trước giờ bắt đầu đến 30 phút sau giờ kết thúc).
- Mỗi buổi chỉ check-in một lần; unique index chặn cả request gửi đồng thời.
- Lần check-in đầu tiên chuyển đăng ký sang `Attended`. Từ đó không hủy đăng ký được nữa.

Mã lỗi:

| Status | Trường hợp |
|---|---|
| `400 validation_failed` | Sai mã |
| `409 business_rule_violation` | Ngoài cửa sổ; chưa phát mã; hoạt động điểm danh thủ công; đăng ký đã hủy; trạng thái hoạt động không cho phép |
| `409 resource_conflict` | Đã check-in buổi này |
| `404` | Không có hoạt động hoặc buổi, hoặc sinh viên chưa đăng ký |

Điểm danh thủ công (`Manual`) do Phòng QL HSSV thực hiện trên màn hình kiểm tra người tham gia (US-21).

### Nộp và quản lý minh chứng (US-08)

Endpoint sinh viên nằm dưới `/api/v1/student-activities/{activityId}/evidences`, policy `StudentOrMonitor`.

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/student-activities/{id}/evidences` | GET | Danh sách minh chứng đã nộp của sinh viên cho hoạt động, mới nhất trước |
| `/api/v1/student-activities/{id}/evidences` | POST | Nộp minh chứng multipart/form-data (`evidenceType`, `file`, `confirmationCode`, `sessionId`, `note`) |
| `/api/v1/student-activities/{id}/evidences/{evidenceId}/file` | GET | Tải file minh chứng của chính sinh viên (stream đúng Content-Type và tên file gốc) |
| `/api/v1/student-activities/{id}/evidences/{evidenceId}` | DELETE | Xóa minh chứng khi còn `Pending` và còn trong hạn nộp |

Quy tắc nghiệp vụ (S3-D6..S3-D9):
- **3 loại minh chứng:** `Image` (.jpg, .jpeg, .png), `Document` (.pdf), `ConfirmationCode` (chuỗi 4–100 ký tự).
- **Giới hạn dung lượng và số lượng:** File tối đa 5 MB, kiểm tra magic bytes qua `IEvidenceFileValidator`. Mỗi đăng ký tối đa 5 minh chứng.
- **Điều kiện nộp:** Sinh viên có đăng ký `Registered` hoặc `Attended`; hoạt động đang ở trạng thái `Ongoing` hoặc `Completed`; trong hạn nộp (hạn nộp = `max(session.EndTime)` + `SubmissionWindowDays` (mặc định 7 ngày)). Hoạt động `Published` (chưa diễn ra) hoặc `Cancelled` nhận `409 business_rule_violation`.
- **Xóa minh chứng:** Chỉ xóa được khi `ReviewStatus == Pending` và còn trong hạn nộp. Minh chứng đã duyệt (`Accepted`/`Rejected`) nhận `409`. Xóa là xóa cả bản ghi lẫn file trên ổ cứng.
- **Bảo mật:** File được lưu ngoài `wwwroot` qua `IEvidenceFileStorage` dưới tên GUID; response API tuyệt đối không để lộ `storedFileName` hoặc đường dẫn vật lý. Sinh viên chỉ xem, tải và xóa minh chứng của chính sinh viên; truy cập minh chứng người khác trả `404 resource_not_found`. Cán bộ gọi endpoint sinh viên nhận `403 access_denied`.

### Kiểm tra người tham gia và duyệt minh chứng (US-21)

Mọi endpoint nằm dưới `/api/v1/admin/activities/{id}`, chỉ dành cho HSSV và SystemAdmin.

| Endpoint | Mô tả |
|---|---|
| `GET participants?status=&search=&hasPendingEvidence=&pageIndex=&pageSize=` | Bộ đếm toàn hoạt động (giữ chỗ, đã tham gia, đã hủy, minh chứng chờ duyệt), số lượt check-in từng buổi, và một trang người đăng ký kèm check-in từng buổi và số minh chứng theo trạng thái duyệt |
| `GET participants/{registrationId}` | Một người đăng ký kèm toàn bộ minh chứng (có `fileUrl` khi là file) |
| `POST sessions/{sessionId}/check-ins` body `{ "registrationId" }` | Điểm danh thủ công (phương thức `Manual`, ghi người thực hiện). Dùng được cho cả hoạt động check-in bằng mã và ngoài cửa sổ check-in; lần đầu chuyển đăng ký sang `Attended` |
| `DELETE sessions/{sessionId}/check-ins/{registrationId}` | Hủy ghi nhận nhầm. Không còn buổi nào thì đăng ký trở về `Registered` |
| `PATCH evidences/{evidenceId}/review` body `{ "status", "note" }` | `Accepted`; `Rejected` (bắt buộc `note`); hoặc `Pending` để mở lại |
| `GET evidences/{evidenceId}/file` | Tải file minh chứng |

Ràng buộc:

- Chỉ điểm danh hoặc hủy điểm danh khi hoạt động `Published`, `Ongoing` hoặc `Completed`; trường hợp khác trả `409`.
- Đăng ký đã hủy không điểm danh và không duyệt minh chứng được (`409`).
- Mã đăng ký, buổi hoặc minh chứng không thuộc hoạt động trả `404`.
- Khi đăng ký đã có quyết định điểm (US-22), điểm danh và duyệt minh chứng của đăng ký đó trả `409`. Muốn sửa phải đi qua chuyển trạng thái bản ghi điểm (US-23).

### Kỳ đánh giá và sổ điểm (US-22)

Migration `Sprint4ScoreLedger` thêm 3 bảng:
- `EvaluationPeriods`: kỳ đánh giá; tên không trùng, thời gian không chồng lấn; có điểm mục tiêu và trạng thái `Open`/`Closed`.
- `PointRecords`: sổ điểm; mỗi đăng ký tối đa một quyết định.
- `PointRecordHistories`: lịch sử phê duyệt, chỉ thêm, không sửa.

Điểm của sinh viên trong kỳ = tổng `Points` của các bản ghi `Approved`. Không dùng `User.AccumulatedPoints` cho tính năng mới.

| Endpoint | Mô tả |
|---|---|
| `GET/POST /api/v1/admin/evaluation-periods`, `PUT .../{id}`, `PATCH .../{id}/status` | Quản lý kỳ. Kỳ đã có điểm chỉ sửa được tên và điểm mục tiêu |
| `GET /api/v1/admin/activities/{id}/scoring` | Bảng ghi điểm: kỳ chứa thời điểm kết thúc của hoạt động, `canApprove` kèm lý do bị chặn, và từng đăng ký với check-in, minh chứng, điều kiện, quyết định đã có |
| `POST /api/v1/admin/activities/{id}/scoring/approve` body `{ "registrationIds": [..] \| null, "note" }` | Ghi điểm `Approved` (bằng `TrainingPoints` của hoạt động) cho các đăng ký đã chọn, hoặc mọi đăng ký đủ điều kiện khi không gửi danh sách |
| `GET /api/v1/admin/point-records/{id}/history` | Lịch sử phê duyệt của một bản ghi điểm |

Quy tắc:
- **Đủ điều kiện:** đăng ký `Attended` (có check-in bằng mã hoặc điểm danh thủ công), hoặc có ít nhất một minh chứng `Accepted`; và chưa có quyết định điểm.
- **Điều kiện để ghi điểm:** hoạt động `Completed`, kỳ tìm được và đang `Open`; trường hợp khác trả `409`.
- **Mỗi đăng ký chỉ ghi một lần:** unique index kết hợp transaction serializable.

`PointRecordStatus` đã khai báo sẵn các chuyển trạng thái cho US-23 (Kiệt): `Approved → Revoked/Pending`, `Pending → Approved/Rejected`, `Rejected → Pending`, `Revoked` là trạng thái cuối. Mỗi lần chuyển phải kèm lý do và ghi một dòng `PointRecordHistories`.

### Báo cáo theo kỳ (US-24)

`GET /api/v1/admin/reports/overview | activities | at-risk-students`, chỉ HSSV và SystemAdmin.

Tham số lọc:

| Tham số | Ý nghĩa |
|---|---|
| `periodId` | Kỳ đánh giá. Bỏ trống thì lấy kỳ `Open` chứa ngày hôm nay; nếu không có thì lấy kỳ gần nhất |
| `campus`, `academicYear`, `classId` | Lọc sinh viên theo cơ sở, khóa, lớp |
| `groupBy` | Chỉ dùng cho `overview`: `class` (mặc định), `academicYear` hoặc `campus` |
| `pageIndex`, `pageSize` | Chỉ dùng cho `at-risk-students` |

Định nghĩa các chỉ số (mọi báo cáo dùng chung):

| Chỉ số | Cách tính |
|---|---|
| Sinh viên trong phạm vi | Tài khoản `Student`/`Monitor` đang hoạt động, khớp bộ lọc |
| Hoạt động của kỳ | Hoạt động có thời điểm kết thúc nằm trong kỳ, trạng thái `Published`/`Ongoing`/`Completed`. Hoạt động `Cancelled` đếm riêng; `Draft` không tính |
| Đăng ký | Đăng ký chưa hủy của sinh viên trong phạm vi vào các hoạt động của kỳ |
| Tham gia | Đăng ký ở trạng thái `Attended` (đã check-in hoặc được điểm danh thủ công) |
| Tỷ lệ tham gia | Tham gia / đăng ký × 100, làm tròn 1 chữ số thập phân; trả `null` khi không có đăng ký |
| Điểm | Tổng `Points` của các bản ghi `Approved` trong kỳ (US-22) |
| Nguy cơ thiếu điểm | Điểm trong kỳ nhỏ hơn `TargetPoints` của kỳ; danh sách xếp theo số điểm còn thiếu giảm dần |

Không tìm thấy kỳ (hoặc chưa có kỳ nào) trả `404`. `groupBy` không hợp lệ trả `400`.

### Không gian trao đổi của lớp (US-09)

`/api/v1/class-space`, chỉ `Student`/`Monitor`. Lớp lấy theo tài khoản đang đăng nhập (route không có mã lớp), nên không ai đọc được không gian của lớp khác.

| Endpoint | Mô tả |
|---|---|
| `GET threads?kind=&search=&pageIndex=&pageSize=` | Thông tin lớp, lớp trưởng và danh sách chủ đề: ghim trước, sau đó theo hoạt động gần nhất. `pageSize` tối đa 50 |
| `GET threads/{id}` | Chủ đề và các trả lời theo thứ tự thời gian, kèm `canDelete` cho người đang xem |
| `POST threads` | `{ kind, title, body }`; `kind` chỉ nhận `Question` hoặc `Discussion` (`Announcement` dành cho thông báo của lớp trưởng ở US-13). Trả `201` |
| `POST threads/{id}/replies` | `{ body }`; cập nhật thời điểm hoạt động của chủ đề. Trả `201` |
| `DELETE threads/{id}` | Chỉ người tạo, khi chưa có người khác trả lời |
| `DELETE threads/{id}/replies/{replyId}` | Chỉ người viết, trừ trả lời đã được chọn làm câu trả lời (`isAnswer`) |

Mã lỗi: sinh viên chưa được xếp lớp `409 business_rule_violation`; chủ đề của lớp khác hoặc không tồn tại `404`; không phải tác giả `403 access_denied`; chủ đề `Closed` không nhận trả lời `409`. Trạng thái `Answered`/`Closed`, ghim chủ đề và chọn câu trả lời đã có sẵn trong schema cho các chức năng của lớp trưởng (US-13/US-14).

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
- Web UI hiện nằm trong project `StudentActivityManagement.Web` và chạy ở `http://localhost:5220`; xem hướng dẫn chạy trong README của project Web. Mobile UI là client dự kiến, dùng chung API này qua HTTP/JSON.
