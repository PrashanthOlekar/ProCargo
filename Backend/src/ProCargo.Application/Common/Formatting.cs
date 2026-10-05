using System.Globalization;

namespace ProCargo.Application.Common;

/// <summary>
/// Display helpers for notification text. All storage is UTC; customers read Indian Standard Time.
/// India has no daylight saving, so a fixed +05:30 offset is exact and avoids time-zone database differences
/// between Windows and Linux hosts.
/// </summary>
public static class Formatting
{
    public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string ToIst(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(IstOffset).ToString("dd MMM yyyy, hh:mm tt 'IST'", India);

    public static string ToIstDate(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(IstOffset).ToString("dd MMM yyyy", India);

    public static string Money(decimal amount) => amount.ToString("N2", India);

    /// <summary>Start of an IST calendar day expressed in UTC.</summary>
    public static DateTime IstDateStartUtc(DateOnly istDate) =>
        istDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).Add(-IstOffset);
}
