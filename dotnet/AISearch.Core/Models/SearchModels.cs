namespace AISearch.Core.Models;

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public SearchConfig Config { get; set; } = new();
    public List<ChatMessage> ChatHistory { get; set; } = [];
}

public class SearchConfig
{
    /// <summary>
    ///     When set to true, enables agentic chat functionality that uses advanced query analysis,
    ///     multi-step search strategies, and enhanced response synthesis for more intelligent responses.
    /// </summary>
    public bool UseKnowledgeAgent { get; set; } = false;

    public int Top { get; set; } = 10;
    public bool IncludeImages { get; set; } = true;
    public bool IncludeText { get; set; } = true;
    public double Threshold { get; set; } = 0.7;
    public string[]? Filter { get; set; }
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
    public string ContentPath { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class ProcessingStep
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Agentic Chat Models - Support for advanced multi-step reasoning and query analysis
public class QueryAnalysis
{
    public string Intent { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public List<string> SearchQueries { get; set; } = [];
    public List<string> ExpectedContentTypes { get; set; } = [];
    public List<string> ReasoningSteps { get; set; } = [];
    public string SynthesisApproach { get; set; } = string.Empty;
}

public class AgenticSearchResults
{
    public List<Citation> AllReferences { get; set; } = [];
    public List<SearchStep> SearchSteps { get; set; } = [];
}

public class SearchStep
{
    public string Query { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public string Intent { get; set; } = string.Empty;
}

public class AgenticChatResponse
{
    public string Response { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = [];
    public List<ProcessingStep> ProcessingSteps { get; set; } = [];
}