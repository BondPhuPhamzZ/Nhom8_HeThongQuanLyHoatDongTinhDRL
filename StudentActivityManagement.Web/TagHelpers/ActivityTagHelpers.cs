using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using StudentActivityManagement.Web.Infrastructure;

namespace StudentActivityManagement.Web.TagHelpers;

/// <summary><c>&lt;status-badge value="Published" /&gt;</c> renders an activity or registration status as a coloured badge.</summary>
[HtmlTargetElement("status-badge", TagStructure = TagStructure.WithoutEndTag)]
public sealed class StatusBadgeTagHelper : TagHelper
{
    public string? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (label, css) = Value switch
        {
            "Draft" => ("Nháp", "text-bg-secondary"),
            "Published" => ("Đang mở", "text-bg-success"),
            "Ongoing" => ("Đang diễn ra", "text-bg-primary"),
            "Completed" => ("Đã kết thúc", "text-bg-dark"),
            "Cancelled" => ("Đã hủy", "text-bg-danger"),
            "Registered" => ("Đã đăng ký", "text-bg-success"),
            "Attended" => ("Đã tham gia", "text-bg-primary"),
            "Absent" => ("Vắng mặt", "text-bg-warning"),
            "Required" => ("Bắt buộc", "text-bg-danger"),
            "Recommended" => ("Khuyến khích", "text-bg-warning"),
            "Optional" => ("Tự chọn", "text-bg-info"),
            "Pending" => ("Chờ duyệt", "text-bg-warning"),
            "Accepted" => ("Đã duyệt", "text-bg-success"),
            "Rejected" => ("Từ chối", "text-bg-danger"),
            "Approved" => ("Đã ghi điểm", "text-bg-success"),
            "Revoked" => ("Đã thu hồi", "text-bg-dark"),
            "Open" => ("Đang mở", "text-bg-success"),
            "Closed" => ("Đã khóa", "text-bg-secondary"),
            _ => (Value ?? string.Empty, "text-bg-light")
        };
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"badge rounded-pill {css}");
        output.Content.SetContent(label);
    }
}

/// <summary><c>&lt;vn-time value="@utc" /&gt;</c> shows a UTC time from the API in Vietnam time (UTC+7).</summary>
[HtmlTargetElement("vn-time", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VnTimeTagHelper : TagHelper
{
    public DateTime Value { get; set; }

    /// <summary>"datetime" (default) or "date".</summary>
    public string Format { get; set; } = "datetime";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "time";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("datetime",
            DateTime.SpecifyKind(Value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture));
        output.Content.SetContent(Format == "date" ? VnTime.FormatDate(Value) : VnTime.Format(Value));
    }
}
