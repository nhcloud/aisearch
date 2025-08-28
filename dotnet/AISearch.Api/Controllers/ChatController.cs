using System.Runtime.CompilerServices;
using AISearch.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AISearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController(
    IMultimodalRagService ragService,
    IApiUserService userService,
    ILogger<ChatController> logger)
    : BaseApiController(userService, logger)
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Message = "Chat API is healthy and accessible",
            Service = "ChatController",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    [HttpGet("test-auth")]
    public IActionResult TestAuthentication()
    {
        LogAuthenticationInfo("TestAuthentication");
        var token = GetJwtToken();
        return Ok(new
        {
            Message = "Chat API Authentication Test",
            AuthInfo = GetAuthDebugInfo(),
            TokenAvailable = !string.IsNullOrEmpty(token),
            TokenPreview = token is { Length: > 0 } ? $"{token[..Math.Min(10, token.Length)]}..." : null,
            UserId = GetCurrentUserId(),
            UserEmail = GetCurrentUserEmail(),
            UserName = GetCurrentUserName(),
            IsAuthenticated = IsAuthenticated(),
            Claims = GetAllUserClaims(),
            AccessMethod = "This endpoint requires Authorization header with Bearer token",
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        LogAuthenticationInfo("Test");
        return Ok(new
        {
            Message = "Chat API is working!",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            HasToken = !string.IsNullOrEmpty(GetJwtToken())
        });
    }

    [HttpPost("test-model")]
    public IActionResult TestModel([FromBody] ChatRequest request)
    {
        LogAuthenticationInfo("TestModel");
        return Ok(new
        {
            Message = "Model binding successful",
            Request = new
            {
                request.Message,
                ChatHistoryCount = request.ChatHistory?.Count ?? 0,
                request.RequireSecurityTrimming,
                SearchConfig = new
                {
                    request.SearchConfig.UseKnowledgeAgent,
                    request.SearchConfig.Top,
                    request.SearchConfig.IncludeImages,
                    request.SearchConfig.IncludeText,
                    request.SearchConfig.Threshold,
                    FilterCount = request.SearchConfig.Filter?.Length ?? 0
                }
            },
            AuthInfo = new
            {
                HasToken = !string.IsNullOrEmpty(GetJwtToken()),
                UserId = GetCurrentUserId(),
                IsAuthenticated = IsAuthenticated()
            },
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> ChatAsync([FromBody] ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        LogAuthenticationInfo("ChatAsync");
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Chat message cannot be empty");

        _logger.LogInformation("Processing chat request for user: {UserId} ({UserEmail}), Token available: {HasToken}, RequireSecurityTrimming: {RequireSecurityTrimming}",
            GetCurrentUserId(), GetCurrentUserEmail(), !string.IsNullOrEmpty(GetJwtToken()), request.RequireSecurityTrimming);
        
        // Set the token from the Authorization header
        request.Token = Request.Headers.Authorization.ToString();
        
        var response = await ragService.ChatAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("stream")]
    public async IAsyncEnumerable<string> ChatStreamAsync(
        [FromBody] ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LogAuthenticationInfo("ChatStreamAsync");
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            yield return "Error: Chat message cannot be empty";
            yield break;
        }

        await foreach (var chunk in ragService.ChatStreamAsync(request, cancellationToken))
            yield return chunk;
    }

    [HttpPost("grounding")]
    public async Task<ActionResult<GroundingResult>> GetGroundingAsync([FromBody] GroundingRequest request,
        CancellationToken cancellationToken = default)
    {
        LogAuthenticationInfo("GetGroundingAsync");
        if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest("Query cannot be empty");

        var groundingResult = await ragService.GetGroundingAsync(request.Query, request.ChatHistory,
            request.SearchConfig, cancellationToken);
        return Ok(groundingResult);
    }

    [HttpGet("user-groups")]
    public async Task<IActionResult> GetUserGroups(CancellationToken cancellationToken = default)
    {
        LogAuthenticationInfo("GetUserGroups");

        var token = GetJwtToken();
        if (string.IsNullOrWhiteSpace(token)) return Unauthorized(new { error = "No access token available" });

        try
        {
            // Simple usage - utility handles HttpClient creation internally
            var groupIds = await GraphUserGroupsUtility.GetUserGroupObjectIdsAsync(token, cancellationToken);

            return Ok(new
            {
                Message = "Retrieved user groups successfully",
                GroupCount = groupIds.Length,
                GroupIds = groupIds,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to retrieve user groups from Microsoft Graph");
            return BadRequest(new { error = "Failed to retrieve user groups", details = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error retrieving user groups");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // Debug endpoint - commented out for production
    /*
    [HttpPost("test-security-trimming")]
    public IActionResult TestSecurityTrimming([FromBody] ChatRequest request)
    {
        LogAuthenticationInfo("TestSecurityTrimming");
        
        var result = new
        {
            Message = "Security trimming test successful",
            ReceivedValues = new
            {
                RequireSecurityTrimming = request.RequireSecurityTrimming,
                Message = request.Message,
                HasToken = !string.IsNullOrEmpty(request.Token),
                TokenLength = request.Token?.Length ?? 0,
                ChatHistoryCount = request.ChatHistory?.Count ?? 0,
                SearchConfig = new
                {
                    request.SearchConfig.UseKnowledgeAgent,
                    request.SearchConfig.Top,
                    request.SearchConfig.IncludeImages,
                    request.SearchConfig.IncludeText,
                    request.SearchConfig.Threshold
                }
            },
            AuthInfo = new
            {
                HasToken = !string.IsNullOrEmpty(GetJwtToken()),
                UserId = GetCurrentUserId(),
                IsAuthenticated = IsAuthenticated()
            },
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("?? TestSecurityTrimming: RequireSecurityTrimming={RequireSecurityTrimming}, HasAuthToken={HasAuthToken}", 
            request.RequireSecurityTrimming, !string.IsNullOrEmpty(GetJwtToken()));

        return Ok(result);
    }
    */
}