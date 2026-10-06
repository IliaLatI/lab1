using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {




    app.MapGet("/api/incidents/search", async (
        string? q,
        string? sortBy,
        SecureLabDbContext db,
        CancellationToken ct) =>
    {
        if (sortBy is not (null or "" or "createdAtUtc" or "severity" or "status"))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                        ["Допустимі значення: createdAtUtc, severity, status."]
                });
        }

        // Екрануємо спеціальні символи шаблону пошуку.
        var searchText = (q ?? "")
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        var pattern = $"%{searchText}%";

        var query = db.Incidents
            .AsNoTracking()
            .Where(row =>
                EF.Functions.ILike(row.Title, pattern, "\\")
                || EF.Functions.ILike(row.Description, pattern, "\\"));

        var sortedQuery = sortBy switch
        {
            "severity" => query.OrderBy(row => row.Severity),
            "status" => query.OrderBy(row => row.Status),
            _ => query.OrderByDescending(row => row.CreatedAtUtc)
        };

        var rows = await sortedQuery
            .Take(50)
            .ToListAsync(ct);

        return Results.Ok(rows.Select(row => new
        {
            row.Id,
            row.Title,
            row.Description,
            Severity = row.Severity.ToString(),
            Status = row.Status.ToString(),
            row.CreatedAtUtc
        }));
    });



        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            // ЛР 02: доповніть server validation, business rules та response contract.
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

           if (string.IsNullOrWhiteSpace(request.Title))

            errors["title"] = ["Title є обов'язковим."];
            else if (request.Title.Length > 160)
                errors["title"] = ["Title не може містити більше 160 символів."];

            if (string.IsNullOrWhiteSpace(request.Description))
                errors["description"] = ["Description є обов'язковим."];
            else if (request.Description.Length > 4000)
                errors["description"] = ["Description не може містити більше 4000 символів."];

            var title = request.Title?.Trim();
            var description = request.Description?.Trim();

            IncidentSeverity severity = default;

            if (string.IsNullOrWhiteSpace(request.Severity)
                || !Enum.TryParse<IncidentSeverity>(
                    request.Severity,
                    true,
                    out severity)
                || !Enum.IsDefined(severity))
            {
                errors["severity"] =
                    ["Допустимі значення: Low, Medium, High, Critical."];
            }

            if (request.OccurredAtUtc is null)
            {
                errors["occurredAtUtc"] =
                    ["OccurredAtUtc є обов'язковим."];
            }
            else if (request.OccurredAtUtc > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] =
                    ["OccurredAtUtc не може бути більш ніж на 5 хвилин у майбутньому."];
            }


            if (!errors.ContainsKey("severity")
                && !errors.ContainsKey("description")
                && severity is IncidentSeverity.High or IncidentSeverity.Critical
                && description!.Length < 40)
            {
                errors["description"] =
                    ["Для High або Critical description має містити щонайменше 40 символів."];
            }


            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

           var duplicateExists = await db.Incidents.AnyAsync(
            row => row.Title == title
                && (row.Status == IncidentStatus.New
                    || row.Status == IncidentStatus.Triaged
                    || row.Status == IncidentStatus.InProgress
                    || row.Status == IncidentStatus.Resolved),
            ct);

            if (duplicateExists)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Incident title conflict",
                    detail: "Активний інцидент із таким title уже існує.");
            } 

            var incident = new Incident
            {
                Id = Guid.NewGuid(), OwnerUserId = DbSeeder.AliceId,
                Title = title!,
                Description = description!,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);
            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.UpdatedAtUtc);

            return Results.Created(
                $"/api/incidents/{incident.Id}",
                response);
      });
    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
