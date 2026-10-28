namespace StudentActivityManagement.API.Models;

/// <summary>
/// Activity lifecycle. Completed and Cancelled are final; Published may only go back to Draft
/// while nobody has ever registered (checked by the service, not here).
/// </summary>
public static class ActivityStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, Published, Ongoing, Completed, Cancelled];

    /// <summary>Statuses a new activity may start in.</summary>
    public static readonly string[] Initial = [Draft, Published];

    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Draft] = [Published, Cancelled],
        [Published] = [Ongoing, Cancelled, Draft],
        [Ongoing] = [Completed, Cancelled],
        [Completed] = [],
        [Cancelled] = []
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return All.FirstOrDefault(status => status.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> NextOf(string current) =>
        Transitions.TryGetValue(current, out var next) ? next : [];

    public static bool IsFinal(string status) => NextOf(status).Count == 0;
}
