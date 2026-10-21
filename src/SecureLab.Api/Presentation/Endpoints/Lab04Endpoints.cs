
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using SecureLab.Api.Application.Authorization;

namespace SecureLab.Api.Presentation.Endpoints;

public static class Lab04Endpoints
{
    public static IEndpointRouteBuilder MapLab04Endpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/incidents/{id:guid}", UpdateAsync)
            .RequireAuthorization()
            .WithName("UpdateIncident");

        // Видалення інциденту.
        app.MapDelete("/api/incidents/{id:guid}", DeleteAsync)
            .RequireAuthorization()
            .WithName("DeleteIncident");

        // Пряме читання коментаря.
        app.MapGet("/api/incident-comments/{id:guid}", CommentAsync)
            .RequireAuthorization()
            .WithName("GetIncidentComment");

        // Захищений маршрут зміни статусу інциденту.e.
        app.MapPost("/api/incidents/{id:guid}/triage", TriageAsync)
            .RequireAuthorization()
            .WithName("TriageIncident");
        return app;
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        SecureLabDbContext db,
        HttpContext context,
        CancellationToken ct)
    {
        // Завантаження інциденту за його ідентифікатором.
        // Перед редагуванням виконується перевірка права власника.
        var incident = await db.Incidents
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: 404);
        }
        // Серверна перевірка права редагування.
        if (!IncidentAccess.Allows(
            context.User,
            IncidentOperation.Update,
            incident))
        {
            return Results.Problem(
                title: "Forbidden",
                statusCode: 403);
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

    // Видалення інциденту з перевіркою прав.
private static async Task<IResult> DeleteAsync(
    Guid id,
    HttpContext context,
    SecureLabDbContext db,
    CancellationToken ct)
{
    // Шукаємо інцидент у базі.
    var incident = await db.Incidents
        .SingleOrDefaultAsync(x => x.Id == id, ct);

    if (incident is null)
    {
        return Results.Problem(
            title: "Інцидент не знайдено",
            statusCode: 404);
    }

    // За нашою матрицею Delete заборонений усім ролям.
    if (!IncidentAccess.Allows(
        context.User,
        IncidentOperation.Delete,
        incident))
    {
        return Results.Problem(
            title: "Forbidden",
            statusCode: 403);
    }

    // Виконується лише за наявності дозволу.
    db.Incidents.Remove(incident);
    await db.SaveChangesAsync(ct);

    return Results.NoContent();
}

// Пряме читання дочірнього ресурсу IncidentComment.
private static async Task<IResult> CommentAsync(
    Guid id,
    HttpContext context,
    SecureLabDbContext db,
    CancellationToken ct)
{
    // Завантажуємо коментар разом із батьківським інцидентом.
    var comment = await db.IncidentComments
        .AsNoTracking()
        .Include(x => x.Incident)
        .Include(x => x.Author)
        .SingleOrDefaultAsync(x => x.Id == id, ct);

    if (comment is null)
    {
        return Results.Problem(
            title: "Коментар не знайдено",
            statusCode: 404);
    }

    // Спочатку перевіряємо право читання інциденту.
    if (!IncidentAccess.Allows(
        context.User,
        IncidentOperation.Read,
        comment.Incident))
    {
        return Results.Problem(
            title: "Forbidden",
            statusCode: 403);
    }

    // Додаткова перевірка для внутрішніх коментарів.
    if (comment.IsInternal &&
        !IncidentAccess.CanSeeInternal(context.User))
    {
        return Results.Problem(
            title: "Forbidden",
            statusCode: 403);
    }

    // Повертаємо дозволений коментар.
    return Results.Ok(new IncidentCommentResponse(
        comment.Id,
        comment.Author.DisplayName,
        comment.Text,
        comment.CreatedAtUtc));
}

// Виконання привілейованого переходу з перевіркою ролі та поточного статусу.
private static async Task<IResult> TriageAsync(
    Guid id,
    SecureLabDbContext db,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct)
{
    var incident = await db.Incidents
        .SingleOrDefaultAsync(x => x.Id == id, ct);

    if (incident is null)
    {
        return Results.Problem(
            title: "Інцидент не знайдено",
            statusCode: 404);
    }

    var actorId = IncidentAccess.GetUserId(context.User);

    if (actorId is null)
    {
        return Results.Unauthorized();
    }

    // Перевіряємо роль користувача та право виконання triage.
    if (!IncidentAccess.Allows(
        context.User,
        IncidentOperation.Triage,
        incident))
    {
        Lab04Audit.Denied(logger, context, incident.Id);

        return Results.Problem(
            title: "Forbidden",
            statusCode: 403,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = System.Diagnostics.Activity.Current?.Id
                     ?? context.TraceIdentifier
            });
    }

    if (incident.Status != IncidentStatus.New)
    {
        Lab04Audit.Denied(logger, context, incident.Id);

        return Results.Problem(
            title: "Only New incidents can be triaged",
            statusCode: 409,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = System.Diagnostics.Activity.Current?.Id
                    ?? context.TraceIdentifier
            });
    }

    var oldStatus = incident.Status;
    var now = DateTimeOffset.UtcNow;

    incident.Status = IncidentStatus.Triaged;
    incident.UpdatedAtUtc = now;

    db.IncidentStatusHistory.Add(new IncidentStatusHistory
    {
        Id = Guid.NewGuid(),
        IncidentId = incident.Id,
        ChangedByUserId = actorId.Value,
        OldStatus = oldStatus,
        NewStatus = incident.Status,
        Note = "Incident triaged by authorized Analyst.",
        CreatedAtUtc = now
    });

    await db.SaveChangesAsync(ct);

    return Results.NoContent();
}



}
