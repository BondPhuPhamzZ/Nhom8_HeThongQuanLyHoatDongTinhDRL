using System.ComponentModel.DataAnnotations;
using StudentActivityManagement.API.DTOs.Common;

namespace StudentActivityManagement.API.DTOs.ClassSpace;

/// <summary>The signed-in student's class space: which class it is and one page of its threads.</summary>
public sealed class ClassSpaceDto
{
    public int ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string? MonitorName { get; set; }
    public PagedResultDto<ClassThreadSummaryDto> Threads { get; set; } = new();
}

public class ClassThreadSummaryDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>First 200 characters of the body.</summary>
    public string Preview { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public PostAuthorDto Author { get; set; } = new();
    public int ReplyCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}

public sealed class ClassThreadDetailDto : ClassThreadSummaryDto
{
    public string Body { get; set; } = string.Empty;
    /// <summary>True when the signed-in user may delete the thread (author, and nobody else replied).</summary>
    public bool CanDelete { get; set; }
    public List<ClassThreadReplyDto> Replies { get; set; } = [];
}

public sealed class ClassThreadReplyDto
{
    public int Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public PostAuthorDto Author { get; set; } = new();
    public bool IsAnswer { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>True when the signed-in user wrote the reply and it is not marked as the answer.</summary>
    public bool CanDelete { get; set; }
}

public sealed class PostAuthorDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsMonitor { get; set; }
    /// <summary>True when the author is the signed-in user.</summary>
    public bool IsMe { get; set; }
}

public sealed class ClassThreadCreateDto
{
    /// <summary>Question or Discussion (Announcement is reserved for the class monitor, US-13).</summary>
    [Required(ErrorMessage = "Chọn loại bài: Question hoặc Discussion.")]
    public string Kind { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập tiêu đề.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Tiêu đề từ 3 đến 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập nội dung.")]
    [StringLength(4000, ErrorMessage = "Nội dung tối đa 4000 ký tự.")]
    public string Body { get; set; } = string.Empty;
}

public sealed class ClassThreadReplyCreateDto
{
    [Required(ErrorMessage = "Nhập nội dung trả lời.")]
    [StringLength(4000, ErrorMessage = "Nội dung tối đa 4000 ký tự.")]
    public string Body { get; set; } = string.Empty;
}
