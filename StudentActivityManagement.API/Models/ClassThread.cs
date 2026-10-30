using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentActivityManagement.API.Models;

/// <summary>
/// A thread in a class's private discussion space (US-09). Only students and the monitor of that class
/// can read or post; the class comes from the signed-in account, never from the request.
/// </summary>
public class ClassThread
{
    [Key]
    public int Id { get; set; }

    public int ClassId { get; set; }

    [ForeignKey(nameof(ClassId))]
    public Class? Class { get; set; }

    public int AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    /// <summary>One of <see cref="ClassThreadKind"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Kind { get; set; } = ClassThreadKind.Question;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>One of <see cref="ClassThreadStatus"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = ClassThreadStatus.Open;

    /// <summary>Pinned threads are listed first (set by the monitor, US-13).</summary>
    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Time of the last post (thread or reply); the list is ordered by it.</summary>
    public DateTime LastActivityAt { get; set; }

    public List<ClassThreadReply> Replies { get; set; } = [];

    public List<ThreadEscalation> Escalations { get; set; } = [];
}

/// <summary>
/// A question the class monitor raises from a class thread to Phòng QL HSSV for an official answer (US-15).
/// Staff answer it in US-20; the monitor relays the answer back to the class in US-16.
/// </summary>
public class ThreadEscalation
{
    [Key]
    public int Id { get; set; }

    public int ThreadId { get; set; }

    [ForeignKey(nameof(ThreadId))]
    public ClassThread? Thread { get; set; }

    /// <summary>The monitor who raised it; kept null if that account is deleted later.</summary>
    public int? RaisedById { get; set; }

    [ForeignKey(nameof(RaisedById))]
    public User? RaisedBy { get; set; }

    /// <summary>What the monitor needs Phòng QL HSSV to confirm.</summary>
    [Required]
    [StringLength(2000)]
    public string Question { get; set; } = string.Empty;

    /// <summary>One of <see cref="EscalationStatus"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = EscalationStatus.Pending;

    public DateTime CreatedAt { get; set; }

    /// <summary>Official answer from Phòng QL HSSV (US-20).</summary>
    [StringLength(4000)]
    public string? Answer { get; set; }

    public int? AnsweredById { get; set; }

    [ForeignKey(nameof(AnsweredById))]
    public User? AnsweredBy { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }
}

public class ClassThreadReply
{
    [Key]
    public int Id { get; set; }

    public int ThreadId { get; set; }

    [ForeignKey(nameof(ThreadId))]
    public ClassThread? Thread { get; set; }

    public int AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    [Required]
    [StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Marked by the monitor as the answer to a question (US-14).</summary>
    public bool IsAnswer { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Question and Discussion are opened by any class member (US-09); Announcement is for the monitor (US-13).</summary>
public static class ClassThreadKind
{
    public const string Question = "Question";
    public const string Discussion = "Discussion";
    public const string Announcement = "Announcement";

    public static readonly string[] All = [Question, Discussion, Announcement];

    /// <summary>Kinds any class member may open.</summary>
    public static readonly string[] MemberKinds = [Question, Discussion];
}

/// <summary>Open accepts replies; Answered is set when the monitor marks an answer (US-14); Closed is read-only.</summary>
public static class ClassThreadStatus
{
    public const string Open = "Open";
    public const string Answered = "Answered";
    public const string Closed = "Closed";

    public static readonly string[] All = [Open, Answered, Closed];
}

/// <summary>Pending waits for Phòng QL HSSV; it ends as Answered (US-20) or Withdrawn by the monitor.</summary>
public static class EscalationStatus
{
    public const string Pending = "Pending";
    public const string Answered = "Answered";
    public const string Withdrawn = "Withdrawn";

    public static readonly string[] All = [Pending, Answered, Withdrawn];

    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Pending] = [Answered, Withdrawn],
        [Answered] = [],
        [Withdrawn] = []
    };

    public static IReadOnlyList<string> NextOf(string current) =>
        Transitions.TryGetValue(current, out var next) ? next : [];
}
