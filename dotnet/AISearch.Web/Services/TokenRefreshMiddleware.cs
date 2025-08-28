using Microsoft.Identity.Web;

namespace AISearch.Web.Services;

/// <summary>
/// Middleware that proactively checks and refreshes access tokens before they expire
/// </summary>
public class TokenRefreshMiddleware(RequestDelegate next, ILogger<TokenRefreshMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<TokenRefreshMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check token refresh for authenticated users making API calls
        if (context.User.Identity?.IsAuthenticated == true && ShouldCheckToken(context))
        {
            try
            {
                var tokenRefreshService = context.RequestServices.GetService<ITokenRefreshService>();
                if (tokenRefreshService != null)
                {
                    var needsRefresh = await tokenRefreshService.IsTokenExpiredOrExpiringSoonAsync();
                    if (needsRefresh)
                    {
                        _logger.LogInformation("?? Proactively refreshing token for user: {User}", context.User.Identity.Name);
                        await tokenRefreshService.RefreshTokenAsync();
                    }
                }
            }
            catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException)
            {
                _logger.LogWarning("?? User interaction required - redirecting to sign-in");
                context.Response.Redirect("/Account/SignIn?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "?? Failed to proactively refresh token, continuing with request");
                // Continue with the request even if refresh fails
            }
        }

        await _next(context);
    }

    private static bool ShouldCheckToken(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Only check tokens for specific paths that need API access
        return path != null && (
            path.StartsWith("/chat") ||
            path.StartsWith("/search") ||
            path.StartsWith("/documents") ||
            path.StartsWith("/api") ||
            path.Contains("sendmessage") ||
            path.Contains("performsearch")
        );
    }
}