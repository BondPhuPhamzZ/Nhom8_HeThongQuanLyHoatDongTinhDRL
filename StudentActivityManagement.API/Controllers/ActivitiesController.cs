using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.Authorization;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/activities")]
    [Authorize(Policy = AuthorizationPolicies.BusinessAdmin)]
    public class ActivitiesController : ApiControllerBase
    {
        private readonly IActivityService _activityService;

        public ActivitiesController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        /// <summary>
        /// Lấy danh sách Hoạt động rèn luyện dành cho Admin (Phân trang, Lọc theo Cơ sở, Trạng thái, Tìm kiếm)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<ActivityResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActivities(
            [FromQuery] string? campus,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _activityService.GetActivitiesAsync(campus, status, search, pageIndex, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Xem chi tiết thông tin một Hoạt động rèn luyện
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActivityById(int id)
        {
            var result = await _activityService.GetActivityByIdAsync(id);
            if (!result.Success)
            {
                return ProblemFrom(result);
            }
            return Ok(result);
        }

        /// <summary>
        /// Tạo mới Hoạt động rèn luyện (Dành cho Admin)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateActivity([FromBody] ActivityCreateDto request)
        {
            var result = await _activityService.CreateActivityAsync(request);
            if (!result.Success)
            {
                return ProblemFrom(result);
            }

            return CreatedAtAction(nameof(GetActivityById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật thông tin Hoạt động rèn luyện (Dành cho Admin)
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateActivity(int id, [FromBody] ActivityUpdateDto request)
        {
            var result = await _activityService.UpdateActivityAsync(id, request);
            if (!result.Success)
            {
                return ProblemFrom(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Đổi trạng thái Hoạt động rèn luyện theo vòng đời: Draft → Published/Cancelled;
        /// Published → Ongoing/Cancelled/Draft (khi chưa có đăng ký); Ongoing → Completed/Cancelled.
        /// Completed và Cancelled là trạng thái cuối.
        /// </summary>
        [HttpPatch("{id}/status")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateActivityStatus(int id, [FromBody] UpdateActivityStatusDto request)
        {
            var result = await _activityService.UpdateActivityStatusAsync(id, request.Status);
            if (!result.Success)
            {
                return ProblemFrom(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Bắt đầu ngay: đóng đăng ký và dời giờ bắt đầu buổi đầu tiên về hiện tại, hoạt động chuyển sang Đang diễn ra.
        /// Chỉ dùng khi hoạt động đã công bố và chưa diễn ra.
        /// </summary>
        [HttpPost("{id}/start-now")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> StartNow(int id)
        {
            var result = await _activityService.StartNowAsync(id);
            return result.Success ? Ok(result) : ProblemFrom(result);
        }

        /// <summary>
        /// Kết thúc ngay: các buổi đang diễn ra kết thúc tại thời điểm hiện tại và đóng check-in ngay,
        /// hoạt động chuyển sang Đối chiếu điểm danh. Mọi buổi phải đã bắt đầu.
        /// </summary>
        [HttpPost("{id}/end-now")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> EndNow(int id)
        {
            if (!TryGetUserId(out var staffId))
            {
                return InvalidSession();
            }

            var result = await _activityService.EndNowAsync(id, staffId);
            return result.Success ? Ok(result) : ProblemFrom(result);
        }

        /// <summary>
        /// Xóa Hoạt động rèn luyện — chỉ hoạt động Nháp chưa từng có đăng ký; trường hợp khác dùng trạng thái Cancelled.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteActivity(int id)
        {
            var result = await _activityService.DeleteActivityAsync(id);
            if (!result.Success)
            {
                return ProblemFrom(result);
            }

            return Ok(result);
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

        private ObjectResult InvalidSession() => ApiProblem(
            StatusCodes.Status401Unauthorized,
            ApiErrorCodes.AuthenticationRequired,
            "Không xác định được người dùng.");
    }
}
