using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = Role.Admin)]
    public class ActivitiesController : ControllerBase
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
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActivityById(int id)
        {
            var result = await _activityService.GetActivityByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        /// <summary>
        /// Tạo mới Hoạt động rèn luyện (Dành cho Admin)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateActivity([FromBody] ActivityCreateDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<ActivityResponseDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _activityService.CreateActivityAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(nameof(GetActivityById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật thông tin Hoạt động rèn luyện (Dành cho Admin)
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateActivity(int id, [FromBody] ActivityUpdateDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<ActivityResponseDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _activityService.UpdateActivityAsync(id, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Đổi trạng thái Hoạt động rèn luyện (Draft, Published, Ongoing, Completed, Cancelled)
        /// </summary>
        [HttpPatch("{id}/status")]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponseDto<ActivityResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateActivityStatus(int id, [FromBody] UpdateActivityStatusDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<ActivityResponseDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _activityService.UpdateActivityStatusAsync(id, request.Status);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa Hoạt động rèn luyện (Dành cho Admin)
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteActivity(int id)
        {
            var result = await _activityService.DeleteActivityAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
    }
}
