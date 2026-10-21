using StudentActivityManagement.API.DTOs.Activity;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

public interface IActivityEligibilityService
{
    IQueryable<Activity> ApplyEligibleFor(IQueryable<Activity> query, User student);
    ActivityEligibilityDto Evaluate(Activity activity, User student);
}
