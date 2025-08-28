using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AISearch.Core.Extraction;

public class EmbeddingGenerator(
    OpenAIClient openAiClient,
    ILogger<EmbeddingGenerator> logger,
    ISearchConfiguration searchConfiguration)
{
    private readonly string _embeddingModel = searchConfiguration.OpenAIEmbeddingModel;
    private readonly int _vectorDimensions = searchConfiguration.VectorDimensions;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new EmbeddingsOptions(_embeddingModel, [text]);
            options.Dimensions = _vectorDimensions; // Read from configuration instead of hardcoded value
            var response = await openAiClient.GetEmbeddingsAsync(options, cancellationToken);
            return response.Value.Data.First().Embedding.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error generating embedding, using default vector");
            return new float[_vectorDimensions]; // Use configured dimension for fallback vector
        }
    }
}