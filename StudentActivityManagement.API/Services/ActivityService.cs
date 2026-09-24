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
                return ApiResponseDto<ActivityResponseDto>.Fail("KhÃ´ng tÃ¬m tháº¥y hoáº¡t Ä‘á»™ng.");
            }

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity));
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> CreateActivityAsync(ActivityCreateDto request)
        {
            if (request.EndTime <= request.StartTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thá»i gian káº¿t thÃºc pháº£i sau thá»i gian báº¯t Ä‘áº§u.");
            }

            if (request.RegistrationCloseTime <= request.RegistrationOpenTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thá»i gian Ä‘Ã³ng Ä‘Äƒng kÃ½ pháº£i sau thá»i gian má»Ÿ Ä‘Äƒng kÃ½.");
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

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), "Táº¡o má»›i hoáº¡t Ä‘á»™ng rÃ¨n luyá»‡n thÃ nh cÃ´ng.");
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityAsync(int id, ActivityUpdateDto request)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("KhÃ´ng tÃ¬m tháº¥y hoáº¡t Ä‘á»™ng.");
            }

            if (request.EndTime <= request.StartTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thá»i gian káº¿t thÃºc pháº£i sau thá»i gian báº¯t Ä‘áº§u.");
            }

            if (request.RegistrationCloseTime <= request.RegistrationOpenTime)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("Thá»i gian Ä‘Ã³ng Ä‘Äƒng kÃ½ pháº£i sau thá»i gian má»Ÿ Ä‘Äƒng kÃ½.");
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

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), "Cáº­p nháº­t hoáº¡t Ä‘á»™ng rÃ¨n luyá»‡n thÃ nh cÃ´ng.");
        }

        public async Task<ApiResponseDto<ActivityResponseDto>> UpdateActivityStatusAsync(int id, string newStatus)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<ActivityResponseDto>.Fail("KhÃ´ng tÃ¬m tháº¥y hoáº¡t Ä‘á»™ng.");
            }

            activity.Status = newStatus;
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponseDto<ActivityResponseDto>.Ok(MapToResponse(activity), $"ÄÃ£ cáº­p nháº­t tráº¡ng thÃ¡i hoáº¡t Ä‘á»™ng thÃ nh '{newStatus}'.");
        }

        public async Task<ApiResponseDto<bool>> DeleteActivityAsync(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
            {
                return ApiResponseDto<bool>.Fail("KhÃ´ng tÃ¬m tháº¥y hoáº¡t Ä‘á»™ng.");
            }

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            return ApiResponseDto<bool>.Ok(true, "XÃ³a hoáº¡t Ä‘á»™ng rÃ¨n luyá»‡n thÃ nh cÃ´ng.");
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
        public async Task<ApiResponseDto<bool>> RegisterActivityAsync(int activityId, int studentId)
        {
            var activity = await _context.Activities.FindAsync(activityId);
            if (activity == null || activity.Status != "Published")
                return ApiResponseDto<bool>.Fail("Hoạt động không tồn tại hoặc chưa mở.");
                
            var now = DateTime.UtcNow;
            if (now < activity.RegistrationOpenTime || now > activity.RegistrationCloseTime)
                return ApiResponseDto<bool>.Fail("Không trong thời gian đăng ký.");
                
            if (activity.CurrentParticipantsCount >= activity.MaxParticipants)
                return ApiResponseDto<bool>.Fail("Hoạt động đã đủ số lượng.");
                
            var existingReg = await _context.ActivityRegistrations
                .FirstOrDefaultAsync(ar => ar.ActivityId == activityId && ar.StudentId == studentId);
                
            if (existingReg != null)
                return ApiResponseDto<bool>.Fail("Bạn đã đăng ký hoạt động này rồi.");
                
            var registration = new ActivityRegistration
            {
                ActivityId = activityId,
                StudentId = studentId,
                RegisteredAt = DateTime.UtcNow,
                Status = "Registered"
            };
            
            _context.ActivityRegistrations.Add(registration);
            activity.CurrentParticipantsCount++;
            await _context.SaveChangesAsync();
            
            return ApiResponseDto<bool>.Ok(true, "Đăng ký thành công.");
        }
    }
}

