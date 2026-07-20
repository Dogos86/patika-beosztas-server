using Microsoft.AspNetCore.Identity;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }

    public required string DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? EmployeeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Organization? Organization { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<UserPermission> Permissions { get; } = new List<UserPermission>();
}
