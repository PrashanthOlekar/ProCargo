using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Infrastructure.Jobs;

/// <summary>
/// Expires quotations past their validity every 15 minutes (and returns their bookings to UnderReview).
/// The procedure is idempotent and set-based, so running it on several instances at once is harmless.
/// </summary>
internal sealed class QuotationExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<QuotationExpiryJob> _logger;

    public QuotationExpiryJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<QuotationExpiryJob> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        do
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var quotations = scope.ServiceProvider.GetRequiredService<IQuotationRepository>();
                var expired = await quotations.ExpireOverdueAsync(_clock.GetUtcNow().UtcDateTime, stoppingToken);
                if (expired > 0) _logger.LogInformation("Expired {Count} quotations", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Quotation expiry run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>
/// Creates the first SuperAdmin from configuration (Bootstrap:SuperAdminEmail / Password / Phone / Name, supplied
/// through user-secrets or Key Vault) when no user with that e-mail exists. Nothing happens when not configured.
/// The account is flagged to change its password at first sign-in.
/// </summary>
internal sealed class SuperAdminBootstrapper : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SuperAdminBootstrapper> _logger;

    public SuperAdminBootstrapper(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<SuperAdminBootstrapper> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _configuration["Bootstrap:SuperAdminEmail"];
        var password = _configuration["Bootstrap:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var phone = IndianFormats.NormalizePhone(_configuration["Bootstrap:SuperAdminPhone"] ?? "+919000000000");
            var name = _configuration["Bootstrap:SuperAdminName"] ?? "Platform Administrator";
            var created = await identity.EnsureSuperAdminAsync(email.Trim(), email.Trim().ToUpperInvariant(), phone, name,
                hasher.Hash(password), cancellationToken);

            if (created) _logger.LogWarning("Bootstrap SuperAdmin account created; remove Bootstrap:SuperAdminPassword from configuration");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database may not be deployed yet (first container start); the API still starts and /health/ready reports it.
            _logger.LogError(ex, "SuperAdmin bootstrap failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
