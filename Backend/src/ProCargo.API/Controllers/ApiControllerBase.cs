using Microsoft.AspNetCore.Mvc;
using ProCargo.Application.Common;
using ProCargo.Application.Requests;

namespace ProCargo.API.Controllers;

/// <summary>Base for all v1 controllers: JSON in/out, standard error body, helpers for files.</summary>
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(Application.Responses.ErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Application.Responses.ErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(Application.Responses.ErrorResponse), StatusCodes.Status403Forbidden)]
public abstract class ApiControllerBase : ControllerBase
{
    protected static FileUpload ToUpload(IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);

    protected FileStreamResult ToFileResult(FileDownload download) =>
        File(download.Content, download.ContentType, download.FileName);

    protected string? IdempotencyKey =>
        Request.Headers.TryGetValue("Idempotency-Key", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString().Trim()
            : null;
}
