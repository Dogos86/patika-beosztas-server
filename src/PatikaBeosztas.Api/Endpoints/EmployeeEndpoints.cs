using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/employees")
            .WithTags("Employees")
            .RequireAuthorization(PermissionPolicies.For(ApplicationPermission.ManageEmployees));

        group.MapGet("", ListAsync)
            .WithSummary("Dolgozók szervezeten belüli listázása")
            .Produces<PagedResponse<EmployeeResponse>>()
            .ProducesStandardErrors();
        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Dolgozó részletes lekérése")
            .Produces<EmployeeResponse>()
            .ProducesStandardErrors();
        group.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó létrehozása")
            .Produces<EmployeeResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó módosítása vagy logikai deaktiválása")
            .Produces<EmployeeResponse>()
            .ProducesStandardErrors(includeConflict: true);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        int page = 1,
        int pageSize = 25,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        (page, pageSize) = NormalizePage(page, pageSize);
        var query = dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.OrganizationId == actor.OrganizationId);
        if (!includeInactive)
        {
            query = query.Where(employee => employee.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(employee =>
                EF.Functions.ILike(employee.FullName, pattern) ||
                EF.Functions.ILike(employee.DisplayName, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var employees = await IncludeEmployeeDetails(query)
            .OrderBy(employee => employee.DisplayName)
            .ThenBy(employee => employee.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var linkedUsers = await LoadLinkedUsersAsync(
            employees.Select(employee => employee.Id),
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        var response = employees
            .Select(employee => MapEmployee(employee, linkedUsers.GetValueOrDefault(employee.Id)))
            .ToArray();
        return Results.Ok(new PagedResponse<EmployeeResponse>(
            response,
            page,
            pageSize,
            totalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var employee = await IncludeEmployeeDetails(dbContext.Employees.AsNoTracking())
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        var linkedUser = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user =>
                    user.OrganizationId == actor.OrganizationId &&
                    user.EmployeeId == employee.Id,
                cancellationToken);
        return Results.Ok(MapEmployee(employee, linkedUser));
    }

    private static async Task<IResult> CreateAsync(
        CreateEmployeeRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var locationError = await ValidateLocationsAsync(
            request.Locations,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (locationError is not null)
        {
            return EndpointHelpers.ValidationProblem([locationError]);
        }

        var now = timeProvider.GetUtcNow();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            FullName = request.FullName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            ProfessionalRole = request.ProfessionalRole,
            IsActive = request.IsActive,
            IsSchedulable = request.IsSchedulable,
            IncludeInAutoFill = request.IncludeInAutoFill,
            CountsAsPharmacist = request.CountsAsPharmacist,
            MonthlyMinutesLimit = request.MonthlyMinutesLimit,
            MaxDailyMinutes = request.MaxDailyMinutes,
            BirthDate = request.BirthDate,
            ExternalPayrollId = NormalizeOptional(request.ExternalPayrollId),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ReplaceEmployeeSettings(
            employee,
            request.Locations,
            request.TimeWindows,
            request.AllowedTimeTypes,
            actor.OrganizationId);
        dbContext.Employees.Add(employee);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "Employee.Created",
            "Employee",
            employee.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozó létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var createdEmployee = await IncludeEmployeeDetails(dbContext.Employees.AsNoTracking())
            .AsSplitQuery()
            .SingleAsync(item => item.Id == employee.Id, cancellationToken);

        return Results.Created(
            $"/api/admin/employees/{employee.Id}",
            MapEmployee(createdEmployee, null));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var employee = await IncludeEmployeeDetails(dbContext.Employees)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (employee.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A dolgozó adatai a lekérés óta megváltoztak. Töltse újra az adatokat.");
        }

        var locationError = await ValidateLocationsAsync(
            request.Locations,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (locationError is not null)
        {
            return EndpointHelpers.ValidationProblem([locationError]);
        }

        employee.FullName = request.FullName.Trim();
        employee.DisplayName = request.DisplayName.Trim();
        employee.ProfessionalRole = request.ProfessionalRole;
        employee.IsActive = request.IsActive;
        employee.IsSchedulable = request.IsSchedulable;
        employee.IncludeInAutoFill = request.IncludeInAutoFill;
        employee.CountsAsPharmacist = request.CountsAsPharmacist;
        employee.MonthlyMinutesLimit = request.MonthlyMinutesLimit;
        employee.MaxDailyMinutes = request.MaxDailyMinutes;
        employee.BirthDate = request.BirthDate;
        employee.ExternalPayrollId = NormalizeOptional(request.ExternalPayrollId);
        employee.UpdatedAtUtc = timeProvider.GetUtcNow();

        UpdateEmployeeSettings(
            employee,
            request.Locations,
            request.TimeWindows,
            request.AllowedTimeTypes,
            actor.OrganizationId,
            dbContext);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            request.IsActive ? "Employee.Updated" : "Employee.Deactivated",
            "Employee",
            employee.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozó alapadatai és beoszthatósági beállításai módosítva.");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A dolgozó adatai mentés közben megváltoztak. Töltse újra az adatokat.");
        }

        var linkedUser = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user =>
                    user.OrganizationId == actor.OrganizationId &&
                    user.EmployeeId == employee.Id,
                cancellationToken);
        var updatedEmployee = await IncludeEmployeeDetails(dbContext.Employees.AsNoTracking())
            .AsSplitQuery()
            .SingleAsync(item => item.Id == employee.Id, cancellationToken);
        return Results.Ok(MapEmployee(updatedEmployee, linkedUser));
    }

    private static IQueryable<Employee> IncludeEmployeeDetails(IQueryable<Employee> query) =>
        query
            .Include(employee => employee.Locations)
            .ThenInclude(location => location.Location)
            .Include(employee => employee.TimeWindows)
            .Include(employee => employee.AllowedTimeTypes);

    private static async Task<Dictionary<Guid, ApplicationUser>> LoadLinkedUsersAsync(
        IEnumerable<Guid> employeeIds,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToArray();
        return await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.OrganizationId == organizationId &&
                user.EmployeeId != null &&
                ids.Contains(user.EmployeeId.Value))
            .ToDictionaryAsync(user => user.EmployeeId!.Value, cancellationToken);
    }

    private static EmployeeResponse MapEmployee(
        Employee employee,
        ApplicationUser? linkedUser) =>
        new(
            employee.Id,
            employee.FullName,
            employee.DisplayName,
            employee.ProfessionalRole,
            employee.IsActive,
            employee.IsSchedulable,
            employee.IncludeInAutoFill,
            employee.CountsAsPharmacist,
            employee.MonthlyMinutesLimit,
            employee.MaxDailyMinutes,
            employee.BirthDate,
            employee.ExternalPayrollId,
            employee.Locations
                .OrderBy(location => location.Location?.Name)
                .Select(location => new EmployeeLocationResponse(
                    location.LocationId,
                    location.Location?.Name ?? string.Empty,
                    location.Enabled))
                .ToArray(),
            employee.TimeWindows
                .OrderBy(window => window.DayOfWeek)
                .ThenBy(window => window.StartTime)
                .Select(window => new EmployeeTimeWindowResponse(
                    window.Id,
                    window.DayOfWeek,
                    window.StartTime,
                    window.EndTime,
                    window.Type))
                .ToArray(),
            employee.AllowedTimeTypes
                .Select(item => item.TimeType)
                .Order()
                .ToArray(),
            linkedUser is null
                ? null
                : new LinkedUserSummary(
                    linkedUser.Id,
                    linkedUser.Email ?? string.Empty,
                    linkedUser.DisplayName,
                    linkedUser.IsActive),
            InputValidation.EmployeeWarnings(
                employee.ProfessionalRole,
                employee.CountsAsPharmacist),
            employee.Version,
            employee.CreatedAtUtc,
            employee.UpdatedAtUtc);

    private static IReadOnlyList<ApiValidationError> ValidateRequest(
        CreateEmployeeRequest request) =>
        InputValidation.ValidateEmployee(
            request.FullName,
            request.DisplayName,
            request.MonthlyMinutesLimit,
            request.MaxDailyMinutes,
            request.ExternalPayrollId,
            request.Locations,
            request.TimeWindows,
            request.AllowedTimeTypes);

    private static IReadOnlyList<ApiValidationError> ValidateRequest(
        UpdateEmployeeRequest request) =>
        InputValidation.ValidateEmployee(
            request.FullName,
            request.DisplayName,
            request.MonthlyMinutesLimit,
            request.MaxDailyMinutes,
            request.ExternalPayrollId,
            request.Locations,
            request.TimeWindows,
            request.AllowedTimeTypes);

    private static async Task<ApiValidationError?> ValidateLocationsAsync(
        IReadOnlyList<EmployeeLocationRequest>? locations,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ids = (locations ?? []).Select(location => location.LocationId).Distinct().ToArray();
        var matchingCount = await dbContext.Locations.CountAsync(
            location =>
                location.OrganizationId == organizationId &&
                ids.Contains(location.Id),
            cancellationToken);
        return matchingCount == ids.Length
            ? null
            : new ApiValidationError(
                "LOCATION_OUTSIDE_ORGANIZATION",
                "Minden telephelynek az aktuális szervezethez kell tartoznia.",
                "locations");
    }

    private static void ReplaceEmployeeSettings(
        Employee employee,
        IReadOnlyList<EmployeeLocationRequest>? locations,
        IReadOnlyList<EmployeeTimeWindowRequest>? timeWindows,
        IReadOnlyList<TimeType>? allowedTimeTypes,
        Guid organizationId)
    {
        foreach (var location in locations ?? [])
        {
            employee.Locations.Add(new EmployeeLocation
            {
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                LocationId = location.LocationId,
                Enabled = location.Enabled
            });
        }

        foreach (var window in timeWindows ?? [])
        {
            employee.TimeWindows.Add(new EmployeeTimeWindow
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                DayOfWeek = window.DayOfWeek,
                StartTime = window.StartTime,
                EndTime = window.EndTime,
                Type = window.Type
            });
        }

        foreach (var timeType in allowedTimeTypes ?? [])
        {
            employee.AllowedTimeTypes.Add(new EmployeeAllowedTimeType
            {
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                TimeType = timeType
            });
        }
    }

    private static void UpdateEmployeeSettings(
        Employee employee,
        IReadOnlyList<EmployeeLocationRequest>? locations,
        IReadOnlyList<EmployeeTimeWindowRequest>? timeWindows,
        IReadOnlyList<TimeType>? allowedTimeTypes,
        Guid organizationId,
        PatikaDbContext dbContext)
    {
        var requestedLocations = (locations ?? [])
            .ToDictionary(location => location.LocationId);
        foreach (var existing in employee.Locations.ToArray())
        {
            if (requestedLocations.Remove(existing.LocationId, out var requested))
            {
                existing.Enabled = requested.Enabled;
            }
            else
            {
                dbContext.EmployeeLocations.Remove(existing);
                employee.Locations.Remove(existing);
            }
        }

        foreach (var requested in requestedLocations.Values)
        {
            employee.Locations.Add(new EmployeeLocation
            {
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                LocationId = requested.LocationId,
                Enabled = requested.Enabled
            });
        }

        dbContext.EmployeeTimeWindows.RemoveRange(employee.TimeWindows);
        employee.TimeWindows.Clear();
        foreach (var window in timeWindows ?? [])
        {
            employee.TimeWindows.Add(new EmployeeTimeWindow
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                DayOfWeek = window.DayOfWeek,
                StartTime = window.StartTime,
                EndTime = window.EndTime,
                Type = window.Type
            });
        }

        var requestedTimeTypes = (allowedTimeTypes ?? []).ToHashSet();
        foreach (var existing in employee.AllowedTimeTypes.ToArray())
        {
            if (!requestedTimeTypes.Remove(existing.TimeType))
            {
                dbContext.EmployeeAllowedTimeTypes.Remove(existing);
                employee.AllowedTimeTypes.Remove(existing);
            }
        }

        foreach (var requested in requestedTimeTypes)
        {
            employee.AllowedTimeTypes.Add(new EmployeeAllowedTimeType
            {
                OrganizationId = organizationId,
                EmployeeId = employee.Id,
                TimeType = requested
            });
        }
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
