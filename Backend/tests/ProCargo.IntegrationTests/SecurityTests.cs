using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProCargo.IntegrationTests;

/// <summary>Authentication, authorization boundaries and the API's security behaviour.</summary>
public sealed class SecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [SkippableFact]
    public async Task Health_endpoints_respond()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var client = Api.Client(_factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [SkippableFact]
    public async Task Protected_endpoints_require_a_token()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).GetAsync("/api/v1/bookings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Wrong_password_is_rejected_without_revealing_which_part_was_wrong()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).PostAsJsonAsync("/api/v1/auth/login",
            new { email = "support@procargo.test", password = "Wrong@Pass1", portal = "Operations" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("INVALID_CREDENTIALS", body!["errorCode"]!.GetValue<string>());
    }

    [SkippableFact]
    public async Task Customers_cannot_sign_in_to_the_operations_portal()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).PostAsJsonAsync("/api/v1/auth/login",
            new { email = "customer@procargo.test", password = Api.Password, portal = "Operations" });
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Customer_token_cannot_reach_staff_endpoints()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var customer = await Api.SignInAsync(_factory, "customer@procargo.test", "Web");

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/dashboard/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/audit-logs")).StatusCode);
    }

    [SkippableFact]
    public async Task Owner_cannot_read_another_partys_booking()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var ops = await Api.SignInAsync(_factory, "ops@procargo.test", "Operations");
        var bookings = await ops.GetJsonAsync("/api/v1/bookings?pageSize=1");
        var bookingId = bookings["items"]!.AsArray()[0]!["bookingId"]!.GetValue<long>();

        // The owner has no trip on this booking yet: 404, not 403, so ids cannot be probed.
        var owner = await Api.SignInAsync(_factory, "owner@procargo.test", "Web");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/bookings/{bookingId}")).StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_rotates_the_cookie_and_old_token_reuse_is_rejected()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var client = Api.Client(_factory);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "support@procargo.test", password = Api.Password, portal = "Operations" });
        await Api.EnsureAsync(login);

        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pc_rt_ops=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var oldToken = cookie.Split(';')[0];

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh") { Content = JsonContent.Create(new { portal = "Operations" }) };
        refresh.Headers.Add("X-ProCargo-Client", "tests");
        await Api.EnsureAsync(await client.SendAsync(refresh));

        // Replaying the old refresh token is detected and the whole token family is revoked.
        var replayClient = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh") { Content = JsonContent.Create(new { portal = "Operations" }) };
        replay.Headers.Add("X-ProCargo-Client", "tests");
        replay.Headers.Add("Cookie", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replayClient.SendAsync(replay)).StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_without_client_header_is_refused()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).PostAsJsonAsync("/api/v1/auth/refresh", new { portal = "Web" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task Validation_errors_use_the_standard_error_body()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).PostAsJsonAsync("/api/v1/auth/register",
            new { accountType = "Customer", fullName = "", email = "not-an-email", phoneNumber = "12", password = "weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.False(body!["success"]!.GetValue<bool>());
        Assert.Equal("VALIDATION_FAILED", body["errorCode"]!.GetValue<string>());
        Assert.NotNull(body["errors"]!["email"]);
    }

    [SkippableFact]
    public async Task Security_headers_are_present()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var response = await Api.Client(_factory).GetAsync("/health");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [SkippableFact]
    public async Task Public_reference_data_and_estimate_work_anonymously()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");
        var client = Api.Client(_factory);
        var reference = await client.GetJsonAsync("/api/v1/master-data/reference");
        var truck = reference["vehicleTypes"]!.AsArray().First(v => v!["code"]!.GetValue<string>() == "TRUCK_19FT")!;

        var estimate = await client.PostJsonAsync("/api/v1/pricing/estimate",
            new { vehicleTypeId = truck["vehicleTypeId"]!.GetValue<int>(), distanceKm = 145, weightKg = 4200, includeLoading = true });
        Assert.True(estimate!["totalAmount"]!.GetValue<decimal>() > 0);
        Assert.Equal(estimate["subTotal"]!.GetValue<decimal>(), estimate["lines"]!.AsArray().Sum(l => l!["amount"]!.GetValue<decimal>()));
    }
}
