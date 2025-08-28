using Microsoft.AspNetCore.Mvc;

namespace AISearch.Api.Controllers;

/// <summary>
///     Base API controller that provides consistent access to user information and tokens across all API controllers
/// </summary>
public abstract class BaseApiController(IApiUserService userService, ILogger logger) : ControllerBase
{
    protected readonly ILogger _logger = logger;
    protected readonly IApiUserService _userService = userService;

    /// <summary>
    ///     Gets the current user's ID from the JWT token
    /// </summary>
    protected string? GetCurrentUserId()
    {
        return _userService.GetCurrentUserId();
    }

    /// <summary>
    ///     Gets the current user's email from the JWT token
    /// </summary>
    protected string? GetCurrentUserEmail()
    {
        return _userService.GetCurrentUserEmail();
    }

    /// <summary>
    ///     Gets the current user's name from the JWT token
    /// </summary>
    protected string? GetCurrentUserName()
    {
        return _userService.GetCurrentUserName();
    }

    /// <summary>
    ///     Gets the JWT Bearer token from the Authorization header
    /// </summary>
    protected string? GetJwtToken()
    {
        return _userService.GetJwtToken();
    }

    /// <summary>
    ///     Checks if the current user is authenticated
    /// </summary>
    protected bool IsAuthenticated()
    {
        return _userService.IsAuthenticated();
    }

    /// <summary>
    ///     Gets all user claims as a dictionary
    /// </summary>
    protected Dictionary<string, string> GetAllUserClaims()
    {
        return _userService.GetAllClaims();
    }

    /// <summary>
    ///     Gets comprehensive debug information about the current request and authentication state
    /// </summary>
    protected Dictionary<string, object> GetAuthDebugInfo()
    {
        return _userService.GetDebugInfo();
    }

    /// <summary>
    ///     Logs authentication information for debugging purposes
    /// </summary>
    protected void LogAuthenticationInfo(string action)
    {
        var debugInfo = GetAuthDebugInfo();
        _logger.LogInformation("Action: {Action}, Auth Debug: {@AuthDebug}", action, debugInfo);
    }
}