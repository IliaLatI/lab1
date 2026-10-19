
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Authorization;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        IncidentQueries queries,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Перевірка дозволу на перегляд списку.
        if (!IncidentAccess.CanList(context.User))
        {
            return Results.Problem(
                title: "Forbidden",
                statusCode: 403);
        }

        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(
                    status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] =
                        [
                            "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
                        ]
                    });
            }

            parsedStatus = candidate;
        }

        // Список уже фільтрується за користувачем у IncidentQueries.
        var incidents = await queries.GetListAsync(
            parsedStatus,
            context.User,
            cancellationToken);

        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        IncidentQueries queries,
        SecureLabDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Перевіряємо існування інциденту.
        var incident = await db.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: 404);
        }

        // Перевіряємо право читання конкретного інциденту.
        if (!IncidentAccess.Allows(
                context.User,
                IncidentOperation.Read,
                incident))
        {
            return Results.Problem(
                title: "Forbidden",
                statusCode: 403);
        }

        // Отримуємо дозволені дані та коментарі.
        var details = await queries.GetDetailsAsync(
            id,
            context.User,
            cancellationToken);

        if (details is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: 404);
        }

        return Results.Ok(details);
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            var isKnown = Enum.TryParse<IncidentStatus>(
                status, ignoreCase: true, out var candidate)
                && Enum.IsDefined(candidate);

            if (!isKnown)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] = ["Допустимі значення: New, " +
                            "Triaged, InProgress, Resolved, Closed."]
                    });
            }

            parsedStatus = candidate;
        }

        var summary = await queries.GetSeveritySummaryAsync(
            parsedStatus,
            cancellationToken);

        return Results.Ok(summary);
    }
}
