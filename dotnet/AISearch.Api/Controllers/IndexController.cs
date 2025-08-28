using Microsoft.AspNetCore.Mvc;

namespace AISearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IndexController(IIndexService indexService, ILogger<IndexController> logger)
    : ControllerBase
{
    private readonly ILogger<IndexController> _logger = logger;

    [HttpPost]
    public async Task<ActionResult<IndexResponse>> CreateIndexAsync([FromBody] IndexRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Index name cannot be empty");
        var response = await indexService.CreateIndexAsync(request, cancellationToken);
        return response.Success
            ? CreatedAtRoute("GetIndex", new { indexName = response.Name }, response)
            : BadRequest(response);
    }

    [HttpGet("{indexName}", Name = "GetIndex")]
    public async Task<ActionResult<IndexInfo>> GetIndexAsync(string indexName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexName)) return BadRequest("Index name cannot be empty");
        var indexInfo = await indexService.GetIndexAsync(indexName, cancellationToken);
        return indexInfo is null
            ? NotFound($"Index '{indexName}' not found")
            : Ok(indexInfo);
    }

    [HttpGet]
    public async Task<ActionResult<List<IndexInfo>>> ListIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = await indexService.ListIndexesAsync(cancellationToken);
        return Ok(indexes);
    }

    [HttpDelete("{indexName}")]
    public async Task<ActionResult> DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexName)) return BadRequest("Index name cannot be empty");
        var success = await indexService.DeleteIndexAsync(indexName, cancellationToken);
        return success ? NoContent() : NotFound($"Index '{indexName}' not found or could not be deleted");
    }
}