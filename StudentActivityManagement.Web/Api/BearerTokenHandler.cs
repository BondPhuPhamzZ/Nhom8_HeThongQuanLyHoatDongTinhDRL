using System.Net.Http.Headers;

namespace StudentActivityManagement.Web.Api;

/// <summary>Attaches the signed-in user's JWT (kept inside the encrypted auth cookie) to every API request.</summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    public const string TokenClaim = "access_token";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BearerTokenHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
