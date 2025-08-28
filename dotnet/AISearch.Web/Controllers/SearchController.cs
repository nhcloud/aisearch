using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AISearch.Web.Controllers;

[Authorize]
public class SearchController(
    IHttpClientFactory httpClientFactory,
    IUserAuthService authService,
    ILogger<SearchController> logger)
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

    [HttpPost]
    public async Task<IActionResult> PerformSearch([FromBody] SearchRequest request)
    {
        return await ExecuteSearchRequest(request, "/api/search/search", "Search");
    }

    [HttpPost]
    public async Task<IActionResult> PerformVectorSearch([FromBody] VectorSearchRequest request)
    {
        return await ExecuteSearchRequest(request, "/api/search/vector-search", "Vector search");
    }

    [HttpGet]
    public async Task<IActionResult> GetSimilarDocuments(string documentId, int count = 5)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return BadRequest(new { error = "Document ID cannot be empty" });

        var client = CreateApiClientWithAuth();
        var response = await client.GetAsync($"/api/search/similar/{documentId}?count={count}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Similar Documents API error: {StatusCode} - {Content}", response.StatusCode,
                errorContent);
            return BadRequest(new { error = "Failed to retrieve similar documents", details = errorContent });
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var similarDocuments = JsonConvert.DeserializeObject<List<SearchResult>>(responseContent);
        return Json(similarDocuments);
    }

    private async Task<IActionResult> ExecuteSearchRequest<T>(T request, string endpoint, string operationName)
    {
        var client = CreateApiClientWithAuth();
        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("{Operation} API error: {StatusCode} - {Content}", operationName, response.StatusCode,
                errorContent);
            return BadRequest(new { error = $"{operationName} request failed", details = errorContent });
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonConvert.DeserializeObject<SearchResponse>(responseContent);
        return Json(searchResponse);
    }
}