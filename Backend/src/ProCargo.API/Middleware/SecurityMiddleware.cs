using System.Text.Json;
using ProCargo.Application.Responses;
using ProCargo.Domain.Constants;

namespace ProCargo.API.Middleware;

/// <summary>Security response headers for an API that only ever returns JSON or file downloads.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        // Swagger UI needs scripts/styles; everything else is data only.
        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers.CacheControl = "no-store";
        }

        return _next(context);
    }
}

/// <summary>
/// Accounts created by an administrator (or with a temporary password) must change their password before using
/// anything except the auth endpoints. The access token carries the "mcp" claim until they do.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(ProCargoClaimTypes.MustChangePassword, "true")
            && context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                ErrorResponse.Create("Change your password to continue.", "PASSWORD_CHANGE_REQUIRED", context.TraceIdentifier), JsonOptions));
            return;
        }

        await _next(context);
    }
}
