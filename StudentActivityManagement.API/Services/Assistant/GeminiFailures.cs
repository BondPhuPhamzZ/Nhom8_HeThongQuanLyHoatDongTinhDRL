using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>One mapping from Gemini failures to API problems, shared by every AI feature.</summary>
public static class GeminiFailures
{
    public static ApiResponseDto<T> NotConfigured<T>() => ApiResponseDto<T>.Fail(
        "Tính năng AI chưa được bật trên máy chủ (chưa có Gemini API key).",
        StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.AssistantUnavailable);

    public static ApiResponseDto<T> From<T>(GeminiException exception) => exception.Failure switch
    {
        GeminiFailure.RateLimited => ApiResponseDto<T>.Fail(
            "Đã dùng hết lượt miễn phí của Gemini trong lúc này. Vui lòng thử lại sau ít phút.",
            StatusCodes.Status429TooManyRequests, ApiErrorCodes.AssistantRateLimited),
        GeminiFailure.Configuration => ApiResponseDto<T>.Fail(
            "Tính năng AI chưa được cấu hình đúng trên máy chủ (Gemini API key hoặc model). Vui lòng báo quản trị hệ thống.",
            StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.AssistantUnavailable),
        _ => ApiResponseDto<T>.Fail(
            "AI đang không phản hồi. Vui lòng thử lại sau.",
            StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.AssistantUnavailable)
    };
}
