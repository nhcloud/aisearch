using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AISearch.Core.Interfaces;
using AISearch.Web.Services;

namespace AISearch.Web.Controllers;

[AllowAnonymous]
[Route("[controller]")]
public class AccountController(
    ILogger<AccountController> logger,
    IUserAuthService userAuthService) : Controller
{
    [HttpGet("SignIn")]
    public IActionResult SignIn(string? error = null, string? returnUrl = null)
    {
        // Log any authentication errors
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("Sign in requested with error: {Error}", error);
            ViewData["ErrorMessage"] = error switch
            {
                "remote_failure" => "Authentication failed due to a remote service error. Please try again.",
                "auth_failed" => "Authentication failed. Please check your credentials and try again.",
                "access_denied" => "Access was denied. You may not have permission to access this application.",
                _ => "An authentication error occurred. Please try again."
            };
        }

        // Check if user is already authenticated
        if (User.Identity?.IsAuthenticated == true)
        {
            logger.LogInformation("User {User} is already authenticated, redirecting to home", User.Identity.Name);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        var redirectUrl = !string.IsNullOrEmpty(returnUrl)
            ? returnUrl
            : Url.Action(nameof(HomeController.Index), "Home");

        logger.LogInformation("Initiating sign in with redirect URL: {RedirectUrl}", redirectUrl);

        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("SignOut")]
    [Authorize]
    public new async Task<IActionResult> SignOut()
    {
        var callbackUrl = Url.Action(nameof(SignedOut), "Account", null, Request.Scheme);

        logger.LogInformation("User {User} signing out", User.Identity?.Name);

        return base.SignOut(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("SignedOut")]
    public IActionResult SignedOut()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            logger.LogWarning("User is still authenticated after sign out, redirecting to sign out again");
            // If the user is still authenticated, redirect to sign out again
            return RedirectToAction(nameof(SignOut));
        }

        logger.LogInformation("User successfully signed out");
        return View();
    }

    [HttpGet("AccessDenied")]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        logger.LogWarning("Access denied for user {User}, return URL: {ReturnUrl}", User.Identity?.Name, returnUrl);

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["IsAuthenticated"] = User.Identity?.IsAuthenticated == true;
        ViewData["UserName"] = User.Identity?.Name;

        return View();
    }

    /// <summary>
    ///     Debug endpoint to check current authentication status
    /// </summary>
    [HttpGet("AuthStatus")]
    public IActionResult AuthStatus()
    {
        var authInfo = new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            UserName = User.Identity?.Name,
            User.Identity?.AuthenticationType,
            Claims = User.Claims?.Select(c => new { c.Type, c.Value }).ToList(),
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        };

        logger.LogInformation("Authentication status check: {@AuthInfo}", authInfo);

        return Json(authInfo);
    }

    /// <summary>
    ///     Provides guidance on how to test API endpoints with authentication
    /// </summary>
    [HttpGet("api-testing-guide")]
    public IActionResult ApiTestingGuide()
    {
        var guide = new
        {
            Title = "API Testing Guide",
            Message = "Here's how to properly test API endpoints that require authentication",

            DirectBrowserAccess = new
            {
                Status = "? Won't Work",
                Reason = "Browser cannot automatically include Authorization header with Bearer token",
                Examples = new[]
                {
                    "https://localhost:7002/api/documents/test",
                    "https://localhost:7002/api/chat/test-auth"
                }
            },

            RecommendedMethods = new object[]
            {
                new
                {
                    Method = "? Through Web Application",
                    Description = "Web app acts as proxy and includes Authorization header",
                    Examples = new[]
                    {
                        "/Chat/TestApiConnection",
                        "/Chat/test-documents",
                        "/Documents/GetDocuments"
                    }
                },
                new
                {
                    Method = "? Anonymous Health Checks",
                    Description = "Test API availability without authentication",
                    Examples = new[]
                    {
                        "https://localhost:7002/api/documents/health",
                        "https://localhost:7002/api/chat/health"
                    }
                },
                new
                {
                    Method = "? Postman/Insomnia",
                    Description = "Add Authorization header manually",
                    HeaderFormat = "Authorization: Bearer {your-jwt-token}"
                },
                new
                {
                    Method = "? Swagger UI",
                    Description = "Use Swagger's authentication feature",
                    Url = "https://localhost:7002/swagger"
                }
            },

            AuthenticationFlow = new
            {
                Step1 = "User signs in through Web application (/Account/SignIn)",
                Step2 = "Web app acquires JWT token from Azure AD",
                Step3 = "Web app includes Bearer token in API requests",
                Step4 = "API validates token and provides user context"
            },

            DebuggingEndpoints = new object[]
            {
                new { Endpoint = "/Account/AuthStatus", Purpose = "Check Web app authentication" },
                new { Endpoint = "/Account/api-testing-guide", Purpose = "This guide" },
                new { Endpoint = "/Chat/TestApiConnection", Purpose = "Test Web?API token flow" }
            },

            CurrentUser = new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                UserName = User.Identity?.Name,
                CanTestAPIs = User.Identity?.IsAuthenticated == true
                    ? "Yes - use Web app endpoints"
                    : "No - please sign in first"
            }
        };

        return Json(guide);
    }

    /// <summary>
    ///     Simple test endpoint to verify routing is working
    /// </summary>
    [HttpGet("test-route")]
    public IActionResult TestRoute()
    {
        return Json(new
        {
            Message = "? Routing is working!",
            Controller = "Account",
            Action = "TestRoute",
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            UserName = User.Identity?.Name,
            Timestamp = DateTime.UtcNow,
            RequestPath = HttpContext.Request.Path,
            RequestUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{HttpContext.Request.Path}",
            AvailableRoutes = new[]
            {
                "/Account/TokenTest",
                "/Account/token-test",
                "/Account/TokenInfo",
                "/Account/token-info",
                "/Account/AuthStatus",
                "/Account/api-testing-guide",
                "/Account/test-route",
                "/Account/debug-routes"
            }
        });
    }

    /// <summary>
    ///     Comprehensive Bearer Token Test Page - Shows complete token information and testing interface
    ///     Route: /Account/TokenTest
    /// </summary>
    [HttpGet("TokenTest")]
    public IActionResult TokenTest()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction(nameof(SignIn));

        return View();
    }

    /// <summary>
    ///     Alternative route for token test with kebab-case
    ///     Route: /Account/token-test
    /// </summary>
    [HttpGet("token-test")]
    public IActionResult TokenTestKebab()
    {
        return TokenTest(); // Just redirect to the main TokenTest action
    }

    /// <summary>
    ///     API endpoint that returns comprehensive token information for testing
    ///     Route: /Account/TokenInfo
    /// </summary>
    [HttpGet("TokenInfo")]
    public async Task<IActionResult> TokenInfo()
    {
        return await GetTokenInfo();
    }

    /// <summary>
    ///     Alternative route for token info with kebab-case
    ///     Route: /Account/token-info
    /// </summary>
    [HttpGet("token-info")]
    public async Task<IActionResult> TokenInfoKebab()
    {
        return await GetTokenInfo();
    }

    /// <summary>
    ///     Internal method that actually gets the token information
    /// </summary>
    private async Task<IActionResult> GetTokenInfo()
    {
        try
        {
            // Get token information
            var token = userAuthService.GetAuthorizationToken();
            var isAuthenticated = userAuthService.IsAuthenticated();
            var userId = userAuthService.GetCurrentUserId();
            var userEmail = userAuthService.GetCurrentUserEmail();
            var userName = userAuthService.GetCurrentUserName();

            // Parse JWT token if available
            string? tokenHeader = null;
            string? tokenPayload = null;
            string? tokenSignature = null;
            Dictionary<string, object>? decodedPayload = null;

            if (!string.IsNullOrEmpty(token))
                try
                {
                    var parts = token.Split('.');
                    if (parts.Length == 3)
                    {
                        tokenHeader = parts[0];
                        tokenPayload = parts[1];
                        tokenSignature = parts[2];

                        // Decode payload (base64url)
                        var payloadBytes =
                            Convert.FromBase64String(
                                AddBase64Padding(tokenPayload.Replace('-', '+').Replace('_', '/')));
                        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                        decodedPayload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse JWT token");
                }

            var tokenInfo = new
            {
                // Authentication Status
                IsAuthenticated = isAuthenticated,
                UserId = userId,
                UserEmail = userEmail,
                UserName = userName,

                // Token Information
                HasToken = !string.IsNullOrEmpty(token),
                TokenLength = token?.Length ?? 0,
                TokenPreview = token != null ? $"{token.Substring(0, Math.Min(50, token.Length))}..." : null,

                // Full token (for testing - be careful in production!)
                FullToken = token,

                // JWT Parts
                TokenStructure = new
                {
                    Header = tokenHeader,
                    Payload = tokenPayload,
                    Signature = tokenSignature,
                    IsValidJWT = !string.IsNullOrEmpty(tokenHeader) && !string.IsNullOrEmpty(tokenPayload) &&
                                 !string.IsNullOrEmpty(tokenSignature)
                },

                // Decoded payload
                DecodedPayload = decodedPayload,

                // Claims from HttpContext
                Claims = User?.Claims?.Select(c => new { c.Type, c.Value }).ToList(),

                // Headers for API testing
                AuthorizationHeader = !string.IsNullOrEmpty(token) ? $"Bearer {token}" : null,

                // Timestamps
                TokenAcquiredAt = DateTime.UtcNow,

                // Testing URLs
                TestEndpoints = new
                {
                    ApiDocumentsHealth = "https://localhost:7002/api/documents/health",
                    ApiChatHealth = "https://localhost:7002/api/chat/health",
                    ApiDocumentsTest = "https://localhost:7002/api/documents/test",
                    ApiChatTest = "https://localhost:7002/api/chat/test-auth",
                    WebChatTest = "/Chat/TestApiConnection",
                    WebDocumentsTest = "/Chat/test-documents"
                }
            };

            return Json(tokenInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting token information");
            return BadRequest(new { error = "Failed to get token information", details = ex.Message });
        }
    }

    private static string AddBase64Padding(string base64)
    {
        var padding = 4 - base64.Length % 4;
        if (padding < 4) base64 += new string('=', padding);
        return base64;
    }

    /// <summary>
    ///     Route debugging page to help identify routing issues
    /// </summary>
    [HttpGet("debug-routes")]
    public IActionResult DebugRoutes()
    {
        var routeInfo = new
        {
            RequestPath = HttpContext.Request.Path,
            RequestMethod = HttpContext.Request.Method,
            QueryString = HttpContext.Request.QueryString.ToString(),
            Controller = ControllerContext.ActionDescriptor.ControllerName,
            Action = ControllerContext.ActionDescriptor.ActionName,
            RouteValues = ControllerContext.RouteData.Values.ToDictionary(x => x.Key, x => x.Value?.ToString()),
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            UserName = User.Identity?.Name,

            RoutingConfiguration = new
            {
                AttributeRoutingEnabled = "MapControllers() is now enabled",
                ConventionalRoutingEnabled = "MapControllerRoute() is enabled as fallback",
                ControllerRoute = "[Route(\"[controller]\")]",
                ExpectedBehavior = "Attribute routes should work alongside conventional routes"
            },

            AllRouteActions = new[]
            {
                new
                {
                    Action = "SignIn", Route = "/Account/SignIn", Method = "GET", Description = "Sign in page",
                    Type = "Conventional"
                },
                new
                {
                    Action = "SignOut", Route = "/Account/SignOut", Method = "GET", Description = "Sign out action",
                    Type = "Conventional"
                },
                new
                {
                    Action = "AuthStatus", Route = "/Account/AuthStatus", Method = "GET",
                    Description = "Authentication status (JSON)", Type = "Conventional"
                },
                new
                {
                    Action = "TokenTest", Route = "/Account/TokenTest", Method = "GET",
                    Description = "Bearer token test page (Pascal case)", Type = "Conventional"
                },
                new
                {
                    Action = "TokenTestKebab", Route = "/Account/token-test", Method = "GET",
                    Description = "Bearer token test page (kebab case)", Type = "Attribute"
                },
                new
                {
                    Action = "TokenInfo", Route = "/Account/TokenInfo", Method = "GET",
                    Description = "Token information API (Pascal case)", Type = "Conventional"
                },
                new
                {
                    Action = "TokenInfoKebab", Route = "/Account/token-info", Method = "GET",
                    Description = "Token information API (kebab case)", Type = "Attribute"
                },
                new
                {
                    Action = "ApiTestingGuide", Route = "/Account/api-testing-guide", Method = "GET",
                    Description = "API testing guide (JSON)", Type = "Attribute"
                },
                new
                {
                    Action = "TestRoute", Route = "/Account/test-route", Method = "GET",
                    Description = "Simple routing test (JSON)", Type = "Attribute"
                },
                new
                {
                    Action = "DebugRoutes", Route = "/Account/debug-routes", Method = "GET",
                    Description = "This debugging page", Type = "Attribute"
                }
            },

            TestUrls = new
            {
                ConventionalRoutes = new[]
                {
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/SignIn",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/AuthStatus",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/TokenTest",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/TokenInfo"
                },
                AttributeRoutes = new[]
                {
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/token-test",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/token-info",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/test-route",
                    $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Account/debug-routes"
                }
            },

            BaseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}",
            Timestamp = DateTime.UtcNow,
            FixApplied = "Added MapControllers() and [Route(\"[controller]\")] attribute"
        };

        return Json(routeInfo);
    }

    [HttpGet("token-status")]
    public async Task<IActionResult> TokenStatus()
    {
        if (!userAuthService.IsAuthenticated())
            return Json(new { isAuthenticated = false, message = "User not authenticated" });

        try
        {
            var tokenRefreshService = HttpContext.RequestServices.GetService<ITokenRefreshService>();
            var currentToken = userAuthService.GetAuthorizationToken();
            
            var tokenInfo = new
            {
                isAuthenticated = true,
                hasToken = !string.IsNullOrEmpty(currentToken),
                tokenLength = currentToken?.Length ?? 0,
                tokenExpiry = GetTokenExpiry(currentToken),
                isExpired = tokenRefreshService != null ? await tokenRefreshService.IsTokenExpiredOrExpiringSoonAsync(currentToken) : (bool?)null,
                userInfo = new
                {
                    userId = userAuthService.GetCurrentUserId(),
                    userEmail = userAuthService.GetCurrentUserEmail(),
                    userName = userAuthService.GetCurrentUserName()
                },
                tokenPreview = !string.IsNullOrEmpty(currentToken) 
                    ? currentToken.Substring(0, Math.Min(20, currentToken.Length)) + "..."
                    : null,
                timestamp = DateTime.UtcNow
            };

            return Json(tokenInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving token status");
            return Json(new 
            { 
                isAuthenticated = userAuthService.IsAuthenticated(),
                hasToken = false,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!userAuthService.IsAuthenticated())
            return Json(new { success = false, message = "User not authenticated" });

        try
        {
            var tokenRefreshService = HttpContext.RequestServices.GetService<ITokenRefreshService>();
            if (tokenRefreshService == null)
            {
                return Json(new { success = false, message = "Token refresh service not available" });
            }

            await tokenRefreshService.RefreshTokenAsync();
            var newToken = userAuthService.GetAuthorizationToken();

            return Json(new
            {
                success = true,
                message = "Token refreshed successfully",
                hasNewToken = !string.IsNullOrEmpty(newToken),
                newTokenLength = newToken?.Length ?? 0,
                newTokenExpiry = GetTokenExpiry(newToken),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException)
        {
            return Json(new 
            { 
                success = false, 
                message = "User interaction required - please sign in again",
                requiresReauth = true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing token");
            return Json(new 
            { 
                success = false, 
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    private DateTime? GetTokenExpiry(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            return jsonToken.ValidTo;
        }
        catch
        {
            return null;
        }
    }
}