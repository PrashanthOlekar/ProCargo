using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProCargo.Application.Common;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Infrastructure.Jobs;
using ProCargo.Infrastructure.Messaging;
using ProCargo.Infrastructure.Payments;
using ProCargo.Infrastructure.Persistence;
using ProCargo.Infrastructure.Platform;
using ProCargo.Infrastructure.Repositories;
using ProCargo.Infrastructure.Security;
using ProCargo.Infrastructure.Storage;

namespace ProCargo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("ProCargo")
                               ?? throw new InvalidOperationException("ConnectionStrings:ProCargo is not configured.");

        services.AddDbContext<ProCargoDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            if (environment.IsDevelopment()) options.EnableDetailedErrors();
        });

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Options
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddOptions<PortalOptions>().Bind(configuration.GetSection(PortalOptions.SectionName));
        services.AddOptions<FileUploadOptions>().Bind(configuration.GetSection(FileUploadOptions.SectionName));
        services.AddOptions<PaymentOptions>().Bind(configuration.GetSection(PaymentOptions.SectionName));
        services.AddOptions<PaymentGatewayOptions>().Bind(configuration.GetSection(PaymentGatewayOptions.SectionName));
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddOptions<SmsOptions>().Bind(configuration.GetSection(SmsOptions.SectionName));

        // Persistence
        services.AddScoped<StoredProcedureExecutor>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOwnerRepository, OwnerRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPricingRepository, PricingRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISettlementRepository, SettlementRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        // Security
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IOtpService, HmacOtpService>();
        services.AddSingleton<IFieldEncryptor, DataProtectionFieldEncryptor>();
        AddDataProtection(services, configuration);

        // Platform
        services.AddScoped<ISettingsProvider, SettingsProvider>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddSingleton<IDistanceEstimator, HaversineDistanceEstimator>();

        // File storage
        var storageProvider = configuration[$"{FileStorageOptions.SectionName}:Provider"] ?? "Local";
        if (storageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        // E-mail / SMS
        if ((configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Smtp").Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, NullEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        if ((configuration[$"{SmsOptions.SectionName}:Provider"] ?? "None").Equals("Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ISmsSender, HttpSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
        }
        else
        {
            services.AddSingleton<ISmsSender, NullSmsSender>();
        }

        // Payment gateway
        var gateway = configuration[$"{PaymentOptions.SectionName}:Gateway"] ?? "Sandbox";
        if (gateway.Equals("Razorpay", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
        }
        else
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException("The Sandbox payment gateway cannot be used in Production. Configure Payments:Gateway=Razorpay.");
            }

            services.AddSingleton<SandboxPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
            services.AddSingleton<ISandboxPaymentSimulator>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
        }

        if (environment.IsProduction() && configuration.GetValue<bool>($"{SecurityOptions.SectionName}:ExposeOtpForTesting"))
        {
            throw new InvalidOperationException("Security:ExposeOtpForTesting must not be enabled in Production.");
        }

        // Background work
        services.AddHostedService<SuperAdminBootstrapper>();
        if (configuration.GetValue("Jobs:QuotationExpiryEnabled", true))
        {
            services.AddHostedService<QuotationExpiryJob>();
        }

        return services;
    }

    /// <summary>
    /// Data Protection keys encrypt PAN / bank account numbers, so they must survive restarts and be shared by all
    /// instances: Blob Storage + Key Vault wrapping in Azure, a local folder in development.
    /// </summary>
    private static void AddDataProtection(IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName("ProCargo");
        var blobUri = configuration["DataProtection:BlobUri"];
        var keyId = configuration["DataProtection:KeyVaultKeyId"];

        if (!string.IsNullOrWhiteSpace(blobUri))
        {
            var credential = new Azure.Identity.DefaultAzureCredential();
            builder.PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);
            if (!string.IsNullOrWhiteSpace(keyId))
            {
                builder.ProtectKeysWithAzureKeyVault(new Uri(keyId), credential);
            }
        }
        else
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeysPath"] ?? "App_Data/keys"));
        }
    }
}
