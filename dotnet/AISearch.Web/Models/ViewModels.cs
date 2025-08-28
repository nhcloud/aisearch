namespace AISearch.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public string? ErrorMessage { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public SearchConfig Config { get; set; } = new();
    public List<ChatMessage> ChatHistory { get; set; } = [];
}

public class SearchConfig
{
    public bool UseKnowledgeAgent { get; set; }
    public int Top { get; set; } = 10;
    public bool IncludeImages { get; set; } = true;
    public bool IncludeText { get; set; } = true;
    public double Threshold { get; set; } = 0.7;
    public List<string>? Filter { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class SearchResponse
{
    public List<SearchResult> Results { get; set; } = [];
    public string RequestId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<ProcessingStep> ProcessingSteps { get; set; } = [];
}

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public double Score { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ProcessingStep
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Content { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public SearchConfig SearchConfig { get; set; } = new(); // Changed from Config to SearchConfig
    public bool RequireSecurityTrimming { get; set; } = false;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public List<SearchResult> Sources { get; set; } = [];
    public string RequestId { get; set; } = string.Empty;
    public List<ProcessingStep> ProcessingSteps { get; set; } = [];
}

public class DocumentModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class UploadResponse
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class IndexModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public class CreateIndexRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

// Additional models for new endpoints

public class VectorSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public float[] QueryVector { get; set; } = [];
    public SearchConfig Config { get; set; } = new();
}

public class GroundingRequest
{
    public string Query { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public SearchConfig SearchConfig { get; set; } = new();
}

public class GroundingResult
{
    public List<SearchResult> Sources { get; set; } = [];
    public string RequestId { get; set; } = string.Empty;
    public List<ProcessingStep> ProcessingSteps { get; set; } = [];
}

public class DocumentContent
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<DocumentChunk> Chunks { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class DocumentChunk
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ChunkType { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}