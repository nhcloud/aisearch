using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using AzureSearchField = Azure.Search.Documents.Indexes.Models.SearchField;

namespace AISearch.Core.Services;

public class IndexService(
    SearchIndexClient indexClient,
    ILogger<IndexService> logger,
    ISearchConfiguration searchConfiguration)
    : IIndexService
{
    private readonly int _vectorDimensions = searchConfiguration.VectorDimensions;

    public async Task<IndexResponse> CreateIndexAsync(IndexRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = new List<AzureSearchField>
            {
                // Add default fields for multimodal search
                new("id", SearchFieldDataType.String) { IsKey = true },
                new("content_id", SearchFieldDataType.String) { IsFilterable = true },
                new("content_text", SearchFieldDataType.String)
                    { IsFilterable = false, IsSortable = false, IsFacetable = false, IsSearchable = true },
                new("content_type", SearchFieldDataType.String) { IsFilterable = true },
                new("content_path", SearchFieldDataType.String),
                new("content_title", SearchFieldDataType.String) { IsSearchable = true },
                new("chunk_index", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                new("size", SearchFieldDataType.Int64) { IsFilterable = true, IsSortable = true },
                new("created_at", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                new("user_ids", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                new("group_ids", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                new("path_parts", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                // Add vector field for embeddings
                new("content_embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = _vectorDimensions,
                    VectorSearchProfileName = "hnsw_text_3_large"
                }
            };

            // Add custom fields from request
            foreach (var customField in request.Schema.Fields)
                if (fields.All(f => f.Name != customField.Name))
                {
                    var field = new AzureSearchField(customField.Name, GetSearchFieldDataType(customField.Type))
                    {
                        IsSearchable = customField.IsSearchable,
                        IsFilterable = customField.IsFilterable,
                        IsSortable = customField.IsSortable,
                        IsFacetable = customField.IsFacetable
                    };
                    fields.Add(field);
                }

            var vectorizer = new AzureOpenAIVectorizer("azure_openai_text_3_large")
            {
                Parameters = new AzureOpenAIVectorizerParameters
                {
                    ResourceUri = new Uri(searchConfiguration.OpenAIEndpoint),
                    DeploymentName = searchConfiguration.OpenAIEmbeddingDeploymentName,
                    ModelName = searchConfiguration.OpenAIEmbeddingModel
                }
            };

            // Define the vector search profile and algorithm
            var vectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile(
                        "hnsw_text_3_large",
                        "alg"
                    )
                    {
                        VectorizerName = "azure_openai_text_3_large"
                    }
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("alg")
                },
                Vectorizers =
                {
                    vectorizer
                }
            };

            // Define semantic configuration
            var semanticConfig = new SemanticConfiguration(
                "semantic_config",
                new SemanticPrioritizedFields
                {
                    ContentFields = { new SemanticField("content_text") },
                    TitleField = new SemanticField("content_title")
                }
            );

            var semanticSearch = new SemanticSearch
            {
                DefaultConfigurationName = "semantic_config",
                Configurations =
                {
                    semanticConfig
                }
            };

            var index = new SearchIndex(request.Name)
            {
                Fields = fields,
                VectorSearch = vectorSearch,
                SemanticSearch = semanticSearch
            };

            await indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
            var openAiParameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(searchConfiguration.OpenAIEndpoint),
                DeploymentName = searchConfiguration.OpenAIDeploymentName,
                ModelName = searchConfiguration.OpenAIModelName
            };
            var agentModel = new KnowledgeAgentAzureOpenAIModel(openAiParameters);
            var targetIndex = new KnowledgeAgentTargetIndex(request.Name)
            {
                DefaultRerankerThreshold = 2.5f
            };
            var agent = new KnowledgeAgent(
                searchConfiguration.KnowledgeAgentName,
                [agentModel],
                [targetIndex]
            );

            await indexClient.CreateOrUpdateKnowledgeAgentAsync(agent, cancellationToken: cancellationToken);

            return new IndexResponse
            {
                Name = request.Name,
                Success = true,
                Message = "Index created successfully"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index: {IndexName}", request.Name);
            return new IndexResponse
            {
                Name = request.Name,
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            await indexClient.DeleteKnowledgeAgentAsync(searchConfiguration.KnowledgeAgentName, cancellationToken);
            await indexClient.DeleteIndexAsync(indexName, cancellationToken);
            logger.LogInformation("Index {IndexName} deleted successfully", indexName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index: {IndexName}", indexName);
            return false;
        }
    }

    public async Task<List<IndexInfo>> ListIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var indexes = new List<IndexInfo>();
            await foreach (var index in indexClient.GetIndexesAsync(cancellationToken))
            {
                var stats = await indexClient.GetIndexStatisticsAsync(index.Name, cancellationToken);
                indexes.Add(new IndexInfo
                {
                    Name = index.Name,
                    DocumentCount = (int)stats.Value.DocumentCount,
                    StorageSize = stats.Value.StorageSize,
                    LastModified = DateTime.UtcNow // Note: Azure Search doesn't provide this directly
                });
            }

            return indexes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing indexes");
            throw;
        }
    }

    public async Task<IndexInfo?> GetIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var index = await indexClient.GetIndexAsync(indexName, cancellationToken);
            var stats = await indexClient.GetIndexStatisticsAsync(indexName, cancellationToken);

            return new IndexInfo
            {
                Name = index.Value.Name,
                DocumentCount = (int)stats.Value.DocumentCount,
                StorageSize = stats.Value.StorageSize,
                LastModified = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting index: {IndexName}", indexName);
            return null;
        }
    }

    private static SearchFieldDataType GetSearchFieldDataType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => SearchFieldDataType.String,
            "int32" => SearchFieldDataType.Int32,
            "int64" => SearchFieldDataType.Int64,
            "double" => SearchFieldDataType.Double,
            "boolean" => SearchFieldDataType.Boolean,
            "datetimeoffset" => SearchFieldDataType.DateTimeOffset,
            "geographypoint" => SearchFieldDataType.GeographyPoint,
            _ => SearchFieldDataType.String
        };
    }
}