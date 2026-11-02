using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Student;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class StudentPointsService : IStudentPointsService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activities;

    public StudentPointsService(AppDbContext context, IActivityService activities)
    {
        _context = context;
        _activities = activities;
    }

    public async Task<ApiResponseDto<StudentPointsRecommendationDto>> GetPointsAndRecommendationsAsync(int studentId, int? periodId)
    {
        var now = DateTime.UtcNow;
        var period = periodId.HasValue
            ? await _context.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(item => item.Id == periodId.Value)
            : await _context.EvaluationPeriods.AsNoTracking()
                .OrderByDescending(item => item.StartDate <= now && item.EndDate >= now)
                .ThenByDescending(item => item.StartDate)
                .FirstOrDefaultAsync();
        if (period == null)
        {
            return ApiResponseDto<StudentPointsRecommendationDto>.Fail(
                "Không tìm thấy kỳ đánh giá.", StatusCodes.Status404NotFound, ApiErrorCodes.ResourceNotFound);
        }

        var approvedPoints = await _context.PointRecords.AsNoTracking()
            .Where(record => record.StudentId == studentId && record.PeriodId == period.Id && record.Status == PointRecordStatus.Approved)
            .SumAsync(record => (int?)record.Points) ?? 0;
        var missingPoints = Math.Max(0, period.TargetPoints - approvedPoints);
        var recommendations = new List<ActivityResponseDto>();
        if (missingPoints > 0 && period.Status == EvaluationPeriodStatus.Open)
        {
            var eligible = await _activities.GetStudentActivitiesAsync(studentId, null, null, 1, 100, registeredOnly: false);
            if (!eligible.Success || eligible.Data == null)
            {
                return ApiResponseDto<StudentPointsRecommendationDto>.Fail(
                    eligible.Message, eligible.HttpStatusCode, eligible.ErrorCode);
            }

            recommendations = eligible.Data.Items
                .Where(activity => activity.Status == ActivityStatus.Published && activity.IsRegistrationOpen &&
                    activity.RemainingSlots > 0 && activity.TrainingPoints > 0 &&
                    activity.StartTime <= period.EndDate && activity.EndTime >= period.StartDate &&
                    activity.MyRegistrationStatus is not (RegistrationStatus.Registered or RegistrationStatus.Attended))
                .OrderBy(activity => activity.Eligibility?.ParticipationType == AudienceParticipationType.Required ? 0 :
                    activity.Eligibility?.ParticipationType == AudienceParticipationType.Recommended ? 1 : 2)
                .ThenByDescending(activity => activity.TrainingPoints)
                .ThenBy(activity => activity.StartTime)
                .ToList();
        }

        return ApiResponseDto<StudentPointsRecommendationDto>.Ok(new StudentPointsRecommendationDto
        {
            Period = new EvaluationPeriodSummaryDto
            {
                Id = period.Id,
                Name = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                TargetPoints = period.TargetPoints,
                Status = period.Status
            },
            ApprovedPoints = approvedPoints,
            MissingPoints = missingPoints,
            IsAtRisk = missingPoints > 0,
            RecommendedActivities = recommendations
        });
    }
}
