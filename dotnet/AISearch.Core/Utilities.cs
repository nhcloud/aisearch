using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AISearch.Core;

public class Utilities
{
    public static string[] GetPathHierarchy(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        path = path.ToLowerInvariant().Replace(" ", "-");
        var results = new List<string>();
        var parts = path.Trim('/').Split('/');

        // Build intermediate paths
        var current = "/";
        for (var i = 0; i < parts.Length; i++)
            if (i < parts.Length - 1)
            {
                current += parts[i] + "/";
                results.Add(current);
            }
            else
            {
                var withExt = Path.Combine(current, parts[i]).Replace("\\", "/");
                results.Add(withExt);
            }

        return results.ToArray();
    }

    public static string ExtractPathAfterFirstSegment(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/');

        if (segments.Length <= 1)
            return segments.Last();

        return string.Join("/", segments.Skip(1));
    }

    public static string[] GetUsers()
    {
        return ["none"];
    }

    public static string[] GetGroups()
    {
        return ["0700fd06-4cc2-445a-b011-b68352082af7", "02c58089-348e-4262-98a9-3b4ac9596f3a"];
    }

    public static string GetContentTypeFromUrl(string url)
    {
        if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return "application/pdf";
        if (url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return "text/plain";
        if (url.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (url.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        if (url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            return "image/jpeg";
        if (url.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return "image/png";
        return "application/octet-stream";
    }

    public static string ExtractGuid(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 36)
            return string.Empty;

        var guidCandidate = input.Substring(0, 36);
        return Guid.TryParse(guidCandidate, out var guid) ? guid.ToString() : string.Empty;
    }
}

/// <summary>
///     Utility class for calling Microsoft Graph API to retrieve user groups
/// </summary>
public static class GraphUserGroupsUtility
{
    private const string GraphApiBaseUrl = "https://graph.microsoft.com/v1.0";

    /// <summary>
    ///     Retrieves the Object IDs of groups that the current user is a member of
    ///     This overload creates its own HttpClient instance internally
    /// </summary>
    /// <param name="accessToken">Bearer access token for Microsoft Graph API with GroupMember.Read.All or Group.Read.All scope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of group Object IDs</returns>
    /// <exception cref="ArgumentException">Thrown when access token is null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the Graph API request fails</exception>
    public static async Task<string[]> GetUserGroupObjectIdsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return await GetUserGroupObjectIdsAsync(accessToken, httpClient, cancellationToken);
    }

    /// <summary>
    ///     Retrieves the Object IDs of groups that the current user is a member of
    ///     This overload uses the provided HttpClient instance
    /// </summary>
    /// <param name="accessToken">Bearer access token for Microsoft Graph API with GroupMember.Read.All or Group.Read.All scope</param>
    /// <param name="httpClient">HttpClient instance for making HTTP requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of group Object IDs</returns>
    /// <exception cref="ArgumentException">Thrown when access token is null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the Graph API request fails</exception>
    public static async Task<string[]> GetUserGroupObjectIdsAsync(
        string accessToken,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token cannot be null or empty", nameof(accessToken));

        try
        {
            // Configure HTTP client for Graph API call
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Call Graph API to get user's group memberships
            // Using /me/memberOf endpoint with $select to retrieve id and @odata.type
            // We'll filter on the client side to avoid Graph API filter limitations
            var requestUrl = $"{GraphApiBaseUrl}/me/memberOf?$select=id";

            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Graph API request failed with status {response.StatusCode}: {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var graphResponse = JsonSerializer.Deserialize<GraphApiResponseWithType>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (graphResponse?.Value == null)
                return [];

            // Filter only groups on the client side and extract object IDs
            return graphResponse.Value
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => item.Id!)
                .ToArray();
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException($"Failed to parse Graph API response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new HttpRequestException("Graph API request timed out", ex);
        }
    }

    /// <summary>
    ///     Retrieves the Object IDs of groups that the current user is a member of with pagination support
    ///     This overload creates its own HttpClient instance internally
    /// </summary>
    /// <param name="accessToken">Bearer access token for Microsoft Graph API with GroupMember.Read.All or Group.Read.All scope</param>
    /// <param name="maxResults">Maximum number of results to return (default: 999, set to null for all results)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of group Object IDs</returns>
    public static async Task<string[]> GetUserGroupObjectIdsWithPaginationAsync(
        string accessToken,
        int? maxResults = 999,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return await GetUserGroupObjectIdsWithPaginationAsync(accessToken, httpClient, maxResults, cancellationToken);
    }

    /// <summary>
    ///     Retrieves the Object IDs of groups that the current user is a member of with pagination support
    ///     This overload uses the provided HttpClient instance
    /// </summary>
    /// <param name="accessToken">Bearer access token for Microsoft Graph API with GroupMember.Read.All or Group.Read.All scope</param>
    /// <param name="httpClient">HttpClient instance for making HTTP requests</param>
    /// <param name="maxResults">Maximum number of results to return (default: 999, set to null for all results)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of group Object IDs</returns>
    public static async Task<string[]> GetUserGroupObjectIdsWithPaginationAsync(
        string accessToken,
        HttpClient httpClient,
        int? maxResults = 999,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token cannot be null or empty", nameof(accessToken));

        var allGroupIds = new List<string>();
        var requestUrl = $"{GraphApiBaseUrl}/me/memberOf?$select=id,@odata.type";

        try
        {
            // Configure HTTP client for Graph API call
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            while (!string.IsNullOrEmpty(requestUrl) && (maxResults == null || allGroupIds.Count < maxResults))
            {
                var response = await httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException(
                        $"Graph API request failed with status {response.StatusCode}: {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var graphResponse = JsonSerializer.Deserialize<GraphApiResponseWithType>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (graphResponse?.Value != null)
                {
                    // Filter only groups on the client side
                    var groupIds = graphResponse.Value
                        .Where(item => !string.IsNullOrWhiteSpace(item.Id) && 
                                      item.OdataType == "microsoft.graph.group")
                        .Select(item => item.Id!)
                        .ToList();

                    if (maxResults.HasValue)
                    {
                        var remainingSlots = maxResults.Value - allGroupIds.Count;
                        allGroupIds.AddRange(groupIds.Take(remainingSlots));
                    }
                    else
                    {
                        allGroupIds.AddRange(groupIds);
                    }
                }

                // Check for next page
                requestUrl = graphResponse?.OdataNextLink;
            }

            return allGroupIds.ToArray();
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException($"Failed to parse Graph API response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new HttpRequestException("Graph API request timed out", ex);
        }
    }

    /// <summary>
    ///     Data models for Graph API response deserialization
    /// </summary>
    private class GraphApiResponse
    {
        public List<GraphDirectoryObject>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")] public string? OdataNextLink { get; set; }
    }

    private class GraphApiResponseWithType
    {
        public List<GraphDirectoryObjectWithType>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")] public string? OdataNextLink { get; set; }
    }

    private class GraphDirectoryObject
    {
        public string? Id { get; set; }
    }

    private class GraphDirectoryObjectWithType
    {
        public string? Id { get; set; }
        
        [JsonPropertyName("@odata.type")]
        public string? OdataType { get; set; }
    }
}