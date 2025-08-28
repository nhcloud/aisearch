using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AISearch.Api.Controllers;

/// <summary>
///     Document management controller providing endpoints for document upload, indexing, retrieval, and deletion.
///     Supports multimodal document types including text, images, and PDFs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DocumentsController(
    IDocumentService documentService,
    IApiUserService userService,
    ILogger<DocumentsController> logger)
    : BaseApiController(userService, logger)
{
    /// <summary>
    ///     Anonymous test endpoint to verify API is accessible without authentication
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Message = "Documents API is healthy and accessible",
            Service = "DocumentsController",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    /// <summary>
    ///     Test endpoint to verify API is accessible and check if authentication headers are present
    ///     This endpoint shows the difference between authenticated and anonymous access
    /// </summary>
    [HttpGet("test")]
    public IActionResult TestEndpoint()
    {
        LogAuthenticationInfo("TestEndpoint");

        var authInfo = GetAuthDebugInfo();
        var token = GetJwtToken();

        // Get ALL request headers for debugging
        var allHeaders = new Dictionary<string, string>();
        foreach (var header in Request.Headers) 
            allHeaders[header.Key] = string.Join(", ", header.Value.ToArray());

        return Ok(new
        {
            Message = "Documents API test endpoint - DEBUGGING HEADERS",

            // Show ALL headers the API received
            ReceivedHeaders = allHeaders,

            // Specifically check for Authorization
            HasAuthorizationHeader = Request.Headers.ContainsKey("Authorization"),
            AuthorizationHeaderValue = Request.Headers.ContainsKey("Authorization")
                ? Request.Headers["Authorization"].ToString()
                : "NOT PRESENT",

            // Other debug info
            AuthInfo = authInfo,
            TokenAvailable = !string.IsNullOrEmpty(token),
            TokenPreview = token != null ? $"{token.Substring(0, Math.Min(10, token.Length))}..." : null,
            UserId = GetCurrentUserId(),
            UserEmail = GetCurrentUserEmail(),
            UserName = GetCurrentUserName(),
            IsAuthenticated = IsAuthenticated(),
            ClaimsCount = GetAllUserClaims().Count,

            // Request info
            RequestMethod = Request.Method,
            RequestPath = Request.Path.ToString(),
            RequestQuery = Request.QueryString.ToString(),
            RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),

            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Uploads and indexes a new document
    /// </summary>
    /// <param name="file">Document file to upload</param>
    /// <param name="title">Optional document title</param>
    /// <param name="description">Optional document description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload response with document ID</returns>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocumentAsync(
        IFormFile file,
        [FromForm] string? title = null,
        [FromForm] string? description = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("UploadDocument");

            if (file == null || file.Length == 0) return BadRequest("No file provided");

            // Access token and user info using base class methods
            var userId = GetCurrentUserId();
            var token = GetJwtToken();

            _logger.LogInformation("Document upload request from user: {UserId}, Token available: {HasToken}",
                userId, !string.IsNullOrEmpty(token));

            var request = new DocumentUploadRequest
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Title = title,
                Description = description
            };

            var response = await documentService.UploadDocumentAsync(request, cancellationToken);

            if (response.Success)
                return CreatedAtRoute(
                    "GetDocument",
                    new { documentId = response.DocumentId },
                    response);

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    /// <summary>
    ///     Gets information about a specific document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document information</returns>
    [HttpGet("{documentId}", Name = "GetDocument")]
    public async Task<ActionResult<DocumentInfo>> GetDocumentAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("GetDocument");

            if (string.IsNullOrWhiteSpace(documentId)) return BadRequest("Document ID cannot be empty");

            var documentInfo = await documentService.GetDocumentInfoAsync(documentId, cancellationToken);

            if (documentInfo == null) return NotFound($"Document '{documentId}' not found");

            return Ok(documentInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document: {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while retrieving the document");
        }
    }

    /// <summary>
    ///     Gets the content of a specific document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document content with chunks</returns>
    [HttpGet("{documentId}/content")]
    public async Task<ActionResult<DocumentContent>> GetDocumentContentAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("GetDocumentContent");

            if (string.IsNullOrWhiteSpace(documentId)) return BadRequest("Document ID cannot be empty");

            var documentContent = await documentService.GetDocumentContentAsync(documentId, cancellationToken);

            if (documentContent == null) return NotFound($"Document content for '{documentId}' not found");

            return Ok(documentContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document content: {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while retrieving the document content");
        }
    }

    /// <summary>
    ///     Lists all documents with pagination
    /// </summary>
    /// <param name="skip">Number of documents to skip</param>
    /// <param name="take">Number of documents to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of documents</returns>
    [HttpGet]
    public async Task<ActionResult<List<DocumentInfo>>> ListDocumentsAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("ListDocuments");

            if (take > 100) return BadRequest("Take parameter cannot exceed 100");

            var documents = await documentService.ListDocumentsAsync(skip, take, cancellationToken);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
            return StatusCode(500, "An error occurred while listing documents");
        }
    }

    /// <summary>
    ///     Deletes a document and removes it from the index
    /// </summary>
    /// <param name="documentId">Document ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion result</returns>
    [HttpDelete("{documentId}")]
    public async Task<ActionResult> DeleteDocumentAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("DeleteDocument");

            if (string.IsNullOrWhiteSpace(documentId)) return BadRequest("Document ID cannot be empty");

            var success = await documentService.DeleteDocumentAsync(documentId, cancellationToken);

            if (success) return NoContent();

            return NotFound($"Document '{documentId}' not found or could not be deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }

    /// <summary>
    ///     Re-indexes a specific document
    /// </summary>
    /// <param name="documentId">Document ID to re-index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Re-indexing result</returns>
    [HttpPost("{documentId}/reindex")]
    public async Task<ActionResult> ReindexDocumentAsync(string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LogAuthenticationInfo("ReindexDocument");

            if (string.IsNullOrWhiteSpace(documentId)) return BadRequest("Document ID cannot be empty");

            var success = await documentService.IndexDocumentAsync(documentId, cancellationToken);

            if (success) return Ok(new { message = "Document re-indexed successfully" });

            return BadRequest("Failed to re-index document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing document: {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while re-indexing the document");
        }
    }

    // Add this to your DocumentsController for debugging header receipt
    [HttpGet("debug-headers")]
    [AllowAnonymous]
    public IActionResult DebugHeaders()
    {
        var headers = new Dictionary<string, string>();
        foreach (var h in Request.Headers)
            headers[h.Key] = string.Join(", ", h.Value.ToArray());
        return Ok(new
        {
            ReceivedHeaders = headers,
            HasAuthorization = Request.Headers.ContainsKey("Authorization"),
            AuthorizationHeader = Request.Headers["Authorization"].ToString()
        });
    }
}