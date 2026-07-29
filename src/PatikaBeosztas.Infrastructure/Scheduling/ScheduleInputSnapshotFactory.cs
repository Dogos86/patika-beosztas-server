using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Infrastructure.Scheduling;

public sealed class ScheduleInputSnapshotFactory(PatikaDbContext dbContext)
{
    public async Task<ScheduleInputSnapshot> CreateAsync(
        Guid schedulePlanId,
        ScheduleGenerationOptions options,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.SchedulePlans
            .AsNoTracking()
            .SingleAsync(item => item.Id == schedulePlanId, cancellationToken);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == plan.OrganizationId, cancellationToken);
        var locations = await dbContext.Locations
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.Id)
            .Select(item => new SnapshotLocation(item.Id, item.Name, item.IsActive))
            .ToArrayAsync(cancellationToken);
        var openings = await dbContext.LocationWeeklyOpenings
            .AsNoTracking()
            .Include(item => item.Intervals)
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.LocationId)
            .ToArrayAsync(cancellationToken);
        var openingIntervals = openings
            .SelectMany(opening => Enum.GetValues<DayOfWeek>().SelectMany(day =>
            {
                var mode = opening.GetMode(day);
                var intervals = opening.Intervals
                    .Where(item => item.DayOfWeek == day)
                    .OrderBy(item => item.StartTime)
                    .Select(item => new SnapshotOpeningInterval(
                        opening.LocationId,
                        day,
                        mode,
                        item.StartTime,
                        item.EndTime))
                    .ToArray();
                return intervals.Length > 0
                    ? intervals
                    : [new SnapshotOpeningInterval(
                        opening.LocationId,
                        day,
                        mode,
                        null,
                        null)];
            }))
            .OrderBy(item => item.LocationId)
            .ThenBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartTime)
            .ToArray();
        var templates = await dbContext.LocationShiftTemplates
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.Id)
            .Select(item => new SnapshotShiftTemplate(
                item.Id,
                item.LocationId,
                item.Name,
                item.Category,
                item.WeekdayMask,
                item.StartTime,
                item.EndTime,
                item.IsActive,
                item.RequiredCapability,
                item.TimeType))
            .ToArrayAsync(cancellationToken);
        var coverage = await dbContext.CoverageRequirements
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.Id)
            .Select(item => new SnapshotCoverageRequirement(
                item.Id,
                item.LocationId,
                item.DayOfWeek,
                item.StartTime,
                item.EndTime,
                item.RequiredCapability,
                item.RequiredCount,
                item.Severity,
                item.IsActive,
                item.TimeType))
            .ToArrayAsync(cancellationToken);
        var employees = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.Id)
            .Select(item => new SnapshotEmployee(
                item.Id,
                item.DisplayName,
                item.ProfessionalRole,
                item.IsActive,
                item.IsSchedulable,
                item.IncludeInAutoFill,
                item.CountsAsPharmacist))
            .ToArrayAsync(cancellationToken);
        var employeeLocations = await dbContext.EmployeeLocations
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.LocationId)
            .Select(item => new SnapshotEmployeeLocation(
                item.EmployeeId,
                item.LocationId,
                item.Enabled))
            .ToArrayAsync(cancellationToken);
        var capabilities = await dbContext.EmployeeCapabilities
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.Capability)
            .Select(item => new SnapshotEmployeeCapability(
                item.EmployeeId,
                item.Capability))
            .ToArrayAsync(cancellationToken);
        var profiles = await dbContext.EmployeeWorkProfiles
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.EmployeeId)
            .Select(item => new SnapshotEmployeeWorkProfile(
                item.EmployeeId,
                item.ContractedMonthlyMinutes,
                item.ContractedWeeklyMinutes,
                item.StandardShiftMinutes,
                item.MinimumShiftMinutes,
                item.MaximumRegularShiftMinutes,
                item.MaximumDailyMinutes,
                item.AllowsLongShift,
                item.MaximumLongShiftMinutes,
                item.AllowsFullOpeningHoursShift,
                item.AllowsOvertime,
                item.MaximumOvertimeMinutesPerMonth,
                item.AllowsOnCallDuty,
                item.MaximumOnCallAssignmentsPerMonth,
                item.AllowsStandby,
                item.MaximumStandbyAssignmentsPerMonth,
                item.AllowsSaturday,
                item.MaximumSaturdaysPerMonth,
                item.AllowsSunday,
                item.MaximumSundaysPerMonth,
                item.IncludeInAutoFill))
            .ToArrayAsync(cancellationToken);
        var quotas = await dbContext.EmployeeShiftQuotaRules
            .AsNoTracking()
            .Where(item => item.OrganizationId == plan.OrganizationId)
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.Dimension)
            .Select(item => new SnapshotShiftQuota(
                item.Id,
                item.EmployeeId,
                item.Dimension,
                item.Period,
                item.Minimum,
                item.Target,
                item.Maximum,
                item.Severity,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
        var preferences = await dbContext.WorkPreferences
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == plan.OrganizationId &&
                item.DateFrom <= plan.PeriodEnd &&
                item.DateTo >= plan.PeriodStart)
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.Id)
            .Select(item => new SnapshotWorkPreference(
                item.Id,
                item.EmployeeId,
                item.Type,
                item.DateFrom,
                item.DateTo,
                item.DayOfWeek,
                item.IsFullDay,
                item.StartTime,
                item.EndTime,
                item.LocationId,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
        var leaves = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == plan.OrganizationId &&
                item.DateFrom <= plan.PeriodEnd &&
                (item.DateTo == null || item.DateTo >= plan.PeriodStart))
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.Id)
            .Select(item => new SnapshotLeave(
                item.Id,
                item.EmployeeId,
                item.Type,
                item.DateFrom,
                item.DateTo,
                item.IsFullDay,
                item.StartTime,
                item.EndTime,
                item.Status))
            .ToArrayAsync(cancellationToken);
        var existing = await LoadExistingShiftsAsync(
            plan,
            options.Scope,
            cancellationToken);
        var rejected = await dbContext.GeneratedSuggestionDecisions
            .AsNoTracking()
            .Where(item =>
                item.OrganizationId == plan.OrganizationId &&
                item.SchedulePlanId == plan.Id &&
                item.DecisionType == GeneratedSuggestionDecisionType.Reject)
            .Join(
                dbContext.ShiftAssignments.AsNoTracking(),
                decision => new
                {
                    decision.OrganizationId,
                    Id = decision.ShiftAssignmentId
                },
                shift => new { shift.OrganizationId, Id = shift.Id },
                (decision, shift) => new { Decision = decision, Shift = shift })
            .OrderBy(item => item.Shift.Id)
            .Select(item => new SnapshotRejectedSuggestion(
                item.Shift.Id,
                item.Shift.EmployeeId,
                item.Shift.LocationId,
                item.Shift.Date,
                item.Shift.StartTime,
                item.Shift.EndTime,
                item.Decision.ExclusionScope))
            .ToArrayAsync(cancellationToken);
        return new(
            organization.Id,
            organization.Name,
            organization.TimeZoneId,
            plan.PeriodStart,
            plan.PeriodEnd,
            OrToolsScheduleOptimizer.AlgorithmVersion,
            options,
            locations,
            openingIntervals,
            templates,
            coverage,
            employees,
            employeeLocations,
            capabilities,
            profiles,
            quotas,
            preferences,
            leaves,
            existing,
            rejected);
    }

    private async Task<SnapshotExistingShift[]> LoadExistingShiftsAsync(
        SchedulePlan plan,
        RegenerationScope scope,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.ShiftAssignments
            .AsNoTracking()
            .Include(item => item.Segments)
            .Where(item =>
                item.OrganizationId == plan.OrganizationId &&
                item.SchedulePlanId == plan.Id &&
                item.ChangeKind != ShiftChangeKind.Deleted)
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var basedOn = plan.BasedOnScheduleId is null
            ? []
            : await dbContext.ShiftAssignments
                .AsNoTracking()
                .Include(item => item.Segments)
                .Where(item =>
                    item.OrganizationId == plan.OrganizationId &&
                    item.SchedulePlanId == plan.BasedOnScheduleId &&
                    item.ChangeKind != ShiftChangeKind.Deleted)
                .OrderBy(item => item.Id)
                .ToArrayAsync(cancellationToken);
        return current
            .Select(item => MapExisting(
                item,
                item.IsLocked || !IsInScope(item, scope)))
            .Concat(basedOn.Select(item => MapExisting(item, false)))
            .GroupBy(item => CanonicalKey(item), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Date)
            .ThenBy(item => item.StartTime)
            .ThenBy(item => item.EmployeeId)
            .ToArray();
    }

    private static SnapshotExistingShift MapExisting(
        ShiftAssignment shift,
        bool locked) =>
        new(
            shift.Id,
            shift.EmployeeId,
            shift.LocationId,
            shift.Date,
            shift.StartTime,
            shift.EndTime,
            locked,
            shift.Source,
            shift.Segments
                .OrderBy(segment => segment.StartTime)
                .Select(segment => new SnapshotShiftSegment(
                    segment.StartTime,
                    segment.EndTime,
                    segment.TimeType))
                .ToArray());

    private static bool IsInScope(
        ShiftAssignment shift,
        RegenerationScope scope) =>
        scope.Type switch
        {
            RegenerationScopeType.FullPeriod => true,
            RegenerationScopeType.Day => shift.Date == scope.DateFrom,
            RegenerationScopeType.DateRange => scope.DateFrom <= shift.Date &&
                                               shift.Date <= scope.DateTo,
            RegenerationScopeType.Week => scope.DateFrom <= shift.Date &&
                                          shift.Date <= scope.DateFrom?.AddDays(6),
            RegenerationScopeType.Location => shift.LocationId == scope.LocationId,
            RegenerationScopeType.CapabilityAndTimeType =>
                scope.TimeType is null ||
                shift.Segments.Any(segment => segment.TimeType == scope.TimeType),
            RegenerationScopeType.Issues => true,
            _ => false
        };

    private static string CanonicalKey(SnapshotExistingShift shift) =>
        $"{shift.EmployeeId:N}|{shift.LocationId:N}|{shift.Date:yyyyMMdd}|" +
        $"{shift.StartTime:HHmm}|{shift.EndTime:HHmm}";
}
