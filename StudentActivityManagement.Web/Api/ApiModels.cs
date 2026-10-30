namespace StudentActivityManagement.Web.Api;

// Client-side copies of the API contract (/api/v1). Times from the API are UTC.

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = new();
}

public sealed class UserInfo
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string? ClassName { get; set; }
    public bool IsClassMonitor { get; set; }
    public int AccumulatedPoints { get; set; }
}

public sealed class ActivityDto
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationOpenTime { get; set; }
    public DateTime RegistrationCloseTime { get; set; }
    public int MaxParticipants { get; set; }
    public int CurrentParticipantsCount { get; set; }
    public int TrainingPoints { get; set; }
    public string TargetAudience { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string>? AllowedNextStatuses { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SessionDto> Sessions { get; set; } = [];
    public List<AudienceRuleDto> AudienceRules { get; set; } = [];
    public EligibilityDto? Eligibility { get; set; }
    public string? MyRegistrationStatus { get; set; }
    public int RemainingSlots { get; set; }
    public bool IsRegistrationOpen { get; set; }
}

public sealed class SessionDto
{
    public int Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AudienceRuleDto
{
    public int Id { get; set; }
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
    public string? ClassCode { get; set; }
    public string ParticipationType { get; set; } = "Optional";
}

public sealed class EligibilityDto
{
    public bool IsEligible { get; set; }
    public string? ParticipationType { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ClassDto
{
    public int Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int? MonitorStudentId { get; set; }
    public string? MonitorStudentName { get; set; }
    public int TotalStudents { get; set; }
}

/// <summary>Request body for POST/PUT /admin/activities. Times carry the Vietnam offset.</summary>
public sealed class ActivityWriteRequest
{
    public string ActivityName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public DateTimeOffset RegistrationOpenTime { get; set; }
    public DateTimeOffset RegistrationCloseTime { get; set; }
    public int MaxParticipants { get; set; }
    public int TrainingPoints { get; set; }
    public string TargetAudience { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public List<SessionWriteRequest> Sessions { get; set; } = [];
    public List<AudienceRuleWriteRequest> AudienceRules { get; set; } = [];
}

public sealed class SessionWriteRequest
{
    public int? Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AudienceRuleWriteRequest
{
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
    public string ParticipationType { get; set; } = "Optional";
}

// ---- Check-in (US-07)

public sealed class StudentCheckInStatus
{
    public int ActivityId { get; set; }
    public string ActivityStatus { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public List<SessionCheckInStatus> Sessions { get; set; } = [];
}

public sealed class SessionCheckInStatus
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public bool CanCheckIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

public sealed class ActivityCheckInCodes
{
    public int ActivityId { get; set; }
    public string ActivityStatus { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int OpenBeforeMinutes { get; set; }
    public int CloseAfterMinutes { get; set; }
    public List<SessionCheckInCode> Sessions { get; set; } = [];
}

public sealed class SessionCheckInCode
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public string? Code { get; set; }
    public DateTime? CodeGeneratedAt { get; set; }
    public int CheckInCount { get; set; }
}

public sealed class CheckInResult
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime CheckedInAt { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
}

// ---- Participants and evidence review (US-21)

public sealed class ActivityParticipants
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityStatus { get; set; } = string.Empty;
    public ParticipantSummary Summary { get; set; } = new();
    public List<SessionAttendance> Sessions { get; set; } = [];
    public PagedResult<Participant> Participants { get; set; } = new();
}

public sealed class ParticipantSummary
{
    public int Active { get; set; }
    public int Registered { get; set; }
    public int Attended { get; set; }
    public int Cancelled { get; set; }
    public int PendingEvidence { get; set; }
}

public sealed class SessionAttendance
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int CheckInCount { get; set; }
}

public class Participant
{
    public int RegistrationId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public List<ParticipantCheckIn> CheckIns { get; set; } = [];
    public int PendingEvidenceCount { get; set; }
    public int AcceptedEvidenceCount { get; set; }
    public int RejectedEvidenceCount { get; set; }
}

public sealed class ParticipantDetail : Participant
{
    public List<StaffEvidence> Evidences { get; set; } = [];
}

public sealed class ParticipantCheckIn
{
    public int SessionId { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? RecordedByName { get; set; }
}

public sealed class StaffEvidence
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public string? SessionName { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public string? ConfirmationCode { get; set; }
    public string? Note { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? FileUrl { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>A file downloaded from the API (evidence), passed through to the browser.</summary>
public sealed record ApiFile(byte[] Content, string ContentType, string FileName);

// ---- Students (US-17)

public sealed class StudentDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int? ClassId { get; set; }
    public string? ClassCode { get; set; }
    public string? ClassName { get; set; }
    public bool IsClassMonitor { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Body of POST/PUT /admin/students; Password is required on create and optional (keep current) on update.</summary>
public sealed class StudentWriteRequest
{
    public string? StudentCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int? ClassId { get; set; }
}

// ---- Accounts (US-25, SystemAdmin only)

public sealed class AccountDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Campus { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Body of POST /admin/accounts: staff accounts only (HSSV or SystemAdmin).</summary>
public sealed class AccountCreateRequest
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "HSSV";
    public string Campus { get; set; } = "Cơ sở 1";
}

// ---- Student Evidences (US-08)

public sealed class StudentEvidence
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int? SessionId { get; set; }
    public string? SessionName { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public string? ConfirmationCode { get; set; }
    public string? Note { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? FileUrl { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool CanDelete { get; set; }
}

// ---- Evaluation periods and scoring (US-22)

public sealed class EvaluationPeriod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TargetPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PointRecordCount { get; set; }
}

public sealed class ActivityScoring
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityStatus { get; set; } = string.Empty;
    public int TrainingPoints { get; set; }
    public EvaluationPeriod? Period { get; set; }
    public bool CanApprove { get; set; }
    public string? BlockedReason { get; set; }
    public int EligibleCount { get; set; }
    public int ApprovedCount { get; set; }
    public List<ScoringCandidate> Candidates { get; set; } = [];
}

public sealed class ScoringCandidate
{
    public int RegistrationId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public int CheckInCount { get; set; }
    public int AcceptedEvidenceCount { get; set; }
    public int PendingEvidenceCount { get; set; }
    public bool Eligible { get; set; }
    public string? IneligibleReason { get; set; }
    public PointRecordInfo? PointRecord { get; set; }
}

public sealed class PointRecordInfo
{
    public int Id { get; set; }
    public int Points { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime DecidedAt { get; set; }
}

public sealed class PointRecordHistoryEntry
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? Reason { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class ApprovePointsResult
{
    public int ApprovedCount { get; set; }
    public List<SkippedRegistration> Skipped { get; set; } = [];
}

public sealed class SkippedRegistration
{
    public int RegistrationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ---- Reports (US-24)

public sealed class ReportOverview
{
    public EvaluationPeriod Period { get; set; } = new();
    public int ActivityCount { get; set; }
    public int CancelledActivityCount { get; set; }
    public int StudentCount { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    public double? ParticipationRate { get; set; }
    public int ApprovedPoints { get; set; }
    public double AveragePoints { get; set; }
    public int StudentsMeetingTarget { get; set; }
    public int AtRiskCount { get; set; }
    public string GroupBy { get; set; } = string.Empty;
    public List<ReportGroupRow> Groups { get; set; } = [];
}

public sealed class ReportGroupRow
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    public double? ParticipationRate { get; set; }
    public int ApprovedPoints { get; set; }
    public double AveragePoints { get; set; }
    public int AtRiskCount { get; set; }
}

public sealed class ActivityReportRow
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EndTime { get; set; }
    public int TrainingPoints { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
    public double? ParticipationRate { get; set; }
    public int ApprovedCount { get; set; }
    public int ApprovedPoints { get; set; }
}

public sealed class AtRiskStudent
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public int Points { get; set; }
    public int TargetPoints { get; set; }
    public int MissingPoints { get; set; }
    public int RegistrationCount { get; set; }
    public int AttendedCount { get; set; }
}

/// <summary>Report filters, bound from the query string and passed to the API unchanged.</summary>
public sealed class ReportFilter
{
    public int? PeriodId { get; set; }
    public string? Campus { get; set; }
    public string? AcademicYear { get; set; }
    public int? ClassId { get; set; }
    public string? GroupBy { get; set; }
}
