namespace AISearch.Core.Models;

/// <summary>
/// Request model for Copilot data retrieval
/// </summary>
public class CopilotDataRequest
{
    /// <summary>
    /// The query string to search for
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// The scopes to search within (e.g., "https://graph.microsoft.com/sites.read.all")
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Additional configuration for the request
    /// </summary>
    public CopilotRequestConfig Config { get; set; } = new();

    /// <summary>
    /// Optional filters to apply to the search
    /// </summary>
    public Dictionary<string, object> Filters { get; set; } = new();
}

/// <summary>
/// Configuration options for Copilot data requests
/// </summary>
public class CopilotRequestConfig
{
    /// <summary>
    /// Maximum number of results to return (default: 10)
    /// </summary>
    public int Top { get; set; } = 10;

    /// <summary>
    /// Whether to include content in the response (default: true)
    /// </summary>
    public bool IncludeContent { get; set; } = true;

    /// <summary>
    /// Additional headers to include in the request
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Request timeout in milliseconds (default: 30000)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Response model for Copilot data retrieval
/// </summary>
public class CopilotDataResponse
{
    /// <summary>
    /// The retrieved data items
    /// </summary>
    public List<CopilotDataItem> Items { get; set; } = [];

    /// <summary>
    /// Total number of items found
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Request identifier for tracking
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Any warnings or messages from the service
    /// </summary>
    public List<string> Messages { get; set; } = [];

    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error information if the request failed
    /// </summary>
    public CopilotError? Error { get; set; }
}

/// <summary>
/// Individual data item retrieved from Copilot
/// </summary>
public class CopilotDataItem
{
    /// <summary>
    /// Unique identifier for the item
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title or name of the item
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Content of the item
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of content (document, email, chat, etc.)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// URL or path to the original item
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score (0-1)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Additional metadata about the item
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// When the item was created
    /// </summary>
    public DateTime? CreatedDateTime { get; set; }

    /// <summary>
    /// When the item was last modified
    /// </summary>
    public DateTime? LastModifiedDateTime { get; set; }

    /// <summary>
    /// Author or creator of the item
    /// </summary>
    public string Author { get; set; } = string.Empty;
}

/// <summary>
/// Error information for failed Copilot requests
/// </summary>
public class CopilotError
{
    /// <summary>
    /// Error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Authentication information for Copilot API calls
/// </summary>
public class CopilotAuthContext
{
    /// <summary>
    /// Bearer token for authentication
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the token is still valid
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow < ExpiresAt;
}