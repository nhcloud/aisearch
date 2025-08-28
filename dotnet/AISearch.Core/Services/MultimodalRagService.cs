using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents.Agents;
using Azure.Search.Documents.Agents.Models;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AISearch.Core.Services;

public class MultimodalRagService(
    ISearchService searchService,
    OpenAIClient openAiClient,
    BlobServiceClient blobServiceClient,
    SearchIndexClient searchIndexClient,
    ILogger<MultimodalRagService> logger,
    IConfiguration configuration,
    ISearchConfiguration searchConfiguration)
    : IMultimodalRagService
{
    private readonly string _containerName = "artifacts";

    private readonly string
        _modelName = searchConfiguration.OpenAIModelName; // Use configuration instead of hardcoded value

    private readonly SearchIndexClient _searchIndexClient = searchIndexClient;

    /// <summary>
    ///     Main chat method that routes to either enhanced agentic chat or traditional RAG based on configuration.
    ///     When UseKnowledgeAgent is true, uses Knowledge Agent pattern implementation.
    ///     When false, uses traditional RAG approach with single-step search and direct response generation.
    ///     This implementation follows the Knowledge Agent pattern from:
    ///     https://github.com/Azure-Samples/azure-search-dotnet-samples/blob/main/quickstart-agentic-retrieval/
    ///     CURRENT STATUS: The Knowledge Agent classes (KnowledgeAgentAzureOpenAIModel, KnowledgeAgentMessage, etc.)
    ///     are not yet available in Azure.Search.Documents v11.7.0-beta.5. This implementation provides the same
    ///     pattern and behavior, and includes commented code for easy migration when the APIs become available.
    ///     MIGRATION READY: When Knowledge Agent APIs are released, uncomment the TODO sections to use:
    ///     - KnowledgeAgentAzureOpenAIModel for direct agent communication
    ///     - KnowledgeAgentRetrievalRequest for structured retrieval requests
    ///     - KnowledgeAgentMessage and KnowledgeAgentMessageTextContent for message formatting
    ///     - KnowledgeAgentIndexParams for index configuration with reranker thresholds
    /// </summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // Log the UseKnowledgeAgent and RequireSecurityTrimming settings for debugging
        logger.LogInformation("Chat request received. UseKnowledgeAgent: {UseKnowledgeAgent}, RequireSecurityTrimming: {RequireSecurityTrimming}",
            request.SearchConfig.UseKnowledgeAgent, request.RequireSecurityTrimming);

        // Check if Knowledge Agent should be used
        if (request.SearchConfig.UseKnowledgeAgent)
        {
            logger.LogInformation("Using enhanced agentic approach (Knowledge Agent)");

            // Check if beta Knowledge Agent APIs should be used
            var useBetaApis = bool.Parse(configuration["AzureSearch:UseKnowledgeAgentBetaAPIs"] ?? "false");

            if (useBetaApis)
            {
                logger.LogInformation("Using Knowledge Agent beta APIs");
                return await ChatWithKnowledgeAISearchAgent(request, cancellationToken);
            }

            logger.LogInformation("Using Knowledge Agent pattern simulation");
            return await ChatWithKnowledgeAgentAsync(request, cancellationToken);
        }

        logger.LogInformation("Using traditional RAG approach");
        // Use traditional RAG approach
        return await ChatWithTraditionalRagAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For agentic chat, we'll provide the full response since it involves complex multi-step processing
        if (request.SearchConfig.UseKnowledgeAgent)
        {
            var response = await ChatWithKnowledgeAgentAsync(request, cancellationToken);
            yield return response.Response;
            yield break;
        }

        // Traditional streaming implementation
        var groundingResult = request.RequireSecurityTrimming == false
            ? await GetGroundingAsync(request.Message, request.ChatHistory, request.SearchConfig, cancellationToken)
            : await GetGroundingWithSecurityTrimmingAsync(request.Message, request.ChatHistory, request.SearchConfig,
                request.Token, cancellationToken);
        var llmMessages =
            await PrepareLlmMessagesAsync(groundingResult, request.ChatHistory, request.Message, cancellationToken);

        var chatCompletionOptions = new ChatCompletionsOptions
        {
            DeploymentName = _modelName,
            Temperature = 0.7f,
            MaxTokens = 1000
        };

        foreach (var message in llmMessages)
        {
            var role = message.Role switch
            {
                "system" => ChatRole.System,
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User
            };

            var content = new StringBuilder();
            foreach (var contentItem in message.Content)
                if (contentItem.Type == "text" && !string.IsNullOrEmpty(contentItem.Text))
                    content.AppendLine(contentItem.Text);

            chatCompletionOptions.Messages.Add(role == ChatRole.User
                ? new ChatRequestUserMessage(content.ToString())
                : new ChatRequestSystemMessage(content.ToString()));
        }

        var streamingResponse =
            await openAiClient.GetChatCompletionsStreamingAsync(chatCompletionOptions, cancellationToken);

        await foreach (var update in streamingResponse)
            if (update.ContentUpdate != null)
                yield return update.ContentUpdate;
    }

    public async Task<GroundingResult> GetGroundingAsync(string query, List<ChatMessage> chatHistory,
        SearchConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchRequest = new SearchRequest
            {
                Query = query,
                Config = config,
                ChatHistory = chatHistory
            };

            var searchResponse = await searchService.SearchAsync(searchRequest, cancellationToken);

            var references = searchResponse.Results.Select(result => new Citation
            {
                Id = result.Id,
                Content = result.Content,
                ContentType = result.ContentType,
                ContentPath = result.ContentPath,
                Title = result.Metadata.TryGetValue("content_title", out var title) ? title?.ToString() : null,
                Relevance = result.Score
            }).ToList();

            return new GroundingResult
            {
                References = references,
                Metadata = new Dictionary<string, object>
                {
                    ["total_results"] = searchResponse.TotalCount,
                    ["search_query"] = query
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in grounding for query: {Query}", query);
            return new GroundingResult();
        }
    }

    /// <summary>
    ///     Enhanced grounding method that includes user group filtering
    /// </summary>
    public async Task<GroundingResult> GetGroundingWithSecurityTrimmingAsync(string query, List<ChatMessage> chatHistory,
        SearchConfig config, string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user groups if access token is provided
            string[]? userGroups = null;
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                accessToken = accessToken.Replace("Bearer ", "");
                userGroups = await GraphUserGroupsUtility.GetUserGroupObjectIdsAsync(accessToken,
                        cancellationToken);
            }

            // Apply group-based filtering to search config
            var enhancedConfig = config;
            if (userGroups?.Length > 0)
            {
                var groupFilter = $"group_ids/any(g:search.in(g, '{string.Join(",", userGroups)}'))";

                enhancedConfig = new SearchConfig
                {
                    Top = config.Top,
                    Threshold = config.Threshold,
                    IncludeImages = config.IncludeImages,
                    IncludeText = config.IncludeText,
                    UseKnowledgeAgent = config.UseKnowledgeAgent,
                    Filter = config.Filter?.Length > 0
                        ? [string.Join(" and ", config.Filter), groupFilter]
                        : [groupFilter]
                };

                logger.LogInformation("Applied group filtering for {GroupCount} groups", userGroups.Length);
            }

            var searchRequest = new SearchRequest
            {
                Query = query,
                Config = enhancedConfig,
                ChatHistory = chatHistory
            };

            var searchResponse = await searchService.SearchAsync(searchRequest, cancellationToken);

            var references = searchResponse.Results.Select(result => new Citation
            {
                Id = result.Id,
                Content = result.Content,
                ContentType = result.ContentType,
                ContentPath = result.ContentPath,
                Title = result.Metadata.TryGetValue("content_title", out var title) ? title?.ToString() : null,
                Relevance = result.Score
            }).ToList();

            return new GroundingResult
            {
                References = references,
                Metadata = new Dictionary<string, object>
                {
                    ["total_results"] = searchResponse.TotalCount,
                    ["search_query"] = query,
                    ["user_groups_applied"] = userGroups?.Length ?? 0,
                    ["user_groups"] = userGroups ?? []
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in grounding with user groups for query: {Query}", query);
            return new GroundingResult();
        }
    }

    private async Task<ChatResponse> ChatWithKnowledgeAgentAsync(ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var processingSteps = new List<ProcessingStep>();

        try
        {
            processingSteps.Add(new ProcessingStep
            {
                Title = "Initializing Knowledge Agent Pattern",
                Type = "agent",
                Description = "Setting up Azure Search Knowledge Agent pattern simulation"
            });

            // Get configuration values (preparing for when Knowledge Agent APIs become available)
            var agentName = configuration["AzureSearch:KnowledgeAgentName"] ?? "knowledge-agent";
            var rerankerThreshold = float.Parse(configuration["AzureSearch:RerankerThreshold"] ?? "2.5");

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Configuration",
                Type = "agent",
                Description = $"Configured for agent '{agentName}' with reranker threshold {rerankerThreshold}"
            });

            // TODO: When Knowledge Agent APIs are available in Azure.Search.Documents, replace with:
            // var knowledgeAgent = new KnowledgeAgentAzureOpenAIModel(
            //     endpoint: new Uri(endpoint),
            //     agentName: agentName,
            //     credential: new DefaultAzureCredential()
            // );

            processingSteps.Add(new ProcessingStep
            {
                Title = "Preparing Knowledge Agent Messages",
                Type = "agent",
                Description = "Converting chat history to Knowledge Agent message format"
            });

            // Prepare messages in Knowledge Agent format (exactly as in the GitHub sample)
            var messages = new List<Dictionary<string, string>>();

            // Add chat history (excluding system messages as per Knowledge Agent pattern)
            foreach (var historyMessage in request.ChatHistory.Where(m => m.Role.ToLowerInvariant() != "system"))
                messages.Add(new Dictionary<string, string>
                {
                    { "role", historyMessage.Role.ToLowerInvariant() },
                    { "content", historyMessage.Content }
                });

            // Add current user message
            messages.Add(new Dictionary<string, string>
            {
                { "role", "user" },
                { "content", request.Message }
            });

            processingSteps.Add(new ProcessingStep
            {
                Title = "Executing Knowledge Agent Pattern",
                Type = "agent",
                Description = $"Processing {messages.Count} messages using Knowledge Agent pattern"
            });

            // Execute Knowledge Agent pattern using our enhanced implementation
            var knowledgeAgentResponse = await ExecuteKnowledgeAgentPatternAsync(
                messages,
                request.SearchConfig,
                rerankerThreshold,
                request, // Pass the full request for security trimming
                cancellationToken);

            processingSteps.AddRange(knowledgeAgentResponse.ProcessingSteps);

            // TODO: When Knowledge Agent APIs are available, replace above with:
            // var knowledgeAgentMessages = messages
            //     .Where(message => message["role"] != "system")
            //     .Select(message => new KnowledgeAgentMessage(
            //         role: message["role"],
            //         content: new[] { new KnowledgeAgentMessageTextContent(message["content"]) }))
            //     .ToList();
            //
            // var retrievalResult = await knowledgeAgent.RetrieveAsync(
            //     retrievalRequest: new KnowledgeAgentRetrievalRequest(messages: knowledgeAgentMessages)
            //     {
            //         TargetIndexParams = { 
            //             new KnowledgeAgentIndexParams { 
            //                 IndexName = indexName, 
            //                 RerankerThreshold = rerankerThreshold 
            //             } 
            //         }
            //     },
            //     cancellationToken: cancellationToken
            // );
            //
            // var agentResponse = (retrievalResult.Value.Response[0].Content[0] as KnowledgeAgentMessageTextContent)?.Text;
            // messages.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", agentResponse } });

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Pattern Complete",
                Type = "agent",
                Description = "Successfully completed Knowledge Agent pattern processing"
            });

            return new ChatResponse
            {
                Response = knowledgeAgentResponse.Response,
                RequestId = requestId,
                Citations = knowledgeAgentResponse.Citations,
                ProcessingSteps = processingSteps
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Knowledge Agent pattern processing for request: {RequestId}", requestId);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Pattern Error",
                Type = "error",
                Description = $"Error in Knowledge Agent pattern: {ex.Message}"
            });

            // Fallback to traditional RAG if Knowledge Agent pattern fails
            logger.LogWarning("Falling back to traditional RAG due to Knowledge Agent pattern error: {Error}",
                ex.Message);
            return await ChatWithTraditionalRagAsync(request, cancellationToken);
        }
    }

    private async Task<ChatResponse> ChatWithTraditionalRagAsync(ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var processingSteps = new List<ProcessingStep>();

        try
        {
            // Step 1: Get grounding results
            processingSteps.Add(new ProcessingStep
            {
                Title = "Grounding user message",
                Type = "search",
                Description = $"Searching for relevant content for: {request.Message}"
            });

            var groundingResult = request.RequireSecurityTrimming == false
                ? await GetGroundingAsync(request.Message, request.ChatHistory, request.SearchConfig, cancellationToken)
                : await GetGroundingWithSecurityTrimmingAsync(request.Message, request.ChatHistory, request.SearchConfig,
                    request.Token, cancellationToken);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Grounding results received",
                Type = "data",
                Description = $"Retrieved {groundingResult.References.Count} relevant references",
                Content = new
                {
                    ReferenceCount = groundingResult.References.Count, References = groundingResult.References.Take(5)
                }
            });

            // Step 2: Prepare LLM messages
            var llmMessages = await PrepareLlmMessagesAsync(groundingResult, request.ChatHistory, request.Message,
                cancellationToken);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Preparing LLM messages",
                Type = "llm",
                Description = $"Prepared {llmMessages.Count} messages for the language model"
            });

            // Step 3: Generate response
            var chatCompletionOptions = new ChatCompletionsOptions
            {
                DeploymentName = _modelName,
                Temperature = 0.7f,
                MaxTokens = 10000
            };

            foreach (var message in llmMessages)
            {
                var role = message.Role switch
                {
                    "system" => ChatRole.System,
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };

                var content = new StringBuilder();
                foreach (var contentItem in message.Content)
                    if (contentItem.Type == "text" && !string.IsNullOrEmpty(contentItem.Text))
                        content.AppendLine(contentItem.Text);

                chatCompletionOptions.Messages.Add(role == ChatRole.User
                    ? new ChatRequestUserMessage(content.ToString())
                    : new ChatRequestSystemMessage(content.ToString()));
            }

            var response = await openAiClient.GetChatCompletionsAsync(chatCompletionOptions, cancellationToken);
            var responseText = response.Value.Choices.First().Message.Content;

            processingSteps.Add(new ProcessingStep
            {
                Title = "LLM response generated",
                Type = "llm",
                Description = "Generated response from language model"
            });

            // Extract citations
            var citations = ExtractCitations(responseText, groundingResult.References);

            return new ChatResponse
            {
                Response = responseText,
                RequestId = requestId,
                Citations = citations,
                ProcessingSteps = processingSteps
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in traditional RAG chat processing for request: {RequestId}", requestId);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Error occurred",
                Type = "error",
                Description = ex.Message
            });

            return new ChatResponse
            {
                Response = "I apologize, but I encountered an error while processing your request. Please try again.",
                RequestId = requestId,
                ProcessingSteps = processingSteps
            };
        }
    }

    private async Task<List<LLMMessage>> PrepareLlmMessagesAsync(GroundingResult groundingResult,
        List<ChatMessage> chatHistory, string userMessage, CancellationToken cancellationToken)
    {
        var messages = new List<LLMMessage>
        {
            // System message
            new()
            {
                Role = "system",
                Content =
                [
                    new MessageContent
                    {
                        Type = "text",
                        Text = GetSystemPrompt()
                    }
                ]
            }
        };

        // Chat history
        foreach (var historyMessage in chatHistory)
            messages.Add(new LLMMessage
            {
                Role = historyMessage.Role,
                Content =
                [
                    new MessageContent
                    {
                        Type = "text",
                        Text = historyMessage.Content
                    }
                ]
            });

        // User message
        messages.Add(new LLMMessage
        {
            Role = "user",
            Content =
            [
                new MessageContent
                {
                    Type = "text",
                    Text = userMessage
                }
            ]
        });

        // Context from grounding
        var contextContent = new List<MessageContent>();
        foreach (var reference in groundingResult.References)
            switch (reference.ContentType)
            {
                case "image/jpeg":
                case "image/png":
                    contextContent.Add(new MessageContent
                    {
                        Type = "text",
                        Text = $"Image reference [{reference.Id}]: {reference.ContentPath}"
                    });

                    // For images, we would add the base64 encoded image
                    var imageBase64 = await GetImageAsBase64Async(reference.ContentPath, cancellationToken);
                    if (!string.IsNullOrEmpty(imageBase64))
                        contextContent.Add(new MessageContent
                        {
                            Type = "image_url",
                            ImageUrl = new ImageUrl
                            {
                                Url = $"data:{reference.ContentType};base64,{imageBase64}"
                            }
                        });
                    break;

                default:
                    // Everything else is considered text
                    contextContent.Add(new MessageContent
                    {
                        Type = "text",
                        Text = $"Source [{reference.Id}]: {reference.Content}"
                    });
                    break;
            }

        if (contextContent.Any())
            messages.Add(new LLMMessage
            {
                Role = "user",
                Content = contextContent
            });

        return messages;
    }

    private async Task<string?> GetImageAsBase64Async(string imagePath, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(imagePath);

            if (await blobClient.ExistsAsync(cancellationToken))
            {
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                return Convert.ToBase64String(response.Value.Content.ToArray());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error retrieving image: {ImagePath}", imagePath);
        }

        return null;
    }

    private List<Citation> ExtractCitations(string responseText, List<Citation> availableCitations)
    {
        var citations = new List<Citation>();

        // Simple citation extraction - look for [id] patterns in the response
        foreach (var citation in availableCitations)
            if (responseText.Contains($"[{citation.Id}]", StringComparison.OrdinalIgnoreCase))
                citations.Add(citation);

        return citations;
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant that helps users find and understand information from their documents. 
You have access to a collection of documents and images that have been indexed for search.

When answering questions:
1. Use the provided context from the search results to inform your responses
2. When referencing specific information, cite the source using [id] format
3. If an image is relevant, describe what you can see in it
4. Be concise but thorough in your explanations
5. If you cannot find relevant information in the provided context, say so clearly

Always provide helpful, accurate responses based on the available information.";
    }

    private string GetKnowledgeAgentSystemPrompt()
    {
        return
            @"You are an advanced Knowledge Agent with sophisticated reasoning capabilities. You have access to a curated set of knowledge sources that have been retrieved and reranked based on relevance to the user's query.

KNOWLEDGE AGENT PRINCIPLES:
1. Multi-source Integration: Synthesize information from multiple sources to provide comprehensive answers
2. Citation Accuracy: Always cite sources using [id] format when referencing specific information
3. Relevance Filtering: Focus on the most relevant information from the provided sources
4. Contextual Understanding: Consider the conversation history and user intent
5. Accuracy Priority: Only make claims that can be supported by the provided sources

RESPONSE GUIDELINES:
- Provide direct, helpful answers based on the knowledge sources
- Use [id] format to cite sources for specific claims or information
- If information is not available in the sources, clearly state this limitation
- Synthesize information from multiple sources when appropriate
- Maintain conversational flow while being informative and accurate
- Structure responses clearly with good organization and flow

QUALITY STANDARDS:
- Ensure all factual claims are supported by cited sources
- Provide comprehensive coverage of the topic when sources allow
- Maintain consistency with previous conversation context
- Use clear, professional, and helpful language
- Balance completeness with conciseness

You are designed to be a reliable, accurate, and helpful Knowledge Agent that users can trust for well-sourced information.";
    }

    private async Task<AgenticChatResponse> ExecuteKnowledgeAgentPatternAsync(
        List<Dictionary<string, string>> knowledgeAgentMessages,
        SearchConfig config,
        float rerankerThreshold,
        ChatRequest request, // Added full request for security trimming
        CancellationToken cancellationToken)
    {
        var processingSteps = new List<ProcessingStep>();

        try
        {
            // Extract the latest user message for search
            var userMessage = knowledgeAgentMessages.LastOrDefault(m => m["role"] == "user")?["content"] ??
                              string.Empty;

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Query Analysis",
                Type = "agent",
                Description = "Analyzing query using Knowledge Agent principles"
            });

            // Apply security trimming if required
            var enhancedConfig = config;
            if (request.RequireSecurityTrimming && !string.IsNullOrWhiteSpace(request.Token))
            {
                try
                {
                    var accessToken = request.Token.Replace("Bearer ", "");
                    var userGroups = await GraphUserGroupsUtility.GetUserGroupObjectIdsAsync(accessToken, cancellationToken);
                    
                    if (userGroups?.Length > 0)
                    {
                        var groupFilter = $"group_ids/any(g:search.in(g, '{string.Join(",", userGroups)}'))";
                        
                        enhancedConfig = new SearchConfig
                        {
                            Top = config.Top,
                            Threshold = config.Threshold,
                            IncludeImages = config.IncludeImages,
                            IncludeText = config.IncludeText,
                            UseKnowledgeAgent = config.UseKnowledgeAgent,
                            Filter = config.Filter?.Length > 0
                                ? [string.Join(" and ", config.Filter), groupFilter]
                                : [groupFilter]
                        };

                        logger.LogInformation("Knowledge Agent: Applied group filtering for {GroupCount} groups", userGroups.Length);
                        
                        processingSteps.Add(new ProcessingStep
                        {
                            Title = "Security Trimming Applied",
                            Type = "security",
                            Description = $"Applied security trimming with {userGroups.Length} user groups"
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Knowledge Agent: Failed to apply security trimming, proceeding without group filtering");
                    processingSteps.Add(new ProcessingStep
                    {
                        Title = "Security Trimming Warning",
                        Type = "warning",
                        Description = "Security trimming requested but failed to retrieve user groups"
                    });
                }
            }

            // Perform enhanced search using Knowledge Agent principles
            // This mimics what the actual Knowledge Agent would do internally
            var searchRequest = new SearchRequest
            {
                Query = userMessage,
                Config = new SearchConfig
                {
                    Top = Math.Min(enhancedConfig.Top, 20), // Knowledge Agent typically uses more sources
                    Threshold = Math.Max(enhancedConfig.Threshold - 0.2, 0.3), // Lower threshold for broader retrieval
                    Filter = enhancedConfig.Filter,
                    IncludeImages = enhancedConfig.IncludeImages,
                    IncludeText = enhancedConfig.IncludeText,
                    UseKnowledgeAgent = true // Mark as Knowledge Agent request
                },
                ChatHistory = knowledgeAgentMessages
                    .Select(m => new ChatMessage { Role = m["role"], Content = m["content"] })
                    .ToList()
            };

            var searchResponse = await searchService.SearchAsync(searchRequest, cancellationToken);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Retrieval",
                Type = "agent",
                Description =
                    $"Retrieved {searchResponse.Results.Count} knowledge sources with reranker threshold {rerankerThreshold}"
            });

            // Apply reranking similar to Knowledge Agent's approach
            var rerankedResults = searchResponse.Results
                .Where(r => r.Score >= rerankerThreshold / 4.0) // Convert threshold to our scoring system
                .OrderByDescending(r => r.Score)
                .Take(enhancedConfig.Top)
                .ToList();

            var citations = rerankedResults.Select(result => new Citation
            {
                Id = result.Id,
                Content = result.Content,
                ContentType = result.ContentType,
                ContentPath = result.ContentPath,
                Title = result.Metadata.TryGetValue("content_title", out var title) ? title?.ToString() : null,
                Relevance = result.Score
            }).ToList();

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Synthesis",
                Type = "agent",
                Description = $"Synthesizing response from {citations.Count} reranked sources"
            });

            // Generate response using Knowledge Agent-style prompting
            var knowledgeAgentPrompt = GetKnowledgeAgentSystemPrompt();
            var contextBuilder = new StringBuilder();

            contextBuilder.AppendLine("# Knowledge Sources Retrieved:");
            foreach (var citation in citations)
            {
                contextBuilder.AppendLine($"## Source [{citation.Id}] - {citation.Title ?? "Document"}");
                contextBuilder.AppendLine($"Relevance Score: {citation.Relevance:F3}");
                contextBuilder.AppendLine($"Content: {citation.Content}");
                if (!string.IsNullOrEmpty(citation.ContentPath))
                    contextBuilder.AppendLine($"Path: {citation.ContentPath}");
                contextBuilder.AppendLine();
            }

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _modelName,
                Temperature = 0.6f, // Knowledge Agent typically uses slightly lower temperature
                MaxTokens = 4000,
                Messages = { new ChatRequestSystemMessage(knowledgeAgentPrompt) }
            };

            // Add conversation history
            foreach (var message in knowledgeAgentMessages.Take(knowledgeAgentMessages.Count - 1))
            {
                var role = message["role"].ToLowerInvariant() == "user" ? ChatRole.User : ChatRole.Assistant;
                chatOptions.Messages.Add(role == ChatRole.User
                    ? new ChatRequestUserMessage(message["content"])
                    : new ChatRequestAssistantMessage(message["content"]));
            }

            // Add current query with knowledge context
            chatOptions.Messages.Add(new ChatRequestUserMessage($@"
User Query: {userMessage}

{contextBuilder}

Using the knowledge sources above, provide a comprehensive and accurate response. 
Cite sources using [id] format when referencing specific information.
Focus on being helpful, accurate, and thorough in your analysis.
"));

            var response = await openAiClient.GetChatCompletionsAsync(chatOptions, cancellationToken);
            var responseText = response.Value.Choices.First().Message.Content;

            // Extract citations
            var usedCitations = ExtractCitations(responseText, citations);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Response Generated",
                Type = "agent",
                Description = $"Generated response with {usedCitations.Count} active citations"
            });

            return new AgenticChatResponse
            {
                Response = responseText,
                Citations = usedCitations,
                ProcessingSteps = processingSteps
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Knowledge Agent pattern execution");
            throw;
        }
    }

    /// <summary>
    ///     Chat method using the actual Azure Search Knowledge Agent APIs.
    ///     This method uses the KnowledgeAgentRetrievalClient from Azure.Search.Documents.Agents
    ///     to perform retrieval and response generation using the Knowledge Agent pattern.
    ///     Requires Azure.Search.Documents v11.7.0-beta.5 or later with Knowledge Agent APIs.
    /// </summary>
    private async Task<ChatResponse> ChatWithKnowledgeAISearchAgent(ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var processingSteps = new List<ProcessingStep>();

        try
        {
            processingSteps.Add(new ProcessingStep
            {
                Title = "Initializing Knowledge Agent Retrieval Client",
                Type = "agent",
                Description = "Setting up Azure Search Knowledge Agent with beta APIs"
            });

            // Get configuration values
            var agentName = configuration["AzureSearch:KnowledgeAgentName"] ?? "knowledge-agent";
            var indexName = configuration["AzureSearch:IndexName"] ??
                            throw new InvalidOperationException("AzureSearch:IndexName configuration is required");
            var openAIEndpoint = configuration["AzureSearch:OpenAIEndpoint"] ??
                                 throw new InvalidOperationException(
                                     "AzureSearch:OpenAIEndpoint configuration is required");
            var rerankerThreshold = float.Parse(configuration["AzureSearch:RerankerThreshold"] ?? "2.5");

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Configuration",
                Type = "agent",
                Description = $"Agent: '{agentName}', Index: '{indexName}', Reranker Threshold: {rerankerThreshold}"
            });

            // Prepare messages in Knowledge Agent format (excluding system messages as per Knowledge Agent pattern)
            var messages = new List<Dictionary<string, string>>();

            // Add chat history (excluding system messages as per Knowledge Agent pattern)
            foreach (var historyMessage in request.ChatHistory.Where(m => m.Role.ToLowerInvariant() != "system"))
                messages.Add(new Dictionary<string, string>
                {
                    { "role", historyMessage.Role.ToLowerInvariant() },
                    { "content", historyMessage.Content }
                });

            // Add current user message
            messages.Add(new Dictionary<string, string>
            {
                { "role", "user" },
                { "content", request.Message }
            });

            processingSteps.Add(new ProcessingStep
            {
                Title = "Preparing Knowledge Agent Messages",
                Type = "agent",
                Description = $"Converted {messages.Count} messages for Knowledge Agent processing"
            });

            // Initialize the Knowledge Agent Retrieval Client using the beta APIs
            var agentClient = new KnowledgeAgentRetrievalClient(
                new Uri(openAIEndpoint),
                agentName,
                new DefaultAzureCredential()
            );

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Client Initialized",
                Type = "agent",
                Description = "Successfully created KnowledgeAgentRetrievalClient"
            });

            // Execute the Knowledge Agent retrieval using the beta APIs
            var retrievalResult = await agentClient.RetrieveAsync(
                new KnowledgeAgentRetrievalRequest(
                    messages
                        .Where(message => message["role"] != "system")
                        .Select(message => new KnowledgeAgentMessage(
                            message["role"],
                            [new KnowledgeAgentMessageTextContent(message["content"])]))
                        .ToList()
                )
                {
                    TargetIndexParams =
                    {
                        new KnowledgeAgentIndexParams
                        {
                            IndexName = indexName,
                            RerankerThreshold = rerankerThreshold
                        }
                    }
                },
                cancellationToken: cancellationToken);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Retrieval Complete",
                Type = "agent",
                Description = "Successfully retrieved response using Knowledge Agent APIs"
            });

            // Extract the response from the Knowledge Agent result
            var agentResponse = string.Empty;
            var citations = new List<Citation>();

            if (retrievalResult?.Value?.Response != null && retrievalResult.Value.Response.Count > 0)
            {
                var responseMessage = retrievalResult.Value.Response[0];
                if (responseMessage.Content != null && responseMessage.Content.Count > 0)
                {
                    var textContent = responseMessage.Content[0] as KnowledgeAgentMessageTextContent;
                    agentResponse = textContent?.Text ?? string.Empty;
                }

                // Extract citations from the Knowledge Agent response
                // The actual citation extraction will depend on the specific structure returned by the API
                //if (retrievalResult.Value.Citations != null)
                //{
                //    citations = retrievalResult.Value.Citations.Select(citation => new Citation
                //    {
                //        Id = citation.Id ?? Guid.NewGuid().ToString(),
                //        Content = citation.Content ?? string.Empty,
                //        ContentType = citation.ContentType ?? "text/plain",
                //        ContentPath = citation.Url ?? string.Empty,
                //        Title = citation.Title ?? "Document",
                //        Relevance = citation.Score ?? 0.0
                //    }).ToList();
                //}
            }

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Response Processing",
                Type = "agent",
                Description = $"Processed response with {citations.Count} citations from Knowledge Agent"
            });

            if (string.IsNullOrEmpty(agentResponse))
            {
                logger.LogWarning("Knowledge Agent returned empty response for request: {RequestId}", requestId);
                agentResponse =
                    "I apologize, but I wasn't able to generate a response using the Knowledge Agent. Please try rephrasing your question.";
            }

            return new ChatResponse
            {
                Response = agentResponse,
                RequestId = requestId,
                Citations = citations,
                ProcessingSteps = processingSteps
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Knowledge Agent AI Search processing for request: {RequestId}", requestId);

            processingSteps.Add(new ProcessingStep
            {
                Title = "Knowledge Agent Error",
                Type = "error",
                Description = $"Error in Knowledge Agent processing: {ex.Message}"
            });

            // Fallback to traditional RAG if Knowledge Agent fails
            logger.LogWarning("Falling back to traditional RAG due to Knowledge Agent error: {Error}", ex.Message);
            return await ChatWithTraditionalRagAsync(request, cancellationToken);
        }
    }

    /// <summary>
    ///     Example method showing how to use user groups in your chat processing
    /// </summary>
    private async Task<ChatResponse> ChatWithUserGroupsAsync(ChatRequest request, string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user groups for potential filtering/personalization
            var userGroups = await GraphUserGroupsUtility.GetUserGroupObjectIdsAsync(accessToken, cancellationToken);

            logger.LogInformation("Processing chat request with {GroupCount} user groups", userGroups.Length);

            // Example: Add user groups to search filter
            if (userGroups.Length > 0)
            {
                // Modify the search configuration to include group-based filtering using search.in syntax
                var groupFilter = $"group_ids/any(g:search.in(g, '{string.Join(",", userGroups)}'))";

                if (request.SearchConfig.Filter?.Length > 0)
                {
                    // Combine existing filters with group filter
                    var existingFilters = string.Join(" and ", request.SearchConfig.Filter);
                    request.SearchConfig.Filter = [existingFilters, groupFilter];
                }
                else
                {
                    request.SearchConfig.Filter = [groupFilter];
                }
            }

            // Continue with your existing chat logic
            return await ChatAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in chat processing with user groups");
            // Fallback to regular chat without group filtering
            return await ChatAsync(request, cancellationToken);
        }
    }
}