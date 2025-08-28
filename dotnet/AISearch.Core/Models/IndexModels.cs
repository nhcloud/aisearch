namespace AISearch.Core.Models;

public class IndexRequest
{
    public string Name { get; set; } = string.Empty;
    public IndexSchema Schema { get; set; } = new();
}

public class IndexSchema
{
    public List<SearchField> Fields { get; set; } = [];
    public List<string> ScoringProfiles { get; set; } = [];
    public CorsOptions? CorsOptions { get; set; }
}

public class SearchField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsKey { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsFacetable { get; set; }
    public bool IsRetrievable { get; set; } = true;
    public string? AnalyzerName { get; set; }
}

public class CorsOptions
{
    public List<string> AllowedOrigins { get; set; } = [];
    public int MaxAgeInSeconds { get; set; } = 300;
}

public class IndexResponse
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class IndexInfo
{
    public string Name { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public long StorageSize { get; set; }
    public DateTime LastModified { get; set; }
}