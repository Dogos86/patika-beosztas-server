using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string InitialPassword,
    Guid? EmployeeId,
    IReadOnlyList<ApplicationPermission>? Permissions,
    bool IsActive = true);

public sealed record UpdateUserPermissionsRequest(
    IReadOnlyList<ApplicationPermission> Permissions,
    uint ExpectedVersion);

public sealed record UpdateUserEmployeeLinkRequest(
    Guid? EmployeeId,
    uint ExpectedVersion);

public sealed record UpdateUserStatusRequest(
    bool IsActive,
    uint ExpectedVersion);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    LinkedEmployeeSummary? LinkedEmployee,
    IReadOnlyList<ApplicationPermission> Permissions,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
