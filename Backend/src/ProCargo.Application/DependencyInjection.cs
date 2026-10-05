using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ProCargo.Application.Authorization;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Services.Bookings;
using ProCargo.Application.Services.Finance;
using ProCargo.Application.Services.Identity;
using ProCargo.Application.Services.MasterData;
using ProCargo.Application.Services.Notifications;
using ProCargo.Application.Services.Partners;
using ProCargo.Application.Services.Pricing;
using ProCargo.Application.Services.Quotations;
using ProCargo.Application.Services.Reports;
using ProCargo.Application.Services.Support;
using ProCargo.Application.Services.Trips;

namespace ProCargo.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case services, the resource-level access guard and all FluentValidation validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        services.AddScoped<AccessGuard>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountInviteService, AccountInviteService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMasterDataService, MasterDataService>();

        services.AddScoped<VerificationRules>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOwnerService, OwnerService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDocumentService, DocumentService>();

        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<ITripService, TripService>();

        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISettlementService, SettlementService>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();

        return services;
    }
}
