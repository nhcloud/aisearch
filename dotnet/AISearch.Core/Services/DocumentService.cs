using System.Text;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AISearch.Core.Services;

public class DocumentService(
    SearchClient searchClient,
    BlobServiceClient blobServiceClient,
    OpenAIClient openAiClient,
    ILogger<DocumentService> logger,
    IConfiguration configuration,
    IContentExtraction contentExtraction)
    : IDocumentService
{
    private readonly string _blobPrefix = "15d05f6e-046b-4ed5-9ab8-4b6c25f719b5";
    private readonly IConfiguration _configuration = configuration;
    private readonly string _containerName = "documents";
    private readonly OpenAIClient _openAIClient = openAiClient;

    public async Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = request.FileName;
            var contentType = request.ContentType;

            // Upload to blob storage
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient($"{_blobPrefix}/{fileName}");
            await blobClient.UploadAsync(request.FileStream, true, cancellationToken);

            // Get the blob URL for DocumentIntelligence
            var blobUrl = blobClient.Uri.ToString();

            // Extract content using DocumentIntelligence service
            var chunks = await contentExtraction.ExtractContentAsync(blobUrl, []);


            // Index the document chunks
            var documents = chunks.Select(chunk => new SearchDocument
            {
                ["id"] = chunk.Id,
                ["content_id"] = chunk.ContentId,
                ["content_text"] = chunk.Content,
                ["content_type"] = chunk.ContentType,
                ["size"] = request.FileStream.Length,
                ["content_path"] = chunk.ContentPath,
                ["content_title"] = request.Title ?? fileName,
                ["chunk_index"] = chunk.ChunkIndex,
                ["created_at"] = DateTimeOffset.UtcNow,
                ["content_embedding"] = chunk.Vector,
                ["user_ids"] = Utilities.GetUsers(),
                ["group_ids"] = Utilities.GetGroups(),
                ["path_parts"] = Utilities.GetPathHierarchy(chunk.ContentPath)
            }).ToList();

            await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents),
                cancellationToken: cancellationToken);

            return new DocumentUploadResponse
            {
                DocumentId = chunks[0].ContentId,
                FileName = fileName,
                ContentType = contentType,
                FileSize = request.FileStream.Length,
                Success = true,
                Message = "Document uploaded and indexed successfully"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading document: {FileName}", request.FileName);
            return new DocumentUploadResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete from search index
            var searchOptions = new SearchOptions
            {
                Filter = $"content_id eq '{documentId}'"
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            var documentsToDelete = new List<SearchDocument>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
                documentsToDelete.Add(new SearchDocument { ["id"] = result.Document["id"] });

            if (documentsToDelete.Any())
                await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Delete(documentsToDelete),
                    cancellationToken: cancellationToken);

            // Delete from blob storage
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await foreach (var blob in containerClient.GetBlobsAsync(prefix: documentId,
                               cancellationToken: cancellationToken))
                await containerClient.DeleteBlobAsync(blob.Name, cancellationToken: cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return false;
        }
    }

    public async Task<DocumentInfo?> GetDocumentInfoAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = $"content_id eq '{documentId}'",
                Size = 1
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            SearchResult<SearchDocument>? firstResult = null;

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                firstResult = result;
                break;
            }

            if (firstResult == null) return null;

            var document = firstResult.Document;
            return new DocumentInfo
            {
                Id = documentId,
                Title = document.GetString("content_title") ?? string.Empty,
                ContentType = document.GetString("content_type") ?? string.Empty,
                CreatedAt = (document.GetDateTimeOffset("created_at") ?? DateTimeOffset.UtcNow).DateTime,
                LastModified = (document.GetDateTimeOffset("created_at") ?? DateTimeOffset.UtcNow).DateTime
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting document info: {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<DocumentContent?> GetDocumentContentAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = $"content_id eq '{documentId}'",
                OrderBy = { "chunk_index" }
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            var chunks = new List<DocumentChunk>();
            var contentBuilder = new StringBuilder();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                var document = result.Document;
                var content = document.GetString("content_text") ?? string.Empty;
                contentBuilder.AppendLine(content);

                chunks.Add(new DocumentChunk
                {
                    Id = document.GetString("id") ?? string.Empty,
                    Content = content,
                    ContentType = document.GetString("content_type") ?? "text",
                    ChunkIndex = document.GetInt32("chunk_index") ?? 0,
                    ContentPath = document.GetString("content_path") ?? string.Empty
                });
            }

            if (!chunks.Any()) return null;

            return new DocumentContent
            {
                Id = documentId,
                Content = contentBuilder.ToString(),
                ContentType = chunks.First().ContentType,
                Chunks = chunks
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting document content: {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<List<DocumentInfo>> ListDocumentsAsync(int skip = 0, int take = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = "chunk_index eq 0", // Get only the first chunk of each document
                Skip = skip,
                Size = take,
                OrderBy = { "created_at desc" }
            };

            var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            var documents = new List<DocumentInfo>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                var document = result.Document;

                documents.Add(new DocumentInfo
                {
                    Id = document.GetString("content_id") ?? string.Empty,
                    Title = document.GetString("content_title") ?? string.Empty,
                    ContentType = document.GetString("content_type") ?? string.Empty,
                    CreatedAt = (document.GetDateTimeOffset("created_at") ?? DateTimeOffset.UtcNow).DateTime,
                    LastModified = (document.GetDateTimeOffset("created_at") ?? DateTimeOffset.UtcNow).DateTime,
                    FileSize = document.GetInt64("size") ?? 0
                });
            }

            return documents;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing documents");
            throw;
        }
    }

    public Task<bool> IndexDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically re-process and re-index an existing document
            // For now, return true as the document should already be indexed during upload
            logger.LogInformation("Re-indexing document: {DocumentId}", documentId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error re-indexing document: {DocumentId}", documentId);
            return Task.FromResult(false);
        }
    }
}