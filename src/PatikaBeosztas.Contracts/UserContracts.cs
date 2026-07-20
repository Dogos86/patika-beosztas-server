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
    IReadOnlyList<ApplicationPermission> Permissions);

public sealed record UpdateUserEmployeeLinkRequest(Guid? EmployeeId);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    LinkedEmployeeSummary? LinkedEmployee,
    IReadOnlyList<ApplicationPermission> Permissions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
