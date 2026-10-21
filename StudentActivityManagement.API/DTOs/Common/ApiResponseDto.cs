using System.Text.Json.Serialization;

namespace StudentActivityManagement.API.DTOs.Common
{
    public class ApiResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; } = StatusCodes.Status200OK;

        [JsonIgnore]
        public string ErrorCode { get; set; } = string.Empty;

        public static ApiResponseDto<T> Ok(T data, string message = "Success")
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponseDto<T> Fail(
            string message,
            int httpStatusCode = StatusCodes.Status400BadRequest,
            string errorCode = ApiErrorCodes.BusinessRuleViolation)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                Data = default,
                HttpStatusCode = httpStatusCode,
                ErrorCode = errorCode
            };
        }
    }
}
