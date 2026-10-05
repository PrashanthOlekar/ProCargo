namespace ProCargo.Domain.Constants;

/// <summary>Keys of business settings stored in mst.SystemSetting (editable by administrators).</summary>
public static class SettingKeys
{
    public const string BookingMinLeadTimeMinutes = "Booking.MinLeadTimeMinutes";
    public const string BookingMaxAdvanceDays = "Booking.MaxAdvanceDays";
    public const string BookingMaxItems = "Booking.MaxItems";
    public const string QuotationDefaultValidityHours = "Quotation.DefaultValidityHours";
    public const string QuotationDiscountApprovalThresholdPercent = "Quotation.DiscountApprovalThresholdPercent";
    public const string InvoicePaymentTermsDays = "Invoice.PaymentTermsDays";
    public const string OtpValidityMinutes = "Otp.ValidityMinutes";
    public const string OtpMaxAttempts = "Otp.MaxAttempts";
    public const string SettlementTdsPercent = "Settlement.TdsPercent";
    public const string SupportEmail = "Platform.SupportEmail";
    public const string SupportPhone = "Platform.SupportPhone";
}
