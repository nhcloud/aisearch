namespace AISearch.Core.Interfaces;

public interface IContentExtraction
{
    Task<List<DocumentChunk>> ExtractContentAsync(string url, Dictionary<string, string> properties);
}