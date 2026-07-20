using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Security;

public static class PermissionPolicies
{
    public static string For(ApplicationPermission permission) => $"Permission:{permission}";
}
