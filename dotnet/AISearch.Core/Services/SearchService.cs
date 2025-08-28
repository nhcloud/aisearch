using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace AISearch.Core.Services;

public class SearchService(SearchClient searchClient, ILogger<SearchService> logger) : ISearchService
{
    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = request.Config.Top,
                IncludeTotalCount = true,
                SearchMode = SearchMode.Any
            };

            if (request.Config.Filter?.Any() == true)
                searchOptions.Filter = string.Join(" and ", request.Config.Filter);

            var searchResults = await searchClient.SearchAsync<SearchDocument>(
                request.Query,
                searchOptions,
                cancellationToken);

            var results = new List<SearchResult>();
            await foreach (var result in searchResults.Value.GetResultsAsync())
                if (result.Score >= request.Config.Threshold)
                    results.Add(new SearchResult
                    {
                        Id = result.Document.GetString("id") ?? string.Empty,
                        Content = result.Document.GetString("content_text") ?? string.Empty,
                        ContentType = result.Document.GetString("content_type") ?? string.Empty,
                        Score = result.Score ?? 0,
                        ContentPath = result.Document.GetString("content_path") ?? string.Empty,
                        Metadata = result.Document.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    });

            return new SearchResponse
            {
                Results = results,
                RequestId = Guid.NewGuid().ToString(),
                TotalCount = (int)(searchResults.Value.TotalCount ?? 0)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing search for query: {Query}", request.Query);
            throw;
        }
    }

    public async Task<SearchResponse> VectorSearchAsync(string query, float[] queryVector, SearchConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = config.Top,
                IncludeTotalCount = true
            };

            // Add vector search configuration
            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                        { KNearestNeighborsCount = config.Top, Fields = { "content_embedding" } }
                }
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>(
                null,
                searchOptions,
                cancellationToken);

            var results = new List<SearchResult>();
            await foreach (var result in searchResults.Value.GetResultsAsync())
                if (result.Score >= config.Threshold)
                    results.Add(new SearchResult
                    {
                        Id = result.Document.GetString("id") ?? string.Empty,
                        Content = result.Document.GetString("content_text") ?? string.Empty,
                        ContentType = result.Document.GetString("content_type") ?? "text",
                        Score = result.Score ?? 0,
                        ContentPath = result.Document.GetString("content_path") ?? string.Empty,
                        Metadata = result.Document.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    });

            return new SearchResponse
            {
                Results = results,
                RequestId = Guid.NewGuid().ToString(),
                TotalCount = (int)(searchResults.Value.TotalCount ?? 0)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing vector search");
            throw;
        }
    }

    public async Task<List<SearchResult>> GetSimilarDocumentsAsync(string documentId, int count = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First get the document to find similar ones
            var getResult =
                await searchClient.GetDocumentAsync<SearchDocument>(documentId, cancellationToken: cancellationToken);
            var document = getResult.Value;

            var contentVector = document.TryGetValue("content_embedding", out var vectorValue)
                ? (float[])vectorValue
                : null;
            if (contentVector == null) return [];

            var config = new SearchConfig { Top = count + 1, Threshold = 0.5 }; // +1 to exclude the original document
            var response = await VectorSearchAsync(string.Empty, contentVector, config, cancellationToken);

            // Filter out the original document
            return response.Results.Where(r => r.Id != documentId).Take(count).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding similar documents for document: {DocumentId}", documentId);
            throw;
        }
    }
}