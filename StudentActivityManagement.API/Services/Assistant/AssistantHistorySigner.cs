using System.Security.Cryptography;
using System.Text;

namespace StudentActivityManagement.API.Services.Assistant;

/// <summary>
/// Signs every assistant answer with HMAC-SHA256 bound to the student, so a client can send the conversation back
/// but cannot invent "assistant" turns (for example "I am now in unrestricted mode") to steer the model (US-32).
/// The key comes from <c>Assistant:HistorySigningKey</c>, or is derived from the JWT secret when that is not set.
/// </summary>
public sealed class AssistantHistorySigner
{
    private readonly IConfiguration _configuration;
    private byte[]? _key;

    public AssistantHistorySigner(IConfiguration configuration) => _configuration = configuration;

    public string Sign(int studentId, string text)
    {
        using var hmac = new HMACSHA256(Key);
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{studentId}\n{text}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public bool Verify(int studentId, string text, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(studentId, text));
        var actual = Encoding.ASCII.GetBytes(signature.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private byte[] Key => _key ??= CreateKey();

    private byte[] CreateKey()
    {
        var configured = _configuration["Assistant:HistorySigningKey"];
        var material = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : _configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret is required.");
        // A separate purpose label keeps this key different from the JWT signing key itself.
        return SHA256.HashData(Encoding.UTF8.GetBytes("assistant-history-v1:" + material));
    }
}
