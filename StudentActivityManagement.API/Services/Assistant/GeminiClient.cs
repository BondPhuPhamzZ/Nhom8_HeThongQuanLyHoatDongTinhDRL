using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>
/// Calls Gemini's REST <c>models/{model}:generateContent</c> endpoint. All Google-specific HTTP code lives here,
/// so moving to another Gemini endpoint (or provider) only changes this class.
/// </summary>
public sealed class GeminiClient
{
    private readonly HttpClient _http;
    private readonly IOptions<GeminiOptions> _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string Model => _options.Value.Model;

    public async Task<GeminiResponse> GenerateContentAsync(JsonObject body, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            throw new GeminiException(GeminiFailure.Configuration, "Gemini:ApiKey is not set.");
        }

        ApplySafetyDefaults(body, options);
        var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
        var url = new Uri(new Uri(baseUrl), $"models/{Uri.EscapeDataString(options.Model)}:generateContent");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
        var started = Stopwatch.GetTimestamp();

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiException(GeminiFailure.Unavailable, $"Gemini did not answer within {options.TimeoutSeconds}s.");
        }
        catch (HttpRequestException exception)
        {
            throw new GeminiException(GeminiFailure.Unavailable, $"Gemini could not be reached: {exception.Message}");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var failure = status switch
                {
                    429 => GeminiFailure.RateLimited,
                    400 or 401 or 403 or 404 => GeminiFailure.Configuration,
                    _ => GeminiFailure.Unavailable
                };
                // Google's error message never contains the key; the question text is not logged.
                _logger.LogWarning("Gemini {Model} returned HTTP {Status}: {Error}", options.Model, status, ReadErrorMessage(text));
                throw new GeminiException(failure, $"Gemini returned HTTP {status}.");
            }

            GeminiResponse parsed;
            try
            {
                parsed = GeminiResponse.Parse(text);
            }
            catch (JsonException exception)
            {
                throw new GeminiException(GeminiFailure.Unavailable, $"Gemini returned an unreadable response: {exception.Message}");
            }

            _logger.LogInformation(
                "Gemini {Model}: {PromptTokens} prompt + {OutputTokens} output tokens, {FunctionCalls} function call(s), finish {FinishReason}, {ElapsedMs} ms",
                options.Model, parsed.PromptTokens, parsed.OutputTokens, parsed.FunctionCalls.Count, parsed.FinishReason,
                (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return parsed;
        }
    }

    private static readonly string[] HarmCategories =
    [
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT"
    ];

    /// <summary>
    /// Every call gets explicit safety settings and an output cap (US-32), unless the caller set its own.
    /// Explicit settings keep the behaviour stable when Google changes its defaults.
    /// </summary>
    private static void ApplySafetyDefaults(JsonObject body, GeminiOptions options)
    {
        if (body["safetySettings"] == null)
        {
            body["safetySettings"] = new JsonArray(HarmCategories.Select(category => (JsonNode?)new JsonObject
            {
                ["category"] = category,
                ["threshold"] = options.SafetyThreshold
            }).ToArray());
        }

        var generation = body["generationConfig"] as JsonObject;
        if (generation == null)
        {
            generation = new JsonObject();
            body["generationConfig"] = generation;
        }

        if (generation["maxOutputTokens"] == null) generation["maxOutputTokens"] = Math.Max(64, options.MaxOutputTokens);
    }

    private static string ReadErrorMessage(string text)
    {
        try
        {
            var error = JsonNode.Parse(text)?["error"];
            return $"{error?["status"]?.GetValue<string>()} {error?["message"]?.GetValue<string>()}".Trim();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return "(non-JSON error body)";
        }
    }
}

public enum GeminiFailure
{
    /// <summary>No key, an invalid key or a request Gemini rejects (HTTP 400/401/403/404).</summary>
    Configuration,

    /// <summary>Quota of the free tier (or of the project) is used up (HTTP 429).</summary>
    RateLimited,

    /// <summary>Timeout, network error or a 5xx from Google.</summary>
    Unavailable
}

public sealed class GeminiException : Exception
{
    public GeminiException(GeminiFailure failure, string message) : base(message) => Failure = failure;

    public GeminiFailure Failure { get; }
}

public sealed record GeminiFunctionCall(string Name, JsonObject Args, string? Id);

/// <summary>The first candidate of a generateContent response.</summary>
public sealed class GeminiResponse
{
    /// <summary>The candidate's <c>content</c> exactly as received; sent back unchanged so thought signatures stay valid.</summary>
    public JsonObject? Content { get; private init; }

    public string Text { get; private init; } = string.Empty;

    public IReadOnlyList<GeminiFunctionCall> FunctionCalls { get; private init; } = [];

    public string? FinishReason { get; private init; }

    public string? BlockReason { get; private init; }

    public int PromptTokens { get; private init; }

    public int OutputTokens { get; private init; }

    public static GeminiResponse Parse(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Empty response.");
        var candidate = root["candidates"] is JsonArray { Count: > 0 } candidates ? candidates[0]?.AsObject() : null;
        var content = candidate?["content"] as JsonObject;

        var text = new StringBuilder();
        var calls = new List<GeminiFunctionCall>();
        if (content?["parts"] is JsonArray parts)
        {
            foreach (var part in parts.OfType<JsonObject>())
            {
                // Thought summaries (thought: true) are not part of the answer.
                if (part["thought"] is JsonValue thought && thought.TryGetValue<bool>(out var isThought) && isThought) continue;
                if (part["text"] is JsonValue value && value.TryGetValue<string>(out var partText)) text.Append(partText);
                if (part["functionCall"] is JsonObject call && call["name"] is JsonValue name && name.TryGetValue<string>(out var callName))
                {
                    var args = call["args"] as JsonObject;
                    calls.Add(new GeminiFunctionCall(
                        callName,
                        args == null ? new JsonObject() : (JsonObject)args.DeepClone(),
                        call["id"] is JsonValue id && id.TryGetValue<string>(out var callId) ? callId : null));
                }
            }
        }

        var usage = root["usageMetadata"] as JsonObject;
        return new GeminiResponse
        {
            Content = content == null ? null : (JsonObject)content.DeepClone(),
            Text = text.ToString(),
            FunctionCalls = calls,
            FinishReason = ReadString(candidate?["finishReason"]),
            BlockReason = ReadString(root["promptFeedback"]?["blockReason"]),
            PromptTokens = ReadInt(usage?["promptTokenCount"]),
            OutputTokens = ReadInt(usage?["candidatesTokenCount"])
        };
    }

    private static string? ReadString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int ReadInt(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
}
