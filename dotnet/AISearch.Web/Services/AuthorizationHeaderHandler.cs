using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace AISearch.Web.Services;

/// <summary>
///     Enhanced DelegatingHandler that properly acquires and forwards Azure AD access tokens to API requests
///     with automatic token refresh and retry logic for expired tokens
/// </summary>
public class AuthorizationHeaderHandler(
    ITokenAcquisition tokenAcquisition,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthorizationHeaderHandler> logger,
    IConfiguration configuration)
    : DelegatingHandler
{
    private const int MaxRetryAttempts = 2;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("?? AuthorizationHeaderHandler: Processing request to {RequestUri}", request.RequestUri);

        var attempt = 1;
        HttpResponseMessage? response = null;

        while (attempt <= MaxRetryAttempts)
        {
            try
            {
                await AddAuthorizationHeaderAsync(request, attempt);
                response = await base.SendAsync(request, cancellationToken);

                // If we get a 401, the token might be expired - try to refresh and retry
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && attempt < MaxRetryAttempts)
                {
                    logger.LogWarning("?? Received 401 Unauthorized on attempt {Attempt} for {RequestUri} - token may be expired, retrying with fresh token",
                        attempt, request.RequestUri);

                    // Force token refresh for next attempt
                    await RefreshTokenAsync();
                    attempt++;
                    response.Dispose();
                    continue;
                }

                // Success or non-auth error
                break;
            }
            catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException ex)
            {
                logger.LogWarning("?? User interaction required: {Message}", ex.Message);
                // User needs to re-authenticate - don't retry
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "? Error in AuthorizationHeaderHandler for request: {RequestUri} (attempt {Attempt}",
                    request.RequestUri, attempt);

                if (attempt >= MaxRetryAttempts)
                {
                    // Last attempt failed, continue without auth header
                    response = await base.SendAsync(request, cancellationToken);
                    break;
                }
                attempt++;
            }
        }

        if (response != null)
        {
            logger.LogDebug("?? AuthorizationHeaderHandler: API response status: {StatusCode} for {RequestUri}",
                response.StatusCode, request.RequestUri);
        }

        return response ?? await base.SendAsync(request, cancellationToken);
    }

    private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request, int attempt)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            logger.LogDebug("?? User not authenticated, skipping authorization header for {RequestUri}", request.RequestUri);
            return;
        }

        try
        {
            // Get the API scope from configuration
            var apiScope = configuration["DownstreamApi:Scopes"];
            var scopes = !string.IsNullOrEmpty(apiScope)
                ? new[] { apiScope }
                : new[] { "https://graph.microsoft.com/User.Read" };

            logger.LogDebug("?? Acquiring access token for attempt {Attempt} with scopes: {Scopes}",
                attempt, string.Join(", ", scopes));

            // Use TokenAcquisitionOptions to force refresh on retry attempts
            var tokenOptions = attempt > 1
                ? new TokenAcquisitionOptions { ForceRefresh = true }
                : new TokenAcquisitionOptions();

            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user, tokenAcquisitionOptions: tokenOptions);

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                logger.LogDebug("? Added authorization header (attempt {Attempt}) - token length: {TokenLength}",
                    attempt, accessToken.Length);

                // Log token expiry for monitoring
                var expiry = GetTokenExpiry(accessToken);
                if (expiry.HasValue)
                {
                    var timeUntilExpiry = expiry.Value - DateTime.UtcNow;
                    logger.LogDebug("?? Token expires in {Minutes} minutes", timeUntilExpiry.TotalMinutes);
                }
            }
            else
            {
                logger.LogWarning("?? Token acquisition returned null/empty token for {RequestUri} (attempt {Attempt})",
                    request.RequestUri, attempt);
            }
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException)
        {
            // Re-throw this specific exception as it indicates user interaction is required
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Failed to acquire access token for {RequestUri} (attempt {Attempt})",
                request.RequestUri, attempt);
            throw;
        }
    }

    private async Task RefreshTokenAsync()
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return;

            var apiScope = configuration["DownstreamApi:Scopes"];
            var scopes = !string.IsNullOrEmpty(apiScope)
                ? new[] { apiScope }
                : new[] { "https://graph.microsoft.com/User.Read" };

            logger.LogInformation("?? Forcing token refresh...");

            // Force refresh the token
            await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user, tokenAcquisitionOptions: new TokenAcquisitionOptions
            {
                ForceRefresh = true
            });

            logger.LogInformation("? Token refresh completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Failed to refresh token");
        }
    }

    private DateTime? GetTokenExpiry(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            return jsonToken.ValidTo;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse token expiry");
            return null;
        }
    }
}

/// <summary>
///     Alternative implementation for direct header forwarding (when Authorization header is already present)
///     This version directly forwards the Authorization header from the incoming Web request
/// </summary>
public class HeaderForwardingHandler(IHttpContextAccessor httpContextAccessor, ILogger<HeaderForwardingHandler> logger)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;

            // Check if there's already an Authorization header in the incoming request
            if (httpContext?.Request.Headers.ContainsKey("Authorization") == true)
            {
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    // Forward the Authorization header from the incoming request
                    request.Headers.TryAddWithoutValidation("Authorization", authHeader);

                    logger.LogDebug("?? Forwarded Authorization header to API request: {RequestUri}", request.RequestUri);
                }
            }
            else
            {
                logger.LogDebug("?? No Authorization header to forward for API request: {RequestUri}",
                    request.RequestUri);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Error forwarding Authorization header to API request: {RequestUri}",
                request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}