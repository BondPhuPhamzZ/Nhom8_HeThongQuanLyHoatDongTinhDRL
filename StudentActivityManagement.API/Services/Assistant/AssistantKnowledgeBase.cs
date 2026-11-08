namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>The system rules in the embedded AssistantKnowledge.md, shared by the student assistant and staff AI tools.</summary>
public static class AssistantKnowledgeBase
{
    private static readonly Lazy<string> Content = new(() =>
    {
        using var stream = typeof(AssistantKnowledgeBase).Assembly.GetManifestResourceStream("AssistantKnowledge.md")
            ?? throw new InvalidOperationException("Embedded resource AssistantKnowledge.md is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string Text => Content.Value;
}
