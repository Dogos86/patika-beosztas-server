using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record CreateLocationRequest(
    string Name,
    LocationType Type,
    string? Address,
    bool IsActive);

public sealed record UpdateLocationRequest(
    string Name,
    LocationType Type,
    string? Address,
    bool IsActive,
    uint ExpectedVersion);

public sealed record LocationResponse(
    Guid Id,
    string Name,
    LocationType Type,
    string? Address,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
