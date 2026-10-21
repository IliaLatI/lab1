using System.Net;
using SecureLab.Api.Data;
using System.Text.Json;
using SecureLab.Api.Data.Entities;

using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SecureLab.Api.Tests;

public sealed class AuthorizationTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string StudyPassword =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException(
            "SeedUsers__Password is not configured.");

    [Fact]
    public async Task Anonymous_CannotReadIncidents()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Alice_CanReadOwnIncident()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Alice_CannotReadBobIncident()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }


        [Fact]
    public async Task Bob_CannotReadAliceIncident()
    {
        using var client = await factory.LoginAsync(
            "bob", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Morgan_CanReadAliceIncident()
    {
        using var client = await factory.LoginAsync(
            "morgan", StudyPassword);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Alice_CannotDeleteOwnIncident()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        using var response = await client.DeleteAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        using var checkResponse = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            checkResponse.StatusCode);
    }

        [Fact]
    public async Task Alice_CannotTriageOwnIncident()
    {
        using var client = await factory.LoginAsync(
            "alice", StudyPassword);

        var url = $"/api/incidents/{DbSeeder.AliceIncidentId}";

        using var beforeResponse = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        using var beforeJson = JsonDocument.Parse(
            await beforeResponse.Content.ReadAsStringAsync());

        var statusBefore = beforeJson.RootElement
            .GetProperty("status")
            .ToString();

        using var triageResponse = await client.PostAsync(
            $"{url}/triage", null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            triageResponse.StatusCode);

        using var afterResponse = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        using var afterJson = JsonDocument.Parse(
            await afterResponse.Content.ReadAsStringAsync());

        var statusAfter = afterJson.RootElement
            .GetProperty("status")
            .ToString();

        Assert.Equal(statusBefore, statusAfter);
    }


[Fact]
public async Task Morgan_CanTriageNewIncident_OnlyOnce()
{
    Guid createdId = Guid.Empty;

    try
    {
        using var alice = await factory.LoginAsync(
            "alice", StudyPassword);

        using var morgan = await factory.LoginAsync(
            "morgan", StudyPassword);

        var newIncident = new
        {
            Title = $"Triage test {Guid.NewGuid():N}",
            Description = "Test incident for checking Analyst permissions.",
            Severity = "Low",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        using var createResponse = await alice.PostAsJsonAsync(
            "/api/incidents", newIncident);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        createdId = createJson.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.Equal(
            "New",
            createJson.RootElement
                .GetProperty("status")
                .GetString());

        var triageUrl = $"/api/incidents/{createdId}/triage";

        using var firstResponse = await morgan.PostAsync(
            triageUrl, null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            firstResponse.StatusCode);

        using var secondResponse = await morgan.PostAsync(
            triageUrl, null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);

        using var checkResponse = await morgan.GetAsync(
            $"/api/incidents/{createdId}");

        Assert.Equal(
            HttpStatusCode.OK,
            checkResponse.StatusCode);

        using var checkJson = JsonDocument.Parse(
            await checkResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Triaged",
            checkJson.RootElement
                .GetProperty("status")
                .GetString());
    }
    finally
    {
        if (createdId != Guid.Empty)
        {
            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            var history = await db.IncidentStatusHistory
                .Where(x => x.IncidentId == createdId)
                .ToListAsync();

            db.IncidentStatusHistory.RemoveRange(history);

            var incident = await db.Incidents
                .SingleOrDefaultAsync(x => x.Id == createdId);

            if (incident is not null)
            {
                db.Incidents.Remove(incident);
            }

            await db.SaveChangesAsync();
        }
    }
}


[Fact]
public async Task Alice_CannotChangeProtectedFields_A08a()
{
    Guid id = Guid.Empty;
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

    try
    {
        using var alice = await factory.LoginAsync("alice", StudyPassword);

        using var created = await alice.PostAsJsonAsync("/api/incidents", new
        {
            Title = $"A08a-{Guid.NewGuid():N}",
            Description = "Original description",
            Severity = "Low",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var json = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        id = json.RootElement.GetProperty("id").GetGuid();

        var title = $"Updated-{Guid.NewGuid():N}";

        using var updated = await alice.PutAsJsonAsync(
            $"/api/incidents/{id}", new
            {
                Title = title,
                Description = "Updated description",
                OwnerUserId = DbSeeder.BobId,
                Status = "Triaged",
                Severity = "Critical",
                Role = "Administrator"
            });

        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var incident = await db.Incidents.AsNoTracking()
            .SingleAsync(x => x.Id == id);

        Assert.Equal(title, incident.Title);
        Assert.Equal("Updated description", incident.Description);
        Assert.Equal(DbSeeder.AliceId, incident.OwnerUserId);
        Assert.Equal(IncidentStatus.New, incident.Status);
        Assert.Equal(IncidentSeverity.Low, incident.Severity);

        using var triage = await alice.PostAsync(
            $"/api/incidents/{id}/triage", null);

        Assert.Equal(HttpStatusCode.Forbidden, triage.StatusCode);
    }
    finally
    {
        if (id != Guid.Empty)
        {
            await db.IncidentStatusHistory
                .Where(x => x.IncidentId == id).ExecuteDeleteAsync();

            await db.Incidents
                .Where(x => x.Id == id).ExecuteDeleteAsync();
        }
    }
}



[Theory]
[InlineData("alice", "30000000-0000-0000-0000-000000000001", HttpStatusCode.OK)]
[InlineData("alice", "30000000-0000-0000-0000-000000000003", HttpStatusCode.Forbidden)]
[InlineData("bob", "30000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden)]
[InlineData("morgan", "30000000-0000-0000-0000-000000000003", HttpStatusCode.OK)]
[InlineData("alice", "40000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound)]
public async Task IncidentComment_AccessMatchesPolicy(
    string userName, string commentId, HttpStatusCode expected)
{
    using var client = await factory.LoginAsync(userName, StudyPassword);

    using var response = await client.GetAsync(
        $"/api/incident-comments/{commentId}");

    Assert.Equal(expected, response.StatusCode);
}



[Fact]
public async Task Administrator_CannotCreateIncident()
{
    using var admin = await factory.LoginAsync(
        "admin", StudyPassword);

    using var response = await admin.PostAsJsonAsync(
        "/api/incidents", new
        {
            Title = $"Admin test {Guid.NewGuid():N}",
            Description = "Administrator permission check.",
            Severity = "Low",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

}