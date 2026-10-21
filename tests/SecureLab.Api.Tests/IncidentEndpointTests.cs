using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string StudyPassword =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException(
            "SeedUsers__Password is not configured.");

    [Fact]
    public async Task GetList_ReturnsOnlyAuthorizedIncidents()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        var incidents = await client.GetFromJsonAsync<
            List<IncidentListItemResponse>>("/api/incidents/");

        Assert.NotNull(incidents);

        // Alice бачить власний інцидент.
        Assert.Contains(incidents,
            incident => incident.Id == DbSeeder.AliceIncidentId);

        // Інцидент Bob не повинен бути доступним Alice.
        Assert.DoesNotContain(incidents,
            incident => incident.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDetails_DoesNotExposeInternalOwnerFields()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        // Внутрішні ідентифікатори та email не розкриваються.
        Assert.False(
            root.TryGetProperty("ownerUserId", out _));

        Assert.False(
            root.TryGetProperty("email", out _));

        // Публічне ім'я власника присутнє.
        var ownerName = root
            .GetProperty("ownerDisplayName")
            .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(ownerName));
    }

    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        using var client = factory.CreateClient();

        var script = await client.GetStringAsync("/app.js");

        Assert.DoesNotContain(
            "innerHTML",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "textContent",
            script,
            StringComparison.Ordinal);
    }
}