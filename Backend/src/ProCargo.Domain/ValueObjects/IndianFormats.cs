using System.Text.RegularExpressions;

namespace ProCargo.Domain.ValueObjects;

/// <summary>
/// Normalisation and validation of Indian business identifiers. Keeping these in the domain means the
/// API validators, services and tests all agree on one definition.
/// </summary>
public static partial class IndianFormats
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{4}$")]
    private static partial Regex VehicleRegistrationRegex();

    [GeneratedRegex("^[0-9]{2}BH[0-9]{4}[A-Z]{1,2}$")]
    private static partial Regex BharatSeriesRegex();

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex GstRegex();

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanRegex();

    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$")]
    private static partial Regex IfscRegex();

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex PincodeRegex();

    [GeneratedRegex("^\\+[1-9][0-9]{9,14}$")]
    private static partial Regex E164Regex();

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("[^0-9+]")]
    private static partial Regex NonPhoneCharacterRegex();

    /// <summary>"ka-25 ab 1234" -> "KA25AB1234".</summary>
    public static string NormalizeVehicleNumber(string value) =>
        NonAlphanumericRegex().Replace(value.ToUpperInvariant(), string.Empty);

    public static bool IsValidVehicleNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = NormalizeVehicleNumber(value);
        return VehicleRegistrationRegex().IsMatch(normalized) || BharatSeriesRegex().IsMatch(normalized);
    }

    /// <summary>
    /// Normalises to E.164. Ten-digit Indian mobile numbers get the +91 prefix:
    /// "98450 12345" -> "+919845012345".
    /// </summary>
    public static string NormalizePhone(string value)
    {
        var cleaned = NonPhoneCharacterRegex().Replace(value.Trim(), string.Empty);
        if (cleaned.StartsWith('+')) return cleaned;
        if (cleaned.Length == 10 && cleaned[0] is >= '6' and <= '9') return "+91" + cleaned;
        if (cleaned.Length == 12 && cleaned.StartsWith("91", StringComparison.Ordinal)) return "+" + cleaned;
        if (cleaned.Length == 11 && cleaned.StartsWith('0')) return "+91" + cleaned[1..];
        return "+" + cleaned;
    }

    public static bool IsValidPhone(string? value) =>
        !string.IsNullOrWhiteSpace(value) && E164Regex().IsMatch(NormalizePhone(value));

    /// <summary>"+919845012345" -> "+91******2345". Used wherever a phone number is shown to someone other than its owner.</summary>
    public static string MaskPhone(string phone)
    {
        if (phone.Length <= 7) return new string('*', phone.Length);
        return phone[..3] + new string('*', phone.Length - 7) + phone[^4..];
    }

    public static bool IsValidGst(string? value) => value is not null && GstRegex().IsMatch(value.ToUpperInvariant());

    public static bool IsValidPan(string? value) => value is not null && PanRegex().IsMatch(value.ToUpperInvariant());

    public static bool IsValidIfsc(string? value) => value is not null && IfscRegex().IsMatch(value.ToUpperInvariant());

    public static bool IsValidPincode(string? value) => value is not null && PincodeRegex().IsMatch(value);

    /// <summary>Last four characters for display of sensitive identifiers (PAN, account numbers).</summary>
    public static string LastFour(string value) => value.Length <= 4 ? value : value[^4..];
}
