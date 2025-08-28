namespace AISearch.Core.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public SearchConfig SearchConfig { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    public bool RequireSecurityTrimming { get; set; } = false;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = [];
    public List<ProcessingStep> ProcessingSteps { get; set; } = [];
}

public class Citation
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentPath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public double Relevance { get; set; }
}

public class GroundingResult
{
    public List<Citation> References { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class GroundingRequest
{
    public string Query { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = [];
    public SearchConfig SearchConfig { get; set; } = new();
}

public class LLMMessage
{
    public string Role { get; set; } = string.Empty;
    public List<MessageContent> Content { get; set; } = [];
}

public class MessageContent
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public ImageUrl? ImageUrl { get; set; }
}

public class ImageUrl
{
    public string Url { get; set; } = string.Empty;
}