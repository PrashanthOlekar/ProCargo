using Microsoft.Extensions.Options;
using ProCargo.Application.Common;
using ProCargo.Application.Services.Notifications;
using ProCargo.Application.Services.Reports;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Platform;
using ProCargo.Infrastructure.Security;

namespace ProCargo.UnitTests.Application;

public sealed class SecurityAndFormattingTests
{
    private static HmacOtpService Otp() =>
        new(Options.Create(new SecurityOptions { OtpHashingKey = "unit-test-otp-hashing-key-0123456789abcdef" }));

    [Fact]
    public void Otp_is_six_digits_and_verifies_only_for_same_trip_and_step()
    {
        var service = Otp();
        var otp = service.GenerateOtp();
        Assert.Matches("^[0-9]{6}$", otp);

        var hash = service.HashOtp(42, VerificationType.Pickup, otp);
        Assert.True(service.Verify(42, VerificationType.Pickup, otp, hash));
        Assert.False(service.Verify(43, VerificationType.Pickup, otp, hash));
        Assert.False(service.Verify(42, VerificationType.Delivery, otp, hash));
    }

    [Fact]
    public void Otp_service_refuses_short_keys()
    {
        Assert.Throws<InvalidOperationException>(() => new HmacOtpService(Options.Create(new SecurityOptions { OtpHashingKey = "short" })));
    }

    [Fact]
    public void Opaque_tokens_are_random_and_hashed()
    {
        var tokens = new JwtTokenService(Options.Create(new JwtOptions { SigningKey = new string('k', 64) }), TimeProvider.System);
        var a = tokens.GenerateOpaqueToken();
        var b = tokens.GenerateOpaqueToken();

        Assert.NotEqual(a, b);
        Assert.Equal(32, tokens.HashOpaqueToken(a).Length);
        Assert.Equal(tokens.HashOpaqueToken(a), tokens.HashOpaqueToken(a));
    }

    [Fact]
    public void Password_hasher_round_trips()
    {
        var hasher = new IdentityPasswordHasher();
        var hash = hasher.Hash("ProCargo@Dev1");
        Assert.NotEqual("ProCargo@Dev1", hash);
        Assert.NotEqual(ProCargo.Application.Interfaces.Services.PasswordCheckResult.Failed, hasher.Verify(hash, "ProCargo@Dev1"));
        Assert.Equal(ProCargo.Application.Interfaces.Services.PasswordCheckResult.Failed, hasher.Verify(hash, "wrong"));
    }

    [Fact]
    public void Audit_values_mask_secrets()
    {
        var json = AuditLogger.Serialize(new { Email = "a@b.com", Password = "x", RefreshToken = "y", CompanyName = "Acme", Nested = new { Otp = "123456" } });
        Assert.NotNull(json);
        Assert.DoesNotContain("123456", json, StringComparison.Ordinal);
        Assert.Contains("Acme", json, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"***\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Templates_html_encode_values_for_email()
    {
        var data = new Dictionary<string, string> { ["Name"] = "<script>x</script>" };
        Assert.Equal("Hi &lt;script&gt;x&lt;/script&gt;", NotificationService.Render("Hi {{Name}}", data, htmlEncode: true));
        Assert.Equal("Hi ", NotificationService.Render("Hi {{Missing}}", data, htmlEncode: false));
    }

    [Theory]
    [InlineData("=SUM(A1)", "\"'=SUM(A1)\"")]
    [InlineData("Acme, Ltd", "\"Acme, Ltd\"")]
    [InlineData("Plain", "Plain")]
    public void Csv_cells_are_quoted_and_formula_safe(string value, string expected)
    {
        Assert.Equal(expected, ReportService.CsvCell(value));
    }

    [Fact]
    public void Ist_day_starts_at_1830_utc_previous_day()
    {
        Assert.Equal(new DateTime(2026, 10, 4, 18, 30, 0), Formatting.IstDateStartUtc(new DateOnly(2026, 10, 5)));
    }
}
