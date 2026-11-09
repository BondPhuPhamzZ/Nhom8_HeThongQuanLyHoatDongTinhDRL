using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using StudentActivityManagement.API.DTOs.Common;
using StudentActivityManagement.API.DTOs.Reports;

namespace StudentActivityManagement.API.Services;

/// <summary>Writes the existing US-24 report results to a workbook without a second set of report queries.</summary>
public sealed class ReportExportService : IReportExportService
{
    private readonly IReportService _reports;

    public ReportExportService(IReportService reports) => _reports = reports;

    public async Task<ApiResponseDto<ReportExportFile>> ExportAsync(ReportQueryDto query)
    {
        var overviewResult = await _reports.GetOverviewAsync(query);
        if (!overviewResult.Success || overviewResult.Data == null)
            return FailFrom(overviewResult);

        // Pin the period chosen by the overview so each sheet uses the same period even at a boundary.
        query.PeriodId = overviewResult.Data.Period.Id;
        var activitiesResult = await _reports.GetActivitiesAsync(query);
        if (!activitiesResult.Success || activitiesResult.Data == null)
            return FailFrom(activitiesResult);

        var atRisk = new List<AtRiskStudentDto>();
        for (var page = 1; ; page++)
        {
            // The report service caps a page at 100 rows.
            var result = await _reports.GetAtRiskStudentsAsync(query, page, 100);
            if (!result.Success || result.Data == null) return FailFrom(result);
            atRisk.AddRange(result.Data.Items);
            if (atRisk.Count >= result.Data.TotalCount) break;
        }

        using var workbook = new XLWorkbook();
        WriteOverview(workbook.Worksheets.Add("Tổng quan"), overviewResult.Data);
        WriteActivities(workbook.Worksheets.Add("Hoạt động"), activitiesResult.Data);
        WriteAtRisk(workbook.Worksheets.Add("Sinh viên nguy cơ"), atRisk);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var localDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var fileName = $"BaoCao_{SafeFilePart(overviewResult.Data.Period.Name)}_{localDate}.xlsx";
        return ApiResponseDto<ReportExportFile>.Ok(
            new ReportExportFile(stream.ToArray(), fileName, atRisk.Count, overviewResult.Data.Period.Id));
    }

    private static ApiResponseDto<ReportExportFile> FailFrom<T>(ApiResponseDto<T> result) =>
        ApiResponseDto<ReportExportFile>.Fail(result.Message, result.HttpStatusCode, result.ErrorCode);

    private static void WriteOverview(IXLWorksheet sheet, ReportOverviewDto overview)
    {
        sheet.Cell(1, 1).Value = "Báo cáo tổng quan";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = "Kỳ đánh giá";
        sheet.Cell(2, 2).Value = overview.Period.Name;
        sheet.Cell(3, 1).Value = "Từ";
        SetVnDate(sheet.Cell(3, 2), overview.Period.StartDate);
        sheet.Cell(3, 3).Value = "Đến";
        SetVnDate(sheet.Cell(3, 4), overview.Period.EndDate);
        sheet.Cell(4, 1).Value = "Điểm mục tiêu";
        sheet.Cell(4, 2).Value = overview.Period.TargetPoints;

        var labels = new[] { "Hoạt động", "Đã hủy", "Sinh viên", "Đăng ký", "Tham gia", "Tỷ lệ tham gia", "Điểm đã duyệt", "Điểm TB", "Đạt mục tiêu", "Nguy cơ" };
        for (var column = 0; column < labels.Length; column++) sheet.Cell(6, column + 1).Value = labels[column];
        var values = new object?[] { overview.ActivityCount, overview.CancelledActivityCount, overview.StudentCount,
            overview.RegistrationCount, overview.AttendedCount, overview.ParticipationRate, overview.ApprovedPoints,
            overview.AveragePoints, overview.StudentsMeetingTarget, overview.AtRiskCount };
        for (var column = 0; column < values.Length; column++) SetValue(sheet.Cell(7, column + 1), values[column]);
        SetRate(sheet.Cell(7, 6), overview.ParticipationRate);
        StyleHeader(sheet, 6, labels.Length);

        var groupLabels = new[] { "Nhóm", "Sinh viên", "Đăng ký", "Tham gia", "Tỷ lệ tham gia", "Điểm đã duyệt", "Điểm TB", "Nguy cơ" };
        for (var column = 0; column < groupLabels.Length; column++) sheet.Cell(9, column + 1).Value = groupLabels[column];
        StyleHeader(sheet, 9, groupLabels.Length);
        var row = 10;
        foreach (var group in overview.Groups)
        {
            sheet.Cell(row, 1).Value = group.Label;
            sheet.Cell(row, 2).Value = group.StudentCount;
            sheet.Cell(row, 3).Value = group.RegistrationCount;
            sheet.Cell(row, 4).Value = group.AttendedCount;
            SetRate(sheet.Cell(row, 5), group.ParticipationRate);
            sheet.Cell(row, 6).Value = group.ApprovedPoints;
            sheet.Cell(row, 7).Value = group.AveragePoints;
            sheet.Cell(row, 8).Value = group.AtRiskCount;
            row++;
        }
        Finish(sheet);
    }

    private static void WriteActivities(IXLWorksheet sheet, List<ActivityReportRowDto> activities)
    {
        string[] labels = ["Mã", "Hoạt động", "Đơn vị tổ chức", "Trạng thái", "Kết thúc", "Điểm", "Đăng ký", "Tham gia", "Tỷ lệ tham gia", "Đã duyệt", "Điểm đã duyệt"];
        for (var column = 0; column < labels.Length; column++) sheet.Cell(1, column + 1).Value = labels[column];
        StyleHeader(sheet, 1, labels.Length);
        var row = 2;
        foreach (var activity in activities)
        {
            sheet.Cell(row, 1).Value = activity.ActivityId;
            sheet.Cell(row, 2).Value = activity.ActivityName;
            sheet.Cell(row, 3).Value = activity.OrganizerName;
            sheet.Cell(row, 4).Value = activity.Status;
            SetVnDate(sheet.Cell(row, 5), activity.EndTime);
            sheet.Cell(row, 6).Value = activity.TrainingPoints;
            sheet.Cell(row, 7).Value = activity.RegistrationCount;
            sheet.Cell(row, 8).Value = activity.AttendedCount;
            SetRate(sheet.Cell(row, 9), activity.ParticipationRate);
            sheet.Cell(row, 10).Value = activity.ApprovedCount;
            sheet.Cell(row, 11).Value = activity.ApprovedPoints;
            row++;
        }
        Finish(sheet);
    }

    private static void WriteAtRisk(IXLWorksheet sheet, List<AtRiskStudentDto> students)
    {
        string[] labels = ["MSSV", "Họ tên", "Lớp", "Khóa", "Cơ sở", "Điểm", "Mục tiêu", "Còn thiếu"];
        for (var column = 0; column < labels.Length; column++) sheet.Cell(1, column + 1).Value = labels[column];
        StyleHeader(sheet, 1, labels.Length);
        var row = 2;
        foreach (var student in students)
        {
            sheet.Cell(row, 1).Value = student.StudentCode;
            sheet.Cell(row, 2).Value = student.FullName;
            sheet.Cell(row, 3).Value = student.ClassCode ?? string.Empty;
            sheet.Cell(row, 4).Value = student.AcademicYear;
            sheet.Cell(row, 5).Value = student.Campus;
            sheet.Cell(row, 6).Value = student.Points;
            sheet.Cell(row, 7).Value = student.TargetPoints;
            sheet.Cell(row, 8).Value = student.MissingPoints;
            row++;
        }
        Finish(sheet);
    }

    private static void StyleHeader(IXLWorksheet sheet, int row, int columns)
    {
        var range = sheet.Range(row, 1, row, columns);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
    }

    private static void Finish(IXLWorksheet sheet)
    {
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private static void SetRate(IXLCell cell, double? percent)
    {
        if (percent.HasValue) cell.Value = percent.Value / 100d;
        cell.Style.NumberFormat.Format = "0.0%";
    }

    private static void SetVnDate(IXLCell cell, DateTime utc)
    {
        cell.Value = DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
        cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        if (value is int integer) cell.Value = integer;
        else if (value is double number) cell.Value = number;
    }

    private static string SafeFilePart(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var ascii = character is 'đ' or 'Đ' ? 'd' : character;
            if (char.IsLetterOrDigit(ascii) && ascii <= 127) builder.Append(ascii);
            else if (char.IsWhiteSpace(ascii) || ascii is '-' or '_') builder.Append('_');
        }
        var cleaned = builder.ToString().Trim('_');
        return cleaned.Length == 0 ? "KyDanhGia" : cleaned[..Math.Min(cleaned.Length, 60)];
    }
}
