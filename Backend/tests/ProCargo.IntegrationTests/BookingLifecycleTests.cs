using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace ProCargo.IntegrationTests;

/// <summary>
/// The complete business flow through the public API, each step performed by the role that owns it:
/// customer books -> operations quotes -> customer accepts -> operations assigns -> driver verifies pickup (OTP),
/// starts, verifies delivery (OTP), uploads POD -> finance invoices -> customer pays (sandbox gateway) ->
/// finance creates settlement -> a different finance user approves -> settlement completes -> booking closed.
/// </summary>
public sealed class BookingLifecycleTests : IClassFixture<ApiFactory>
{
    // 1x1 transparent PNG
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly ApiFactory _factory;

    public BookingLifecycleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [SkippableFact]
    public async Task Booking_goes_from_request_to_closed_settlement()
    {
        Skip.If(ApiFactory.ConnectionString is null, "PROCARGO_TEST_CONNECTION not set");

        var customer = await Api.SignInAsync(_factory, "customer@procargo.test", "Web");
        var ops = await Api.SignInAsync(_factory, "ops@procargo.test", "Operations");
        var driver = await Api.SignInAsync(_factory, "driver@procargo.test", "Web");
        var owner = await Api.SignInAsync(_factory, "owner@procargo.test", "Web");
        var finance = await Api.SignInAsync(_factory, "finance@procargo.test", "Operations");
        var admin = await Api.SignInAsync(_factory, "admin@procargo.test", "Operations");

        // ---- reference data
        var reference = await customer.GetAsync("/api/v1/master-data/reference");
        var truck = reference["vehicleTypes"]!.AsArray().First(v => v!["code"]!.GetValue<string>() == "TRUCK_19FT")!;
        var goods = reference["goodsTypes"]!.AsArray().First(g => g!["code"]!.GetValue<string>() == "FMCG")!;
        var cities = await customer.GetArrayAsync("/api/v1/master-data/cities");
        int City(string name) => cities.First(c => c!["name"]!.GetValue<string>() == name)!["cityId"]!.GetValue<int>();

        // ---- 1. customer books
        var booking = await customer.PostAsync("/api/v1/bookings", new
        {
            vehicleTypeId = truck["vehicleTypeId"]!.GetValue<int>(),
            goodsTypeId = goods["goodsTypeId"]!.GetValue<int>(),
            goodsDescription = "Packaged snacks",
            requestedPickupDateUtc = DateTime.UtcNow.AddDays(2),
            pickupAddress = new { addressLine1 = "12 Peenya Industrial Area", cityId = City("Bengaluru"), pincode = "560058", latitude = 13.0285m, longitude = 77.5197m },
            deliveryAddress = new { addressLine1 = "KIADB Hebbal", cityId = City("Mysuru"), pincode = "570016", latitude = 12.3528m, longitude = 76.6115m },
            pickupContact = new { contactName = "Chetan Kumar", phoneNumber = "9900000010" },
            deliveryContact = new { contactName = "Manjunath R", phoneNumber = "9900000011" },
            items = new[] { new { description = "Snack cartons", quantity = 100, weightKg = 2500m } },
            submit = true
        });
        var bookingId = booking.Id();

        // ---- 2. operations quotes, customer accepts
        var quotation = await ops.PostAsync("/api/v1/quotations",
            new { bookingId, distanceKm = 150m, tollAmount = 450m, includeLoading = true, includeUnloading = true, sendImmediately = true });
        await customer.PostAsync($"/api/v1/quotations/{quotation.Id()}/accept");

        // ---- 3. operations assigns the verified vehicle and driver
        var vehicles = await ops.GetArrayAsync(
            $"/api/v1/vehicles/available?vehicleTypeId={truck["vehicleTypeId"]}&minCapacityKg=2500&onDateUtc={DateTime.UtcNow.AddDays(2):O}");
        var vehicle = vehicles.First(v => v!["vehicleNumber"]!.GetValue<string>() == "KA25AB1234")!;
        var drivers = await ops.GetArrayAsync($"/api/v1/drivers/available?ownerId={vehicle["ownerId"]}&onDateUtc={DateTime.UtcNow.AddDays(2):O}");
        var trip = await ops.PostAsync("/api/v1/trips", new
        {
            bookingId,
            vehicleId = vehicle["vehicleId"]!.GetValue<long>(),
            driverId = drivers[0]!["driverId"]!.GetValue<long>(),
            plannedPickupDateUtc = DateTime.UtcNow.AddDays(2),
            plannedDeliveryDateUtc = DateTime.UtcNow.AddDays(2).AddHours(6)
        });
        var tripId = trip.Id();

        // ---- 4. driver: pickup OTP -> start -> delivery OTP -> POD
        var pickupOtp = await driver.PostAsync($"/api/v1/trips/{tripId}/pickup/otp");
        await driver.PostAsync($"/api/v1/trips/{tripId}/pickup/verify", new { otp = pickupOtp!["testOtp"]!.GetValue<string>(), odometer = 10000 });
        await driver.PostAsync($"/api/v1/trips/{tripId}/start");
        await driver.PostAsync($"/api/v1/trips/{tripId}/locations",
            new { points = new[] { new { latitude = 12.8m, longitude = 77.2m, recordedDateUtc = DateTime.UtcNow, speedKmph = 48m } } });

        var tracking = await customer.GetAsync($"/api/v1/trips/{tripId}/tracking");
        Assert.NotNull(tracking["lastLocation"]);

        var deliveryOtp = await driver.PostAsync($"/api/v1/trips/{tripId}/delivery/otp");
        await driver.PostAsync($"/api/v1/trips/{tripId}/delivery/verify",
            new { otp = deliveryOtp!["testOtp"]!.GetValue<string>(), receiverName = "Manjunath R", odometer = 10152 });

        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("Manjunath R"), "receiverName");
            form.Add(new StringContent("Received in good condition"), "remarks");
            var photo = new ByteArrayContent(Png);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(photo, "photos", "delivery.png");
            await Api.EnsureAsync(await driver.PostAsync($"/api/v1/trips/{tripId}/proof-of-delivery", form));
        }

        // ---- 5. finance invoices, customer pays through the sandbox gateway
        var invoice = await finance.PostAsync("/api/v1/invoices", new { bookingId });
        var invoiceId = invoice.Id();

        var payRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { invoiceId, method = "Upi" })
        };
        payRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var payResponse = await customer.SendAsync(payRequest);
        await Api.EnsureAsync(payResponse);
        var payment = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<JsonObject>(payResponse.Content, Api.Json);
        var paid = await customer.PostAsync($"/api/v1/payments/{payment.Id("paymentId")}/sandbox/complete");
        Assert.Equal(4, paid!["paymentStatusId"]!.GetValue<int>()); // Paid

        var paidInvoice = await customer.GetAsync($"/api/v1/invoices/{invoiceId}");
        Assert.Equal(4, paidInvoice["invoice"]!["invoiceStatusId"]!.GetValue<int>());

        // ---- 6. owner adds a payout account; finance settles with maker-checker approval
        var me = await owner.GetAsync("/api/v1/owners/me");
        var ownerId = me["owner"]!["ownerId"]!.GetValue<long>();
        await owner.PostAsync($"/api/v1/owners/{ownerId}/bank-accounts",
            new { accountHolderName = "Olekar Transport", bankName = "State Bank of India", accountNumber = "123456789012", ifscCode = "SBIN0001234", isPrimary = true });

        var settlement = await finance.PostAsync("/api/v1/settlements", new { tripId });
        var settlementId = settlement.Id();

        // Maker-checker: the creator cannot approve their own settlement.
        var selfApproval = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(finance, $"/api/v1/settlements/{settlementId}/approve", new { });
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, selfApproval.StatusCode);

        await admin.PostAsync($"/api/v1/settlements/{settlementId}/approve");
        await finance.PostAsync($"/api/v1/settlements/{settlementId}/process");
        await finance.PostAsync($"/api/v1/settlements/{settlementId}/complete", new { transactionReference = "UTR" + DateTime.UtcNow.Ticks });

        var closed = await customer.GetAsync($"/api/v1/bookings/{bookingId}");
        Assert.Equal(11, closed["booking"]!["bookingStatusId"]!.GetValue<int>()); // Closed

        var earnings = await owner.GetAsync("/api/v1/settlements/summary");
        Assert.True(earnings["totalEarned"]!.GetValue<decimal>() > 0);
    }
}
