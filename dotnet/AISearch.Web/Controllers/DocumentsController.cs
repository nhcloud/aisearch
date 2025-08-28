using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AISearch.Web.Controllers;

[Authorize]
public class DocumentsController(
    IHttpClientFactory httpClientFactory,
    IUserAuthService authService,
    ILogger<DocumentsController> logger)
    : Controller
{
    private HttpClient CreateApiClientWithAuth()
    {
        var client = httpClientFactory.CreateClient("APIClient");
        client.BaseAddress = new Uri("https://localhost:7001");
        var token = authService.GetAuthorizationToken();
        logger.LogInformation("🔍 GetDocuments: Starting - HasToken: {HasToken}, TokenLength: {TokenLength}",
            !string.IsNullOrEmpty(token), token?.Length ?? 0);
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientHeaders = client.DefaultRequestHeaders.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
        logger.LogInformation("🔍 GetDocuments: Client headers before request: {@Headers}", clientHeaders);
        return client;
    }

    public IActionResult Index()
    {
        if (!authService.IsAuthenticated()) return RedirectToAction("SignIn", "Account");
        ViewData["Title"] = "Document Management";
        ViewData["UserName"] = authService.GetCurrentUserName();
        ViewData["UserEmail"] = authService.GetCurrentUserEmail();
        return View();
    }

    public async Task<IActionResult> GetDocuments(int skip = 0, int take = 20)
    {
        var client = CreateApiClientWithAuth();

        var response = await client.GetAsync($"/api/documents?skip={skip}&take={take}");
        logger.LogInformation("API response status: {StatusCode}", response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Documents API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new
            {
                error = "Failed to retrieve documents",
                details = errorContent,
                statusCode = (int)response.StatusCode,
                apiUrl = $"{client.BaseAddress}/api/documents?skip={skip}&take={take}"
            });
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var documents = JsonConvert.DeserializeObject<List<DocumentModel>>(responseContent);
        return Json(documents);
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] string? title = null,
        [FromForm] string? description = null)
    {
        if (file is not { Length: > 0 }) return BadRequest(new { error = "No file uploaded", success = false });

        var client = CreateApiClientWithAuth();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(fileContent, "file", file.FileName);

        if (!string.IsNullOrWhiteSpace(title)) content.Add(new StringContent(title), "title");
        if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");

        var response = await client.PostAsync("/api/documents/upload", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Upload API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Upload failed", details = errorContent, success = false });
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<UploadResponse>(responseContent);
        return Json(new
        {
            success = true,
            message = "Document uploaded successfully",
            documentId = result?.DocumentId,
            fileName = file.FileName
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteDocument(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return BadRequest(new { error = "Document ID cannot be empty", success = false });

        var client = CreateApiClientWithAuth();
        var response = await client.DeleteAsync($"/api/documents/{documentId}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Delete API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Delete failed", details = errorContent, success = false });
        }

        return Json(new { success = true, message = "Document deleted successfully" });
    }

    [HttpGet]
    public async Task<IActionResult> AuthDiagnostics()
    {
        var client = CreateApiClientWithAuth();
        var token = authService.GetAuthorizationToken();
        var user = authService.GetCurrentUser();

        var diagnostics = new
        {
            WebApp = new
            {
                IsAuthenticated = authService.IsAuthenticated(),
                UserId = authService.GetCurrentUserId(),
                UserEmail = authService.GetCurrentUserEmail(),
                UserName = authService.GetCurrentUserName(),
                HasToken = !string.IsNullOrEmpty(token),
                TokenLength = token?.Length ?? 0,
                TokenPreview = token?.Substring(0, Math.Min(20, token?.Length ?? 0)) + "..."
            },
            HttpClient = new
            {
                BaseAddress = client.BaseAddress?.ToString(),
                DefaultHeaders = client.DefaultRequestHeaders.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                HasAuthHeader = client.DefaultRequestHeaders.Authorization != null
            },
            Claims = user?.Claims?.Select(c => new { c.Type, c.Value }).ToArray() ?? Array.Empty<object>(),
            Timestamp = DateTime.UtcNow
        };

        return Json(new { diagnostics });
    }
}