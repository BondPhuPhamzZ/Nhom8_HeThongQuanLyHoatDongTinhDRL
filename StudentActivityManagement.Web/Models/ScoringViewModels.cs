using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.Web.Api;

namespace StudentActivityManagement.Web.Models;

public sealed class PeriodListViewModel
{
    public List<EvaluationPeriod> Periods { get; set; } = [];
    public PeriodCreateForm NewPeriod { get; set; } = PeriodCreateForm.Default();
}

public sealed class PeriodCreateForm
{
    [Required(ErrorMessage = "Nhập tên kỳ."), StringLength(100)]
    [Display(Name = "Tên kỳ")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Từ ngày")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Display(Name = "Đến ngày")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Range(0, 1000, ErrorMessage = "Điểm mục tiêu từ 0 đến 1000.")]
    [Display(Name = "Điểm mục tiêu")]
    public int TargetPoints { get; set; } = 50;

    public static PeriodCreateForm Default()
    {
        var today = DateTime.Today;
        return new PeriodCreateForm { StartDate = today, EndDate = today.AddMonths(5) };
    }
}

public sealed class PointHistoryViewModel
{
    public int ActivityId { get; set; }
    public string StudentLabel { get; set; } = string.Empty;
    public List<PointRecordHistoryEntry> Entries { get; set; } = [];
}
