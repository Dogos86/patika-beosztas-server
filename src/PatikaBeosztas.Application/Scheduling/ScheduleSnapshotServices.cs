using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Scheduling;

public static class ScheduleSnapshotCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Serialize(ScheduleInputSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static string ComputeHash(string canonicalSnapshot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSnapshot));
        return Convert.ToHexStringLower(bytes);
    }
}

public sealed record ScheduleCandidateBuildResult(
    ScheduleOptimizerInput OptimizerInput,
    IReadOnlyList<ScheduleOptimizationIssue> InputIssues);

public sealed record ScheduleGenerationDiagnosticCounts(
    int ActiveLocationCount,
    int OpeningIntervalCount,
    int ActiveShiftTemplateCount,
    int ApplicableShiftTemplateCount,
    int CoverageRequirementCount,
    int ActiveEmployeeCount,
    int SchedulableEmployeeCount,
    int AutoFillEmployeeCount,
    int LocationAssignedEmployeeCount,
    int WorkProfileEmployeeCount,
    int CapableEmployeeCount,
    int CandidateOptionCount)
{
    public IReadOnlyDictionary<string, object?> ToParameters() =>
        new Dictionary<string, object?>
        {
            ["activeLocationCount"] = ActiveLocationCount,
            ["openingIntervalCount"] = OpeningIntervalCount,
            ["activeShiftTemplateCount"] = ActiveShiftTemplateCount,
            ["applicableShiftTemplateCount"] = ApplicableShiftTemplateCount,
            ["coverageRequirementCount"] = CoverageRequirementCount,
            ["activeEmployeeCount"] = ActiveEmployeeCount,
            ["schedulableEmployeeCount"] = SchedulableEmployeeCount,
            ["autoFillEmployeeCount"] = AutoFillEmployeeCount,
            ["locationAssignedEmployeeCount"] = LocationAssignedEmployeeCount,
            ["workProfileEmployeeCount"] = WorkProfileEmployeeCount,
            ["capableEmployeeCount"] = CapableEmployeeCount,
            ["candidateOptionCount"] = CandidateOptionCount
        };
}

public sealed record ScheduleGenerationPreflightIssue(
    string Code,
    ScheduleIssueSeverity Severity,
    string Message,
    string? SettingsPath);

public sealed record ScheduleGenerationLocationCheck(
    Guid LocationId,
    string LocationName,
    bool IsActive,
    bool HasOpeningHours,
    bool HasApplicableShiftTemplate,
    bool HasCoverageRequirement);

public sealed record ScheduleGenerationEmployeeCheck(
    Guid EmployeeId,
    string EmployeeDisplayName,
    bool IsActive,
    bool IsSchedulable,
    bool IncludeInAutoFill,
    bool HasLocationAssignment,
    bool HasWorkProfile,
    bool HasPositiveContractedTime,
    bool HasCapability,
    bool HasBlockingAbsenceOrUnavailable,
    int CandidateOptionCount);

public sealed record ScheduleGenerationPreflightResult(
    ScheduleGenerationDiagnosticCounts Counts,
    IReadOnlyList<ScheduleGenerationPreflightIssue> Issues,
    IReadOnlyList<ScheduleGenerationLocationCheck> Locations,
    IReadOnlyList<ScheduleGenerationEmployeeCheck> Employees)
{
    public bool CanStart => Issues.All(issue => issue.Severity != ScheduleIssueSeverity.Blocking);
}

public static class ScheduleGenerationDiagnostics
{
    public static ScheduleGenerationPreflightResult Analyze(
        ScheduleInputSnapshot snapshot,
        int candidateOptionCount,
        IReadOnlyList<ScheduleCandidateOption>? candidates = null)
    {
        var dates = Enumerable.Range(
                0,
                snapshot.PeriodEnd.DayNumber - snapshot.PeriodStart.DayNumber + 1)
            .Select(snapshot.PeriodStart.AddDays)
            .ToArray();
        var weekdays = dates.Select(date => date.DayOfWeek).ToHashSet();
        var activeLocationIds = snapshot.Locations
            .Where(location => location.IsActive)
            .Select(location => location.Id)
            .ToHashSet();
        var activeEmployees = snapshot.Employees
            .Where(employee => employee.IsActive)
            .ToArray();
        var schedulableEmployees = activeEmployees
            .Where(employee => employee.IsSchedulable)
            .ToArray();
        var autoFillEmployees = schedulableEmployees
            .Where(employee => employee.IncludeInAutoFill)
            .ToArray();
        var autoFillEmployeeIds = autoFillEmployees.Select(employee => employee.Id).ToHashSet();
        var workProfileEmployeeIds = snapshot.WorkProfiles
            .Where(profile => profile.IncludeInAutoFill && autoFillEmployeeIds.Contains(profile.EmployeeId))
            .Select(profile => profile.EmployeeId)
            .ToHashSet();
        var profilesByEmployee = snapshot.WorkProfiles
            .ToDictionary(profile => profile.EmployeeId);
        var activeLocationAssignedEmployeeIds = snapshot.EmployeeLocations
            .Where(assignment =>
                assignment.Enabled &&
                activeLocationIds.Contains(assignment.LocationId))
            .Select(assignment => assignment.EmployeeId)
            .ToHashSet();
        var locationAssignedEmployeeIds = activeLocationAssignedEmployeeIds
            .Where(autoFillEmployeeIds.Contains)
            .ToHashSet();
        var requiredCapabilities = snapshot.CoverageRequirements
            .Where(requirement =>
                requirement.IsActive &&
                activeLocationIds.Contains(requirement.LocationId) &&
                weekdays.Contains(requirement.DayOfWeek))
            .Select(requirement => requirement.RequiredCapability)
            .ToHashSet();
        var explicitCapabilities = snapshot.EmployeeCapabilities
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Capability));
        var capableEmployeeCount = autoFillEmployees.Count(employee =>
        {
            var effective = StaffingCapabilityRules.ResolveEffective(
                explicitCapabilities.GetValueOrDefault(employee.Id, []),
                employee.ProfessionalRole,
                employee.CountsAsPharmacist);
            return requiredCapabilities.Count == 0
                ? effective.Count > 0
                : effective.Overlaps(requiredCapabilities);
        });
        var locationChecks = snapshot.Locations
            .OrderBy(location => location.Name, StringComparer.CurrentCulture)
            .Select(location => new ScheduleGenerationLocationCheck(
                location.Id,
                location.Name,
                location.IsActive,
                snapshot.OpeningIntervals.Any(interval =>
                    interval.LocationId == location.Id &&
                    weekdays.Contains(interval.DayOfWeek) &&
                    interval.Mode != OpeningDayMode.Closed),
                snapshot.ShiftTemplates.Any(template =>
                    template.LocationId == location.Id &&
                    template.IsActive &&
                    dates.Any(date =>
                        (template.WeekdayMask & (1 << (int)date.DayOfWeek)) != 0)),
                snapshot.CoverageRequirements.Any(requirement =>
                    requirement.LocationId == location.Id &&
                    requirement.IsActive &&
                    weekdays.Contains(requirement.DayOfWeek))))
            .ToArray();
        var candidatesByEmployee = (candidates ?? [])
            .GroupBy(candidate => candidate.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Count());
        var employeeChecks = snapshot.Employees
            .OrderBy(employee => employee.DisplayName, StringComparer.CurrentCulture)
            .Select(employee =>
            {
                profilesByEmployee.TryGetValue(employee.Id, out var profile);
                var effective = StaffingCapabilityRules.ResolveEffective(
                    explicitCapabilities.GetValueOrDefault(employee.Id, []),
                    employee.ProfessionalRole,
                    employee.CountsAsPharmacist);
                var hasBlockingAbsence = snapshot.LeaveRequests.Any(leave =>
                    leave.EmployeeId == employee.Id &&
                    IsActiveAbsence(leave.Status) &&
                    leave.DateFrom <= snapshot.PeriodEnd &&
                    (leave.DateTo ?? snapshot.PeriodEnd) >= snapshot.PeriodStart);
                var hasUnavailable = snapshot.WorkPreferences.Any(preference =>
                    preference.EmployeeId == employee.Id &&
                    preference.IsActive &&
                    preference.Type == WorkPreferenceType.Unavailable &&
                    preference.DateFrom <= snapshot.PeriodEnd &&
                    preference.DateTo >= snapshot.PeriodStart);
                return new ScheduleGenerationEmployeeCheck(
                    employee.Id,
                    employee.DisplayName,
                    employee.IsActive,
                    employee.IsSchedulable,
                    employee.IncludeInAutoFill && (profile?.IncludeInAutoFill ?? true),
                    activeLocationAssignedEmployeeIds.Contains(employee.Id),
                    profile is not null,
                    profile is not null &&
                        (profile.ContractedMonthlyMinutes > 0 ||
                         profile.ContractedWeeklyMinutes > 0),
                    requiredCapabilities.Count == 0
                        ? effective.Count > 0
                        : effective.Overlaps(requiredCapabilities),
                    hasBlockingAbsence || hasUnavailable,
                    candidatesByEmployee.GetValueOrDefault(employee.Id));
            })
            .ToArray();
        var counts = new ScheduleGenerationDiagnosticCounts(
            activeLocationIds.Count,
            snapshot.OpeningIntervals.Count(interval =>
                activeLocationIds.Contains(interval.LocationId) &&
                weekdays.Contains(interval.DayOfWeek) &&
                interval.Mode != OpeningDayMode.Closed),
            snapshot.ShiftTemplates.Count(template =>
                template.IsActive && activeLocationIds.Contains(template.LocationId)),
            snapshot.ShiftTemplates.Count(template =>
                template.IsActive &&
                activeLocationIds.Contains(template.LocationId) &&
                dates.Any(date => (template.WeekdayMask & (1 << (int)date.DayOfWeek)) != 0)),
            snapshot.CoverageRequirements.Count(requirement =>
                requirement.IsActive &&
                activeLocationIds.Contains(requirement.LocationId) &&
                weekdays.Contains(requirement.DayOfWeek)),
            activeEmployees.Length,
            schedulableEmployees.Length,
            autoFillEmployees.Length,
            locationAssignedEmployeeIds.Count,
            workProfileEmployeeIds.Count,
            capableEmployeeCount,
            candidateOptionCount);
        var issues = new List<ScheduleGenerationPreflightIssue>();
        AddIfZero(
            counts.ActiveLocationCount,
            "NO_ACTIVE_LOCATIONS",
            "Nincs aktív telephely.",
            "/app/admin/locations");
        foreach (var location in locationChecks.Where(item => item.IsActive))
        {
            var locationPath = $"/app/admin/locations?locationId={location.LocationId}";
            if (!location.HasOpeningHours)
            {
                issues.Add(new(
                    "NO_OPENING_HOURS",
                    ScheduleIssueSeverity.Blocking,
                    $"{location.LocationName} – nincs a kiválasztott időszakra nyitvatartás.",
                    locationPath));
            }
            if (!location.HasApplicableShiftTemplate)
            {
                issues.Add(new(
                    "NO_APPLICABLE_SHIFT_TEMPLATES",
                    ScheduleIssueSeverity.Blocking,
                    $"{location.LocationName} – nincs alkalmazható műszaksablon.",
                    locationPath));
            }
            if (!location.HasCoverageRequirement)
            {
                issues.Add(new(
                    "NO_COVERAGE_REQUIREMENTS",
                    ScheduleIssueSeverity.Blocking,
                    $"{location.LocationName} – nincs beállított lefedettségi követelmény.",
                    "/app/admin/coverage"));
            }
        }
        AddIfZero(
            counts.AutoFillEmployeeCount,
            "NO_ELIGIBLE_EMPLOYEES",
            "Nincs aktív, beosztható és automatikus kitöltésbe bevont dolgozó.",
            "/app/admin/employees");
        foreach (var employee in employeeChecks.Where(item =>
                     item.IsActive && item.IsSchedulable && item.IncludeInAutoFill))
        {
            var employeePath = $"/app/admin/employees/{employee.EmployeeId}";
            if (!employee.HasWorkProfile)
            {
                issues.Add(new(
                    "MISSING_WORK_PROFILE",
                    ScheduleIssueSeverity.Blocking,
                    $"{employee.EmployeeDisplayName} – hiányzó munkaidőprofil.",
                    employeePath));
            }
            else if (!employee.HasPositiveContractedTime)
            {
                issues.Add(new(
                    "NON_POSITIVE_CONTRACTED_TIME",
                    ScheduleIssueSeverity.Blocking,
                    $"{employee.EmployeeDisplayName} – a havi vagy heti szerződéses időnek pozitívnak kell lennie.",
                    employeePath));
            }
            if (!employee.HasLocationAssignment)
            {
                issues.Add(new(
                    "MISSING_LOCATION_ASSIGNMENT",
                    ScheduleIssueSeverity.Blocking,
                    $"{employee.EmployeeDisplayName} – hiányzó aktív telephely-hozzárendelés.",
                    employeePath));
            }
            if (employee.HasBlockingAbsenceOrUnavailable && employee.CandidateOptionCount == 0)
            {
                issues.Add(new(
                    "EMPLOYEE_UNAVAILABLE_FOR_PERIOD",
                    ScheduleIssueSeverity.Warning,
                    $"{employee.EmployeeDisplayName} – blokkoló távollét vagy nem elérhető időszak miatt nincs műszakjelölt.",
                    employeePath));
            }
        }

        if (counts.CoverageRequirementCount > 0 && counts.CapableEmployeeCount == 0)
        {
            issues.Add(new(
                "MISSING_REQUIRED_CAPABILITY",
                ScheduleIssueSeverity.Blocking,
                "Nincs a lefedettségi követelményekhez szükséges kompetenciával rendelkező dolgozó.",
                "/app/admin/employees"));
        }

        AddIfZero(
            counts.CandidateOptionCount,
            "NO_CANDIDATE_OPTIONS",
            "A beállításokból egyetlen beosztható műszakjelölt sem képezhető.",
            "/app/admin/employees");
        return new(counts, issues, locationChecks, employeeChecks);

        void AddIfZero(int value, string code, string message, string settingsPath)
        {
            if (value == 0)
            {
                issues.Add(new(code, ScheduleIssueSeverity.Blocking, message, settingsPath));
            }
        }
    }

    private static bool IsActiveAbsence(LeaveRequestStatus status) =>
        status is LeaveRequestStatus.Approved or
            LeaveRequestStatus.Reported or
            LeaveRequestStatus.Recorded or
            LeaveRequestStatus.Closed;
}

public static class ScheduleCandidateBuilder
{
    public static ScheduleCandidateBuildResult Build(
        ScheduleInputSnapshot snapshot,
        string inputSnapshotHash)
    {
        var issues = new List<ScheduleOptimizationIssue>();
        var activeLocations = snapshot.Locations
            .Where(location => location.IsActive)
            .ToDictionary(location => location.Id);
        var profiles = snapshot.WorkProfiles.ToDictionary(profile => profile.EmployeeId);
        var locationsByEmployee = snapshot.EmployeeLocations
            .Where(item => item.Enabled)
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.LocationId).ToHashSet());
        var capabilitiesByEmployee = snapshot.EmployeeCapabilities
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Capability).ToArray());
        var preferencesByEmployee = snapshot.WorkPreferences
            .Where(preference => preference.IsActive)
            .GroupBy(preference => preference.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var leavesByEmployee = snapshot.LeaveRequests
            .GroupBy(leave => leave.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var existingByKey = snapshot.ExistingShifts.ToDictionary(
            shift => ExistingKey(
                shift.EmployeeId,
                shift.LocationId,
                shift.Date,
                shift.StartTime,
                shift.EndTime));
        var rejected = snapshot.RejectedSuggestions
            .Select(item => ExistingKey(
                item.EmployeeId,
                item.LocationId,
                item.Date,
                item.StartTime,
                item.EndTime))
            .ToHashSet(StringComparer.Ordinal);

        var candidates = new Dictionary<string, ScheduleCandidateOption>(StringComparer.Ordinal);
        for (var date = snapshot.PeriodStart;
             date <= snapshot.PeriodEnd;
             date = date.AddDays(1))
        {
            foreach (var location in activeLocations.Values.OrderBy(item => item.Id))
            {
                var spans = BuildSpans(snapshot, location.Id, date);
                foreach (var employee in snapshot.Employees
                             .Where(item =>
                                 item.IsActive &&
                                 item.IsSchedulable &&
                                 item.IncludeInAutoFill)
                             .OrderBy(item => item.Id))
                {
                    if (!profiles.TryGetValue(employee.Id, out var profile) ||
                        !profile.IncludeInAutoFill ||
                        !locationsByEmployee.GetValueOrDefault(employee.Id, []).Contains(location.Id))
                    {
                        continue;
                    }

                    var effectiveCapabilities = StaffingCapabilityRules.ResolveEffective(
                        capabilitiesByEmployee.GetValueOrDefault(employee.Id, []),
                        employee.ProfessionalRole,
                        employee.CountsAsPharmacist);
                    var employeePreferences =
                        preferencesByEmployee.GetValueOrDefault(employee.Id, []);
                    var applicableFixed = employeePreferences
                        .Where(preference =>
                            preference.Type == WorkPreferenceType.Fixed &&
                            AppliesOnDate(preference, date))
                        .ToArray();

                    foreach (var span in spans)
                    {
                        var option = TryCreateOption(
                            snapshot,
                            employee,
                            profile,
                            effectiveCapabilities,
                            location.Id,
                            date,
                            span,
                            employeePreferences,
                            leavesByEmployee.GetValueOrDefault(employee.Id, []),
                            applicableFixed,
                            existingByKey,
                            rejected);
                        if (option is not null)
                        {
                            candidates.TryAdd(option.Key, option);
                        }
                    }

                    foreach (var fixedPreference in applicableFixed)
                    {
                        if (fixedPreference.LocationId is null ||
                            fixedPreference.StartTime is null ||
                            fixedPreference.EndTime is null)
                        {
                            issues.Add(new(
                                "FIXED_RULE_INCOMPLETE",
                                ScheduleIssueSeverity.Blocking,
                                employee.Id,
                                fixedPreference.LocationId,
                                date,
                                fixedPreference.StartTime,
                                fixedPreference.EndTime,
                                new Dictionary<string, object?>
                                {
                                    ["preferenceId"] = fixedPreference.Id
                                }));
                            continue;
                        }

                        if (fixedPreference.LocationId != location.Id)
                        {
                            continue;
                        }

                        var fixedSpan = new CandidateSpan(
                            fixedPreference.StartTime.Value,
                            fixedPreference.EndTime.Value,
                            TimeType.Work,
                            null,
                            false);
                        var fixedOption = TryCreateOption(
                            snapshot,
                            employee,
                            profile,
                            effectiveCapabilities,
                            location.Id,
                            date,
                            fixedSpan,
                            employeePreferences,
                            leavesByEmployee.GetValueOrDefault(employee.Id, []),
                            applicableFixed,
                            existingByKey,
                            rejected,
                            forceFixed: true);
                        if (fixedOption is null)
                        {
                            issues.Add(new(
                                "FIXED_RULE_CONFLICT",
                                ScheduleIssueSeverity.Blocking,
                                employee.Id,
                                location.Id,
                                date,
                                fixedPreference.StartTime,
                                fixedPreference.EndTime,
                                new Dictionary<string, object?>
                                {
                                    ["preferenceId"] = fixedPreference.Id
                                }));
                        }
                        else
                        {
                            candidates[fixedOption.Key] = fixedOption with { IsFixed = true };
                        }
                    }
                }
            }
        }

        AddLockedExistingCandidates(
            snapshot,
            capabilitiesByEmployee,
            candidates,
            issues);
        var coverageSlots = BuildCoverageSlots(snapshot, activeLocations.Keys);
        if (candidates.Count == 0)
        {
            var diagnostics = ScheduleGenerationDiagnostics.Analyze(snapshot, 0);
            issues.Add(new(
                "NO_CANDIDATE_OPTIONS",
                ScheduleIssueSeverity.Blocking,
                null,
                null,
                null,
                null,
                null,
                diagnostics.Counts.ToParameters()));
        }
        var optimizerEmployees = snapshot.Employees
            .Where(employee => profiles.ContainsKey(employee.Id))
            .OrderBy(employee => employee.Id)
            .Select(employee =>
            {
                var profile = profiles[employee.Id];
                var periodDays = snapshot.PeriodEnd.DayNumber -
                    snapshot.PeriodStart.DayNumber + 1;
                var target = profile.ContractedWeeklyMinutes is not null &&
                             periodDays <= 14
                    ? checked(profile.ContractedWeeklyMinutes.Value * periodDays / 7)
                    : checked(profile.ContractedMonthlyMinutes * periodDays / 31);
                return new ScheduleOptimizerEmployee(
                    employee.Id,
                    target,
                    profile.MaximumOvertimeMinutesPerMonth,
                    profile.MaximumSaturdaysPerMonth,
                    profile.MaximumSundaysPerMonth,
                    profile.MaximumOnCallAssignmentsPerMonth,
                    profile.MaximumStandbyAssignmentsPerMonth);
            })
            .ToArray();
        var quotas = snapshot.ShiftQuotas
            .Where(quota => quota.IsActive)
            .OrderBy(quota => quota.EmployeeId)
            .ThenBy(quota => quota.Dimension)
            .Select(quota => new ScheduleOptimizerQuota(
                quota.Id,
                quota.EmployeeId,
                quota.Dimension,
                quota.Period,
                quota.Minimum,
                quota.Target,
                quota.Maximum,
                quota.Severity))
            .ToArray();
        return new(
            new ScheduleOptimizerInput(
                snapshot.AlgorithmVersion,
                inputSnapshotHash,
                snapshot.PeriodStart,
                snapshot.PeriodEnd,
                snapshot.Options.DeterministicSeed,
                snapshot.Options.MaxSolveSeconds,
                snapshot.Options.WorkerCount,
                snapshot.Options.Weights,
                candidates.Values
                    .OrderBy(candidate => candidate.Key, StringComparer.Ordinal)
                    .ToArray(),
                coverageSlots,
                optimizerEmployees,
                quotas),
            issues);
    }

    private static IEnumerable<CandidateSpan> BuildSpans(
        ScheduleInputSnapshot snapshot,
        Guid locationId,
        DateOnly date)
    {
        var baseSpans = snapshot.ShiftTemplates
            .Where(template =>
                template.LocationId == locationId &&
                template.IsActive &&
                (template.WeekdayMask & (1 << (int)date.DayOfWeek)) != 0)
            .Select(template => new CandidateSpan(
                template.StartTime,
                template.EndTime,
                template.TimeType,
                template.RequiredCapability,
                template.Category == ShiftTemplateCategory.Long))
            .Concat(snapshot.CoverageRequirements
                .Where(requirement =>
                    requirement.LocationId == locationId &&
                    requirement.IsActive &&
                    requirement.DayOfWeek == date.DayOfWeek)
                .Select(requirement => new CandidateSpan(
                    requirement.StartTime,
                    requirement.EndTime,
                    requirement.TimeType,
                    requirement.RequiredCapability,
                    false)))
            .Where(span => IsHalfHourAligned(span.StartTime) &&
                           IsHalfHourAligned(span.EndTime))
            .Distinct()
            .OrderBy(span => span.StartTime)
            .ThenBy(span => span.EndTime)
            .ThenBy(span => span.TimeType)
            .ToArray();

        // A daily assignment is one continuous presence block. Adjacent Work
        // templates therefore also form a single candidate (for example
        // 08:00-14:00 + 14:00-18:00 => 08:00-18:00), while a gap never does.
        var continuousWork = baseSpans
            .Where(span => span.TimeType == TimeType.Work)
            .ToHashSet();
        var added = true;
        while (added)
        {
            added = false;
            var current = continuousWork.ToArray();
            foreach (var left in current)
            {
                foreach (var right in current.Where(item =>
                             item.StartTime == left.EndTime))
                {
                    var combined = new CandidateSpan(
                        left.StartTime,
                        right.EndTime,
                        TimeType.Work,
                        left.RequiredCapability == right.RequiredCapability
                            ? left.RequiredCapability
                            : null,
                        left.IsLong || right.IsLong);
                    added |= continuousWork.Add(combined);
                }
            }
        }

        return baseSpans
            .Concat(continuousWork)
            .Distinct()
            .OrderBy(span => span.StartTime)
            .ThenBy(span => span.EndTime)
            .ThenBy(span => span.TimeType);
    }

    private static ScheduleCandidateOption? TryCreateOption(
        ScheduleInputSnapshot snapshot,
        SnapshotEmployee employee,
        SnapshotEmployeeWorkProfile profile,
        IReadOnlySet<StaffingCapability> effectiveCapabilities,
        Guid locationId,
        DateOnly date,
        CandidateSpan span,
        IReadOnlyList<SnapshotWorkPreference> preferences,
        IReadOnlyList<SnapshotLeave> leaves,
        SnapshotWorkPreference[] applicableFixed,
        Dictionary<string, SnapshotExistingShift> existingByKey,
        HashSet<string> rejected,
        bool forceFixed = false)
    {
        var minutes = Minutes(span.StartTime, span.EndTime);
        if (minutes <= 0 ||
            minutes < profile.MinimumShiftMinutes ||
            minutes > profile.MaximumDailyMinutes ||
            !OpeningContains(snapshot, locationId, date.DayOfWeek, span.StartTime, span.EndTime))
        {
            return null;
        }

        if (span.RequiredCapability is not null &&
            !effectiveCapabilities.Contains(span.RequiredCapability.Value))
        {
            return null;
        }

        if (!AllowsTimeType(profile, span.TimeType) ||
            date.DayOfWeek == DayOfWeek.Saturday && !profile.AllowsSaturday ||
            date.DayOfWeek == DayOfWeek.Sunday && !profile.AllowsSunday)
        {
            return null;
        }

        var overlappingUnavailable = preferences.Any(preference =>
            preference.Type == WorkPreferenceType.Unavailable &&
            AppliesOnDate(preference, date) &&
            AppliesToLocation(preference, locationId) &&
            Overlaps(preference, span.StartTime, span.EndTime));
        if (overlappingUnavailable)
        {
            return null;
        }

        if (!forceFixed && applicableFixed.Length > 0 &&
            !applicableFixed.Any(preference =>
                preference.LocationId == locationId &&
                preference.StartTime == span.StartTime &&
                preference.EndTime == span.EndTime))
        {
            return null;
        }

        var activeLeave = leaves.FirstOrDefault(leave =>
            IsActiveAbsence(leave.Status) &&
            leave.DateFrom <= date &&
            date <= (leave.DateTo ?? snapshot.PeriodEnd) &&
            LeaveOverlaps(leave, span.StartTime, span.EndTime));
        if (activeLeave is not null)
        {
            return null;
        }

        var pendingOverlap = leaves.Any(leave =>
            leave.Status == LeaveRequestStatus.Pending &&
            leave.DateFrom <= date &&
            date <= (leave.DateTo ?? snapshot.PeriodEnd) &&
            LeaveOverlaps(leave, span.StartTime, span.EndTime));
        if (pendingOverlap &&
            snapshot.Options.PendingLeaveHandling ==
            PendingLeaveHandlingMode.TreatAsTemporaryAbsence)
        {
            return null;
        }

        var key = ExistingKey(
            employee.Id,
            locationId,
            date,
            span.StartTime,
            span.EndTime);
        if (rejected.Contains(key))
        {
            return null;
        }

        var segments = BuildSegments(span, profile, minutes);
        if (segments is null)
        {
            return null;
        }

        existingByKey.TryGetValue(key, out var existing);
        return new(
            key,
            employee.Id,
            employee.DisplayName,
            locationId,
            date,
            span.StartTime,
            span.EndTime,
            segments,
            effectiveCapabilities,
            forceFixed || applicableFixed.Any(preference =>
                preference.LocationId == locationId &&
                preference.StartTime == span.StartTime &&
                preference.EndTime == span.EndTime),
            existing?.IsLocked == true,
            existing?.Id,
            preferences.Any(preference =>
                preference.Type == WorkPreferenceType.Preferred &&
                AppliesOnDate(preference, date) &&
                AppliesToLocation(preference, locationId) &&
                Contains(preference, span.StartTime, span.EndTime)),
            preferences.Any(preference =>
                preference.Type == WorkPreferenceType.Avoid &&
                AppliesOnDate(preference, date) &&
                AppliesToLocation(preference, locationId) &&
                Overlaps(preference, span.StartTime, span.EndTime)),
            pendingOverlap,
            existing is not null,
            span.IsLong,
            minutes,
            segments
                .Where(segment => segment.TimeType == TimeType.Overtime)
                .Sum(segment => Minutes(segment.StartTime, segment.EndTime)));
    }

    private static IReadOnlyList<SnapshotShiftSegment>? BuildSegments(
        CandidateSpan span,
        SnapshotEmployeeWorkProfile profile,
        int minutes)
    {
        if (span.TimeType != TimeType.Work)
        {
            return [new(span.StartTime, span.EndTime, span.TimeType)];
        }

        if (minutes <= profile.MaximumRegularShiftMinutes)
        {
            return [new(span.StartTime, span.EndTime, TimeType.Work)];
        }

        if (!profile.AllowsOvertime)
        {
            return null;
        }

        var workEnd = span.StartTime.AddMinutes(profile.MaximumRegularShiftMinutes);
        return
        [
            new(span.StartTime, workEnd, TimeType.Work),
            new(workEnd, span.EndTime, TimeType.Overtime)
        ];
    }

    private static void AddLockedExistingCandidates(
        ScheduleInputSnapshot snapshot,
        Dictionary<Guid, StaffingCapability[]> capabilitiesByEmployee,
        Dictionary<string, ScheduleCandidateOption> candidates,
        List<ScheduleOptimizationIssue> issues)
    {
        var employees = snapshot.Employees.ToDictionary(employee => employee.Id);
        foreach (var shift in snapshot.ExistingShifts.Where(item => item.IsLocked))
        {
            if (!employees.TryGetValue(shift.EmployeeId, out var employee))
            {
                issues.Add(new(
                    "LOCKED_SHIFT_EMPLOYEE_NOT_FOUND",
                    ScheduleIssueSeverity.Blocking,
                    shift.EmployeeId,
                    shift.LocationId,
                    shift.Date,
                    shift.StartTime,
                    shift.EndTime,
                    new Dictionary<string, object?> { ["shiftId"] = shift.Id }));
                continue;
            }

            var key = ExistingKey(
                shift.EmployeeId,
                shift.LocationId,
                shift.Date,
                shift.StartTime,
                shift.EndTime);
            var effective = StaffingCapabilityRules.ResolveEffective(
                capabilitiesByEmployee.GetValueOrDefault(employee.Id, []),
                employee.ProfessionalRole,
                employee.CountsAsPharmacist);
            candidates[key] = new(
                key,
                shift.EmployeeId,
                employee.DisplayName,
                shift.LocationId,
                shift.Date,
                shift.StartTime,
                shift.EndTime,
                shift.Segments,
                effective,
                IsFixed: true,
                IsLocked: true,
                shift.Id,
                HasPreferredMatch: false,
                HasAvoidViolation: false,
                HasPendingLeaveOverlap: false,
                MatchesPreviousPublished: true,
                IsLongShift: false,
                Minutes(shift.StartTime, shift.EndTime),
                shift.Segments
                    .Where(segment => segment.TimeType == TimeType.Overtime)
                    .Sum(segment => Minutes(segment.StartTime, segment.EndTime)));
        }
    }

    private static ScheduleCoverageSlot[] BuildCoverageSlots(
        ScheduleInputSnapshot snapshot,
        IEnumerable<Guid> activeLocationIds)
    {
        var active = activeLocationIds.ToHashSet();
        var slots = new List<ScheduleCoverageSlot>();
        for (var date = snapshot.PeriodStart;
             date <= snapshot.PeriodEnd;
             date = date.AddDays(1))
        {
            var requirements = snapshot.CoverageRequirements
                .Where(requirement =>
                    requirement.IsActive &&
                    requirement.DayOfWeek == date.DayOfWeek &&
                    active.Contains(requirement.LocationId))
                .ToArray();
            foreach (var group in requirements.GroupBy(requirement => new
            {
                requirement.LocationId,
                requirement.RequiredCapability,
                requirement.TimeType
            }))
            {
                var firstMinute = group.Min(item => ToMinute(item.StartTime));
                var lastMinute = group.Max(item => ToMinute(item.EndTime));
                for (var minute = firstMinute; minute < lastMinute; minute += 30)
                {
                    var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
                    var applicable = group
                        .Where(item =>
                            ToMinute(item.StartTime) <= minute &&
                            minute < ToMinute(item.EndTime))
                        .ToArray();
                    if (applicable.Length == 0)
                    {
                        continue;
                    }

                    var required = applicable.Max(item => item.RequiredCount);
                    var severity = applicable.Any(item =>
                        item.Severity == CoverageSeverity.Blocking)
                        ? CoverageSeverity.Blocking
                        : CoverageSeverity.Warning;
                    slots.Add(new(
                        $"{group.Key.LocationId:N}|{date:yyyyMMdd}|{minute:D4}|" +
                        $"{group.Key.RequiredCapability}|{group.Key.TimeType}",
                        group.Key.LocationId,
                        date,
                        start,
                        start.AddMinutes(30),
                        group.Key.RequiredCapability,
                        group.Key.TimeType,
                        required,
                        severity));
                }
            }
        }

        return slots
            .OrderBy(slot => slot.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool OpeningContains(
        ScheduleInputSnapshot snapshot,
        Guid locationId,
        DayOfWeek day,
        TimeOnly start,
        TimeOnly end)
    {
        var definitions = snapshot.OpeningIntervals
            .Where(item => item.LocationId == locationId && item.DayOfWeek == day)
            .ToArray();
        if (definitions.Any(item => item.Mode == OpeningDayMode.Open24Hours))
        {
            return true;
        }

        return definitions
            .Where(item => item.Mode == OpeningDayMode.CustomIntervals)
            .Any(item =>
                item.StartTime <= start &&
                (item.EndTime is null || end <= item.EndTime));
    }

    private static bool AllowsTimeType(
        SnapshotEmployeeWorkProfile profile,
        TimeType timeType) =>
        timeType switch
        {
            TimeType.Work => true,
            TimeType.Overtime => profile.AllowsOvertime,
            TimeType.OnCallDuty => profile.AllowsOnCallDuty,
            TimeType.Standby => profile.AllowsStandby,
            _ => false
        };

    private static bool AppliesOnDate(SnapshotWorkPreference preference, DateOnly date) =>
        preference.DateFrom <= date &&
        date <= preference.DateTo &&
        (preference.DayOfWeek is null || preference.DayOfWeek == date.DayOfWeek);

    private static bool AppliesToLocation(
        SnapshotWorkPreference preference,
        Guid locationId) =>
        preference.LocationId is null || preference.LocationId == locationId;

    private static bool Contains(
        SnapshotWorkPreference preference,
        TimeOnly start,
        TimeOnly end) =>
        preference.IsFullDay ||
        preference.StartTime <= start && end <= preference.EndTime;

    private static bool Overlaps(
        SnapshotWorkPreference preference,
        TimeOnly start,
        TimeOnly end) =>
        preference.IsFullDay ||
        preference.StartTime < end && start < preference.EndTime;

    private static bool LeaveOverlaps(
        SnapshotLeave leave,
        TimeOnly start,
        TimeOnly end) =>
        leave.IsFullDay ||
        leave.StartTime < end && start < leave.EndTime;

    private static bool IsActiveAbsence(LeaveRequestStatus status) =>
        status is LeaveRequestStatus.Approved or
            LeaveRequestStatus.Reported or
            LeaveRequestStatus.Recorded or
            LeaveRequestStatus.Closed;

    private static bool IsHalfHourAligned(TimeOnly value) =>
        value.Minute % 30 == 0 && value.Second == 0;

    private static int Minutes(TimeOnly start, TimeOnly end) =>
        ToMinute(end) - ToMinute(start);

    private static int ToMinute(TimeOnly value) => value.Hour * 60 + value.Minute;

    private static string ExistingKey(
        Guid employeeId,
        Guid locationId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end) =>
        $"{employeeId:N}|{locationId:N}|{date:yyyyMMdd}|" +
        $"{ToMinute(start):D4}|{ToMinute(end):D4}";

    private sealed record CandidateSpan(
        TimeOnly StartTime,
        TimeOnly EndTime,
        TimeType TimeType,
        StaffingCapability? RequiredCapability,
        bool IsLong);
}
