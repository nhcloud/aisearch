using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AISearch.Api.Services;

public interface IApiUserService
{
    string? GetCurrentUserId();
    string? GetCurrentUserEmail();
    string? GetCurrentUserName();
    string? GetJwtToken();
    bool IsAuthenticated();
    ClaimsPrincipal? GetCurrentUser();
    Dictionary<string, string> GetAllClaims();
    string? GetBearerToken();
    Dictionary<string, object> GetDebugInfo();
}

public class ApiUserService(IHttpContextAccessor httpContextAccessor, ILogger<ApiUserService> logger)
    : IApiUserService
{
    public string? GetCurrentUserId()
    {
        var user = GetCurrentUser();
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                     user?.FindFirst("oid")?.Value ??
                     user?.FindFirst("sub")?.Value;

        // If no user ID from claims, try to extract from bearer token
        if (string.IsNullOrEmpty(userId))
        {
            var token = GetBearerToken();
            if (!string.IsNullOrEmpty(token) && IsSimpleInternalToken(token))
                try
                {
                    var decoded = Convert.FromBase64String(token);
                    var content = Encoding.UTF8.GetString(decoded);

                    if (content.StartsWith("user:")) return content.Substring(5); // Remove "user:" prefix

                    if (content.Contains("\"sub\""))
                    {
                        // Parse simple JSON to extract sub claim
                        var jsonDoc = JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("sub", out var subElement))
                            return subElement.GetString();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to extract user ID from simple token");
                }
        }

        return userId;
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

    public string? GetJwtToken()
    {
        return GetBearerToken();
    }

    public string? GetBearerToken()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Request.Headers.ContainsKey("Authorization") == true)
        {
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                logger.LogDebug("Successfully extracted Bearer token from Authorization header. Length: {TokenLength}",
                    token.Length);

                // Check if this is a simple base64 encoded token (internal token)
                if (IsSimpleInternalToken(token))
                    logger.LogDebug("Detected simple internal token format");
                else
                    logger.LogDebug("Detected standard JWT token format");

                return token;
            }

            if (!string.IsNullOrEmpty(authHeader))
                logger.LogWarning("Authorization header found but does not start with 'Bearer ': {AuthHeader}",
                    authHeader.Substring(0, Math.Min(20, authHeader.Length)));
        }
        else
        {
            logger.LogDebug("No Authorization header found in request");
        }

        return null;
    }

    public bool IsAuthenticated()
    {
        // First check if there's a standard authenticated user
        var standardAuth = GetCurrentUser()?.Identity?.IsAuthenticated == true;
        if (standardAuth) return true;

        // If no standard authentication, check for simple internal token
        var token = GetBearerToken();
        if (!string.IsNullOrEmpty(token))
        {
            if (IsSimpleInternalToken(token))
            {
                logger.LogDebug("User authenticated via simple internal token");
                return true;
            }

            // For JWT tokens, we might still be authenticated even if claims aren't parsed yet
            if (token.Contains('.') && token.Split('.').Length == 3)
            {
                logger.LogDebug("User authenticated via JWT token");
                return true;
            }
        }

        return false;
    }

    public ClaimsPrincipal? GetCurrentUser()
    {
        return httpContextAccessor.HttpContext?.User;
    }

    public Dictionary<string, string> GetAllClaims()
    {
        return GetCurrentUser()?.Claims?.ToDictionary(c => c.Type, c => c.Value) ?? new Dictionary<string, string>();
    }

    public Dictionary<string, object> GetDebugInfo()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var authHeader = httpContext?.Request.Headers["Authorization"].FirstOrDefault();
        var token = GetBearerToken();
        var isSimpleToken = !string.IsNullOrEmpty(token) && IsSimpleInternalToken(token);

        return new Dictionary<string, object>
        {
            ["HasHttpContext"] = httpContext != null,
            ["HasAuthHeader"] = !string.IsNullOrEmpty(authHeader),
            ["AuthHeaderValue"] = authHeader ?? "None",
            ["AuthHeaderLength"] = authHeader?.Length ?? 0,
            ["AuthHeaderStartsWithBearer"] = authHeader?.StartsWith("Bearer ") == true,
            ["HasBearerToken"] = !string.IsNullOrEmpty(token),
            ["TokenPreview"] = token != null ? $"{token.Substring(0, Math.Min(20, token.Length))}..." : "None",
            ["TokenLength"] = token?.Length ?? 0,
            ["TokenType"] = isSimpleToken ? "Simple Internal Token" :
                token?.Contains('.') == true ? "JWT Token" : "Unknown",
            ["IsSimpleInternalToken"] = isSimpleToken,
            ["IsAuthenticated"] = IsAuthenticated(),
            ["UserId"] = GetCurrentUserId(),
            ["UserEmail"] = GetCurrentUserEmail(),
            ["UserName"] = GetCurrentUserName(),
            ["ClaimsCount"] = GetAllClaims().Count,
            ["StandardUserAuthenticated"] = GetCurrentUser()?.Identity?.IsAuthenticated == true,
            ["RequestPath"] = httpContext?.Request.Path.ToString(),
            ["RequestMethod"] = httpContext?.Request.Method,
            ["RequestHeaders"] = httpContext?.Request.Headers.Keys.ToList(),
            ["ForwardedFrom"] = "Web Application via DelegatingHandler",
            ["AuthenticationMethod"] = isSimpleToken ? "Simple Internal" : "Standard Azure AD",
            ["Timestamp"] = DateTime.UtcNow
        };
    }

    private bool IsSimpleInternalToken(string token)
    {
        try
        {
            // Check if it's a simple base64 encoded JSON (not a JWT with dots)
            if (!token.Contains('.') && token.Length % 4 == 0)
            {
                var decoded = Convert.FromBase64String(token);
                var json = Encoding.UTF8.GetString(decoded);
                return json.Contains("sub") || json.Contains("user:");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}