using System.Diagnostics.CodeAnalysis;

namespace PatikaBeosztas.Domain;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The phase contract explicitly names this association UserPermission.")]
public sealed class UserPermission
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public ApplicationPermission Permission { get; set; }
}
