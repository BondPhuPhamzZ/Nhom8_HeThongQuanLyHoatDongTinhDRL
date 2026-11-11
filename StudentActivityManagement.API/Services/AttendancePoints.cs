using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Data;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services;

/// <summary>
/// Attendance is the only proof of participation: the first check-in (QR, code or manual) records the
/// activity's points as Approved in the open evaluation period that contains the activity's end time.
/// Staff can still reject that decision later, and the student can appeal it (US-37).
/// </summary>
public static class AttendancePoints
{
    public const string AutoReason = "Tự động ghi điểm khi điểm danh.";

    /// <summary>Adds the automatic decision to the context; the caller saves it inside its own transaction.</summary>
    public static async Task AwardAsync(AppDbContext context, ActivityRegistration registration, Activity activity, int? staffId, DateTime now)
    {
        if (registration.Status != RegistrationStatus.Attended
            || await context.PointRecords.AnyAsync(record => record.RegistrationId == registration.Id))
        {
            return;
        }

        var period = await context.EvaluationPeriods.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StartDate <= activity.EndTime && activity.EndTime <= item.EndDate);
        if (period is not { Status: EvaluationPeriodStatus.Open })
        {
            // No open period yet: staff record these points later from the scoring page.
            return;
        }

        var record = new PointRecord
        {
            RegistrationId = registration.Id,
            StudentId = registration.StudentId,
            ActivityId = activity.Id,
            PeriodId = period.Id,
            Points = activity.TrainingPoints,
            Status = PointRecordStatus.Approved,
            Reason = AutoReason,
            DecidedById = staffId,
            DecidedAt = now
        };
        record.History.Add(new PointRecordHistory
        {
            FromStatus = null,
            ToStatus = PointRecordStatus.Approved,
            Points = activity.TrainingPoints,
            Reason = AutoReason,
            ChangedById = staffId,
            ChangedAt = now
        });
        context.PointRecords.Add(record);
        registration.PointsEarned = activity.TrainingPoints;
    }

    /// <summary>True when staff decided the points (recorded, rejected or changed them), not just the automatic award.</summary>
    public static Task<bool> IsDecidedByStaffAsync(AppDbContext context, int registrationId) =>
        context.PointRecords.AnyAsync(record => record.RegistrationId == registrationId &&
            !(record.Status == PointRecordStatus.Approved && record.Reason == AutoReason && record.History.Count == 1));

    /// <summary>Removes the automatic decision when the registration has no attendance left.</summary>
    public static async Task RevokeAsync(AppDbContext context, ActivityRegistration registration)
    {
        var record = await context.PointRecords.Include(item => item.History)
            .FirstOrDefaultAsync(item => item.RegistrationId == registration.Id);
        if (record is { Status: PointRecordStatus.Approved, Reason: AutoReason, History.Count: 1 })
        {
            context.PointRecordHistories.RemoveRange(record.History);
            context.PointRecords.Remove(record);
            registration.PointsEarned = 0;
        }
    }
}
