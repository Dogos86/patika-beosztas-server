using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Validation;

public static class InputValidation
{
    public static IReadOnlyList<ApiValidationError> ValidateEmployee(
        string fullName,
        string displayName,
        int? monthlyMinutesLimit,
        int? maxDailyMinutes,
        string? externalPayrollId,
        IReadOnlyList<EmployeeLocationRequest>? locations,
        IReadOnlyList<EmployeeTimeWindowRequest>? windows,
        IReadOnlyList<TimeType>? allowedTimeTypes)
    {
        var errors = new List<ApiValidationError>();

        ValidateRequiredText(fullName, 200, "fullName", errors);
        ValidateRequiredText(displayName, 100, "displayName", errors);

        if (monthlyMinutesLimit is < 0)
        {
            errors.Add(new(
                "MONTHLY_MINUTES_NEGATIVE",
                "A havi perclimit nem lehet negatív.",
                "monthlyMinutesLimit"));
        }

        if (maxDailyMinutes is < 0)
        {
            errors.Add(new(
                "MAX_DAILY_MINUTES_NEGATIVE",
                "A napi perclimit nem lehet negatív.",
                "maxDailyMinutes"));
        }

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
