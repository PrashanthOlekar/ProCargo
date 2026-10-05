namespace ProCargo.Domain.Constants;

/// <summary>
/// Permission codes (seeded in sec.Permission). Each code is also the name of an ASP.NET Core
/// authorization policy. Internal permissions are only honoured in tokens issued for the
/// Operations portal; external permissions only in tokens issued for the Web portal.
/// </summary>
public static class Permissions
{
    // ---- internal (staff) ----
    public const string AccessOperationsPortal = nameof(AccessOperationsPortal);
    public const string ManageUsers = nameof(ManageUsers);
    public const string ManageRoles = nameof(ManageRoles);
    public const string ViewCustomers = nameof(ViewCustomers);
    public const string ManageCustomers = nameof(ManageCustomers);
    public const string ViewOwners = nameof(ViewOwners);
    public const string ManageOwners = nameof(ManageOwners);
    public const string ApproveOwners = nameof(ApproveOwners);
    public const string ViewDrivers = nameof(ViewDrivers);
    public const string ManageDrivers = nameof(ManageDrivers);
    public const string ApproveDrivers = nameof(ApproveDrivers);
    public const string ViewVehicles = nameof(ViewVehicles);
    public const string ManageVehicles = nameof(ManageVehicles);
    public const string ApproveVehicles = nameof(ApproveVehicles);
    public const string ViewBookings = nameof(ViewBookings);
    public const string ManageBookings = nameof(ManageBookings);
    public const string ManageQuotations = nameof(ManageQuotations);
    public const string ApproveQuotations = nameof(ApproveQuotations);
    public const string ViewTrips = nameof(ViewTrips);
    public const string AssignTrips = nameof(AssignTrips);
    public const string UpdateTrips = nameof(UpdateTrips);
    public const string ManagePricing = nameof(ManagePricing);
    public const string ViewFinance = nameof(ViewFinance);
    public const string ManageInvoices = nameof(ManageInvoices);
    public const string ManagePayments = nameof(ManagePayments);
    public const string ManageRefunds = nameof(ManageRefunds);
    public const string ManageSettlements = nameof(ManageSettlements);
    public const string ApproveSettlements = nameof(ApproveSettlements);
    public const string ViewReports = nameof(ViewReports);
    public const string ManageSupport = nameof(ManageSupport);
    public const string ManageComplaints = nameof(ManageComplaints);
    public const string ManageMasterData = nameof(ManageMasterData);
    public const string ManageSystemSettings = nameof(ManageSystemSettings);
    public const string ManageNotificationTemplates = nameof(ManageNotificationTemplates);
    public const string ViewAuditLogs = nameof(ViewAuditLogs);

    // ---- external (customer-facing portal) ----
    public const string CreateBookings = nameof(CreateBookings);
    public const string RespondToQuotations = nameof(RespondToQuotations);
    public const string MakePayments = nameof(MakePayments);
    public const string ManageOwnFleet = nameof(ManageOwnFleet);
    public const string PerformTrips = nameof(PerformTrips);
    public const string RaiseSupportRequests = nameof(RaiseSupportRequests);

    public static readonly IReadOnlyCollection<string> Internal =
    [
        AccessOperationsPortal, ManageUsers, ManageRoles, ViewCustomers, ManageCustomers, ViewOwners, ManageOwners,
        ApproveOwners, ViewDrivers, ManageDrivers, ApproveDrivers, ViewVehicles, ManageVehicles, ApproveVehicles,
        ViewBookings, ManageBookings, ManageQuotations, ApproveQuotations, ViewTrips, AssignTrips, UpdateTrips,
        ManagePricing, ViewFinance, ManageInvoices, ManagePayments, ManageRefunds, ManageSettlements, ApproveSettlements,
        ViewReports, ManageSupport, ManageComplaints, ManageMasterData, ManageSystemSettings, ManageNotificationTemplates,
        ViewAuditLogs
    ];

    public static readonly IReadOnlyCollection<string> External =
    [
        CreateBookings, RespondToQuotations, MakePayments, ManageOwnFleet, PerformTrips, RaiseSupportRequests
    ];

    public static IEnumerable<string> All => Internal.Concat(External);

    public static bool IsInternal(string permission) => Internal.Contains(permission);
}
