using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
        private static readonly IReadOnlyDictionary<IncidentSeverity, int>
        SeverityOrder = new Dictionary<IncidentSeverity, int>
        {
            [IncidentSeverity.Critical] = 0,
            [IncidentSeverity.High] = 1,
            [IncidentSeverity.Medium] = 2,
            [IncidentSeverity.Low] = 3,
        };

    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incidents with status filter {Status}", status);

        var query = dbContext.Incidents.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<IncidentDetailsResponse?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incident {IncidentId}", id);

        return dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    .Where(comment => !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.Id,
                        comment.Author.DisplayName,
                        comment.Text,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
        public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>>
        GetSeveritySummaryAsync(
            IncidentStatus? status,
            CancellationToken cancellationToken)
    {
        var query = dbContext.Incidents.AsNoTracking();
 
        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }
 
// 1. Завантажуємо наявні групи з бази у словник
    var dbGroups = await query
        .GroupBy(incident => incident.Severity)
        .Select(group => new
        {
            Severity = group.Key,
            Count = group.Count(),
        })
        .ToDictionaryAsync(group => group.Severity, group => group.Count, cancellationToken);

    logger.LogInformation(
        "Severity summary produced {GroupCount} DB groups",
        dbGroups.Count);

    // 2. Проходимо по всіх enum-значеннях і підставляємо 0 для відсутніх
    return Enum.GetValues<IncidentSeverity>()
        .OrderBy(severity => SeverityOrder[severity])
        .Select(severity => new IncidentSeveritySummaryResponse(
            severity.ToString(),
            dbGroups.GetValueOrDefault(severity, 0)))
        .ToList();
    }

}
