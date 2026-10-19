
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class Lab04Endpoints
{
    public static IEndpointRouteBuilder MapLab04Endpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/incidents/{id:guid}", UpdateAsync)
            .RequireAuthorization()
            .WithName("UpdateIncident");

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        SecureLabDbContext db,
        CancellationToken ct)
    {
        // Навчальний вразливий стан.
        // Перевірки власника поки немає.
        var incident = await db.Incidents
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: 404);
        }

        var errors = new Dictionary<string, string[]>();

        var title = request.Title?.Trim();
        var description = request.Description?.Trim();

        // Перевірка заголовка.
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
        {
            errors["title"] =
                ["Title має містити від 1 до 160 символів."];
        }

        // Перевірка опису.
        if (string.IsNullOrWhiteSpace(description)
            || description.Length > 4000)
        {
            errors["description"] =
                ["Description має містити від 1 до 4000 символів."];
        }

        // Додаткове правило High/Critical.
        if (!string.IsNullOrWhiteSpace(description)
            && incident.Severity is
                IncidentSeverity.High or IncidentSeverity.Critical
            && description.Length < 40)
        {
            errors["description"] =
                ["Для High/Critical потрібно щонайменше 40 символів."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Перевірка конфлікту заголовків.
        var duplicate = await db.Incidents.AnyAsync(
            x => x.Id != id
                && x.Title == title
                && x.Status != IncidentStatus.Closed,
            ct);

        if (duplicate)
        {
            return Results.Problem(
                title: "Конфлікт заголовка інциденту",
                statusCode: 409);
        }

        // Змінюємо тільки дозволені поля.
        incident.Title = title!;
        incident.Description = description!;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
