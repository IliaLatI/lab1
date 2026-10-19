

using System.Text.Json.Serialization;

namespace SecureLab.Api.Presentation.Contracts;

// A-08a: невідомі JSON-поля ігноруються.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record UpdateIncidentRequest(
    string? Title,
    string? Description
);
