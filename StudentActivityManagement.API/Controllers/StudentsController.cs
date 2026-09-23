using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;
using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = Role.Admin)]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentService _studentService;

        public StudentsController(IStudentService studentService)
        {
            _studentService = studentService;
        }

        /// <summary>
        /// Lấy danh sách Sinh viên (Hỗ trợ phân trang, lọc theo Cơ sở, Khóa, Lớp và Tìm kiếm)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<StudentResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStudents(
            [FromQuery] string? campus,
            [FromQuery] string? academicYear,
            [FromQuery] int? classId,
            [FromQuery] string? search,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _studentService.GetStudentsAsync(campus, academicYear, classId, search, pageIndex, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Lấy thông tin chi tiết một Sinh viên
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var result = await _studentService.GetStudentByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        /// <summary>
        /// Thêm mới Sinh viên (Dành cho Admin)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<StudentResponseDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _studentService.CreateStudentAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(nameof(GetStudentById), new { id = result.Data!.Id }, result);
        }

        /// <summary>
        /// Cập nhật thông tin Sinh viên (Dành cho Admin)
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponseDto<StudentResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpdateDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponseDto<StudentResponseDto>.Fail("Dữ liệu đầu vào không hợp lệ."));
            }

            var result = await _studentService.UpdateStudentAsync(id, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Xóa Sinh viên (Dành cho Admin)
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var result = await _studentService.DeleteStudentAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
    }
}
