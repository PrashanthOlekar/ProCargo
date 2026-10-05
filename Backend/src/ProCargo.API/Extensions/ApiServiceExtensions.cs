using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProCargo.API.Authorization;
using ProCargo.API.Filters;
using ProCargo.Application.Common;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Responses;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;
using ProCargo.Infrastructure.Security;

namespace ProCargo.API.Extensions;

public static class RateLimitPolicies
{
    /// <summary>Login, register, refresh, password reset: 10 per minute per IP.</summary>
    public const string Auth = "auth";

    /// <summary>OTP send/verify: 6 per 5 minutes per user.</summary>
    public const string Otp = "otp";

    /// <summary>Anonymous public endpoints (contact form, price estimate).</summary>
    public const string Public = "public";

    /// <summary>Payment gateway webhooks.</summary>
    public const string Webhook = "webhook";
}

public static class ApiServiceExtensions
{
    public const string CorsPolicy = "ProCargoPortals";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Model-binding failures (malformed JSON, wrong types) use the same error body as everything else.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(e => string.IsNullOrEmpty(e.Key) ? "request" : e.Key,
                            e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());
                    return new BadRequestObjectResult(ErrorResponse.Create("One or more validation errors occurred.",
                        ErrorCodes.ValidationFailed, context.HttpContext.TraceIdentifier, errors));
                };
            });

        services.AddProblemDetails();
        services.AddApiAuthentication(configuration);
        services.AddApiRateLimiting();
        services.AddApiCors(configuration, environment);
        services.AddApiSwagger();

        services.AddHealthChecks()
            .AddDbContextCheck<ProCargoDbContext>("database", tags: ["ready"],
                customTestQuery: async (db, ct) => await db.Database.CanConnectAsync(ct));

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Azure App Service / Front Door terminate TLS; their addresses are not known in advance.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddApplicationInsightsTelemetry();
        return services;
    }

    private static void AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudiences = [jwt.WebAudience, jwt.OperationsAudience],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = ProCargoClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // The audience must match the portal claim: a Web token cannot claim to be Operations.
                        var portal = context.Principal?.FindFirst(ProCargoClaimTypes.Portal)?.Value;
                        var audience = context.Principal?.FindFirst("aud")?.Value;
                        var expected = portal == nameof(PortalType.Operations) ? jwt.OperationsAudience : jwt.WebAudience;
                        if (audience != expected) context.Fail("Token audience does not match its portal.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, p => p.RequireAuthenticatedUser().RequireClaim(ProCargoClaimTypes.Portal, nameof(PortalType.Operations)))
            .AddPolicy(Policies.External, p => p.RequireAuthenticatedUser().RequireClaim(ProCargoClaimTypes.Portal, nameof(PortalType.Web)));
    }

    private static void AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    ErrorResponse.Create("Too many requests. Please wait and try again.", ErrorCodes.RateLimited, context.HttpContext.TraceIdentifier), ct);
            };

            // Global: 300 requests per minute per user (or per IP when anonymous).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                "auth:" + IpKey(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(RateLimitPolicies.Otp, context => RateLimitPartition.GetSlidingWindowLimiter(
                "otp:" + PartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromMinutes(5),
                    SegmentsPerWindow = 5
                }));

            options.AddPolicy(RateLimitPolicies.Public, context => RateLimitPartition.GetFixedWindowLimiter(
                "public:" + IpKey(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(RateLimitPolicies.Webhook, context => RateLimitPartition.GetFixedWindowLimiter(
                "webhook:" + IpKey(context), _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirst(ProCargoClaimTypes.UserId)?.Value is { } userId ? "user:" + userId : "ip:" + IpKey(context);

    private static string IpKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static void AddApiCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must list the portal origins.");
        }

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-ProCargo-Client", "X-Requested-With")
            .WithExposedHeaders("Content-Disposition", "Retry-After")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));
    }

    private static void AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ProCargo Logistics API",
                Version = "v1",
                Description = "Customer website, partner portals and Operations portal API. Authenticate with POST /api/v1/auth/login " +
                              "and use the access token as a Bearer token."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access token"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                }
            });

            var xml = Path.Combine(AppContext.BaseDirectory, "ProCargo.API.xml");
            if (File.Exists(xml)) options.IncludeXmlComments(xml);
            options.CustomSchemaIds(t => t.FullName?.Replace("+", ".", StringComparison.Ordinal));
        });
    }

    /// <summary>Claims principal helper used by controllers.</summary>
    public static bool IsOperations(this ClaimsPrincipal user) => user.HasClaim(ProCargoClaimTypes.Portal, nameof(PortalType.Operations));
}
