using System.Security.Claims;

namespace SecureLab.Api.Presentation.Endpoints;

public static class Lab04Audit
{
    public static void Denied(
        ILogger logger,
        HttpContext context,
        Guid resourceId)
    {
        logger.LogWarning(
            "Audit {EventType} subject={SubjectId} action={Action} " +
            "resource={ResourceId} result={Result} time={OccurredAtUtc} " +
            "trace={TraceId}",
            "authorization.denied",
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            "incident.triage",
            resourceId,
            "denied",
            DateTimeOffset.UtcNow,
            System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier);
    }
}