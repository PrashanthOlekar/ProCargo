using System.Globalization;
using System.Text;
using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;

namespace ProCargo.Application.Services.Reports;

public sealed class DashboardService : IDashboardService
{
    private readonly IReportRepository _reports;
    private readonly AccessGuard _access;
    private readonly TimeProvider _clock;

    public DashboardService(IReportRepository reports, AccessGuard access, TimeProvider clock)
    {
        _reports = reports;
        _access = access;
        _clock = clock;
    }

    public Task<OperationsDashboardDto> GetOperationsAsync(CancellationToken cancellationToken)
    {
        if (!_access.User.IsStaff) throw new ForbiddenException();

        // "Today" and "this month" are Indian calendar periods.
        var istNow = _clock.GetUtcNow().ToOffset(Formatting.IstOffset);
        var today = DateOnly.FromDateTime(istNow.DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return _reports.GetOperationsDashboardAsync(Formatting.IstDateStartUtc(today), Formatting.IstDateStartUtc(monthStart), cancellationToken);
    }

    public Task<CustomerDashboardDto> GetCustomerAsync(CancellationToken cancellationToken) =>
        _reports.GetCustomerDashboardAsync(_access.RequireCustomerId(), cancellationToken);

    public Task<OwnerDashboardDto> GetOwnerAsync(CancellationToken cancellationToken) =>
        _reports.GetOwnerDashboardAsync(_access.RequireOwnerId(), cancellationToken);

    public Task<DriverDashboardDto> GetDriverAsync(CancellationToken cancellationToken) =>
        _reports.GetDriverDashboardAsync(_access.RequireDriverId(), cancellationToken);
}

/// <summary>Management reports (ViewReports). Periods are IST calendar dates, inclusive.</summary>
public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reports;
    private readonly AccessGuard _access;

    public ReportService(IReportRepository reports, AccessGuard access)
    {
        _reports = reports;
        _access = access;
    }

    public Task<IReadOnlyList<BookingsByDayDto>> GetBookingsByDayAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetBookingsByDayAsync(from, to, cancellationToken));

    public Task<IReadOnlyList<StatusCountDto>> GetBookingsByStatusAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetBookingsByStatusAsync(from, to, cancellationToken));

    public Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetRevenueAsync(from, to, request.GroupBy, cancellationToken), Permissions.ViewFinance);

    public Task<IReadOnlyList<TripPerformanceRowDto>> GetTripPerformanceAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetTripPerformanceAsync(from, to, cancellationToken));

    public Task<IReadOnlyList<SettlementReportRowDto>> GetSettlementsAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetSettlementsAsync(from, to, cancellationToken), Permissions.ViewFinance);

    public Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(ReportPeriodRequest request, CancellationToken cancellationToken) =>
        Run(request, (from, to) => _reports.GetTopCustomersAsync(from, to, request.Top, cancellationToken));

    public Task<IReadOnlyList<PartnerSummaryRowDto>> GetPartnerSummaryAsync(CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewReports);
        return _reports.GetPartnerSummaryAsync(cancellationToken);
    }

    public async Task<FileDownload> ExportCsvAsync(string report, ReportPeriodRequest request, CancellationToken cancellationToken)
    {
        var (header, rows) = report.ToLowerInvariant() switch
        {
            "bookings-by-day" => (new[] { "Date", "Total", "Confirmed", "CancelledOrRejected" },
                (await GetBookingsByDayAsync(request, cancellationToken))
                .Select(r => new object?[] { r.ReportDate, r.TotalBookings, r.ConfirmedBookings, r.CancelledOrRejected })),
            "bookings-by-status" => (new[] { "Status", "Count" },
                (await GetBookingsByStatusAsync(request, cancellationToken)).Select(r => new object?[] { r.StatusName, r.ItemCount })),
            "revenue" => (new[] { "Period", "Invoices", "SubTotal", "Tax", "Total", "Paid" },
                (await GetRevenueAsync(request, cancellationToken))
                .Select(r => new object?[] { r.Period, r.InvoiceCount, r.SubTotal, r.TaxAmount, r.TotalAmount, r.PaidAmount })),
            "trip-performance" => (new[] { "VehicleType", "Trips", "Delivered", "OnTime", "Cancelled", "AvgTransitHours" },
                (await GetTripPerformanceAsync(request, cancellationToken))
                .Select(r => new object?[] { r.VehicleTypeName, r.TripCount, r.DeliveredCount, r.OnTimeCount, r.CancelledCount, r.AverageTransitHours })),
            "settlements" => (new[] { "Status", "Count", "Gross", "Commission", "Net" },
                (await GetSettlementsAsync(request, cancellationToken))
                .Select(r => new object?[] { r.StatusName, r.SettlementCount, r.GrossAmount, r.CommissionAmount, r.NetAmount })),
            "top-customers" => (new[] { "CustomerNumber", "Customer", "Bookings", "Invoiced" },
                (await GetTopCustomersAsync(request, cancellationToken))
                .Select(r => new object?[] { r.CustomerNumber, r.CustomerName, r.BookingCount, r.InvoicedAmount })),
            _ => throw NotFoundException.For("Report", report)
        };

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', header));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', row.Select(CsvCell)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new FileDownload(new MemoryStream(bytes), "text/csv", $"procargo-{report}-{request.From:yyyyMMdd}-{request.To:yyyyMMdd}.csv");
    }

    private async Task<IReadOnlyList<T>> Run<T>(ReportPeriodRequest request, Func<DateTime, DateTime, Task<IReadOnlyList<T>>> query,
        string? additionalPermission = null)
    {
        _access.EnsureStaffPermission(Permissions.ViewReports);
        if (additionalPermission is not null) _access.EnsureStaffPermission(additionalPermission);

        var fromUtc = Formatting.IstDateStartUtc(request.From);
        var toUtc = Formatting.IstDateStartUtc(request.To.AddDays(1));
        return await query(fromUtc, toUtc);
    }

    /// <summary>Quotes CSV values and neutralises spreadsheet formula injection (=, +, -, @ prefixes).</summary>
    internal static string CsvCell(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0]))
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 || text.StartsWith('\'')
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }
}

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly IAuditRepository _audit;
    private readonly AccessGuard _access;

    public AuditQueryService(IAuditRepository audit, AccessGuard access)
    {
        _audit = audit;
        _access = access;
    }

    public Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogSearchRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewAuditLogs);
        return _audit.GetPagedAsync(request, cancellationToken);
    }
}
