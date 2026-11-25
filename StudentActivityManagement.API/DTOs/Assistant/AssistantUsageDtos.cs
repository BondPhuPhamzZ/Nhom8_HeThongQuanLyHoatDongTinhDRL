namespace StudentActivityManagement.API.DTOs.Assistant;

public sealed record AssistantUsageRowDto(DateOnly UsageDate, string Feature, string StudentCode, string FullName,
    string Role, int QuestionCount, int TokenCount);

public sealed record AssistantUsageDailyTotalDto(DateOnly UsageDate, long QuestionCount, long TokenCount);

public sealed record AssistantUsageReportDto(DateOnly From, DateOnly To, List<AssistantUsageRowDto> Items,
    List<AssistantUsageDailyTotalDto> DailyTotals);
