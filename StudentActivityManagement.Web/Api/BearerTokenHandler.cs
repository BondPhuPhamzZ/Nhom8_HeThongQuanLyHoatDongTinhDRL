using System.Net.Http.Headers;

namespace StudentActivityManagement.Web.Api;

/// <summary>
/// Attaches the signed-in user's JWT (kept inside the encrypted auth cookie) to every API request, and passes on the
/// browser's IP address (US-39) so the API's per-IP login limit does not count every Web user as this one server.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    public const string TokenClaim = "access_token";
    private const string ForwardedForHeader = "X-Forwarded-For";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BearerTokenHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var token = httpContext?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // The API trusts this header only from known proxies (loopback by default, ForwardedHeaders:KnownProxies otherwise).
        var clientIp = httpContext?.Connection.RemoteIpAddress;
        if (clientIp != null)
        {
            request.Headers.Remove(ForwardedForHeader);
            request.Headers.TryAddWithoutValidation(ForwardedForHeader, clientIp.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
