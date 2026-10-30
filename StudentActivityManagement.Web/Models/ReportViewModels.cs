using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

public sealed class ReportViewModel
{
    public ReportFilter Filter { get; set; } = new();
    public List<EvaluationPeriod> Periods { get; set; } = [];
    public List<ClassDto> Classes { get; set; } = [];
    public ReportOverview? Overview { get; set; }
    public List<ActivityReportRow> Activities { get; set; } = [];
    public PagedResult<AtRiskStudent> AtRisk { get; set; } = new();
}
