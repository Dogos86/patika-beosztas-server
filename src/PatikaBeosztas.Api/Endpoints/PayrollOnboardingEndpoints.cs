using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class PayrollOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapPayrollOnboardingEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me/payroll-onboarding", GetOwnSummaryAsync)
            .WithTags("Payroll onboarding")
            .RequireAuthorization()
            .WithSummary("Saját HR/bérszámfejtési belépési összefoglaló")
            .Produces<PayrollOnboardingSummaryResponse>()
            .ProducesStandardErrors();

        var adminSummary = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/payroll-onboarding")
            .WithTags("Payroll onboarding")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding));
        adminSummary.MapGet("", GetAdminSummaryAsync)
            .WithSummary("Dolgozó HR/bérszámfejtési belépési összefoglalója")
            .Produces<PayrollOnboardingSummaryResponse>()
            .ProducesStandardErrors();
        adminSummary.MapPost("/complete", CompleteOnboardingAsync)
            .RequireAntiforgery()
            .WithSummary("HR/bérszámfejtési belépés lezárása")
            .Produces<PayrollOnboardingSummaryResponse>()
            .ProducesStandardErrors(includeConflict: true);

        var profile = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/payroll-profile")
            .WithTags("Payroll onboarding")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding));
        profile.MapGet("", GetProfileAsync)
            .WithSummary("Dolgozó bérszámfejtési profiljának lekérése")
            .Produces<EmployeePayrollProfileResponse>()
            .ProducesStandardErrors();
        profile.MapPut("", PutProfileAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó bérszámfejtési profiljának létrehozása vagy módosítása")
            .Produces<EmployeePayrollProfileResponse>()
            .Produces<EmployeePayrollProfileResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors(includeConflict: true);

        endpoints.MapGet(
                "/api/admin/employees/{employeeId:guid}/payroll-onboarding/export",
                ExportAsync)
            .WithTags("Payroll onboarding exports")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ExportPayrollData))
            .WithSummary("Vendor-neutral belépési export v1 JSON vagy CSV formátumban")
            .Produces<PayrollOnboardingExportV1>()
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesStandardErrors();

        return endpoints;
    }

    private static async Task<IResult> GetOwnSummaryAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
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

        if (actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        return await BuildSummaryAsync(
            actor.OrganizationId,
            actor.EmployeeId.Value,
            includeSensitive: false,
            actor.Id,
            httpContext.TraceIdentifier,
            dbContext,
            auditWriter,
            protector,
            cancellationToken);
    }

    private static async Task<IResult> GetAdminSummaryAsync(
        Guid employeeId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
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

        return await BuildSummaryAsync(
            actor.OrganizationId,
            employeeId,
            includeSensitive: false,
            actor.Id,
            httpContext.TraceIdentifier,
            dbContext,
            auditWriter,
            protector,
            cancellationToken);
    }

    private static async Task<IResult> GetProfileAsync(
        Guid employeeId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
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

        if (!await EmployeeExistsAsync(
            employeeId,
            actor.OrganizationId,
            dbContext,
            cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var profile = await dbContext.EmployeePayrollProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (profile is null)
        {
            return EndpointHelpers.NotFound();
        }

        var includeSensitive = await HasPermissionAsync(
            actor.Id,
            actor.OrganizationId,
            ApplicationPermission.ViewPayrollSensitiveData,
            dbContext,
            cancellationToken);
        var response = PayrollOnboardingMapper.MapProfile(
            profile,
            protector,
            includeSensitive);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "EmployeePayrollProfile.Viewed",
            "EmployeePayrollProfile",
            profile.Id.ToString(),
            httpContext.TraceIdentifier,
            includeSensitive
                ? "Bérszámfejtési profil teljes adóazonosítóval megtekintve; az érték nem került az auditba."
                : "Bérszámfejtési profil maszkolt adóazonosítóval megtekintve.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PutProfileAsync(
        Guid employeeId,
        UpdateEmployeePayrollProfileRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
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

        var employeeExists = await EmployeeExistsAsync(
            employeeId,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (!employeeExists)
        {
            return EndpointHelpers.NotFound();
        }

        var profile = await dbContext.EmployeePayrollProfiles
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        var isNew = profile is null;
        if (isNew && request.ExpectedVersion is not null)
        {
            return EndpointHelpers.Conflict(
                "A bérszámfejtési profil még nem létezik; létrehozáshoz ne adjon meg verziót.");
        }

        if (!isNew &&
            (request.ExpectedVersion is null ||
             profile!.Version != request.ExpectedVersion))
        {
            return EndpointHelpers.Conflict(
                "A bérszámfejtési profil a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (request.Status == EmployeePayrollProfileStatus.Complete)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "PAYROLL_PROFILE_COMPLETE_REQUIRES_WORKFLOW",
                    "A belépés csak a külön lezárási művelettel állítható készre.",
                    "status")]);
        }

        var effectiveTaxIdentifier = request.TaxIdentificationNumber;
        if (string.IsNullOrWhiteSpace(effectiveTaxIdentifier))
        {
            if (profile is null)
            {
                return EndpointHelpers.ValidationProblem(
                    [new ApiValidationError(
                        "TAX_IDENTIFICATION_NUMBER_REQUIRED",
                        "Új profilhoz az adóazonosító jel megadása kötelező.",
                        "taxIdentificationNumber")]);
            }

            effectiveTaxIdentifier = protector.Unprotect(
                profile.TaxIdentificationNumberCiphertext);
        }

        var errors = InputValidation.ValidatePayrollProfile(
            request.EmployeeNumber,
            effectiveTaxIdentifier,
            request.PayrollExternalId);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var normalizedEmployeeNumber = request.EmployeeNumber.Trim();
        var taxIdentifierHash = protector.ComputeLookupHash(effectiveTaxIdentifier);
        var duplicateEmployeeNumber = await dbContext.EmployeePayrollProfiles.AnyAsync(
            item =>
                item.OrganizationId == actor.OrganizationId &&
                item.EmployeeNumber == normalizedEmployeeNumber &&
                item.EmployeeId != employeeId,
            cancellationToken);
        var duplicateTaxIdentifier = await dbContext.EmployeePayrollProfiles.AnyAsync(
            item =>
                item.OrganizationId == actor.OrganizationId &&
                item.TaxIdentificationNumberHash == taxIdentifierHash &&
                item.EmployeeId != employeeId,
            cancellationToken);
        if (duplicateEmployeeNumber || duplicateTaxIdentifier)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    duplicateEmployeeNumber
                        ? "EMPLOYEE_NUMBER_ALREADY_EXISTS"
                        : "TAX_IDENTIFICATION_NUMBER_ALREADY_EXISTS",
                    duplicateEmployeeNumber
                        ? "A dolgozói törzsszám már használatban van a szervezetben."
                        : "Az adóazonosító jel már másik dolgozó profiljához tartozik.",
                    duplicateEmployeeNumber ? "employeeNumber" : "taxIdentificationNumber")]);
        }

        var now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            profile = new EmployeePayrollProfile
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                EmployeeId = employeeId,
                EmployeeNumber = normalizedEmployeeNumber,
                TaxIdentificationNumberCiphertext = protector.Protect(
                    effectiveTaxIdentifier),
                TaxIdentificationNumberHash = taxIdentifierHash,
                EmploymentStartDate = request.EmploymentStartDate,
                Status = request.Status,
                CreatedAtUtc = now,
                CreatedByUserId = actor.Id,
                UpdatedAtUtc = now,
                UpdatedByUserId = actor.Id
            };
            dbContext.EmployeePayrollProfiles.Add(profile);
        }
        else
        {
            profile.EmployeeNumber = normalizedEmployeeNumber;
            if (!string.IsNullOrWhiteSpace(request.TaxIdentificationNumber))
            {
                profile.TaxIdentificationNumberCiphertext = protector.Protect(
                    effectiveTaxIdentifier);
                profile.TaxIdentificationNumberHash = taxIdentifierHash;
            }

            profile.EmploymentStartDate = request.EmploymentStartDate;
            profile.Status = request.Status;
            profile.UpdatedAtUtc = now;
            profile.UpdatedByUserId = actor.Id;
        }

        profile.PayrollExternalId = PayrollOnboardingMapper.NormalizeOptional(
            request.PayrollExternalId);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            isNew ? "EmployeePayrollProfile.Created" : "EmployeePayrollProfile.Updated",
            "EmployeePayrollProfile",
            profile.Id.ToString(),
            httpContext.TraceIdentifier,
            "Bérszámfejtési profil mentve; az adóazonosító és annak lenyomata nem került az auditba.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A bérszámfejtési profil mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var includeSensitive = await HasPermissionAsync(
            actor.Id,
            actor.OrganizationId,
            ApplicationPermission.ViewPayrollSensitiveData,
            dbContext,
            cancellationToken);
        var response = PayrollOnboardingMapper.MapProfile(
            profile,
            protector,
            includeSensitive);
        return isNew
            ? Results.Created(
                $"/api/admin/employees/{employeeId}/payroll-profile",
                response)
            : Results.Ok(response);
    }

    private static async Task<IResult> CompleteOnboardingAsync(
        Guid employeeId,
        CompletePayrollOnboardingRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
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

        var profile = await dbContext.EmployeePayrollProfiles
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (profile is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (profile.Version != request.ExpectedProfileVersion)
        {
            return EndpointHelpers.Conflict(
                "A bérszámfejtési profil a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var survey = await dbContext.TaxAllowanceSurveys
            .Include(item => item.DeclarationRequirements)
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.OrganizationId == actor.OrganizationId)
            .OrderByDescending(item => item.TaxYear)
            .FirstOrDefaultAsync(cancellationToken);
        if (survey is null ||
            survey.Status != TaxAllowanceSurveyStatus.Completed ||
            survey.DeclarationRequirements.Any(requirement =>
                requirement.RequiredDecision &&
                requirement.Status != TaxDeclarationRequirementStatus.Applied))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "PAYROLL_ONBOARDING_NOT_READY",
                    "A belépés lezárásához befejezett felmérés és minden szükséges nyilatkozat alkalmazott állapota kell.",
                    "status")]);
        }

        profile.Status = EmployeePayrollProfileStatus.Complete;
        profile.UpdatedAtUtc = timeProvider.GetUtcNow();
        profile.UpdatedByUserId = actor.Id;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "PayrollOnboarding.Completed",
            "EmployeePayrollProfile",
            profile.Id.ToString(),
            httpContext.TraceIdentifier,
            "HR/bérszámfejtési belépés lezárva.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A bérszámfejtési profil mentés közben megváltozott. Töltse újra az adatokat.");
        }

        return await BuildSummaryAsync(
            actor.OrganizationId,
            employeeId,
            includeSensitive: false,
            actor.Id,
            httpContext.TraceIdentifier,
            dbContext,
            auditWriter,
            protector,
            cancellationToken);
    }

    private static async Task<IResult> ExportAsync(
        Guid employeeId,
        string? format,
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

        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        var profile = await dbContext.EmployeePayrollProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (profile is null)
        {
            return EndpointHelpers.NotFound();
        }

        var survey = await dbContext.TaxAllowanceSurveys
            .AsNoTracking()
            .Include(item => item.DeclarationRequirements)
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.OrganizationId == actor.OrganizationId)
            .OrderByDescending(item => item.TaxYear)
            .FirstOrDefaultAsync(cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var export = BuildExport(employee, profile, survey, generatedAt);

        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "json"
            : format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("json" or "csv"))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "PAYROLL_EXPORT_FORMAT_INVALID",
                    "Az export formátuma json vagy csv lehet.",
                    "format")]);
        }

        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "PayrollOnboarding.Exported",
            "Employee",
            employee.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Belépési export v1 elkészült {normalizedFormat.ToUpperInvariant()} formátumban; érzékeny adóazonosító nélkül.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return normalizedFormat == "json"
            ? Results.Ok(export)
            : Results.File(
                Encoding.UTF8.GetBytes(BuildCsv(export)),
                "text/csv; charset=utf-8",
                $"payroll-onboarding-{employee.Id}.csv");
    }

    private static async Task<IResult> BuildSummaryAsync(
        Guid organizationId,
        Guid employeeId,
        bool includeSensitive,
        Guid actorUserId,
        string correlationId,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        ITaxIdentifierProtector protector,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == employeeId &&
                    item.OrganizationId == organizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        var profile = await dbContext.EmployeePayrollProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == organizationId,
                cancellationToken);
        var survey = await dbContext.TaxAllowanceSurveys
            .AsNoTracking()
            .Include(item => item.DeclarationRequirements)
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.OrganizationId == organizationId)
            .OrderByDescending(item => item.TaxYear)
            .FirstOrDefaultAsync(cancellationToken);
        var requirements = survey?.DeclarationRequirements ?? [];
        var requiredCount = requirements.Count(item => item.RequiredDecision);
        var outstandingCount = requirements.Count(item =>
            item.RequiredDecision &&
            item.Status != TaxDeclarationRequirementStatus.Applied);
        var response = new PayrollOnboardingSummaryResponse(
            employee.Id,
            employee.DisplayName,
            profile is null
                ? null
                : PayrollOnboardingMapper.MapProfile(
                    profile,
                    protector,
                    includeSensitive),
            survey is null ? null : PayrollOnboardingMapper.MapSurvey(survey),
            requiredCount,
            outstandingCount,
            profile?.Status == EmployeePayrollProfileStatus.Complete);
        auditWriter.Add(
            organizationId,
            actorUserId,
            "PayrollOnboarding.Viewed",
            "Employee",
            employee.Id.ToString(),
            correlationId,
            "HR/bérszámfejtési belépési összefoglaló megtekintve; adóazonosító csak maszkolva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static PayrollOnboardingExportV1 BuildExport(
        Employee employee,
        EmployeePayrollProfile profile,
        TaxAllowanceSurvey? survey,
        DateTimeOffset generatedAt) =>
        new(
            "payroll-onboarding-export-v1",
            generatedAt,
            new PayrollExportEmployeeV1(
                employee.Id,
                employee.DisplayName,
                profile.EmployeeNumber,
                profile.EmploymentStartDate,
                profile.PayrollExternalId),
            survey is null
                ? null
                : new PayrollExportSurveyV1(
                    survey.TaxYear,
                    survey.FormVersion,
                    survey.RuleSetVersion,
                    survey.EffectiveFrom,
                    survey.EffectiveTo,
                    survey.SourceMetadata,
                    survey.Status,
                    survey.MonthlyAllowancePreference,
                    survey.DeclaredAtUtc,
                    survey.ReviewedAtUtc),
            survey?.DeclarationRequirements
                .OrderBy(item => item.Type)
                .Select(item => new PayrollExportDeclarationV1(
                    item.Type,
                    item.RequiredDecision,
                    item.Status,
                    item.EffectiveFrom,
                    item.EffectiveTo))
                .ToArray() ?? [],
            profile.Status,
            profile.Status == EmployeePayrollProfileStatus.Complete);

    private static string BuildCsv(PayrollOnboardingExportV1 export)
    {
        const string header =
            "schemaVersion,generatedAtUtc,employeeId,displayName,employeeNumber," +
            "employmentStartDate,payrollExternalId,taxYear,surveyStatus,profileStatus," +
            "onboardingComplete,declarationType,requiredDecision,declarationStatus," +
            "effectiveFrom,effectiveTo";
        var rows = new List<string> { header };
        IEnumerable<PayrollExportDeclarationV1?> declarations =
            export.DeclarationRequirements.Select(
                static requirement => (PayrollExportDeclarationV1?)requirement);
        if (export.DeclarationRequirements.Count == 0)
        {
            declarations = [null];
        }
        foreach (var requirement in declarations)
        {
            rows.Add(string.Join(
                ',',
                Csv(export.SchemaVersion),
                Csv(export.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                Csv(export.Employee.EmployeeId.ToString()),
                Csv(export.Employee.DisplayName),
                Csv(export.Employee.EmployeeNumber),
                Csv(export.Employee.EmploymentStartDate.ToString("O", CultureInfo.InvariantCulture)),
                Csv(export.Employee.PayrollExternalId),
                Csv(export.Survey?.TaxYear.ToString(CultureInfo.InvariantCulture)),
                Csv(export.Survey?.Status.ToString()),
                Csv(export.ProfileStatus.ToString()),
                Csv(export.OnboardingComplete.ToString(CultureInfo.InvariantCulture)),
                Csv(requirement?.Type.ToString()),
                Csv(requirement?.RequiredDecision.ToString(CultureInfo.InvariantCulture)),
                Csv(requirement?.Status.ToString()),
                Csv(requirement?.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture)),
                Csv(requirement?.EffectiveTo?.ToString("O", CultureInfo.InvariantCulture))));
        }

        return string.Join("\r\n", rows) + "\r\n";
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static Task<bool> EmployeeExistsAsync(
        Guid employeeId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Employees.AnyAsync(
            employee =>
                employee.Id == employeeId &&
                employee.OrganizationId == organizationId,
            cancellationToken);

    private static Task<bool> HasPermissionAsync(
        Guid userId,
        Guid organizationId,
        ApplicationPermission permission,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.UserPermissions.AnyAsync(
            item =>
                item.UserId == userId &&
                item.OrganizationId == organizationId &&
                item.Permission == permission,
            cancellationToken);

    private static IResult EmployeeLinkRequired() =>
        EndpointHelpers.ValidationProblem(
            [new ApiValidationError(
                "EMPLOYEE_LINK_REQUIRED",
                "A saját művelethez kapcsolt dolgozói profil szükséges.",
                "employeeId")]);
}
