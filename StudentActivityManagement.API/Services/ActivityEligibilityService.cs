using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public sealed class ActivityEligibilityService : IActivityEligibilityService
{
    public IQueryable<Activity> ApplyEligibleFor(IQueryable<Activity> query, User student)
    {
        return query.Where(activity =>
            !activity.AudienceRules.Any() ||
            activity.AudienceRules.Any(rule =>
                (rule.Campus == null || rule.Campus == student.Campus) &&
                (rule.AcademicYear == null || rule.AcademicYear == student.AcademicYear) &&
                (rule.ClassId == null || rule.ClassId == student.ClassId)));
    }

    public ActivityEligibilityDto Evaluate(Activity activity, User student)
    {
        if (activity.AudienceRules.Count == 0)
        {
            return new ActivityEligibilityDto
            {
                IsEligible = true,
                ParticipationType = AudienceParticipationType.Optional,
                Reason = "Hoạt động mở cho tất cả sinh viên."
            };
        }

        var matchingRule = activity.AudienceRules
            .Where(rule =>
                (rule.Campus == null || rule.Campus == student.Campus) &&
                (rule.AcademicYear == null || rule.AcademicYear == student.AcademicYear) &&
                (rule.ClassId == null || rule.ClassId == student.ClassId))
            .OrderBy(rule => GetPriority(rule.ParticipationType))
            .FirstOrDefault();

        if (matchingRule == null)
        {
            return new ActivityEligibilityDto
            {
                IsEligible = false,
                Reason = "Sinh viên không thuộc đối tượng tham gia của hoạt động."
            };
        }

        return new ActivityEligibilityDto
        {
            IsEligible = true,
            ParticipationType = matchingRule.ParticipationType,
            Reason = matchingRule.ParticipationType switch
            {
                AudienceParticipationType.Required => "Hoạt động bắt buộc với nhóm sinh viên này.",
                AudienceParticipationType.Recommended => "Hoạt động được khuyến khích với nhóm sinh viên này.",
                _ => "Sinh viên có thể tự nguyện tham gia hoạt động."
            }
        };
    }

    private static int GetPriority(string participationType) => participationType switch
    {
        AudienceParticipationType.Required => 0,
        AudienceParticipationType.Recommended => 1,
        _ => 2
    };
}
