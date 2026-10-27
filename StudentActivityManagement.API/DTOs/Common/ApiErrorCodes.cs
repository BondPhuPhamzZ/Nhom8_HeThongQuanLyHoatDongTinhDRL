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
}
