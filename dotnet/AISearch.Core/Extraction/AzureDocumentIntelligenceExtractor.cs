using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AISearch.Core.Extraction;

public class AzureDocumentIntelligenceExtractor : IContentExtraction
{
    private readonly EmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<AzureDocumentIntelligenceExtractor> _logger;
    private readonly DocumentIntelligenceClient DocumentIntelligenceClient;

    public AzureDocumentIntelligenceExtractor(ILogger<AzureDocumentIntelligenceExtractor> logger,
        IConfiguration configuration, EmbeddingGenerator embeddingGenerator)
    {
        _logger = logger;
        var endpoint = configuration["AzureSearch:DocumentIntelligenceEndpoint"];
        var apiKey = configuration["AzureSearch:DocumentIntelligenceApiKey"];

        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("DocumentIntelligenceEndpoint configuration is missing or empty.");

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("DocumentIntelligenceApiKey configuration is missing or empty.");

        DocumentIntelligenceClient = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<List<DocumentChunk>> ExtractContentAsync(string url, Dictionary<string, string> properties)
    {
        var chunks = new List<DocumentChunk>();
        try
        {
            if (string.IsNullOrEmpty(url))
            {
                _logger.LogError(
                    "DocumentIntelligence only supports URI-based analysis in this version. Stream input is not supported.");
                throw new ArgumentException("A valid document URL must be provided.");
            }

            var options = new AnalyzeDocumentOptions("prebuilt-layout", new Uri(url));
            var response = await DocumentIntelligenceClient.AnalyzeDocumentAsync(WaitUntil.Completed, options);
            var result = response.Value;

            var chunkIndex = 0;
            var id = Guid.NewGuid().ToString();
            var contentType = Utilities.GetContentTypeFromUrl(url) ?? "text/plain";
            foreach (var page in result.Pages)
            {
                var contentBuilder = new StringBuilder();
                foreach (var line in page.Lines) contentBuilder.AppendLine(line.Content);
                var content = contentBuilder.ToString();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var vector = await _embeddingGenerator.GenerateEmbeddingAsync(content);
                    chunks.Add(new DocumentChunk
                    {
                        Id = $"{id}-{chunkIndex}",
                        ContentId = id,
                        Content = content,
                        ContentType = contentType,
                        ChunkIndex = chunkIndex,
                        ContentPath = Utilities.ExtractPathAfterFirstSegment(url),
                        Vector = vector,
                        Metadata = new Dictionary<string, object> { { "PageNumber", page.PageNumber } }
                    });
                    chunkIndex++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting chunks from document");
            throw;
        }

        return chunks;
    }
}