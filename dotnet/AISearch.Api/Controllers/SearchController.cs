using Microsoft.AspNetCore.Mvc;

namespace AISearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController(ISearchService searchService, ILogger<SearchController> logger)
    : ControllerBase
{
    private readonly ILogger<SearchController> _logger = logger;

    [HttpPost("search")]
    public async Task<ActionResult<SearchResponse>> SearchAsync([FromBody] SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest("Search query cannot be empty");
        var response = await searchService.SearchAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("vector-search")]
    public async Task<ActionResult<SearchResponse>> VectorSearchAsync([FromBody] VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QueryVector is not { Length: > 0 }) return BadRequest("Query vector cannot be empty");
        var response =
            await searchService.VectorSearchAsync(request.Query, request.QueryVector, request.Config,
                cancellationToken);
        return Ok(response);
    }

    [HttpGet("similar/{documentId}")]
    public async Task<ActionResult<List<SearchResult>>> GetSimilarDocumentsAsync(
        string documentId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return BadRequest("Document ID cannot be empty");
        var results = await searchService.GetSimilarDocumentsAsync(documentId, count, cancellationToken);
        return Ok(results);
    }
}

public record VectorSearchRequest(string Query = "", float[] QueryVector = null!, SearchConfig Config = null!)
{
    public VectorSearchRequest() : this("", [], new SearchConfig())
    {
    }
}