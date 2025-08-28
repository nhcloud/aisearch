using Microsoft.Extensions.Logging;

namespace AISearch.Core.Examples;

/// <summary>
/// Example usage of the CopilotDataRetriever service
/// This demonstrates how to use the service in your application
/// </summary>
public class CopilotUsageExample(ICopilotDataRetriever copilotRetriever, ILogger<CopilotUsageExample> logger)
{
    /// <summary>
    /// Example: Basic search using Copilot API
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="accessToken">Bearer token for Microsoft Graph</param>
    /// <returns>Search results from Microsoft 365</returns>
    public async Task<CopilotDataResponse> SearchDocumentsAsync(string query, string accessToken)
    {
        try
        {
            // Create authentication context
            var authContext = new CopilotAuthContext
            {
                AccessToken = accessToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1) // Assume 1 hour expiry
            };

            // Create search request
            var request = new CopilotDataRequest
            {
                Query = query,
                Scopes =
                [
                    "https://graph.microsoft.com/Sites.Read.All",
                    "https://graph.microsoft.com/Files.Read.All"
                ],
                Config = new CopilotRequestConfig
                {
                    Top = 10,
                    IncludeContent = true
                }
            };

            // Retrieve data
            var response = await copilotRetriever.RetrieveDataAsync(request, authContext);

            if (response.Success)
            {
                logger.LogInformation("Successfully retrieved {Count} items for query: {Query}", 
                    response.Items.Count, query);
            }
            else
            {
                logger.LogWarning("Copilot search failed: {Error}", response.Error?.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Copilot search for query: {Query}", query);
            
            return new CopilotDataResponse
            {
                Success = false,
                Error = new CopilotError
                {
                    Code = "SEARCH_ERROR",
                    Message = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Example: SharePoint-specific search
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="accessToken">Bearer token for Microsoft Graph</param>
    /// <returns>SharePoint search results</returns>
    public async Task<CopilotDataResponse> SearchSharePointAsync(string query, string accessToken)
    {
        var authContext = new CopilotAuthContext
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var scopes = new List<string>
        {
            "https://graph.microsoft.com/Sites.Read.All"
        };

        return await copilotRetriever.RetrieveSharePointDataAsync(query, scopes, authContext);
    }

    /// <summary>
    /// Example: Search for specific file types
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="fileExtension">File extension to filter by (e.g., "docx", "pdf")</param>
    /// <param name="accessToken">Bearer token</param>
    /// <returns>Filtered search results</returns>
    public async Task<CopilotDataResponse> SearchByFileTypeAsync(string query, string fileExtension, string accessToken)
    {
        var authContext = new CopilotAuthContext
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var request = new CopilotDataRequest
        {
            Query = query,
            Scopes = ["https://graph.microsoft.com/Files.Read.All"],
            Config = new CopilotRequestConfig { Top = 20 },
            Filters = new Dictionary<string, object>
            {
                ["fileExtension"] = fileExtension
            }
        };

        return await copilotRetriever.RetrieveDataAsync(request, authContext);
    }

    /// <summary>
    /// Example: Call a custom Graph API endpoint
    /// </summary>
    /// <param name="endpoint">The Graph API endpoint</param>
    /// <param name="accessToken">Bearer token</param>
    /// <returns>API response</returns>
    public async Task<CopilotDataResponse> CallCustomEndpointAsync(string endpoint, string accessToken)
    {
        var authContext = new CopilotAuthContext
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var parameters = new Dictionary<string, object>
        {
            ["$top"] = 10,
            ["$select"] = "id,name,webUrl,createdDateTime,lastModifiedDateTime"
        };

        return await copilotRetriever.RetrieveGenericDataAsync(endpoint, parameters, authContext);
    }

    /// <summary>
    /// Example: Batch process multiple queries
    /// </summary>
    /// <param name="queries">List of search queries</param>
    /// <param name="accessToken">Bearer token</param>
    /// <returns>Combined results from all queries</returns>
    public async Task<List<CopilotDataResponse>> BatchSearchAsync(List<string> queries, string accessToken)
    {
        var results = new List<CopilotDataResponse>();

        var authContext = new CopilotAuthContext
        {
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        foreach (var query in queries)
        {
            var request = new CopilotDataRequest
            {
                Query = query,
                Scopes = ["https://graph.microsoft.com/Sites.Read.All"],
                Config = new CopilotRequestConfig { Top = 5 }
            };

            var response = await copilotRetriever.RetrieveDataAsync(request, authContext);
            results.Add(response);

            // Add small delay to avoid rate limiting
            await Task.Delay(100);
        }

        return results;
    }
}