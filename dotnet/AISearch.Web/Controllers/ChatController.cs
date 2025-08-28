using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using AISearch.Core.Interfaces;
using ChatMessage = AISearch.Core.Models.ChatMessage;
using ChatResponse = AISearch.Core.Models.ChatResponse;

namespace AISearch.Web.Controllers;

[Authorize]
public class ChatController(
    IHttpClientFactory httpClientFactory,
    IUserAuthService authService,
    ILogger<ChatController> logger)
    : Controller
{
    private HttpClient CreateApiClientWithAuth()
    {
        var client = httpClientFactory.CreateClient("APIClient");
        
        // The base address and authorization header are now configured via DI
        // Log token information for debugging
        var token = authService.GetAuthorizationToken();
        logger.LogInformation("🔍 CreateApiClient: HasToken: {HasToken}, TokenLength: {TokenLength}",
            !string.IsNullOrEmpty(token), token?.Length ?? 0);

        return client;
    }

    public IActionResult Index()
    {
        if (!authService.IsAuthenticated()) return RedirectToAction("SignIn", "Account");
        ViewData["Title"] = "AI Chat";
        ViewData["UserName"] = authService.GetCurrentUserName();
        ViewData["UserEmail"] = authService.GetCurrentUserEmail();
        return View();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Temporarily disable to test if this is the issue
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest webRequest)
    {
        try
        {
            logger.LogInformation("💬 SendMessage: Received request with RequireSecurityTrimming: {RequireSecurityTrimming}", 
                webRequest.RequireSecurityTrimming);

            // Check authentication
            if (!authService.IsAuthenticated())
            {
                logger.LogWarning("💬 SendMessage: User not authenticated");
                return Json(new { error = "User not authenticated. Please sign in again.", requiresAuth = true });
            }

            var token = authService.GetAuthorizationToken();
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("💬 SendMessage: No authorization token available");
                return Json(new { error = "Authorization token not available. Please sign in again.", requiresAuth = true });
            }

            var coreRequest = new Core.Models.ChatRequest
            {
                Message = webRequest.Message,
                Token = token,
                RequireSecurityTrimming = webRequest.RequireSecurityTrimming,
                ChatHistory = webRequest.ChatHistory.Select(h => new Core.Models.ChatMessage
                {
                    Role = h.Role,
                    Content = h.Content
                }).ToList(),
                SearchConfig = new Core.Models.SearchConfig
                {
                    UseKnowledgeAgent = webRequest.SearchConfig.UseKnowledgeAgent,
                    Top = webRequest.SearchConfig.Top,
                    IncludeImages = webRequest.SearchConfig.IncludeImages,
                    IncludeText = webRequest.SearchConfig.IncludeText,
                    Threshold = webRequest.SearchConfig.Threshold,
                    Filter = webRequest.SearchConfig.Filter?.ToArray()
                }
            };

            logger.LogInformation("💬 SendMessage: Created core request with RequireSecurityTrimming: {RequireSecurityTrimming}, HasToken: {HasToken}, TokenLength: {TokenLength}", 
                coreRequest.RequireSecurityTrimming, !string.IsNullOrEmpty(coreRequest.Token), coreRequest.Token?.Length ?? 0);

            var client = CreateApiClientWithAuth();
            var content = new StringContent(JsonConvert.SerializeObject(coreRequest), Encoding.UTF8, "application/json");
            
            logger.LogInformation("💬 SendMessage: Sending request to API...");
            var response = await client.PostAsync("/api/chat", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("💬 SendMessage: Chat API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                
                // Check if it's an authentication error
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Json(new { error = "Authentication failed. Your session may have expired. Please sign in again.", requiresAuth = true });
                }
                
                return Json(new { error = "Chat request failed. Please try again.", details = $"API returned {response.StatusCode}: {errorContent}" });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            logger.LogInformation("💬 SendMessage: Received successful response from API, length: {Length}", responseContent.Length);
            
            var coreResponse = JsonConvert.DeserializeObject<ChatResponse>(responseContent);

            var webResponse = new Models.ChatResponse
            {
                Response = coreResponse?.Response ?? string.Empty,
                RequestId = coreResponse?.RequestId ?? string.Empty,
                Sources = coreResponse?.Citations?.Select(c => new SearchResult
                {
                    Id = c.Id,
                    Content = c.Content,
                    ContentType = c.ContentType,
                    Score = c.Relevance,
                    SourcePath = c.ContentPath,
                    Metadata = new Dictionary<string, object>()
                }).ToList() ?? [],
                ProcessingSteps = coreResponse?.ProcessingSteps?.Select(p => new ProcessingStep
                {
                    Title = p.Title,
                    Type = p.Type,
                    Description = p.Description,
                    Content = p.Content,
                    Timestamp = p.Timestamp
                }).ToList() ?? []
            };

            logger.LogInformation("💬 SendMessage: Successfully processed chat request");
            return Json(webResponse);
        }
        catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException ex)
        {
            logger.LogWarning(ex, "💬 SendMessage: Authentication challenge required");
            return Json(new { error = "Authentication required. Please sign in again.", requiresAuth = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "💬 SendMessage: Unexpected error occurred");
            return Json(new { error = "An unexpected error occurred. Please try again.", details = ex.Message });
        }
    }

    [HttpGet("test-auth")]
    public IActionResult TestAuth()
    {
        var authInfo = new
        {
            IsAuthenticated = authService.IsAuthenticated(),
            UserId = authService.GetCurrentUserId(),
            UserName = authService.GetCurrentUserName(),
            UserEmail = authService.GetCurrentUserEmail(),
            HasToken = !string.IsNullOrEmpty(authService.GetAuthorizationToken()),
            TokenLength = authService.GetAuthorizationToken()?.Length ?? 0,
            TokenPreview = authService.GetAuthorizationToken() != null ? 
                authService.GetAuthorizationToken()!.Substring(0, Math.Min(50, authService.GetAuthorizationToken()!.Length)) + "..." : 
                null,
            Timestamp = DateTime.UtcNow
        };

        logger.LogInformation("🔍 TestAuth: Auth info - IsAuthenticated: {IsAuthenticated}, HasToken: {HasToken}", 
            authInfo.IsAuthenticated, authInfo.HasToken);

        return Json(authInfo);
    }

    [HttpGet("debug-config")]
    public IActionResult DebugConfig()
    {
        try
        {
            var debugInfo = new
            {
                Authentication = new
                {
                    IsAuthenticated = authService.IsAuthenticated(),
                    UserId = authService.GetCurrentUserId(),
                    UserName = authService.GetCurrentUserName(),
                    UserEmail = authService.GetCurrentUserEmail(),
                    HasToken = !string.IsNullOrEmpty(authService.GetAuthorizationToken()),
                    TokenLength = authService.GetAuthorizationToken()?.Length ?? 0
                },
                HttpClient = new
                {
                    ApiBaseUrl = "https://localhost:7001",
                    ConfiguredClient = "APIClient"
                },
                Environment = new
                {
                    MachineName = Environment.MachineName,
                    AspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    Timestamp = DateTime.UtcNow
                }
            };

            logger.LogInformation("🔍 Debug config requested: {@DebugInfo}", debugInfo);
            return Json(debugInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error in debug config");
            return Json(new { error = ex.Message, timestamp = DateTime.UtcNow });
        }
    }
}