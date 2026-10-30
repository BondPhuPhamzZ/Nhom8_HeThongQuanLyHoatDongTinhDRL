using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudentActivityManagement.Web.Api;

/// <summary>Result of one API call: either Data, or the ProblemDetails message/errors to show the user.</summary>
public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public HttpStatusCode StatusCode { get; init; }
    public string? ErrorCode { get; init; }
    public Dictionary<string, string[]> Errors { get; init; } = [];
}

/// <summary>Thrown when the API rejects the stored token (expired, account locked, role changed).</summary>
public sealed class ApiUnauthorizedException : Exception
{
    public ApiUnauthorizedException(string message) : base(message) { }
}

/// <summary>
/// Typed HttpClient for the shared RESTful API. The bearer token of the signed-in user is attached by
/// <see cref="BearerTokenHandler"/>; every response is read through one place so errors look the same everywhere.
/// </summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    // ---- Auth
    public Task<ApiResult<AuthResponse>> LoginAsync(string usernameOrEmail, string password) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/login", new { usernameOrEmail, password }, throwOn401: false);

    // ---- Student
    /// <param name="registered">True lists the student's own registered activities, ended ones included.</param>
    public Task<ApiResult<PagedResult<ActivityDto>>> GetStudentActivitiesAsync(string? search, string? campus, int page, int pageSize = 9, bool registered = false) =>
        SendAsync<PagedResult<ActivityDto>>(HttpMethod.Get, $"api/v1/student-activities?{Query(("search", search), ("campus", campus), ("pageIndex", page.ToString()), ("pageSize", pageSize.ToString()), ("registered", registered ? "true" : null))}");

    public Task<ApiResult<ActivityDto>> GetStudentActivityAsync(int id) =>
        SendAsync<ActivityDto>(HttpMethod.Get, $"api/v1/student-activities/{id}");

    public Task<ApiResult<bool>> RegisterAsync(int id) =>
        SendAsync<bool>(HttpMethod.Post, $"api/v1/student-activities/{id}/register");

    public Task<ApiResult<bool>> CancelRegistrationAsync(int id) =>
        SendAsync<bool>(HttpMethod.Delete, $"api/v1/student-activities/{id}/registration");

    public Task<ApiResult<StudentCheckInStatus>> GetCheckInStatusAsync(int activityId) =>
        SendAsync<StudentCheckInStatus>(HttpMethod.Get, $"api/v1/student-activities/{activityId}/check-in-status");

    public Task<ApiResult<CheckInResult>> CheckInAsync(int activityId, int sessionId, string code) =>
        SendAsync<CheckInResult>(HttpMethod.Post, $"api/v1/student-activities/{activityId}/sessions/{sessionId}/check-in", new { code });

    // ---- Student Evidences (US-08)
    public Task<ApiResult<List<StudentEvidence>>> GetStudentEvidencesAsync(int activityId) =>
        SendAsync<List<StudentEvidence>>(HttpMethod.Get, $"api/v1/student-activities/{activityId}/evidences");

    public async Task<ApiResult<StudentEvidence>> UploadEvidenceAsync(
        int activityId, string evidenceType, Microsoft.AspNetCore.Http.IFormFile? file, string? confirmationCode, int? sessionId, string? note)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/student-activities/{activityId}/evidences");
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(evidenceType), "evidenceType");

        if (file != null && file.Length > 0)
        {
            var streamContent = new StreamContent(file.OpenReadStream());
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            }
            content.Add(streamContent, "file", file.FileName);
        }

        if (!string.IsNullOrWhiteSpace(confirmationCode))
        {
            content.Add(new StringContent(confirmationCode.Trim()), "confirmationCode");
        }

        if (sessionId.HasValue)
        {
            content.Add(new StringContent(sessionId.Value.ToString()), "sessionId");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            content.Add(new StringContent(note.Trim()), "note");
        }

        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return new ApiResult<StudentEvidence>
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "Không kết nối được API. Hãy kiểm tra StudentActivityManagement.API đang chạy."
            };
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new ApiUnauthorizedException("Phiên đăng nhập đã hết hạn hoặc tài khoản đã thay đổi quyền. Vui lòng đăng nhập lại.");
            }

            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                var root = document.RootElement;
                return new ApiResult<StudentEvidence>
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    Message = root.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty,
                    Data = root.TryGetProperty("data", out var data) ? data.Deserialize<StudentEvidence>(Json) : default
                };
            }

            return ReadProblem<StudentEvidence>(response.StatusCode, text);
        }
    }

    public async Task<ApiResult<ApiFile>> DownloadStudentEvidenceFileAsync(int activityId, int evidenceId)
    {
        HttpResponseMessage raw;
        try
        {
            raw = await _http.GetAsync($"api/v1/student-activities/{activityId}/evidences/{evidenceId}/file");
        }
        catch (HttpRequestException)
        {
            return new ApiResult<ApiFile> { StatusCode = HttpStatusCode.ServiceUnavailable, Message = "Không kết nối được API." };
        }

        using var response = raw;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiUnauthorizedException("Phiên đăng nhập đã hết hạn hoặc tài khoản đã thay đổi quyền. Vui lòng đăng nhập lại.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ReadProblem<ApiFile>(response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        return new ApiResult<ApiFile>
        {
            Success = true,
            StatusCode = response.StatusCode,
            Data = new ApiFile(
                await response.Content.ReadAsByteArrayAsync(),
                response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"minh-chung-{evidenceId}")
        };
    }

    public Task<ApiResult<object>> DeleteStudentEvidenceAsync(int activityId, int evidenceId) =>
        SendAsync<object>(HttpMethod.Delete, $"api/v1/student-activities/{activityId}/evidences/{evidenceId}");

    // ---- Activity management (HSSV, SystemAdmin)
    public Task<ApiResult<PagedResult<ActivityDto>>> GetActivitiesAsync(string? search, string? status, int page, int pageSize = 10) =>
        SendAsync<PagedResult<ActivityDto>>(HttpMethod.Get, $"api/v1/admin/activities?{Query(("search", search), ("status", status), ("pageIndex", page.ToString()), ("pageSize", pageSize.ToString()))}");

    public Task<ApiResult<ActivityDto>> GetActivityAsync(int id) =>
        SendAsync<ActivityDto>(HttpMethod.Get, $"api/v1/admin/activities/{id}");

    public Task<ApiResult<ActivityDto>> CreateActivityAsync(ActivityWriteRequest request) =>
        SendAsync<ActivityDto>(HttpMethod.Post, "api/v1/admin/activities", request);

    public Task<ApiResult<ActivityDto>> UpdateActivityAsync(int id, ActivityWriteRequest request) =>
        SendAsync<ActivityDto>(HttpMethod.Put, $"api/v1/admin/activities/{id}", request);

    public Task<ApiResult<ActivityDto>> UpdateActivityStatusAsync(int id, string status) =>
        SendAsync<ActivityDto>(HttpMethod.Patch, $"api/v1/admin/activities/{id}/status", new { status });

    public Task<ApiResult<bool>> DeleteActivityAsync(int id) =>
        SendAsync<bool>(HttpMethod.Delete, $"api/v1/admin/activities/{id}");

    public Task<ApiResult<ActivityCheckInCodes>> GetCheckInCodesAsync(int activityId) =>
        SendAsync<ActivityCheckInCodes>(HttpMethod.Get, $"api/v1/admin/activities/{activityId}/check-in-codes");

    public Task<ApiResult<SessionCheckInCode>> GenerateCheckInCodeAsync(int activityId, int sessionId) =>
        SendAsync<SessionCheckInCode>(HttpMethod.Post, $"api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-in-code");

    public Task<ApiResult<ActivityParticipants>> GetParticipantsAsync(int activityId, string? status, string? search, bool pendingOnly, int page, int pageSize = 20) =>
        SendAsync<ActivityParticipants>(HttpMethod.Get, $"api/v1/admin/activities/{activityId}/participants?{Query(
            ("status", status), ("search", search), ("hasPendingEvidence", pendingOnly ? "true" : null),
            ("pageIndex", page.ToString()), ("pageSize", pageSize.ToString()))}");

    public Task<ApiResult<ParticipantDetail>> GetParticipantAsync(int activityId, int registrationId) =>
        SendAsync<ParticipantDetail>(HttpMethod.Get, $"api/v1/admin/activities/{activityId}/participants/{registrationId}");

    public Task<ApiResult<ParticipantDetail>> RecordCheckInAsync(int activityId, int sessionId, int registrationId) =>
        SendAsync<ParticipantDetail>(HttpMethod.Post, $"api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-ins", new { registrationId });

    public Task<ApiResult<ParticipantDetail>> RemoveCheckInAsync(int activityId, int sessionId, int registrationId) =>
        SendAsync<ParticipantDetail>(HttpMethod.Delete, $"api/v1/admin/activities/{activityId}/sessions/{sessionId}/check-ins/{registrationId}");

    public Task<ApiResult<StaffEvidence>> ReviewEvidenceAsync(int activityId, int evidenceId, string status, string? note) =>
        SendAsync<StaffEvidence>(HttpMethod.Patch, $"api/v1/admin/activities/{activityId}/evidences/{evidenceId}/review", new { status, note });

    /// <summary>Downloads an evidence file; files are capped at 5 MB by the API, so buffering is fine.</summary>
    public async Task<ApiResult<ApiFile>> GetEvidenceFileAsync(int activityId, int evidenceId)
    {
        HttpResponseMessage raw;
        try
        {
            raw = await _http.GetAsync($"api/v1/admin/activities/{activityId}/evidences/{evidenceId}/file");
        }
        catch (HttpRequestException)
        {
            return new ApiResult<ApiFile> { StatusCode = HttpStatusCode.ServiceUnavailable, Message = "Không kết nối được API." };
        }

        using var response = raw;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiUnauthorizedException("Phiên đăng nhập đã hết hạn hoặc tài khoản đã thay đổi quyền. Vui lòng đăng nhập lại.");
        }
        if (!response.IsSuccessStatusCode)
        {
            return ReadProblem<ApiFile>(response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        return new ApiResult<ApiFile>
        {
            Success = true,
            StatusCode = response.StatusCode,
            Data = new ApiFile(
                await response.Content.ReadAsByteArrayAsync(),
                response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"minh-chung-{evidenceId}")
        };
    }

    public Task<ApiResult<List<ClassDto>>> GetClassesAsync() =>
        SendAsync<List<ClassDto>>(HttpMethod.Get, "api/v1/admin/classes");

    // ---- Classes and class monitors (US-18)
    public Task<ApiResult<ClassDto>> CreateClassAsync(string classCode, string className, string department) =>
        SendAsync<ClassDto>(HttpMethod.Post, "api/v1/admin/classes", new { classCode, className, department });

    public Task<ApiResult<bool>> AssignMonitorAsync(int classId, int studentId) =>
        SendAsync<bool>(HttpMethod.Put, $"api/v1/admin/classes/{classId}/monitor/{studentId}");

    // ---- Students (US-17)
    public Task<ApiResult<PagedResult<StudentDto>>> GetStudentsAsync(string? search, string? campus, string? academicYear, int? classId, int page, int pageSize = 15) =>
        SendAsync<PagedResult<StudentDto>>(HttpMethod.Get, $"api/v1/admin/students?{Query(
            ("search", search), ("campus", campus), ("academicYear", academicYear), ("classId", classId?.ToString()),
            ("pageIndex", page.ToString()), ("pageSize", pageSize.ToString()))}");

    public Task<ApiResult<StudentDto>> GetStudentAsync(int id) =>
        SendAsync<StudentDto>(HttpMethod.Get, $"api/v1/admin/students/{id}");

    public Task<ApiResult<StudentDto>> CreateStudentAsync(StudentWriteRequest request) =>
        SendAsync<StudentDto>(HttpMethod.Post, "api/v1/admin/students", request);

    public Task<ApiResult<StudentDto>> UpdateStudentAsync(int id, StudentWriteRequest request) =>
        SendAsync<StudentDto>(HttpMethod.Put, $"api/v1/admin/students/{id}", request);

    public Task<ApiResult<bool>> DeleteStudentAsync(int id) =>
        SendAsync<bool>(HttpMethod.Delete, $"api/v1/admin/students/{id}");

    // ---- Accounts (US-25, SystemAdmin only)
    public Task<ApiResult<PagedResult<AccountDto>>> GetAccountsAsync(string? search, string? role, bool? isActive, int page, int pageSize = 15) =>
        SendAsync<PagedResult<AccountDto>>(HttpMethod.Get, $"api/v1/admin/accounts?{Query(
            ("search", search), ("role", role), ("isActive", isActive?.ToString().ToLowerInvariant()),
            ("pageIndex", page.ToString()), ("pageSize", pageSize.ToString()))}");

    public Task<ApiResult<AccountDto>> CreateAccountAsync(AccountCreateRequest request) =>
        SendAsync<AccountDto>(HttpMethod.Post, "api/v1/admin/accounts", request);

    public Task<ApiResult<AccountDto>> UpdateAccountRoleAsync(int id, string role) =>
        SendAsync<AccountDto>(HttpMethod.Patch, $"api/v1/admin/accounts/{id}/role", new { role });

    public Task<ApiResult<AccountDto>> UpdateAccountActiveAsync(int id, bool isActive) =>
        SendAsync<AccountDto>(HttpMethod.Patch, $"api/v1/admin/accounts/{id}/active-status", new { isActive });

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null, bool throwOn401 = true)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "Không kết nối được API. Hãy kiểm tra StudentActivityManagement.API đang chạy."
            };
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Unauthorized && throwOn401)
            {
                throw new ApiUnauthorizedException("Phiên đăng nhập đã hết hạn hoặc tài khoản đã thay đổi quyền. Vui lòng đăng nhập lại.");
            }

            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                var root = document.RootElement;
                return new ApiResult<T>
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    Message = root.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty,
                    Data = root.TryGetProperty("data", out var data) ? data.Deserialize<T>(Json) : default
                };
            }

            return ReadProblem<T>(response.StatusCode, text);
        }
    }

    private static ApiResult<T> ReadProblem<T>(HttpStatusCode status, string text)
    {
        var errors = new Dictionary<string, string[]>();
        string? message = null, errorCode = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            message = root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String ? detail.GetString()
                : root.TryGetProperty("title", out var title) ? title.GetString() : null;
            errorCode = root.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
            if (root.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in list.EnumerateObject())
                {
                    errors[item.Name] = item.Value.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body: fall back to a generic message below.
        }

        return new ApiResult<T>
        {
            StatusCode = status,
            ErrorCode = errorCode,
            Errors = errors,
            Message = message ?? status switch
            {
                HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                HttpStatusCode.NotFound => "Không tìm thấy dữ liệu.",
                _ => "Có lỗi xảy ra. Vui lòng thử lại."
            }
        };
    }

    private static string Query(params (string Key, string? Value)[] values) => string.Join("&", values
        .Where(item => !string.IsNullOrWhiteSpace(item.Value))
        .Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value!)}"));
}
