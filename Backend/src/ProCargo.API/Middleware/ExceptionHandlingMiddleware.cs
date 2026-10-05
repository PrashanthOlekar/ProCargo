using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Responses;

namespace ProCargo.API.Middleware;

/// <summary>
/// Converts exceptions into the standard error body. Business exceptions keep their message and code;
/// unexpected exceptions are logged with full detail but the client only receives a generic message and the
/// trace id (no stack traces, SQL or internal details).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // client closed request
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception after the response started");
                throw;
            }

            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (status, body) = exception switch
        {
            RequestValidationException v => (StatusCodes.Status400BadRequest,
                ErrorResponse.Create(v.Message, v.ErrorCode, traceId, v.Errors)),
            NotFoundException e => (StatusCodes.Status404NotFound, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            ForbiddenException e => (StatusCodes.Status403Forbidden, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            ConflictException e => (StatusCodes.Status409Conflict, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            ConcurrencyException e => (StatusCodes.Status409Conflict, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            Domain.Exceptions.DomainException e => (StatusCodes.Status422UnprocessableEntity, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            ExternalServiceException e => (StatusCodes.Status502BadGateway, ErrorResponse.Create(e.Message, e.ErrorCode, traceId)),
            BadHttpRequestException e => (e.StatusCode, ErrorResponse.Create("The request could not be read.", ErrorCodes.ValidationFailed, traceId)),
            JsonException => (StatusCodes.Status400BadRequest,
                ErrorResponse.Create("The request body is not valid JSON.", ErrorCodes.ValidationFailed, traceId)),
            _ => (StatusCodes.Status500InternalServerError,
                ErrorResponse.Create("An unexpected error occurred. Quote the trace id when contacting support.", ErrorCodes.InternalError, traceId))
        };

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request {Method} {Path} failed with {Status} {ErrorCode}", context.Request.Method, context.Request.Path,
                status, body.ErrorCode);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = null;
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), context.RequestAborted);
    }
}
