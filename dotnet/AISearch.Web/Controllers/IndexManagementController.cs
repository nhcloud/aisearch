using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AISearch.Web.Controllers;

[Authorize]
public class IndexManagementController(
    IHttpClientFactory httpClientFactory,
    IUserAuthService authService,
    ILogger<IndexManagementController> logger)
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

    [HttpGet]
    public async Task<IActionResult> GetIndexes()
    {
        try
        {
            var client = CreateApiClientWithAuth();

            var response = await client.GetAsync("/api/index");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var indexes = JsonConvert.DeserializeObject<List<IndexModel>>(responseContent);
                return Json(indexes);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Indexes API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Failed to retrieve indexes", details = errorContent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving indexes");
            return BadRequest(new { error = "An error occurred while retrieving indexes", details = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetIndex(string indexName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(indexName)) return BadRequest(new { error = "Index name cannot be empty" });

            var client = CreateApiClientWithAuth();

            var response = await client.GetAsync($"/api/index/{indexName}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var index = JsonConvert.DeserializeObject<IndexModel>(responseContent);
                return Json(index);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return NotFound(new { error = $"Index '{indexName}' not found" });

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Get Index API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Failed to retrieve index", details = errorContent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving index: {IndexName}", indexName);
            return BadRequest(new { error = "An error occurred while retrieving the index", details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateIndex([FromBody] CreateIndexRequest request)
    {
        try
        {
            var client = CreateApiClientWithAuth();

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/index", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<IndexModel>(responseContent);
                return Json(result);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Create Index API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Failed to create index", details = errorContent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index");
            return BadRequest(new { error = "An error occurred while creating index", details = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteIndex(string indexName)
    {
        try
        {
            var client = CreateApiClientWithAuth();

            var response = await client.DeleteAsync($"/api/index/{indexName}");

            if (response.IsSuccessStatusCode) return Json(new { success = true });

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Delete Index API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return BadRequest(new { error = "Failed to delete index", details = errorContent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index");
            return BadRequest(new { error = "An error occurred while deleting index", details = ex.Message });
        }
    }
}