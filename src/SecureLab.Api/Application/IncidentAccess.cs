using System.Security.Claims;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Application.Authorization;

public enum IncidentOperation
{
    Read,
    Update,
    Delete,
    Triage
}

public static class IncidentAccess
{
    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    public static bool Allows(
        ClaimsPrincipal user,
        IncidentOperation operation,
        Incident incident)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var userId = GetUserId(user);

        if (userId is null)
            return false;

        var reporterOwner =
            user.IsInRole(DbSeeder.ReporterRole)
            && incident.OwnerUserId == userId.Value;

        var analyst = user.IsInRole(DbSeeder.AnalystRole);

        return operation switch
        {
            IncidentOperation.Read => analyst || reporterOwner,
            IncidentOperation.Update => reporterOwner,
            IncidentOperation.Delete => false,
            IncidentOperation.Triage => analyst,
            _ => false
        };
    }

    
    public static bool CanList(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true
            || GetUserId(user) is null)
            return false;

        return user.IsInRole(DbSeeder.ReporterRole)
            || user.IsInRole(DbSeeder.AnalystRole);
    }



    public static IQueryable<Incident> VisibleTo(
        IQueryable<Incident> query,
        ClaimsPrincipal user)
    {
        var userId = GetUserId(user);

        if (user.Identity?.IsAuthenticated != true
            || userId is null)
            return query.Where(x => false);

        if (user.IsInRole(DbSeeder.AnalystRole))
            return query;

        if (user.IsInRole(DbSeeder.ReporterRole))
            return query.Where(
                x => x.OwnerUserId == userId.Value);

        return query.Where(x => false);
    }

    public static bool CanSeeInternal(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true
            && GetUserId(user) is not null
            && user.IsInRole(DbSeeder.AnalystRole);
    }
}
