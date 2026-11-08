namespace StudentActivityManagement.API.DTOs.Common;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string AuthenticationRequired = "authentication_required";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountDisabled = "account_disabled";
    public const string AccessDenied = "access_denied";
    public const string ResourceNotFound = "resource_not_found";
    public const string ResourceConflict = "resource_conflict";
    public const string BusinessRuleViolation = "business_rule_violation";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string UnexpectedError = "unexpected_error";

    /// <summary>The AI assistant (US-31) is switched off, has no Gemini key, or Gemini cannot be reached.</summary>
    public const string AssistantUnavailable = "assistant_unavailable";

    /// <summary>Too many assistant questions: the per-student limit or Gemini's free quota.</summary>
    public const string AssistantRateLimited = "assistant_rate_limited";

    /// <summary>The request body is larger than the endpoint accepts (413).</summary>
    public const string PayloadTooLarge = "payload_too_large";
}
