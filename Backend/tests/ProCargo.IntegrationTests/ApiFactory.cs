using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProCargo.IntegrationTests;

/// <summary>
/// Runs the real API in-process against a real SQL Server database deployed with
/// Database/ProCargo.Database/deploy.sh --with-test-data. Set PROCARGO_TEST_CONNECTION to enable; otherwise the
/// tests are skipped (so a plain "dotnet test" works on a laptop without SQL Server).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("PROCARGO_TEST_CONNECTION");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ProCargo", ConnectionString ?? "Server=unused;Database=unused;TrustServerCertificate=True");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789-abcdefghijklmnop");
        builder.UseSetting("Jwt:Issuer", "https://localhost");
        builder.UseSetting("Security:OtpHashingKey", "integration-tests-otp-key-0123456789-abcdefghijklmnop");
        builder.UseSetting("Security:ExposeOtpForTesting", "true");
        builder.UseSetting("Payments:Gateway", "Sandbox");
        builder.UseSetting("FileStorage:Provider", "Local");
        builder.UseSetting("FileStorage:LocalPath", Path.Combine(Path.GetTempPath(), "procargo-it-files"));
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "procargo-it-keys"));
        builder.UseSetting("Email:Provider", "None");
        builder.UseSetting("Sms:Provider", "None");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        builder.UseSetting("Jobs:QuotationExpiryEnabled", "false");
        builder.UseSetting("ApplicationInsights:ConnectionString", "");
    }
}

public static class Api
{
    public const string Password = "ProCargo@Dev1";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static HttpClient Client(ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public static async Task<HttpClient> SignInAsync(ApiFactory factory, string email, string portal)
    {
        var client = Client(factory);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, portal });
        await EnsureAsync(response);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["accessToken"]!.GetValue<string>());
        client.DefaultRequestHeaders.Add("X-ProCargo-Client", "tests");
        return client;
    }

    public static async Task<JsonObject> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json))!;
    }

    public static async Task<JsonArray> GetJsonArrayAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<JsonArray>(Json))!;
    }

    public static async Task<JsonObject?> PostJsonAsync(this HttpClient client, string url, object? body = null)
    {
        var response = await client.PostAsJsonAsync(url, body ?? new { }, Json);
        await EnsureAsync(response);
        return response.StatusCode == HttpStatusCode.NoContent ? null : await response.Content.ReadFromJsonAsync<JsonObject>(Json);
    }

    public static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {text}");
        }
    }

    public static long Id(this JsonObject? node, string property = "id") => node![property]!.GetValue<long>();
}
