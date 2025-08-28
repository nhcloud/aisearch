namespace AISearch.Core.Models;

public class DocumentUploadRequest
{
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class DocumentUploadResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class DocumentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class DocumentContent
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public List<DocumentChunk> Chunks { get; set; } = [];
}

public class DocumentChunk
{
    public string Id { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string ContentPath { get; set; } = string.Empty;
    public float[]? Vector { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
}