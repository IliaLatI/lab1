using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SecureLab.Api.Tests;

public sealed class SearchMechanicsTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    // Початкова перевірка пошуку.
    [Fact]
    public async Task Search_ReturnsJson()
    {
        using var client = factory.CreateClient();
        using var response =
            await client.GetAsync("/api/incidents/search?q=навчальн");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
    }

    // T-02: неправильний severity → 400.
    // T-03: повторний активний title → 409.
    [Theory]
    [InlineData("7", HttpStatusCode.BadRequest)]
    [InlineData("Low", HttpStatusCode.Conflict)]
    public async Task Create_ChecksErrors(
        string severity,
        HttpStatusCode expected)
    {
        // Отримуємо локальний навчальний пароль.
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Авторизуємося як Alice та отримуємо cookie-клієнт.
        using var client = await factory.LoginAsync("alice", password);

        var request = new
        
        {
            title = $"Test-{Guid.NewGuid():N}",
            description = "Навчальна перевірка створення інциденту.",
            severity,
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        // Для перевірки конфлікту спочатку створюємо інцидент.
        if (expected == HttpStatusCode.Conflict)
        {
            using var first =
                await client.PostAsJsonAsync("/api/incidents", request);

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var response =
            await client.PostAsJsonAsync("/api/incidents", request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            (int)expected,
            body.GetProperty("status").GetInt32());

        if (expected == HttpStatusCode.BadRequest)
        {
            Assert.True(
                body.GetProperty("errors")
                    .TryGetProperty("severity", out _));
        }
        else
        {
            Assert.Equal(
                "Incident title conflict",
                body.GetProperty("title").GetString());
        }
    }
}