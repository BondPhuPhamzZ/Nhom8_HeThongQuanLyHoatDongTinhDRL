using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services
{
    public class ActivityService : IActivityService
    {
        private readonly AppDbContext _context;

        public ActivityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponseDto<PagedResultDto<ActivityResponseDto>>> GetActivitiesAsync(
            string? campus, string? status, string? search, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.Activities.AsQueryable();

            if (!string.IsNullOrWhiteSpace(campus))
            {
                query = query.Where(a => a.Campus == campus);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(a => a.ActivityName.ToLower().Contains(keyword) || a.Location.ToLower().Contains(keyword) || a.Description.ToLower().Contains(keyword));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(a => MapToResponse(a))
                .ToListAsync();

            var result = new PagedResultDto<ActivityResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };

            return ApiResponseDto<PagedResultDto<ActivityResponseDto>>.Ok(result);
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> GetActivityByIdAsync(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Không tìm thấy hoạt động.");
            }

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity));
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> CreateActivityAsync(ActivityCreateDto request)
        {
            if (request.EndTime <= request.StartTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            if (request.RegistrationCloseTime <= request.RegistrationOpenTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thời gian đóng đăng ký phải sau thời gian mở đăng ký.");
            }

            var activity = new Activity
            {
                ActivityName = request.ActivityName,
                Description = request.Description,
                Location = request.Location,
                Campus = request.Campus,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                RegistrationOpenTime = request.RegistrationOpenTime,
                RegistrationCloseTime = request.RegistrationCloseTime,
                MaxParticipants = request.MaxParticipants,
                CurrentParticipantsCount = 0,
                TrainingPoints = request.TrainingPoints,
                TargetAudience = request.TargetAudience,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Published" : request.Status,
                CreatedAt = DateTime.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), "Tạo mới hoạt động rèn luyện thành công.");
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityAsync(int id, ActivityUpdateDto request)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Không tìm thấy hoạt động.");
            }

            if (request.EndTime <= request.StartTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            if (request.RegistrationCloseTime <= request.RegistrationOpenTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thời gian đóng đăng ký phải sau thời gian mở đăng ký.");
            }

            activity.ActivityName = request.ActivityName;
            activity.Description = request.Description;
            activity.Location = request.Location;
            activity.Campus = request.Campus;
            activity.StartTime = request.StartTime;
            activity.EndTime = request.EndTime;
            activity.RegistrationOpenTime = request.RegistrationOpenTime;
            activity.RegistrationCloseTime = request.RegistrationCloseTime;
            activity.MaxParticipants = request.MaxParticipants;
            activity.TrainingPoints = request.TrainingPoints;
            activity.TargetAudience = request.TargetAudience;
            activity.Status = request.Status;
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), "Cập nhật hoạt động rèn luyện thành công.");
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityStatusAsync(int id, string newStatus)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Không tìm thấy hoạt động.");
            }

            activity.Status = newStatus;
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), $"Đã cập nhật trạng thái hoạt động thành '{newStatus}'.");
        }

        public async Task<ApiResponseDto<bool>> DeleteActivityAsync(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<bool>.Fail("Không tìm thấy hoạt động.");
            }

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "Xóa hoạt động rèn luyện thành công.");
        }

        private static ActivityResponseDto MapToResponse(Activity a)
        {
            return new ActivityResponseDto
            {
                Id = a.Id,
                ActivityName = a.ActivityName,
                Description = a.Description,
                Location = a.Location,
                Campus = a.Campus,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                RegistrationOpenTime = a.RegistrationOpenTime,
                RegistrationCloseTime = a.RegistrationCloseTime,
                MaxParticipants = a.MaxParticipants,
                CurrentParticipantsCount = a.CurrentParticipantsCount,
                TrainingPoints = a.TrainingPoints,
                TargetAudience = a.TargetAudience,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            };
        }
    }
}
