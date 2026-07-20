using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record SessionResponse(
    Guid UserId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationTimeZoneId,
    string DisplayName,
    string Email,
    IReadOnlyList<ApplicationPermission> Permissions,
    LinkedEmployeeSummary? LinkedEmployee);

public sealed record LinkedEmployeeSummary(
    Guid Id,
    string DisplayName,
    ProfessionalRole ProfessionalRole,
    bool IsActive,
    bool IsSchedulable);
