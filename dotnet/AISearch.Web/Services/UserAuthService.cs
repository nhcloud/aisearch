using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using System.IdentityModel.Tokens.Jwt;
using AISearch.Core.Interfaces;

namespace AISearch.Web.Services;

public interface ITokenRefreshService
{
    Task<string?> GetFreshAccessTokenAsync();
    Task<bool> IsTokenExpiredOrExpiringSoonAsync(string? token = null);
    Task RefreshTokenAsync();
}

public class TokenRefreshService(
    IHttpContextAccessor httpContextAccessor,
    ITokenAcquisition tokenAcquisition,
    ILogger<TokenRefreshService> logger,
    IConfiguration configuration)
    : ITokenRefreshService
{
    private const int TokenExpiryBufferMinutes = 5; // Refresh token 5 minutes before expiry

    public async Task<string?> GetFreshAccessTokenAsync()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;

        try
        {
            // First, check if current token is expired or expiring soon
            var currentToken = await GetCurrentTokenAsync();
            var needsRefresh = await IsTokenExpiredOrExpiringSoonAsync(currentToken);
            
            if (needsRefresh)
            {
                logger.LogInformation("?? Token is expired or expiring soon, refreshing...");
                await RefreshTokenAsync();
            }

            // Get fresh token using MSAL
            var apiScope = configuration["DownstreamApi:Scopes"];
            var scopes = !string.IsNullOrEmpty(apiScope) ? new[] { apiScope } : new[] { "https://graph.microsoft.com/User.Read" };

            logger.LogDebug("?? Acquiring fresh access token with scopes: {Scopes}", string.Join(", ", scopes));

            var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                logger.LogInformation("? Successfully acquired fresh access token. Length: {Length}", accessToken.Length);
                
                // Log token expiry info for monitoring
                var tokenExpiry = GetTokenExpiry(accessToken);
                if (tokenExpiry.HasValue)
                {
                    var timeUntilExpiry = tokenExpiry.Value - DateTime.UtcNow;
                    logger.LogDebug("?? New token expires at: {Expiry} (in {Minutes} minutes)", 
                        tokenExpiry.Value, timeUntilExpiry.TotalMinutes);
                }
                
                return accessToken;
            }
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException ex)
        {
            logger.LogWarning("?? User interaction required for token refresh: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Failed to acquire fresh access token");
        }

        // Fallback to cached tokens if MSAL fails
        return await GetCurrentTokenAsync();
    }

    public async Task<bool> IsTokenExpiredOrExpiringSoonAsync(string? token = null)
    {
        try
        {
            token ??= await GetCurrentTokenAsync();
            if (string.IsNullOrEmpty(token)) return true;

            var expiry = GetTokenExpiry(token);
            if (!expiry.HasValue) return true;

            var timeUntilExpiry = expiry.Value - DateTime.UtcNow;
            var isExpiringSoon = timeUntilExpiry.TotalMinutes <= TokenExpiryBufferMinutes;
            
            if (isExpiringSoon)
            {
                logger.LogInformation("? Token expires in {Minutes} minutes, needs refresh", timeUntilExpiry.TotalMinutes);
            }
            
            return isExpiringSoon;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Error checking token expiry");
            return true; // Assume expired if we can't check
        }
    }

    public async Task RefreshTokenAsync()
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return;

            logger.LogInformation("?? Forcing token refresh...");

            // Clear the token cache to force a refresh
            var apiScope = configuration["DownstreamApi:Scopes"];
            var scopes = !string.IsNullOrEmpty(apiScope) ? new[] { apiScope } : new[] { "https://graph.microsoft.com/User.Read" };
            
            // This will force MSAL to get a new token using the refresh token
            await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user, tokenAcquisitionOptions: new TokenAcquisitionOptions
            {
                ForceRefresh = true
            });

            logger.LogInformation("? Token refresh completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Failed to refresh token");
            throw;
        }
    }

    private async Task<string?> GetCurrentTokenAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        try
        {
            // Try to get cached access token first
            var accessToken = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                return accessToken;
            }

            // Fallback to ID token
            var idToken = await httpContext.GetTokenAsync("id_token");
            return idToken;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get current token");
            return null;
        }
    }

    private DateTime? GetTokenExpiry(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
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

public class EnhancedUserAuthService(
    IHttpContextAccessor httpContextAccessor,
    ITokenRefreshService tokenRefreshService,
    ILogger<EnhancedUserAuthService> logger)
    : IUserAuthService
{
    public string? GetAuthorizationToken()
    {
        try
        {
            // Use async method synchronously (not ideal but required by interface)
            return GetAuthorizationTokenAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "? Failed to get authorization token");
            return null;
        }
    }

    public async Task<string?> GetAuthorizationTokenAsync()
    {
        var user = GetCurrentUser();
        if (user?.Identity?.IsAuthenticated != true) return null;

        try
        {
            var freshToken = await tokenRefreshService.GetFreshAccessTokenAsync();
            if (!string.IsNullOrEmpty(freshToken))
            {
                logger.LogDebug("? Successfully retrieved fresh authorization token");
                return freshToken;
            }
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException)
        {
            logger.LogWarning("?? User needs to re-authenticate - redirecting to sign-in");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "?? Failed to get fresh token, user may need to re-authenticate");
        }

        return null;
    }

    public string? GetCurrentUserId()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user?.FindFirst("oid")?.Value ??
               user?.FindFirst("sub")?.Value;
    }

    public string? GetCurrentUserEmail()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Email)?.Value ??
               user?.FindFirst("email")?.Value ??
               user?.FindFirst("preferred_username")?.Value;
    }

    public string? GetCurrentUserName()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Name)?.Value ??
               user?.FindFirst("name")?.Value ??
               user?.FindFirst(ClaimTypes.GivenName)?.Value;
    }

    public bool IsAuthenticated()
    {
        return GetCurrentUser()?.Identity?.IsAuthenticated == true;
    }

    public ClaimsPrincipal? GetCurrentUser()
    {
        return httpContextAccessor.HttpContext?.User;
    }
}

// Keep existing UserAuthService with enhanced token refresh logic
public class UserAuthService(
    IHttpContextAccessor httpContextAccessor,
    ITokenAcquisition tokenAcquisition,
    ILogger<UserAuthService> logger,
    IConfiguration configuration)
    : IUserAuthService
{
    public string? GetAuthorizationToken()
    {
        var user = GetCurrentUser();
        if (user?.Identity?.IsAuthenticated != true) return null;

        // Try MSAL token acquisition with automatic refresh
        try
        {
            var apiScope = configuration["DownstreamApi:Scopes"];
            var scopes = !string.IsNullOrEmpty(apiScope) ? new[] { apiScope } : new[] { "https://graph.microsoft.com/User.Read" };

            // MSAL will automatically handle token refresh if needed
            var accessToken = tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: user).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(accessToken))
            {
                logger.LogDebug("? Successfully acquired access token silently. Length: {Length}", accessToken.Length);
                return accessToken;
            }
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException ex)
        {
            logger.LogWarning("?? MSAL requires user interaction: {Message}", ex.Message);
            // This exception means the user needs to re-authenticate
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "?? MSAL token acquisition failed, trying fallback methods");
        }

        // Fallback to cached tokens
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return null;

        try
        {
            var cachedToken = httpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(cachedToken))
            {
                // Check if token is expired
                if (IsTokenExpired(cachedToken))
                {
                    logger.LogWarning("? Cached access token is expired");
                    return null;
                }

                logger.LogDebug("? Using cached access token");
                return cachedToken;
            }

            var idToken = httpContext.GetTokenAsync("id_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(idToken))
            {
                if (IsTokenExpired(idToken))
                {
                    logger.LogWarning("? Cached ID token is expired");
                    return null;
                }

                logger.LogDebug("? Using ID token as fallback");
                return idToken;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get cached tokens");
        }

        // Check claims as last resort
        var tokenClaims = new[] { "access_token", "id_token" };
        foreach (var claimType in tokenClaims)
        {
            var tokenClaim = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(tokenClaim))
            {
                if (IsTokenExpired(tokenClaim))
                {
                    logger.LogWarning("? Token from claims is expired");
                    continue;
                }

                logger.LogDebug("? Using {ClaimType} from user claims", claimType);
                return tokenClaim;
            }
        }

        logger.LogWarning("? All token acquisition methods failed - user may need to re-authenticate");
        return null;
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            var expiry = jsonToken.ValidTo;
            var isExpired = expiry <= DateTime.UtcNow.AddMinutes(1); // 1 minute buffer
            
            if (isExpired)
            {
                logger.LogDebug("? Token expired at: {Expiry}", expiry);
            }
            
            return isExpired;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse token expiry, assuming expired");
            return true;
        }
    }

    public string? GetCurrentUserId()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user?.FindFirst("oid")?.Value ??
               user?.FindFirst("sub")?.Value;
    }

    public string? GetCurrentUserEmail()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Email)?.Value ??
               user?.FindFirst("email")?.Value ??
               user?.FindFirst("preferred_username")?.Value;
    }

    public string? GetCurrentUserName()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Name)?.Value ??
               user?.FindFirst("name")?.Value ??
               user?.FindFirst(ClaimTypes.GivenName)?.Value;
    }

    public bool IsAuthenticated()
    {
        return GetCurrentUser()?.Identity?.IsAuthenticated == true;
    }

    public ClaimsPrincipal? GetCurrentUser()
    {
        return httpContextAccessor.HttpContext?.User;
    }
}

public class SimpleUserAuthService(IHttpContextAccessor httpContextAccessor, ILogger<SimpleUserAuthService> logger)
    : IUserAuthService
{
    public string? GetAuthorizationToken()
    {
        var user = GetCurrentUser();
        if (user?.Identity?.IsAuthenticated != true) return null;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return null;

        try
        {
            var accessToken = httpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(accessToken))
            {
                // Check if token is expired
                if (IsTokenExpired(accessToken))
                {
                    logger.LogWarning("? Cached access token is expired - user needs to re-authenticate");
                    return null;
                }

                logger.LogDebug("? Using cached access token from authentication");
                return accessToken;
            }

            var idToken = httpContext.GetTokenAsync("id_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(idToken))
            {
                if (IsTokenExpired(idToken))
                {
                    logger.LogWarning("? Cached ID token is expired - user needs to re-authenticate");
                    return null;
                }

                logger.LogDebug("? Using cached ID token as fallback");
                return idToken;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get cached tokens");
        }

        logger.LogWarning("? No cached tokens available - user may need to re-authenticate");
        return null;
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            var expiry = jsonToken.ValidTo;
            var isExpired = expiry <= DateTime.UtcNow.AddMinutes(1); // 1 minute buffer
            
            if (isExpired)
            {
                logger.LogDebug("? Token expired at: {Expiry}", expiry);
            }
            
            return isExpired;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse token expiry, assuming expired");
            return true;
        }
    }

    public string? GetCurrentUserId()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user?.FindFirst("oid")?.Value ??
               user?.FindFirst("sub")?.Value;
    }

    public string? GetCurrentUserEmail()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Email)?.Value ??
               user?.FindFirst("email")?.Value ??
               user?.FindFirst("preferred_username")?.Value;
    }

    public string? GetCurrentUserName()
    {
        var user = GetCurrentUser();
        return user?.FindFirst(ClaimTypes.Name)?.Value ??
               user?.FindFirst("name")?.Value ??
               user?.FindFirst(ClaimTypes.GivenName)?.Value;
    }

    public bool IsAuthenticated()
    {
        return GetCurrentUser()?.Identity?.IsAuthenticated == true;
    }

    public ClaimsPrincipal? GetCurrentUser()
    {
        return httpContextAccessor.HttpContext?.User;
    }
}