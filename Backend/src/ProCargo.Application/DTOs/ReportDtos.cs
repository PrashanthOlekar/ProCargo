namespace ProCargo.Application.DTOs;

public sealed class OperationsDashboardDto
{
    public int TotalBookings { get; init; }
    public int TodaysBookings { get; init; }
    public int PendingBookings { get; init; }
    public int PendingQuotations { get; init; }
    public int AwaitingAssignment { get; init; }
    public int ActiveTrips { get; init; }
    public int TripExceptions { get; init; }
    public int CompletedTrips { get; init; }
    public int CancelledTrips { get; init; }
    public int PendingOwnerApprovals { get; init; }
    public int PendingDriverApprovals { get; init; }
    public int PendingVehicleApprovals { get; init; }
    public int UnpaidInvoices { get; init; }
    public decimal OutstandingAmount { get; init; }
    public int PendingSettlements { get; init; }
    public decimal PendingSettlementAmount { get; init; }
    public decimal RevenueThisMonth { get; init; }
    public decimal CollectionsThisMonth { get; init; }
    public int OpenTickets { get; init; }
    public int OpenComplaints { get; init; }
    public int VehiclesWithExpiringDocuments { get; init; }
}

public sealed class CustomerDashboardDto
{
    public int ActiveBookings { get; init; }
    public int AwaitingQuotationResponse { get; init; }
    public int InTransit { get; init; }
    public int CompletedBookings { get; init; }
    public decimal OutstandingAmount { get; init; }
    public decimal TotalPaid { get; init; }
}

public sealed class OwnerDashboardDto
{
    public int TotalVehicles { get; init; }
    public int AvailableVehicles { get; init; }
    public int VehiclesPendingVerification { get; init; }
    public int TotalDrivers { get; init; }
    public int ActiveTrips { get; init; }
    public int CompletedTrips { get; init; }
    public decimal PendingSettlementAmount { get; init; }
    public decimal TotalEarned { get; init; }
}

public sealed class DriverDashboardDto
{
    public int ActiveTrips { get; init; }
    public int UpcomingTrips { get; init; }
    public int CompletedTrips { get; init; }
    public int AvailabilityStatusId { get; init; }
}

public sealed class BookingsByDayDto
{
    public DateOnly ReportDate { get; init; }
    public int TotalBookings { get; init; }
    public int ConfirmedBookings { get; init; }
    public int CancelledOrRejected { get; init; }
}

public sealed class StatusCountDto
{
    public int StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}

public sealed class RevenueReportRowDto
{
    public DateOnly Period { get; init; }
    public int InvoiceCount { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
}

public sealed class TripPerformanceRowDto
{
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public int TripCount { get; init; }
    public int DeliveredCount { get; init; }
    public int OnTimeCount { get; init; }
    public int CancelledCount { get; init; }
    public decimal? AverageTransitHours { get; init; }
}

public sealed class SettlementReportRowDto
{
    public int StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int SettlementCount { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal NetAmount { get; init; }
}

public sealed class TopCustomerDto
{
    public long CustomerId { get; init; }
    public string CustomerNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int BookingCount { get; init; }
    public decimal InvoicedAmount { get; init; }
}

public sealed class PartnerSummaryRowDto
{
    public string EntityType { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}
