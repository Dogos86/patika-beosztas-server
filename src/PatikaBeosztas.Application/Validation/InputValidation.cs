using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Validation;

public static class InputValidation
{
    public static IReadOnlyList<ApiValidationError> ValidateEmployee(
        string fullName,
        string displayName,
        bool isActive,
        bool isSchedulable,
        bool includeInAutoFill,
        int? monthlyMinutesLimit,
        int? maxDailyMinutes,
        DateOnly? birthDate,
        DateOnly currentDate,
        string? externalPayrollId,
        IReadOnlyList<EmployeeLocationRequest>? locations,
        IReadOnlyList<EmployeeTimeWindowRequest>? windows,
        IReadOnlyList<TimeType>? allowedTimeTypes)
    {
        var errors = new List<ApiValidationError>();

        ValidateRequiredText(fullName, 200, "fullName", errors);
        ValidateRequiredText(displayName, 100, "displayName", errors);

        errors.AddRange(EmployeeRules.ValidateConfiguration(
                isActive,
                isSchedulable,
                includeInAutoFill,
                monthlyMinutesLimit,
                maxDailyMinutes,
                birthDate,
                currentDate)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "MONTHLY_MINUTES_OUT_OF_RANGE" => "monthlyMinutesLimit",
                    "MAX_DAILY_MINUTES_OUT_OF_RANGE" => "maxDailyMinutes",
                    "BIRTH_DATE_TOO_EARLY" or "BIRTH_DATE_IN_FUTURE" => "birthDate",
                    _ => "includeInAutoFill"
                })));

        if (externalPayrollId?.Length > 100)
        {
            errors.Add(new(
                "EXTERNAL_PAYROLL_ID_TOO_LONG",
                "A külső bérszámfejtési azonosító legfeljebb 100 karakter lehet.",
                "externalPayrollId"));
        }

        var duplicateLocations = (locations ?? [])
            .GroupBy(location => location.LocationId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateLocations.Length > 0)
        {
            errors.Add(new(
                "DUPLICATE_LOCATION",
                "Egy telephely csak egyszer rendelhető a dolgozóhoz.",
                "locations"));
        }

        var duplicateTimeTypes = (allowedTimeTypes ?? [])
            .GroupBy(timeType => timeType)
            .Any(group => group.Count() > 1);
        if (duplicateTimeTypes)
        {
            errors.Add(new(
                "DUPLICATE_TIME_TYPE",
                "Egy munkaidőtípus csak egyszer engedélyezhető.",
                "allowedTimeTypes"));
        }

        var domainWindows = (windows ?? [])
            .Select(window => new EmployeeTimeWindow
            {
                DayOfWeek = window.DayOfWeek,
                StartTime = window.StartTime,
                EndTime = window.EndTime,
                Type = window.Type
            })
            .ToArray();
        errors.AddRange(EmployeeTimeWindowRules.Validate(domainWindows)
            .Select(issue => new ApiValidationError(issue.Code, issue.Message, "timeWindows")));

        return errors;
    }

    public static IReadOnlyList<ApiValidationError> ValidateLocation(
        string name,
        string? address)
    {
        var errors = new List<ApiValidationError>();
        ValidateRequiredText(name, 200, "name", errors);
        if (address?.Length > 500)
        {
            errors.Add(new(
                "ADDRESS_TOO_LONG",
                "A cím legfeljebb 500 karakter lehet.",
                "address"));
        }

        return errors;
    }

    public static IReadOnlyList<string> EmployeeWarnings(
        ProfessionalRole role,
        bool countsAsPharmacist)
    {
        var isPharmacistRole = role is ProfessionalRole.PharmacyManager or ProfessionalRole.Pharmacist;
        return isPharmacistRole == countsAsPharmacist
            ? []
            : ["A szakmai szerepkör és a gyógyszerészi lefedettségi jelző eltér. Ez engedélyezett, de ellenőrizendő."];
    }

    public static IReadOnlyList<ApiValidationError> ValidateWorkPreference(
        DateOnly dateFrom,
        DateOnly dateTo,
        bool isFullDay,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string? note) =>
        WorkPreferenceRules.Validate(
                dateFrom,
                dateTo,
                isFullDay,
                startTime,
                endTime,
                note)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "WORK_PREFERENCE_DATE_ORDER" => "dateTo",
                    "WORK_PREFERENCE_NOTE_TOO_LONG" => "note",
                    _ => "startTime"
                }))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateLeaveRequest(
        LeaveType type,
        DateOnly dateFrom,
        DateOnly? dateTo,
        bool isFullDay,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string? employeeNote) =>
        LeaveRequestRules.ValidatePeriod(
                type,
                dateFrom,
                dateTo,
                isFullDay,
                startTime,
                endTime,
                employeeNote)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "LEAVE_END_DATE_REQUIRED" or "LEAVE_DATE_ORDER" => "dateTo",
                    "LEAVE_NOTE_TOO_LONG" or "SICK_LEAVE_NOTE_NOT_ALLOWED" =>
                        "employeeNote",
                    _ => "startTime"
                }))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateLeaveTransition(
        LeaveRequestStatus from,
        LeaveRequestStatus to,
        DateOnly? dateTo,
        string? reason) =>
        LeaveRequestRules.ValidateTransition(from, to, dateTo, reason)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "SICK_LEAVE_END_DATE_REQUIRED_TO_CLOSE" => "dateTo",
                    "LEAVE_DECISION_REASON_REQUIRED" or
                        "LEAVE_DECISION_REASON_TOO_LONG" => "reason",
                    _ => "status"
                }))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateOpeningWeek(
        IReadOnlyList<OpeningDayRequest>? days)
    {
        if (days is null ||
            days.Any(day =>
                day is null ||
                day.Intervals is null ||
                day.Intervals.Any(interval => interval is null)))
        {
            return
            [
                new ApiValidationError(
                    "OPENING_DAYS_REQUIRED",
                    "A heti nyitvatartás napjai és intervallumai kötelezők.",
                    "days")
            ];
        }

        return OpeningHoursRules.ValidateWeek(days.Select(day => new OpeningDayDefinition(
                day.DayOfWeek,
                day.Mode,
                day.Intervals.Select(interval => new OpeningIntervalDefinition(
                        interval.StartTime,
                        interval.EndTime))
                    .ToArray()))
            .ToArray())
            .Select(issue => new ApiValidationError(issue.Code, issue.Message, "days"))
            .ToArray();
    }

    public static IReadOnlyList<ApiValidationError> ValidateShiftTemplate(
        string? name,
        IReadOnlyList<DayOfWeek>? weekdays,
        TimeOnly startTime,
        TimeOnly endTime) =>
        LocationShiftTemplateRules.Validate(
                name ?? string.Empty,
                weekdays ?? [],
                startTime,
                endTime)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "SHIFT_TEMPLATE_NAME_REQUIRED" or
                        "SHIFT_TEMPLATE_NAME_TOO_LONG" => "name",
                    "SHIFT_TEMPLATE_WEEKDAY_REQUIRED" or
                        "SHIFT_TEMPLATE_WEEKDAY_INVALID" or
                        "DUPLICATE_SHIFT_TEMPLATE_WEEKDAY" => "weekdays",
                    _ => "startTime"
                }))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateCoverageRequirement(
        TimeOnly startTime,
        TimeOnly endTime,
        int requiredCount) =>
        CoverageRequirementRules.Validate(startTime, endTime, requiredCount)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code == "COVERAGE_REQUIRED_COUNT_INVALID"
                    ? "requiredCount"
                    : "startTime"))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateCapabilities(
        IReadOnlyList<StaffingCapability>? capabilities)
    {
        if (capabilities is null)
        {
            return
            [
                new ApiValidationError(
                    "STAFFING_CAPABILITIES_REQUIRED",
                    "A kompetencialista kötelező.",
                    "capabilities")
            ];
        }

        var errors = new List<ApiValidationError>();
        if (capabilities.Any(capability => !Enum.IsDefined(capability)))
        {
            errors.Add(new(
                "STAFFING_CAPABILITY_INVALID",
                "A kompetencia értéke érvénytelen.",
                "capabilities"));
        }

        if (capabilities.Distinct().Count() != capabilities.Count)
        {
            errors.Add(new(
                "DUPLICATE_STAFFING_CAPABILITY",
                "Egy kompetencia csak egyszer rendelhető a dolgozóhoz.",
                "capabilities"));
        }

        return errors;
    }

    public static IReadOnlyList<ApiValidationError> ValidateWorkProfile(
        UpdateEmployeeWorkProfileRequest request,
        bool employeeIsActive,
        bool employeeIsSchedulable)
    {
        var profile = new EmployeeWorkProfile
        {
            ContractedMonthlyMinutes = request.ContractedMonthlyMinutes,
            ContractedWeeklyMinutes = request.ContractedWeeklyMinutes,
            StandardShiftMinutes = request.StandardShiftMinutes,
            MinimumShiftMinutes = request.MinimumShiftMinutes,
            MaximumRegularShiftMinutes = request.MaximumRegularShiftMinutes,
            MaximumDailyMinutes = request.MaximumDailyMinutes,
            AllowsLongShift = request.AllowsLongShift,
            MaximumLongShiftMinutes = request.MaximumLongShiftMinutes,
            AllowsFullOpeningHoursShift = request.AllowsFullOpeningHoursShift,
            AllowsOvertime = request.AllowsOvertime,
            MaximumOvertimeMinutesPerMonth = request.MaximumOvertimeMinutesPerMonth,
            AllowsOnCallDuty = request.AllowsOnCallDuty,
            MaximumOnCallAssignmentsPerMonth = request.MaximumOnCallAssignmentsPerMonth,
            AllowsStandby = request.AllowsStandby,
            MaximumStandbyAssignmentsPerMonth = request.MaximumStandbyAssignmentsPerMonth,
            AllowsSaturday = request.AllowsSaturday,
            MaximumSaturdaysPerMonth = request.MaximumSaturdaysPerMonth,
            AllowsSunday = request.AllowsSunday,
            MaximumSundaysPerMonth = request.MaximumSundaysPerMonth,
            IncludeInAutoFill = request.IncludeInAutoFill
        };
        return EmployeeWorkProfileRules.Validate(
                profile,
                employeeIsActive,
                employeeIsSchedulable)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                WorkProfileField(issue.Code)))
            .ToArray();
    }

    public static IReadOnlyList<ApiValidationError> ValidateShiftQuota(
        int minimum,
        int target,
        int maximum) =>
        EmployeeShiftQuotaRuleRules.Validate(minimum, target, maximum)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code == "SHIFT_QUOTA_NEGATIVE" ? "minimum" : "target"))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidatePayrollProfile(
        string employeeNumber,
        string taxIdentificationNumber,
        string? payrollExternalId) =>
        PayrollOnboardingRules.ValidateProfile(
                employeeNumber,
                taxIdentificationNumber,
                payrollExternalId)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "EMPLOYEE_NUMBER_INVALID" => "employeeNumber",
                    "TAX_IDENTIFICATION_NUMBER_INVALID" => "taxIdentificationNumber",
                    _ => "payrollExternalId"
                }))
            .ToArray();

    public static IReadOnlyList<ApiValidationError> ValidateTaxAllowanceSurvey(
        TaxAllowanceSurvey survey) =>
        TaxAllowanceSurveyRules.Validate(survey)
            .Select(issue => new ApiValidationError(
                issue.Code,
                issue.Message,
                issue.Code switch
                {
                    "TAX_SURVEY_YEAR_NOT_SUPPORTED" => "taxYear",
                    "TAX_SURVEY_EFFECTIVE_DATE_INVALID" => "effectiveFrom",
                    "FAMILY_ELIGIBLE_CHILD_COUNT_INVALID" =>
                        "answers.familyAllowanceEligibleChildrenCount",
                    "DEPENDENT_STUDENT_COUNT_INVALID" =>
                        "answers.dependentStudentCount",
                    "FETUS_ELIGIBILITY_MONTH_INVALID" or
                        "FETUS_ELIGIBILITY_MONTH_NOT_APPLICABLE" =>
                        "answers.fetusEligibilityMonth",
                    "PERSONAL_ALLOWANCE_START_MONTH_INVALID" or
                        "PERSONAL_ALLOWANCE_START_MONTH_NOT_APPLICABLE" =>
                        "answers.personalAllowanceStartMonth",
                    _ => "hrPayrollNote"
                }))
            .ToArray();

    private static string WorkProfileField(string code) =>
        code switch
        {
            "CONTRACTED_MONTHLY_MINUTES_INVALID" => "contractedMonthlyMinutes",
            "CONTRACTED_WEEKLY_MINUTES_INVALID" => "contractedWeeklyMinutes",
            "MINIMUM_SHIFT_MINUTES_INVALID" => "minimumShiftMinutes",
            "STANDARD_SHIFT_MINUTES_INVALID" => "standardShiftMinutes",
            "MAXIMUM_REGULAR_SHIFT_MINUTES_INVALID" or
                "WORK_PROFILE_SHIFT_LIMIT_ORDER" => "maximumRegularShiftMinutes",
            "MAXIMUM_DAILY_MINUTES_INVALID" or
                "REGULAR_SHIFT_EXCEEDS_DAILY_MAXIMUM" or
                "LONG_SHIFT_EXCEEDS_DAILY_MAXIMUM" => "maximumDailyMinutes",
            "LONG_SHIFT_LIMIT_REQUIRED" or
                "LONG_SHIFT_LIMIT_MUST_BE_EMPTY" or
                "LONG_SHIFT_MAXIMUM_TOO_SMALL" => "maximumLongShiftMinutes",
            "OVERTIME_LIMIT_REQUIRED" or
                "OVERTIME_LIMIT_MUST_BE_EMPTY" => "maximumOvertimeMinutesPerMonth",
            "ON_CALL_LIMIT_REQUIRED" or
                "ON_CALL_LIMIT_MUST_BE_EMPTY" => "maximumOnCallAssignmentsPerMonth",
            "STANDBY_LIMIT_REQUIRED" or
                "STANDBY_LIMIT_MUST_BE_EMPTY" => "maximumStandbyAssignmentsPerMonth",
            "SATURDAY_LIMIT_REQUIRED" or
                "SATURDAY_LIMIT_MUST_BE_EMPTY" => "maximumSaturdaysPerMonth",
            "SUNDAY_LIMIT_REQUIRED" or
                "SUNDAY_LIMIT_MUST_BE_EMPTY" => "maximumSundaysPerMonth",
            _ => "includeInAutoFill"
        };

    private static void ValidateRequiredText(
        string value,
        int maximumLength,
        string field,
        List<ApiValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new("REQUIRED", "A mező kitöltése kötelező.", field));
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors.Add(new(
                "TOO_LONG",
                $"A mező legfeljebb {maximumLength} karakter lehet.",
                field));
        }
    }
}
