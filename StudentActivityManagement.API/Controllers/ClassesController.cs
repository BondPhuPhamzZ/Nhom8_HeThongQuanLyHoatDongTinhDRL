using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.DTOs.Class;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = Role.Admin)]
    public class ClassesController : ControllerBase
    {
        private readonly IStudentService _studentService;

        public ClassesController(IStudentService studentService)
        {
            _studentService = studentService;
        }

        /// <summary>
        /// Lấy danh sách Lớp học
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponseDto<List<ClassDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClasses()
        {
            var result = await _studentService.GetClassesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Tạo mới Lớp học (Dành cho Admin)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponseDto<ClassDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<ClassDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateClass([FromBody] ClassCreateDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<ClassDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _studentService.CreateClassAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
