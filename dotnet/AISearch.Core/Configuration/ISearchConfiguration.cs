namespace AISearch.Core.Configuration;

public interface ISearchConfiguration
{
    int VectorDimensions { get; }
    string OpenAIModelName { get; }
    string OpenAIDeploymentName { get; }
    string OpenAIEmbeddingModel { get; }
    string OpenAIEmbeddingDeploymentName { get; }
    string OpenAIEndpoint { get; }
    string KnowledgeAgentName { get; }
}