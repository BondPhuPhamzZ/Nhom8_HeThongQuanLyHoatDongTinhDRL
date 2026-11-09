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
- API tài khoản chỉ tạo tài khoản cán bộ (`HSSV`, `SystemAdmin`) và chỉ đổi vai trò giữa hai role này. Lớp trưởng (`Monitor`) chỉ được chỉ định qua `PUT /api/v1/admin/classes/{id}/monitor/{studentId}` và gỡ qua `DELETE /api/v1/admin/classes/{id}/monitor` (sinh viên trở về `Student`).
- Quản lý lớp (US-18): `PUT /api/v1/admin/classes/{id}` sửa mã, tên lớp, khoa (mã lớp không được trùng, `409`). `DELETE /api/v1/admin/classes/{id}` chỉ xóa lớp không còn sinh viên, không là đối tượng tham gia của hoạt động và chưa có dữ liệu trao đổi; nếu vi phạm trả `409 business_rule_violation`.
- Không thể tự đổi vai trò, tự khóa tài khoản của mình, hoặc làm hệ thống không còn `SystemAdmin` đang hoạt động.
- Khóa tài khoản hoặc đổi vai trò (kể cả chỉ định lớp trưởng mới) có hiệu lực ngay: token cũ nhận `401`, client phải đăng nhập lại. Tài khoản bị khóa đăng nhập nhận `403` với `errorCode = account_disabled`.

### Bảo vệ đăng nhập (US-39)

- Một tài khoản nhập sai mật khẩu 5 lần liên tiếp sẽ bị khóa đăng nhập tạm thời trong 15 phút. Lần sai thứ 5 vẫn nhận `401 invalid_credentials`; các lần thử tiếp theo trong thời gian khóa nhận `423 account_temporarily_locked`. Đăng nhập đúng sau khi hết hạn sẽ xóa bộ đếm sai và trạng thái khóa tạm.
- `POST /api/v1/admin/accounts/{id}/unlock` chỉ dành cho SystemAdmin để xóa trạng thái khóa tạm. Khóa tài khoản thủ công (`IsActive = false`) là quy tắc riêng và không bị endpoint này thay đổi.
- `POST /api/v1/auth/login` giới hạn mặc định 10 yêu cầu mỗi phút cho mỗi địa chỉ IP; vượt mức nhận `429 too_many_login_attempts`. Các ngưỡng nằm trong `LoginSecurity` của `appsettings.json` (`MaxFailedAttempts`, `LockoutMinutes`, `RequestsPerMinutePerIp`).
- **Web gọi API từ server của mình**, nên gửi kèm IP thật của trình duyệt trong `X-Forwarded-For`. Nhờ vậy giới hạn trên được tính theo từng người dùng Web, không dồn chung vào IP của server Web.
  - API chỉ tin header này khi request đến từ proxy đã biết: mặc định là loopback, thêm địa chỉ khác bằng `ForwardedHeaders:KnownProxies`.
  - Client gọi thẳng API không tự chọn được IP để né giới hạn.
  - Khi deploy Web và API trên hai máy khác nhau, thêm IP của máy Web vào `ForwardedHeaders:KnownProxies`.
- API và Web gửi `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`.

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
| `POST /api/v1/admin/point-records/{id}/transition` body `{ "status": "Pending", "reason": "..." }` | Chuyển theo state machine; bắt buộc ghi lý do, trả quyết định mới cùng `allowedNextStatuses` |

Quy tắc:
- **Đủ điều kiện:** đăng ký `Attended` (có check-in bằng mã hoặc điểm danh thủ công), hoặc có ít nhất một minh chứng `Accepted`; và chưa có quyết định điểm.
- **Điều kiện để ghi điểm:** hoạt động `Completed`, kỳ tìm được và đang `Open`; trường hợp khác trả `409`.
- **Mỗi đăng ký chỉ ghi một lần:** unique index kết hợp transaction serializable.

`PointRecordStatus` chuyển theo state machine: `Approved → Revoked/Pending`, `Pending → Approved/Rejected`, `Rejected → Pending`, `Revoked` là trạng thái cuối. Mỗi lần chuyển phải kèm lý do, cập nhật điểm tương thích trên đăng ký (chỉ `Approved` còn điểm), và thêm một dòng `PointRecordHistories`.

### Mức điểm và hoạt động phù hợp của sinh viên (US-11)

`GET /api/v1/student/reports/points-and-recommendations?periodId=` trả điểm đã duyệt, mục tiêu, điểm còn thiếu và hoạt động đề xuất. Bỏ `periodId` để lấy kỳ hiện tại/gần nhất. Gợi ý chỉ xuất hiện khi còn thiếu điểm trong kỳ đang `Open`; mỗi hoạt động phải thuộc thời gian kỳ, còn đăng ký mở và chỗ trống, có điểm, đạt eligibility chung và chưa được sinh viên đăng ký/điểm danh.

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

### Lịch sử đăng ký và điểm cá nhân (US-10)

Student/Monitor xem các kỳ và lịch sử của chính tài khoản đang đăng nhập:

| Endpoint | Nội dung |
|---|---|
| `GET /api/v1/student/reports/evaluation-periods` | Kỳ đánh giá theo ngày bắt đầu giảm dần, kèm tổng điểm `Approved` của sinh viên |
| `GET /api/v1/student/reports/activity-history?periodId=&registrationStatus=` | Đăng ký có buổi giao nhau với kỳ, trạng thái, từng buổi/điểm danh và quyết định điểm |

Bỏ `periodId` để chọn kỳ hiện tại; nếu không có kỳ hiện tại thì dùng kỳ gần nhất. `registrationStatus` nhận `Registered`, `Attended`, `Absent` hoặc `Cancelled`. Điểm cộng và tổng kỳ chỉ tính bản ghi `Approved`; mỗi truy vấn luôn lấy student ID từ JWT, không nhận ID người dùng từ client.

### Không gian trao đổi của lớp (US-09)

`/api/v1/class-space`, chỉ `Student`/`Monitor`. Lớp lấy theo tài khoản đang đăng nhập (route không có mã lớp), nên không ai đọc được không gian của lớp khác.

| Endpoint | Mô tả |
|---|---|
| `GET threads?kind=&search=&pageIndex=&pageSize=` | Thông tin lớp, lớp trưởng và danh sách chủ đề: ghim trước, sau đó theo hoạt động gần nhất. `pageSize` tối đa 50 |
| `GET threads/{id}` | Chủ đề và các trả lời theo thứ tự thời gian, kèm `canDelete` cho người đang xem |
| `POST threads` | `{ kind, title, body }`; `Question`/`Discussion` dành cho thành viên, `Announcement` chỉ lớp trưởng hiện tại mới được đăng và tự ghim lên đầu. Trả `201` |
| `PATCH threads/{id}/pin` | `{ isPinned }`; chỉ lớp trưởng hiện tại được ghim/bỏ ghim chủ đề của lớp mình |
| `POST threads/{id}/replies` | `{ body }`; cập nhật thời điểm hoạt động của chủ đề. Trả `201` |
| `POST threads/{id}/replies/{replyId}/answer` | Chỉ lớp trưởng của lớp chọn một trả lời làm đáp án; chủ đề chuyển `Answered`, khi đổi đáp án thì đáp án cũ được bỏ chọn |
| `DELETE threads/{id}` | Chỉ người tạo, khi chưa có người khác trả lời |
| `DELETE threads/{id}/replies/{replyId}` | Chỉ người viết, trừ trả lời đã được chọn làm câu trả lời (`isAnswer`) |

Mã lỗi: sinh viên chưa được xếp lớp `409 business_rule_violation`; chủ đề của lớp khác hoặc không tồn tại `404`; không phải tác giả/lớp trưởng hiện tại `403 access_denied`; chủ đề `Closed` không nhận trả lời `409`. Lớp trưởng có thể theo dõi câu hỏi theo `kind=Question`, trả lời, chọn một trả lời làm đáp án chính thức và cập nhật trạng thái câu hỏi `Answered` (US-14).

### Lớp trưởng chuyển câu hỏi lên Phòng QL HSSV (US-15)

Cùng prefix `/api/v1/class-space`. Người được thao tác là lớp trưởng hiện tại của lớp (`Classes.MonitorStudentId`), không chỉ dựa vào role.

| Endpoint | Mô tả |
|---|---|
| `POST threads/{id}/escalations` | `{ question }` (5–2000 ký tự): nội dung cần phòng xác nhận. Chỉ áp dụng cho chủ đề `Question`/`Discussion`. Trả `201` |
| `GET escalations?status=&pageIndex=&pageSize=` | Các câu hỏi lớp đã chuyển, mới nhất trước, kèm `answer` khi phòng đã trả lời |
| `POST escalations/{id}/withdraw` | Rút lại câu hỏi đang `Pending` |
| `POST escalations/{id}/relay` | Khi đã `Answered`, lớp trưởng đăng phản hồi chính thức thành trả lời được chọn trong thread của lớp; chỉ chuyển một lần |

Trạng thái (`EscalationStatus`): `Pending → Answered` (Phòng QL HSSV trả lời, US-20) hoặc `Pending → Withdrawn` (lớp trưởng rút lại). `Answered`, `Withdrawn` là trạng thái cuối.

- **Chống trùng:** mỗi chủ đề chỉ có một câu hỏi `Pending`, nhờ unique index có điều kiện. Chuyển lần nữa trả `409`. Sau khi rút lại hoặc đã được trả lời thì chuyển lại được.
- **Khóa chủ đề:** chủ đề đã từng được chuyển lên thì tác giả không xóa được nữa (`409`), để Phòng QL HSSV không mất câu hỏi.
- **Thông tin cho cả lớp:** danh sách và chi tiết chủ đề trả `escalationStatus` (trạng thái lần chuyển gần nhất). Chi tiết trả thêm `canEscalate` cho lớp trưởng. Phản hồi chính thức chỉ hiện trong `GET escalations` của lớp trưởng cho tới khi được chuyển lại (US-16); khi relay, nó xuất hiện trong thread dưới tên lớp trưởng và được đánh dấu là trả lời chính thức.
- **Mã lỗi:** không phải lớp trưởng của lớp `403 access_denied`; chủ đề hoặc câu hỏi của lớp khác `404`; rút lại câu hỏi không còn `Pending` `409`.

### US-20 — Phòng QL HSSV tiếp nhận và trả lời

Prefix `/api/v1/admin/class-escalations`, quyền `BusinessAdmin` (HSSV hoặc SystemAdmin):

| Endpoint | Mô tả |
|---|---|
| `GET ?status=&pageIndex=&pageSize=` | Hộp thư câu hỏi, mới nhất trước; lọc `Pending`, `Answered`, `Withdrawn` |
| `GET {id}` | Xem một câu hỏi cùng trạng thái và phản hồi |
| `POST {id}/answer` | `{ "answer": "..." }` (5–4000 ký tự sau khi trim), chỉ khi `Pending` |

Phản hồi ghi người trả lời và thời gian UTC, hiện lại trong danh sách của lớp trưởng. Trả lời lần hai hoặc trả lời câu hỏi đã rút lại trả `409`; câu hỏi không tồn tại trả `404`. Cập nhật có điều kiện bảo đảm thao tác trả lời và rút lại đồng thời chỉ một bên thành công. Chuyển phản hồi tới cả lớp thuộc US-16.

### Cấu hình check-in và lịch nhắc (US-26)

Phòng QL HSSV và SystemAdmin quản lý cấu hình theo hoạt động:

| Endpoint | Nội dung |
|---|---|
| `GET /api/v1/admin/activities/{id}/check-in-config` | Phương thức `Code`/`Manual`, cửa sổ mở/đóng; chưa lưu thì mặc định `Code`, 15/30 phút |
| `PUT /api/v1/admin/activities/{id}/check-in-config` | `{ method, openBeforeMinutes, closeAfterMinutes }`, lưu người cập nhật từ JWT và thời điểm UTC |
| `GET /api/v1/admin/activities/{id}/reminder-schedule` | Các mốc trước từng buổi; mặc định `[1440]` phút |
| `PUT /api/v1/admin/activities/{id}/reminder-schedule` | `{ offsetsMinutes: [1440, 60] }`; thay toàn bộ, lưu tăng dần |

Cửa sổ check-in từ 0–240 phút; lịch nhắc có 1–3 mốc khác nhau từ 10–10080 phút. Chỉ sửa khi hoạt động ở `Draft`, `Published` hoặc `Ongoing`; không đổi phương thức sau khi đã có check-in. US-26 chỉ lưu cấu hình; gửi thông báo thuộc US-06.

Sprint 2 đã chuẩn hóa hoạt động nhiều buổi. Mỗi hoạt động có `organizerName`, danh sách `sessions` và `audienceRules`. Sinh viên chỉ thấy và đăng ký hoạt động phù hợp cơ sở, khóa hoặc lớp của mình.

### US-06 — thông báo nhắc lịch trong ứng dụng

Worker kiểm tra mỗi phút và khi API khởi động. Mỗi buổi dùng các mốc trong `ActivityReminderOffsets`; nếu chưa cấu hình thì nhắc trước 1 ngày. Khi đến mốc và buổi chưa bắt đầu, hệ thống tạo thông báo cho sinh viên đang đăng ký (`Registered`/`Attended`) hoặc khớp quy tắc đối tượng `Required`; tài khoản khóa và đăng ký đã hủy không được nhận trừ khi thuộc diện bắt buộc. Nội dung gồm giờ Việt Nam, địa điểm, cơ sở và yêu cầu cần chuẩn bị. Khóa `SourceKey` duy nhất bảo đảm không gửi lặp khi worker chạy lại hoặc nhiều instance cùng chạy.

Sinh viên và lớp trưởng dùng `GET /api/v1/notifications?unreadOnly=&pageIndex=&pageSize=` và `POST /api/v1/notifications/{id}/read`; chỉ xem hoặc đánh dấu thông báo của chính mình. Khi hoạt động được công bố, sinh viên thuộc đối tượng nhận thêm thông báo `StudentActivity` trực tiếp; thông báo này khác với lời nhắc trước từng buổi. Đây là thông báo trong ứng dụng, không gửi email/SMS/push. Mốc nhắc tùy chỉnh do US-26 quản lý; thông báo cho lớp trưởng và thông tin chuyển vào lớp thuộc US-12/US-13.

### US-12 — thông báo hoạt động mới cho lớp trưởng

Khi hoạt động được tạo ở trạng thái `Published` hoặc chuyển từ trạng thái khác sang `Published`, hệ thống tạo một thông báo `ClassActivity` cho lớp trưởng đang hoạt động có lớp khớp ít nhất một quy tắc đối tượng của hoạt động. Không khai báo quy tắc thì thông báo tới mọi lớp trưởng đang hoạt động. `SourceKey` theo hoạt động/người nhận ngăn gửi trùng. Nếu đối tượng được sửa khi hoạt động đã công bố, người mới phù hợp cũng nhận thông báo. Lớp trưởng dùng chung API danh sách/lọc chưa đọc và đánh dấu đã đọc ở trên; sinh viên khác nhận thông báo riêng `StudentActivity`, không nhận `ClassActivity`.

## Trợ lý điểm rèn luyện (US-31, Gemini API)

Phần mở rộng AI. Sinh viên và lớp trưởng hỏi bằng tiếng Việt: "kỳ này mình còn thiếu bao nhiêu điểm?", "có hoạt động nào đăng ký được để bù điểm không?", "khi nào check-in được?". Trợ lý trả lời từ dữ liệu thật của chính người hỏi.

| Endpoint | Quyền | Ghi chú |
|---|---|---|
| `GET /api/v1/assistant/status` | StudentOrMonitor | `{ enabled, model, requestsPerMinute }`. `enabled = false` khi chưa có key hoặc `Assistant:Enabled = false`. |
| `POST /api/v1/assistant/chat` | StudentOrMonitor | Body `{ messages: [{ role: "user" \| "assistant", content }] }`, 1–12 tin nhắn, tin cuối là câu hỏi, mỗi tin ≤ 1000 ký tự. Trả `{ reply, activities[], toolsUsed[], droppedActivityReferences, model }`. |

Cách hoạt động:

- **Gọi Gemini:** `Services/Assistant/GeminiClient` gọi REST `models/{model}:generateContent` của Gemini, header `x-goog-api-key`. Toàn bộ code gọi Google nằm trong lớp này.
- **Tool:** model chỉ được gọi 5 tool **chỉ đọc** trong `AssistantTools`. Mỗi tool dùng lại service có sẵn:
  - `get_points_summary` (US-11);
  - `list_evaluation_periods` và `get_my_activity_history` (US-10);
  - `search_activities` (US-02);
  - `get_activity_detail` (US-03, US-07).
- **Phạm vi dữ liệu:** tool lấy sinh viên từ JWT và không nhận mã sinh viên, nên trợ lý không xem được dữ liệu người khác. Trợ lý cũng không đăng ký, hủy hay check-in thay sinh viên.
- **Chống bịa hoạt động:** model ghi mã hoạt động dạng `[#id]`. API chỉ biến mã đó thành thẻ hoạt động khi một tool của **cùng câu hỏi** đã trả về hoạt động đó. Mã khác bị xóa khỏi câu trả lời và được đếm vào `droppedActivityReferences`.
- **Thought signature:** phần `content` của model được gửi lại nguyên vẹn ở lượt tool kế tiếp, đúng yêu cầu của Gemini 3. Sau `Assistant:MaxToolRounds` lượt tool, API gửi `functionCallingConfig.mode = NONE` để buộc model trả lời bằng chữ.
- **Kiến thức nền:** quy tắc của hệ thống nằm trong `Services/Assistant/AssistantKnowledge.md` (embedded resource). Quy chế chính thức của trường để trống, chờ nhóm bổ sung sau khi đối chiếu văn bản gốc.

Cấu hình (`appsettings.json`, **không chứa key**):

| Khóa | Mặc định | Ý nghĩa |
|---|---|---|
| `Gemini:ApiKey` | (trống) | Đặt bằng user-secrets hoặc biến môi trường `Gemini__ApiKey` |
| `Gemini:Model` | `gemini-3.5-flash-lite` | Model miễn phí. Có thể đổi sang `gemini-3.8-flash` hoặc `gemini-3.6-flash`. |
| `Gemini:TimeoutSeconds` | `30` | Thời gian chờ mỗi lần gọi Gemini |
| `Assistant:Enabled` | `true` | Tắt trợ lý mà không cần xóa key |
| `Assistant:MaxToolRounds` | `4` | Số lượt gọi tool tối đa cho một câu hỏi |
| `Assistant:RequestsPerMinute` | `6` | Số câu hỏi tối đa mỗi sinh viên mỗi phút. Vượt thì trả `429 assistant_rate_limited`. |

Đặt key (tạo key miễn phí tại https://aistudio.google.com/apikey). Chạy trong thư mục project API:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<key>"
dotnet user-secrets list
```

User-secrets lưu key trong `%APPDATA%\Microsoft\UserSecrets\StudentActivityManagement.API-2026-Nhom8\secrets.json` của máy bạn. File này nằm ngoài repo, chỉ được nạp khi chạy ở môi trường Development, và là văn bản thường (không mã hóa).

Lỗi và quyền riêng tư:

- **Lỗi:**
  - chưa có key, key sai, hoặc Google lỗi hay quá thời gian chờ → `503 assistant_unavailable`;
  - hết quota miễn phí của Gemini → `429 assistant_rate_limited`;
  - các chức năng khác không bị ảnh hưởng.
- **Quyền riêng tư:** ở bản miễn phí, Google có thể dùng nội dung gửi lên để cải thiện sản phẩm. Vì vậy tool không bao giờ gửi họ tên, MSSV, email hoặc lớp; integration test kiểm tra điều này. Web và Mobile nhắc người dùng không nhập thông tin cá nhân. Log chỉ ghi số token và thời gian, không ghi nội dung câu hỏi.
- **Kiểm thử:** `AssistantTests` thay Gemini bằng một HTTP handler trả JSON soạn sẵn, nên CI không cần key. `CustomWebApplicationFactory` xóa `Gemini:ApiKey` để test không bao giờ dùng key thật của máy.
- **Đánh giá với Gemini thật:** chạy `scripts/assistant-eval.ps1` khi API đang chạy và đã có key. Script lấy đáp án đúng từ chính API, hỏi một bộ câu hỏi cố định, rồi in tỉ lệ đạt, số mã hoạt động bị bỏ và thời gian trả lời.

### Gia cố bảo mật trợ lý (US-32)

| Lớp bảo vệ | Cách làm |
|---|---|
| Chống giả lịch sử | Mỗi câu trả lời có `replySignature` (HMAC-SHA256, gắn với sinh viên). Tin `assistant` gửi lại phải có đúng chữ ký, nếu không API trả 400 trước khi gọi Gemini. Tin tự viết, sửa nội dung hoặc lấy từ sinh viên khác đều bị chặn. Khóa lấy từ `Assistant:HistorySigningKey`, nếu không có thì dẫn xuất từ JWT secret. |
| Ẩn dữ liệu cá nhân | `PersonalDataRedactor` thay họ tên, MSSV, email, số điện thoại của chính người hỏi và các mẫu MSSV, email, SĐT, CCCD bằng `[đã ẩn]` trước khi gửi Gemini. Câu trả lời có cờ `personalDataRedacted`. |
| Thiết lập an toàn của Gemini | Mọi lần gọi đều gửi `safetySettings` cho 4 nhóm (quấy rối, thù ghét, tình dục, nguy hiểm), ngưỡng `Gemini:SafetyThreshold`, và `generationConfig.maxOutputTokens` (`Gemini:MaxOutputTokens`). Câu trả lời bị cắt được ghi chú rõ. |
| Giới hạn dung lượng | `POST /assistant/chat` từ chối body lớn hơn 64 KB với `413 payload_too_large`, trước khi đọc body. |
| Hạn mức theo ngày | Bảng `AssistantDailyUsages` đếm số câu hỏi mỗi sinh viên mỗi ngày (giờ Việt Nam), mặc định `Assistant:DailyQuestionLimit = 40`; vượt thì trả 429. Lần gọi Gemini thất bại được hoàn lại lượt. `status` và mỗi câu trả lời có `remainingToday`. |
| Prompt | Cố định xưng hô; cấm tiết lộ quy tắc, viết mã, HTML hay link. |
| Kiểm tra | `scripts/assistant-eval.ps1 -Suite Security` chạy 10 kiểu tấn công với Gemini thật. |

### AI cho Phòng QL HSSV (US-33, US-34)

| Endpoint | Quyền | Nội dung |
|---|---|---|
| `POST /api/v1/admin/reports/ai-insights?periodId=&campus=&academicYear=&classId=&groupBy=` | BusinessAdmin | Nhận xét báo cáo kỳ: `highlights`, `concerns`, `suggestions`. |
| `POST /api/v1/admin/class-escalations/{id}/ai-draft` | BusinessAdmin | Bản nháp phản hồi cho câu hỏi lớp đang chờ: `draft`, `confidence` (high/medium/low), `missingInformation`. Không lưu, không gửi; cán bộ sửa rồi tự gửi qua `.../answer`. |

- **JSON Schema:** cả hai dùng structured output của Gemini (`responseMimeType: application/json` kèm `responseSchema`). Server vẫn kiểm tra lại kiểu dữ liệu, độ dài và số lượng ý.
- **Kiểm tra số liệu:** mỗi ý trong nhận xét báo cáo chỉ được dùng các số có trong dữ liệu báo cáo (cho phép làm tròn); ý có số lạ bị loại và đếm vào `removedStatements`. Dữ liệu gửi lên chỉ là số tổng hợp theo nhóm và hoạt động, không có tên hay MSSV.
- **Chống prompt injection:** câu hỏi của sinh viên được ẩn dữ liệu cá nhân và đặt trong `<chu_de>` / `<cau_hoi>`. Dấu `<` `>` trong nội dung bị thay, để sinh viên không tự đóng thẻ được.
- **Dùng chung với trợ lý:** cùng `GeminiClient`, cùng giới hạn tần suất `assistant` (theo người dùng), cùng mã lỗi `assistant_unavailable` và `assistant_rate_limited`.

## Cấu trúc project

- `Controllers`, `Services`, `DTOs`, `Models`, `Data`: các lớp chính của RESTful API.
- [`StudentActivityManagement.API.http`](StudentActivityManagement.API.http): request mẫu dùng trong Visual Studio, Rider hoặc VS Code REST Client.
- `scripts/setup-local.ps1`: cấu hình JWT secret an toàn cho máy local.
- `scripts/update-database.ps1`: restore EF tool và áp dụng migration đã commit.
- `scripts/assistant-eval.ps1`: đánh giá trợ lý điểm rèn luyện với Gemini thật (US-31).
- `StudentActivityManagement.API.Tests`: integration test cho JWT, phân quyền, OpenAPI, lịch nhiều buổi, đối tượng tham gia, hủy đăng ký và quản lý tài khoản.
- Hai client cùng dùng API này: `StudentActivityManagement.Web` (ASP.NET Core MVC, bốn vai trò) và `StudentActivityManagement.Mobile` (Flutter, sinh viên và lớp trưởng, US-30). Xem README của từng project.

Backlog, Sprint, báo cáo, kịch bản demo và hướng dẫn thành viên được quản lý ngoài repository để Git chỉ chứa phần mềm cần build/chạy.

## Kiểm tra trước khi push hoặc báo cáo

```powershell
dotnet restore ..\Nhom8_ActivitiesManagement.sln
dotnet build ..\Nhom8_ActivitiesManagement.sln --no-restore -m:1
dotnet test ..\Nhom8_ActivitiesManagement.sln --no-build --no-restore -m:1
```

## Lưu ý

- Không commit JWT secret, Gemini API key, database `.db`, thư mục `bin`, `obj` hoặc file `.user`.
- Script `scripts/setup-local.ps1` sinh khóa ngẫu nhiên và lưu bằng .NET user-secrets ngoài repository; script không in giá trị secret.
- Project dùng SQLite và EF Core migrations; không sửa trực tiếp database rồi đưa file `.db` lên Git.
- Swagger chỉ bật trong Development.
- Web UI hiện nằm trong project `StudentActivityManagement.Web` và chạy ở `http://localhost:5220`; xem hướng dẫn chạy trong README của project Web. Mobile UI là client dự kiến, dùng chung API này qua HTTP/JSON.
