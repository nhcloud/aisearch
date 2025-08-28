using System.Security.Claims;

namespace AISearch.Core.Interfaces;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    Task<SearchResponse> VectorSearchAsync(string query, float[] queryVector, SearchConfig config,
        CancellationToken cancellationToken = default);

    Task<List<SearchResult>> GetSimilarDocumentsAsync(string documentId, int count = 5,
        CancellationToken cancellationToken = default);
}

public interface IIndexService
{
    Task<IndexResponse> CreateIndexAsync(IndexRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);
    Task<List<IndexInfo>> ListIndexesAsync(CancellationToken cancellationToken = default);
    Task<IndexInfo?> GetIndexAsync(string indexName, CancellationToken cancellationToken = default);
}

public interface IDocumentService
{
    Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    Task<DocumentInfo?> GetDocumentInfoAsync(string documentId, CancellationToken cancellationToken = default);
    Task<DocumentContent?> GetDocumentContentAsync(string documentId, CancellationToken cancellationToken = default);

    Task<List<DocumentInfo>> ListDocumentsAsync(int skip = 0, int take = 20,
        CancellationToken cancellationToken = default);

    Task<bool> IndexDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}

public interface IMultimodalRagService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

    Task<GroundingResult> GetGroundingAsync(string query, List<ChatMessage> chatHistory, SearchConfig config,
        CancellationToken cancellationToken = default);
}

public interface IUserAuthService
{
    string? GetAuthorizationToken();
    string? GetCurrentUserId();
    string? GetCurrentUserEmail();
    string? GetCurrentUserName();
    bool IsAuthenticated();
    ClaimsPrincipal? GetCurrentUser();
}

public interface ICopilotDataRetriever
{
    /// <summary>
    /// Retrieves data from Microsoft 365 Copilot using the specified query and configuration
    /// </summary>
    /// <param name="request">The request containing query and configuration</param>
    /// <param name="authContext">Authentication context for the API call</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The retrieved data response</returns>
    Task<CopilotDataResponse> RetrieveDataAsync(CopilotDataRequest request, CopilotAuthContext authContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves data from SharePoint using Microsoft Graph API
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="scopes">Permission scopes</param>
    /// <param name="authContext">Authentication context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The retrieved data response</returns>
    Task<CopilotDataResponse> RetrieveSharePointDataAsync(string query, List<string> scopes, CopilotAuthContext authContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic method to retrieve data from any Microsoft 365 service via Copilot API
    /// </summary>
    /// <param name="endpoint">The API endpoint to call</param>
    /// <param name="parameters">Query parameters</param>
    /// <param name="authContext">Authentication context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The retrieved data response</returns>
    Task<CopilotDataResponse> RetrieveGenericDataAsync(string endpoint, Dictionary<string, object> parameters, CopilotAuthContext authContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates authentication context and refreshes token if needed
    /// </summary>
    /// <param name="authContext">Authentication context to validate</param>
    /// <returns>Whether the context is valid or was successfully refreshed</returns>
    Task<bool> ValidateAuthContextAsync(CopilotAuthContext authContext);
}