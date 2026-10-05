using ProCargo.API.Extensions;
using ProCargo.API.Middleware;
using ProCargo.Application;
using ProCargo.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog: structured logs to console (App Service log stream / container logs) and Application Insights.
    // Request bodies, headers with credentials, tokens, OTPs and passwords are never logged.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ProCargo.API"), preserveStaticLogger: true);

    // Fail fast at startup if any service cannot be constructed or a scoped service leaks into a singleton.
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // POD uploads: up to 5 photos + signature
    });

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration, builder.Environment)
        .AddApi(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
        options.EnrichDiagnosticContext = (diagnostics, http) =>
        {
            diagnostics.Set("UserId", http.User.FindFirst("sub")?.Value ?? "anonymous");
            diagnostics.Set("Portal", http.User.FindFirst("portal")?.Value ?? "none");
        };
    });

    if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProCargo API v1");
            options.DocumentTitle = "ProCargo API";
        });
    }

    app.UseRouting();
    app.UseCors(ApiServiceExtensions.CorsPolicy);
    app.UseAuthentication();
    app.UseMiddleware<PasswordChangeRequiredMiddleware>();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();

    // Liveness: the process is up. Readiness: the database is reachable.
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false })
        .DisableRateLimiting();
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).DisableRateLimiting();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ProCargo API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point (exposed for WebApplicationFactory in integration tests).</summary>
public partial class Program;
