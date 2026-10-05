using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Finance;

/// <summary>
/// Invoices are generated from the ACCEPTED quotation of a delivered booking with uploaded POD (enforced in SQL),
/// so the billed amount always equals what the customer agreed to. Finance can add audited adjustments and cancel
/// unpaid invoices. Customers see only their own invoices.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentRepository _payments;
    private readonly ISettingsProvider _settings;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;

    public InvoiceService(IInvoiceRepository invoices, IPaymentRepository payments, ISettingsProvider settings,
        INotificationService notifications, AccessGuard access, IAuditLogger audit)
    {
        _invoices = invoices;
        _payments = payments;
        _settings = settings;
        _notifications = notifications;
        _access = access;
        _audit = audit;
    }

    public Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceSearchRequest request, CancellationToken cancellationToken)
    {
        var customerScope = _access.IsStaffWith(Permissions.ViewFinance) ? request.CustomerId : _access.RequireCustomerId();
        return _invoices.GetPagedAsync(request, customerScope, cancellationToken);
    }

    public async Task<InvoiceDetailsResponse> GetByIdAsync(long invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await GetAccessibleAsync(invoiceId, cancellationToken);
        var items = await _invoices.GetItemsAsync(invoiceId, cancellationToken);
        var payments = await _payments.GetPagedAsync(new PaymentSearchRequest { InvoiceId = invoiceId, PageSize = 100 },
            invoice.CustomerId, cancellationToken);
        return new InvoiceDetailsResponse(invoice, items, payments.Items, AvailableActions(invoice));
    }

    public async Task<CreatedResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageInvoices);
        var terms = request.PaymentTermsDays ?? await _settings.GetIntAsync(SettingKeys.InvoicePaymentTermsDays, 7, cancellationToken);

        var created = await _invoices.CreateAsync(request.BookingId, terms, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("InvoiceIssued", "Invoice", created.Id, newValue: new { created.Number, request.BookingId, terms },
            cancellationToken: cancellationToken);

        var invoice = await _invoices.GetByIdAsync(created.Id, cancellationToken);
        if (invoice is not null)
        {
            await _notifications.NotifyUserAsync(invoice.CustomerUserId, NotificationTemplates.InvoiceIssued, new Dictionary<string, string>
            {
                ["Name"] = invoice.CustomerName,
                ["InvoiceNumber"] = invoice.InvoiceNumber,
                ["BookingNumber"] = invoice.BookingNumber,
                ["TotalAmount"] = Formatting.Money(invoice.TotalAmount),
                ["DueDate"] = Formatting.ToIstDate(invoice.DueDateUtc)
            }, new NotificationSubject("Invoice", invoice.InvoiceId), cancellationToken);
        }

        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task AddAdjustmentAsync(long invoiceId, InvoiceAdjustmentRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageInvoices);
        var invoice = await GetAccessibleAsync(invoiceId, cancellationToken);

        await _invoices.AddAdjustmentAsync(invoiceId, request.Description.Trim(), request.Amount, request.RowVersion, _access.User.UserId,
            cancellationToken);
        await _audit.LogAsync("InvoiceAdjusted", "Invoice", invoiceId,
            oldValue: new { invoice.TotalAmount }, newValue: new { request.Description, request.Amount }, cancellationToken: cancellationToken);
    }

    public async Task CancelAsync(long invoiceId, CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageInvoices);
        var invoice = await GetAccessibleAsync(invoiceId, cancellationToken);

        await _invoices.CancelAsync(invoiceId, request.Reason.Trim(), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("InvoiceCancelled", "Invoice", invoiceId,
            oldValue: new { invoice.InvoiceStatusId, invoice.TotalAmount }, newValue: new { request.Reason }, cancellationToken: cancellationToken);
    }

    private async Task<InvoiceDto> GetAccessibleAsync(long invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetByIdAsync(invoiceId, cancellationToken) ?? throw NotFoundException.For("Invoice", invoiceId);
        _access.EnsureCustomer(invoice.CustomerId, Permissions.ViewFinance, "Invoice", invoiceId);
        return invoice;
    }

    private IReadOnlyList<string> AvailableActions(InvoiceDto invoice)
    {
        var status = (InvoiceStatus)invoice.InvoiceStatusId;
        var actions = new List<string>();
        var open = status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid;

        if (open && _access.User.CustomerId == invoice.CustomerId && _access.User.HasPermission(Permissions.MakePayments))
        {
            actions.Add("Pay");
        }

        if (_access.IsStaffWith(Permissions.ManageInvoices))
        {
            if (open) actions.Add("Adjust");
            if (status == InvoiceStatus.Issued && invoice.PaidAmount == 0) actions.Add("Cancel");
        }

        if (open && _access.IsStaffWith(Permissions.ManagePayments)) actions.Add("RecordPayment");
        return actions;
    }
}
