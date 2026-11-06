namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>
/// Gemini API settings (US-31). The key is never stored in appsettings: set it with
/// <c>dotnet user-secrets set "Gemini:ApiKey" "..."</c> locally, or the <c>Gemini__ApiKey</c> environment variable.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }

    /// <summary>Model id from https://ai.google.dev/gemini-api/docs/models (a free-tier Flash or Flash-Lite model).</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    /// <summary>Timeout of one generateContent call.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Limits of the training-point assistant (US-31).</summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    public bool Enabled { get; set; } = true;

    /// <summary>Rounds of tool calls per question; the round after the last one must answer in text.</summary>
    public int MaxToolRounds { get; set; } = 4;

    /// <summary>Messages of the conversation the client may send back (user and assistant turns).</summary>
    public int MaxMessages { get; set; } = 12;

    public int MaxMessageLength { get; set; } = 1000;

    /// <summary>Questions one student may ask per minute; protects the shared free Gemini quota.</summary>
    public int RequestsPerMinute { get; set; } = 6;
}
