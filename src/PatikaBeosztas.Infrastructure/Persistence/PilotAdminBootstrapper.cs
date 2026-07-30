using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;

namespace PatikaBeosztas.Infrastructure.Persistence;

public sealed record PilotAdminBootstrapRequest(
    string OrganizationName,
    string Email,
    string DisplayName,
    string Password);

public sealed record PilotAdminBootstrapResult(
    Guid OrganizationId,
    Guid UserId,
    bool Created);

public sealed class PilotAdminBootstrapper(
    PatikaDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
{
    public async Task<PilotAdminBootstrapResult> BootstrapAsync(
        PilotAdminBootstrapRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationName = request.OrganizationName.Trim();
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (organizationName.Length is < 2 or > 200)
        {
            throw new InvalidOperationException(
                "A szervezet neve 2–200 karakteres legyen.");
        }

        if (displayName.Length is < 2 or > 200)
        {
            throw new InvalidOperationException(
                "A megjelenítési név 2–200 karakteres legyen.");
        }

        var normalizedEmail = userManager.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Érvényes admin email-cím kötelező.");
        }

        await EnsureSchemaIsCurrentAsync(cancellationToken);

        var existingUser = await userManager.FindByEmailAsync(email);
        var matchingOrganizations = await dbContext.Organizations
            .Where(organization => organization.Name == organizationName)
            .ToArrayAsync(cancellationToken);
        if (matchingOrganizations.Length > 1)
        {
            throw new InvalidOperationException(
                "Több azonos nevű szervezet található; az első admin nem oldható fel egyértelműen.");
        }

        if (existingUser is not null)
        {
            if (matchingOrganizations.Length != 1 ||
                existingUser.OrganizationId != matchingOrganizations[0].Id)
            {
                throw new InvalidOperationException(
                    "Az email-cím már más szervezethez tartozó fiókhoz van rendelve.");
            }

            return new PilotAdminBootstrapResult(
                existingUser.OrganizationId,
                existingUser.Id,
                Created: false);
        }

        var organization = matchingOrganizations.SingleOrDefault();
        if (organization is null &&
            await dbContext.Organizations.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Már létezik más nevű szervezet; az első admin parancs nem választ tenantot automatikusan.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        organization ??= new Organization
        {
            Id = Guid.NewGuid(),
            Name = organizationName,
            TimeZoneId = "Europe/Budapest",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        if (dbContext.Entry(organization).State == EntityState.Detached)
        {
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var descriptions = string.Join(
                "; ",
                createResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Az első admin nem hozható létre: {descriptions}");
        }

        dbContext.UserPermissions.AddRange(
            Enum.GetValues<ApplicationPermission>().Select(permission =>
                new UserPermission
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    Permission = permission
                }));
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            ActorUserId = user.Id,
            Action = "Pilot.AdminBootstrapped",
            EntityType = "ApplicationUser",
            EntityId = user.Id.ToString(),
            TimestampUtc = now,
            CorrelationId = $"bootstrap-{user.Id:N}",
            ChangeSummary = "Az első pilot admin és szervezet létrehozva."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PilotAdminBootstrapResult(
            organization.Id,
            user.Id,
            Created: true);
    }

    private async Task EnsureSchemaIsCurrentAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "A PostgreSQL adatbázis nem érhető el.");
        }

        var pending = await dbContext.Database
            .GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            throw new InvalidOperationException(
                "Függő EF migráció található. Előbb futtasd a railway-migrate.sh scriptet.");
        }
    }
}
