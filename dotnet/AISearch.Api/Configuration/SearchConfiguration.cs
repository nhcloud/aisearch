namespace AISearch.Api.Configuration;

public class SearchConfiguration : ISearchConfiguration
{
    public string ServiceEndpoint { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string SearchAdminKey { get; set; } = string.Empty;
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string StorageAccountUrl { get; set; } = string.Empty;
    public string StorageAccountKey { get; set; } = string.Empty;
    public string ArtifactsContainer { get; set; } = string.Empty;
    public string SamplesContainer { get; set; } = string.Empty;
    public string OpenAIEndpoint { get; set; } = string.Empty;
    public string KnowledgeAgentName { get; set; } = string.Empty;
    public string OpenAIModelName { get; set; } = string.Empty;
    public string OpenAIDeploymentName { get; set; } = string.Empty;
    public string OpenAIEmbeddingModel { get; set; } = string.Empty;
    public string OpenAIEmbeddingDeploymentName { get; set; } = string.Empty;
    public int VectorDimensions { get; set; } = 3072;
}