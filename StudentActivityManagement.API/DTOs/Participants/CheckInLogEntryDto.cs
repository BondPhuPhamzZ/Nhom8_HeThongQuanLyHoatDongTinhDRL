namespace StudentActivityManagement.API.DTOs.Participants;

public sealed class CheckInLogEntryDto
{
    public int Id { get; init; }
    public int RegistrationId { get; init; }
    public int StudentId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public int SessionId { get; init; }
    public string SessionName { get; init; } = string.Empty;
    public DateTime CheckedInAt { get; init; }
    public string Method { get; init; } = string.Empty;
    public int? RecordedById { get; init; }
    public string? RecordedByName { get; init; }
    public string? Note { get; init; }
    public DateTime? VoidedAt { get; init; }
    public int? VoidedById { get; init; }
    public string? VoidedByName { get; init; }
    public string? VoidReason { get; init; }
}
