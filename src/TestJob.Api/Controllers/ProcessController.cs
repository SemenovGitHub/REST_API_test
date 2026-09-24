using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TestJob.Api.Models;
using TestJob.Api.Services;

namespace TestJob.Api.Controllers;

/// <summary>
///     Processes an HTML page supplied in the request body.
/// </summary>
[Route("api")]
public sealed class ProcessController : ControllerBase
{
    private readonly IPageProcessService _service;

    public ProcessController(IPageProcessService service)
    {
        _service = service;
    }

    /// <summary>
    ///     Parses the page, stores matched elements, and decrypts the payload text.
    /// </summary>
    [HttpPost("process")]
    [SwaggerOperation(OperationId = nameof(Process))]
    [SwaggerResponse(StatusCodes.Status200OK, type: typeof(ProcessResponse))]
    public async Task<IActionResult> Process(
        [FromBody] ProcessRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Ok(new ProcessResponse
            {
                IsError = 1,
                ErrorCode = "missing_parameter",
                ErrorMessage = "Request body is missing."
            });
        }

        var result = await _service.ProcessAsync(request, cancellationToken);
        return Ok(result);
    }
}
