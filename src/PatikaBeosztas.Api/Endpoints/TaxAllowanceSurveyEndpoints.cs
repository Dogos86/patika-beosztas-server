using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class TaxAllowanceSurveyEndpoints
{
    public static IEndpointRouteBuilder MapTaxAllowanceSurveyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var self = endpoints.MapGroup("/api/me/tax-allowance-surveys")
            .WithTags("Tax allowance surveys")
            .RequireAuthorization();
        self.MapGet("/{taxYear:int}", GetOwnAsync)
            .WithSummary("Saját adókedvezmény-felmérő lekérése")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors();
        self.MapPost("", CreateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját belső adókedvezmény-felmérő létrehozása")
            .Produces<TaxAllowanceSurveyResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        self.MapPut("/{id:guid}", UpdateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját piszkozat adókedvezmény-felmérő módosítása")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);
        self.MapPost("/{id:guid}/submit", SubmitOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját adókedvezmény-felmérő beküldése")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);

        endpoints.MapGet(
                "/api/admin/employees/{employeeId:guid}/tax-allowance-surveys/{taxYear:int}",
                GetAdminAsync)
            .WithTags("Tax allowance surveys")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ReviewTaxAllowanceSurvey))
            .WithSummary("Dolgozó adókedvezmény-felmérőjének lekérése")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors();
        endpoints.MapPut(
                "/api/admin/employees/{employeeId:guid}/tax-allowance-surveys/{taxYear:int}",
                PutAdminAsync)
            .WithTags("Tax allowance surveys")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Dolgozó adókedvezmény-felmérőjének létrehozása vagy módosítása")
            .Produces<TaxAllowanceSurveyResponse>()
            .Produces<TaxAllowanceSurveyResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors(includeConflict: true);

        var workflow = endpoints.MapGroup("/api/admin/tax-allowance-surveys/{id:guid}")
            .WithTags("Tax allowance surveys");
        workflow.MapPost("/submit", SubmitAdminAsync)
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Admin által rögzített felmérő beküldése")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);
        workflow.MapPost("/reopen", ReopenAsync)
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Beküldött vagy ellenőrzött felmérő visszanyitása")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);
        workflow.MapPost("/review", ReviewAsync)
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ReviewTaxAllowanceSurvey))
            .RequireAntiforgery()
            .WithSummary("Felmérő HR/bérszámfejtői ellenőrzése")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);
        workflow.MapPost("/complete", CompleteSurveyAsync)
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Ellenőrzött felmérő lezárása")
            .Produces<TaxAllowanceSurveyResponse>()
            .ProducesStandardErrors(includeConflict: true);

        endpoints.MapGet(
                "/api/admin/employees/{employeeId:guid}/tax-declaration-requirements",
                ListRequirementsAsync)
            .WithTags("Tax declaration requirements")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ReviewTaxAllowanceSurvey))
            .WithSummary("Dolgozó javasolt nyilatkozat-checklistjének lekérése")
            .Produces<IReadOnlyList<TaxDeclarationRequirementResponse>>()
            .ProducesStandardErrors();
        endpoints.MapPut(
                "/api/admin/tax-declaration-requirements/{id:guid}/status",
                UpdateRequirementStatusAsync)
            .WithTags("Tax declaration requirements")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Nyilatkozat workflow-státuszának módosítása")
            .Produces<TaxDeclarationRequirementResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPut(
                "/api/admin/tax-declaration-requirements/{id:guid}/override",
                OverrideRequirementAsync)
            .WithTags("Tax declaration requirements")
            .RequireAuthorization(PermissionPolicies.For(
                ApplicationPermission.ManagePayrollOnboarding))
            .RequireAntiforgery()
            .WithSummary("Nyilatkozat-javaslat kézi felülírása indoklással")
            .Produces<TaxDeclarationRequirementResponse>()
            .ProducesStandardErrors(includeConflict: true);

        return endpoints;
    }

    private static async Task<IResult> GetOwnAsync(
        int taxYear,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
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

        return await GetAsync(
            actor.OrganizationId,
            actor.EmployeeId.Value,
            taxYear,
            actor.Id,
            httpContext.TraceIdentifier,
            dbContext,
            auditWriter,
            cancellationToken);
    }

    private static async Task<IResult> GetAdminAsync(
        Guid employeeId,
        int taxYear,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        return actor is null
            ? EndpointHelpers.Unauthorized()
            : await GetAsync(
                actor.OrganizationId,
                employeeId,
                taxYear,
                actor.Id,
                httpContext.TraceIdentifier,
                dbContext,
                auditWriter,
                cancellationToken);
    }

    private static async Task<IResult> GetAsync(
        Guid organizationId,
        Guid employeeId,
        int taxYear,
        Guid actorUserId,
        string correlationId,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        if (!await EmployeeExistsAsync(
            employeeId,
            organizationId,
            dbContext,
            cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var survey = await SurveyQuery(dbContext)
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == organizationId &&
                    item.TaxYear == taxYear &&
                    item.FormVersion == TaxAllowanceDecisionEngine.FormVersion,
                cancellationToken);
        if (survey is null)
        {
            return EndpointHelpers.NotFound();
        }

        var response = PayrollOnboardingMapper.MapSurvey(survey);
        auditWriter.Add(
            organizationId,
            actorUserId,
            "TaxAllowanceSurvey.Viewed",
            "TaxAllowanceSurvey",
            survey.Id.ToString(),
            correlationId,
            "Belső adókedvezmény-felmérő megtekintve; válaszérték nem került az audit payloadba.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateOwnAsync(
        CreateTaxAllowanceSurveyRequest request,
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

        if (actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        return await CreateAsync(
            actor,
            actor.EmployeeId.Value,
            request.TaxYear,
            request.EffectiveFrom,
            request.Answers,
            hrPayrollNote: null,
            httpContext,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> UpdateOwnAsync(
        Guid id,
        UpdateOwnTaxAllowanceSurveyRequest request,
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

        if (actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        var survey = await SurveyQuery(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    item.EmployeeId == actor.EmployeeId,
                cancellationToken);
        return survey is null
            ? EndpointHelpers.NotFound()
            : await UpdateAsync(
                survey,
                request.EffectiveFrom,
                request.Answers,
                survey.HrPayrollNote,
                request.ExpectedVersion,
                actor,
                httpContext,
                dbContext,
                auditWriter,
                timeProvider,
                cancellationToken);
    }

    private static async Task<IResult> PutAdminAsync(
        Guid employeeId,
        int taxYear,
        UpdateTaxAllowanceSurveyRequest request,
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

        if (!await EmployeeExistsAsync(
            employeeId,
            actor.OrganizationId,
            dbContext,
            cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var survey = await SurveyQuery(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId &&
                    item.TaxYear == taxYear &&
                    item.FormVersion == TaxAllowanceDecisionEngine.FormVersion,
                cancellationToken);
        if (survey is null)
        {
            if (request.ExpectedVersion is not null)
            {
                return EndpointHelpers.Conflict(
                    "A felmérő még nem létezik; létrehozáshoz ne adjon meg verziót.");
            }

            return await CreateAsync(
                actor,
                employeeId,
                taxYear,
                request.EffectiveFrom,
                request.Answers,
                request.HrPayrollNote,
                httpContext,
                dbContext,
                auditWriter,
                timeProvider,
                cancellationToken);
        }

        if (request.ExpectedVersion is null)
        {
            return EndpointHelpers.Conflict(
                "A meglévő felmérő módosításához az aktuális verzió kötelező.");
        }

        return await UpdateAsync(
            survey,
            request.EffectiveFrom,
            request.Answers,
            request.HrPayrollNote,
            request.ExpectedVersion.Value,
            actor,
            httpContext,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> CreateAsync(
        ApplicationUser actor,
        Guid employeeId,
        int taxYear,
        DateOnly effectiveFrom,
        TaxAllowanceSurveyAnswers answers,
        string? hrPayrollNote,
        HttpContext httpContext,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.TaxAllowanceSurveys.AnyAsync(
            item =>
                item.OrganizationId == actor.OrganizationId &&
                item.EmployeeId == employeeId &&
                item.TaxYear == taxYear &&
                item.FormVersion == TaxAllowanceDecisionEngine.FormVersion,
            cancellationToken);
        if (exists)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "TAX_SURVEY_ALREADY_EXISTS",
                    "Ehhez a dolgozóhoz, adóévhez és űrlapverzióhoz már van felmérő.",
                    "taxYear")]);
        }

        var now = timeProvider.GetUtcNow();
        var survey = PayrollOnboardingMapper.CreateSurvey(
            actor.OrganizationId,
            employeeId,
            actor.Id,
            taxYear,
            effectiveFrom,
            answers,
            now);
        survey.HrPayrollNote = PayrollOnboardingMapper.NormalizeOptional(
            hrPayrollNote);
        var errors = InputValidation.ValidateTaxAllowanceSurvey(survey);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        dbContext.TaxAllowanceSurveys.Add(survey);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxAllowanceSurvey.Created",
            "TaxAllowanceSurvey",
            survey.Id.ToString(),
            httpContext.TraceIdentifier,
            "Belső adókedvezmény-felmérő piszkozatként létrehozva; nem hivatalos NAV-nyilatkozat.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/me/tax-allowance-surveys/{survey.TaxYear}",
            PayrollOnboardingMapper.MapSurvey(survey));
    }

    private static async Task<IResult> UpdateAsync(
        TaxAllowanceSurvey survey,
        DateOnly effectiveFrom,
        TaxAllowanceSurveyAnswers answers,
        string? hrPayrollNote,
        uint expectedVersion,
        ApplicationUser actor,
        HttpContext httpContext,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (survey.Status != TaxAllowanceSurveyStatus.Draft)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "TAX_SURVEY_NOT_EDITABLE",
                    "A beküldött felmérő csak megfelelő visszanyitás után módosítható.",
                    "status")]);
        }

        if (survey.Version != expectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A felmérő a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        survey.EffectiveFrom = effectiveFrom;
        PayrollOnboardingMapper.ApplyAnswers(survey, answers);
        survey.HrPayrollNote = PayrollOnboardingMapper.NormalizeOptional(
            hrPayrollNote);
        var errors = InputValidation.ValidateTaxAllowanceSurvey(survey);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        survey.UpdatedAtUtc = timeProvider.GetUtcNow();
        survey.UpdatedByUserId = actor.Id;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxAllowanceSurvey.Updated",
            "TaxAllowanceSurvey",
            survey.Id.ToString(),
            httpContext.TraceIdentifier,
            "Belső adókedvezmény-felmérő piszkozata módosítva.");
        return await SaveSurveyAsync(
            survey,
            dbContext,
            "A felmérő mentés közben megváltozott. Töltse újra az adatokat.",
            cancellationToken);
    }

    private static Task<IResult> SubmitOwnAsync(
        Guid id,
        TaxSurveyVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        SubmitAsync(
            id,
            request.ExpectedVersion,
            selfOnly: true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> SubmitAdminAsync(
        Guid id,
        TaxSurveyVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        SubmitAsync(
            id,
            request.ExpectedVersion,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> SubmitAsync(
        Guid id,
        uint expectedVersion,
        bool selfOnly,
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

        if (selfOnly && actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        var survey = await SurveyQuery(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    (!selfOnly || item.EmployeeId == actor.EmployeeId),
                cancellationToken);
        if (survey is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (survey.Status != TaxAllowanceSurveyStatus.Draft)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "TAX_SURVEY_NOT_SUBMITTABLE",
                    "Csak piszkozat állapotú felmérő küldhető be.",
                    "status")]);
        }

        if (survey.Version != expectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A felmérő a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var errors = InputValidation.ValidateTaxAllowanceSurvey(survey);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var employeeBirthDate = await dbContext.Employees
            .Where(employee =>
                employee.Id == survey.EmployeeId &&
                employee.OrganizationId == actor.OrganizationId)
            .Select(employee => employee.BirthDate)
            .SingleAsync(cancellationToken);
        var decision = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            employeeBirthDate);
        var now = timeProvider.GetUtcNow();
        survey.Status = decision.NeedsClarification
            ? TaxAllowanceSurveyStatus.NeedsClarification
            : TaxAllowanceSurveyStatus.Submitted;
        survey.RuleSetVersion = decision.RuleSetVersion;
        survey.DeclaredAtUtc = now;
        survey.DeclaredByUserId = actor.Id;
        survey.UpdatedAtUtc = now;
        survey.UpdatedByUserId = actor.Id;
        ApplySuggestions(survey, decision.Suggestions, actor.Id, now, dbContext);

        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxAllowanceSurvey.Submitted",
            "TaxAllowanceSurvey",
            survey.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Belső felmérő beküldve; szabályverzió: {decision.RuleSetVersion}; végleges jogosultsági döntés nem történt.");
        return await SaveSurveyAsync(
            survey,
            dbContext,
            "A felmérő beküldés közben megváltozott. Töltse újra az adatokat.",
            cancellationToken);
    }

    private static Task<IResult> ReopenAsync(
        Guid id,
        TaxSurveyVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionSurveyAsync(
            id,
            request.ExpectedVersion,
            TaxAllowanceSurveyStatus.Draft,
            hrPayrollNote: null,
            "TaxAllowanceSurvey.Reopened",
            "Felmérő módosításhoz visszanyitva.",
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> ReviewAsync(
        Guid id,
        ReviewTaxAllowanceSurveyRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionSurveyAsync(
            id,
            request.ExpectedVersion,
            TaxAllowanceSurveyStatus.Reviewed,
            request.HrPayrollNote,
            "TaxAllowanceSurvey.Reviewed",
            "Felmérő HR/bérszámfejtő által ellenőrizve; hivatalos nyilatkozat továbbra is szükséges.",
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> CompleteSurveyAsync(
        Guid id,
        TaxSurveyVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionSurveyAsync(
            id,
            request.ExpectedVersion,
            TaxAllowanceSurveyStatus.Completed,
            hrPayrollNote: null,
            "TaxAllowanceSurvey.Completed",
            "Felmérő workflow lezárva.",
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> TransitionSurveyAsync(
        Guid id,
        uint expectedVersion,
        TaxAllowanceSurveyStatus target,
        string? hrPayrollNote,
        string auditAction,
        string auditSummary,
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

        var survey = await SurveyQuery(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (survey is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (survey.Version != expectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A felmérő a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!PayrollOnboardingRules.CanTransitionSurvey(survey.Status, target))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "TAX_SURVEY_TRANSITION_NOT_ALLOWED",
                    "A kért felmérő-állapotátmenet nem engedélyezett.",
                    "status")]);
        }

        if (hrPayrollNote?.Length > 1000)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "HR_PAYROLL_NOTE_TOO_LONG",
                    "A HR/bérszámfejtési megjegyzés legfeljebb 1000 karakter lehet.",
                    "hrPayrollNote")]);
        }

        var now = timeProvider.GetUtcNow();
        survey.Status = target;
        survey.UpdatedAtUtc = now;
        survey.UpdatedByUserId = actor.Id;
        if (target == TaxAllowanceSurveyStatus.Reviewed)
        {
            survey.ReviewedAtUtc = now;
            survey.ReviewedByUserId = actor.Id;
            survey.HrPayrollNote = PayrollOnboardingMapper.NormalizeOptional(
                hrPayrollNote);
        }
        else if (target == TaxAllowanceSurveyStatus.Draft)
        {
            survey.ReviewedAtUtc = null;
            survey.ReviewedByUserId = null;
        }

        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            auditAction,
            "TaxAllowanceSurvey",
            survey.Id.ToString(),
            httpContext.TraceIdentifier,
            auditSummary);
        return await SaveSurveyAsync(
            survey,
            dbContext,
            "A felmérő állapotváltás közben megváltozott. Töltse újra az adatokat.",
            cancellationToken);
    }

    private static async Task<IResult> ListRequirementsAsync(
        Guid employeeId,
        Guid? surveyId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
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

        var query = dbContext.TaxDeclarationRequirements
            .AsNoTracking()
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.OrganizationId == actor.OrganizationId);
        if (surveyId is not null)
        {
            query = query.Where(item => item.SurveyId == surveyId);
        }

        var requirements = await query
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.Type)
            .ToArrayAsync(cancellationToken);
        var response = requirements
            .Select(PayrollOnboardingMapper.MapRequirement)
            .ToArray();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxDeclarationRequirements.Viewed",
            "Employee",
            employeeId.ToString(),
            httpContext.TraceIdentifier,
            "Adónyilatkozat-checklist megtekintve; felmérési válasz nem került az audit payloadba.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateRequirementStatusAsync(
        Guid id,
        UpdateTaxDeclarationStatusRequest request,
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

        var requirement = await dbContext.TaxDeclarationRequirements
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (requirement is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (requirement.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A nyilatkozat-checklist elem a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!PayrollOnboardingRules.CanTransitionRequirement(
            requirement.Status,
            request.Status))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "DECLARATION_STATUS_TRANSITION_NOT_ALLOWED",
                    "A kért nyilatkozat-státuszátmenet nem engedélyezett.",
                    "status")]);
        }

        var errors = ValidateRequirementFields(
            requirement.EffectiveFrom,
            request.EffectiveTo,
            request.Notes);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        requirement.Status = request.Status;
        requirement.EffectiveTo = request.EffectiveTo;
        requirement.Notes = PayrollOnboardingMapper.NormalizeOptional(request.Notes);
        requirement.UpdatedAtUtc = timeProvider.GetUtcNow();
        requirement.UpdatedByUserId = actor.Id;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxDeclarationRequirement.StatusUpdated",
            "TaxDeclarationRequirement",
            requirement.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Nyilatkozat workflow-státusz módosítva: {request.Status}.");
        return await SaveRequirementAsync(
            requirement,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> OverrideRequirementAsync(
        Guid id,
        OverrideTaxDeclarationRequirementRequest request,
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

        var requirement = await dbContext.TaxDeclarationRequirements
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (requirement is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (requirement.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A nyilatkozat-checklist elem a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var errors = ValidateRequirementFields(
            requirement.EffectiveFrom,
            request.EffectiveTo,
            notes: null);
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > 1000)
        {
            errors.Add(new(
                "DECLARATION_OVERRIDE_REASON_INVALID",
                "A kézi felülírás 1–1000 karakteres indoklást igényel.",
                "reason"));
        }

        if ((!request.RequiredDecision &&
             request.Status != TaxDeclarationRequirementStatus.NotRequired) ||
            (request.RequiredDecision &&
             request.Status == TaxDeclarationRequirementStatus.NotRequired))
        {
            errors.Add(new(
                "DECLARATION_OVERRIDE_STATUS_INVALID",
                "A szükségességi döntés és a státusz legyen összhangban.",
                "status"));
        }

        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        requirement.RequiredDecision = request.RequiredDecision;
        requirement.Status = request.Status;
        requirement.EffectiveTo = request.EffectiveTo;
        requirement.ManualOverride = true;
        requirement.ManualOverrideReason = request.Reason.Trim();
        requirement.UpdatedAtUtc = timeProvider.GetUtcNow();
        requirement.UpdatedByUserId = actor.Id;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "TaxDeclarationRequirement.Overridden",
            "TaxDeclarationRequirement",
            requirement.Id.ToString(),
            httpContext.TraceIdentifier,
            "Nyilatkozat-javaslat kézzel felülírva; az indoklás az entitáson tárolva, audit payloadba nem másolva.");
        return await SaveRequirementAsync(
            requirement,
            dbContext,
            cancellationToken);
    }

    private static void ApplySuggestions(
        TaxAllowanceSurvey survey,
        IReadOnlyList<TaxDeclarationSuggestion> suggestions,
        Guid actorUserId,
        DateTimeOffset now,
        PatikaDbContext dbContext)
    {
        var existingByType = survey.DeclarationRequirements
            .ToDictionary(requirement => requirement.Type);
        foreach (var suggestion in suggestions)
        {
            if (!existingByType.TryGetValue(suggestion.Type, out var requirement))
            {
                requirement = new TaxDeclarationRequirement
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = survey.OrganizationId,
                    EmployeeId = survey.EmployeeId,
                    SurveyId = survey.Id,
                    Type = suggestion.Type,
                    RequiredDecision = suggestion.Required,
                    Status = suggestion.Required
                        ? TaxDeclarationRequirementStatus.Required
                        : TaxDeclarationRequirementStatus.NotRequired,
                    EffectiveFrom = survey.EffectiveFrom,
                    Notes = suggestion.Note,
                    GeneratedByRuleVersion = survey.RuleSetVersion,
                    CreatedAtUtc = now,
                    CreatedByUserId = actorUserId,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = actorUserId
                };
                survey.DeclarationRequirements.Add(requirement);
                dbContext.TaxDeclarationRequirements.Add(requirement);
                continue;
            }

            if (requirement.ManualOverride)
            {
                continue;
            }

            requirement.RequiredDecision = suggestion.Required;
            requirement.Status = suggestion.Required
                ? TaxDeclarationRequirementStatus.Required
                : TaxDeclarationRequirementStatus.NotRequired;
            requirement.EffectiveFrom = survey.EffectiveFrom;
            requirement.EffectiveTo = null;
            requirement.Notes = suggestion.Note;
            requirement.GeneratedByRuleVersion = survey.RuleSetVersion;
            requirement.UpdatedAtUtc = now;
            requirement.UpdatedByUserId = actorUserId;
        }
    }

    private static List<ApiValidationError> ValidateRequirementFields(
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string? notes)
    {
        var errors = new List<ApiValidationError>();
        if (effectiveTo < effectiveFrom)
        {
            errors.Add(new(
                "DECLARATION_EFFECTIVE_DATE_ORDER",
                "A hatály vége nem előzheti meg a kezdetét.",
                "effectiveTo"));
        }

        if (notes?.Length > 1000)
        {
            errors.Add(new(
                "DECLARATION_NOTES_TOO_LONG",
                "A nyilatkozat megjegyzése legfeljebb 1000 karakter lehet.",
                "notes"));
        }

        return errors;
    }

    private static async Task<IResult> SaveSurveyAsync(
        TaxAllowanceSurvey survey,
        PatikaDbContext dbContext,
        string conflictDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(conflictDetail);
        }

        return Results.Ok(PayrollOnboardingMapper.MapSurvey(survey));
    }

    private static async Task<IResult> SaveRequirementAsync(
        TaxDeclarationRequirement requirement,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A nyilatkozat-checklist elem mentés közben megváltozott. Töltse újra az adatokat.");
        }

        return Results.Ok(PayrollOnboardingMapper.MapRequirement(requirement));
    }

    private static IQueryable<TaxAllowanceSurvey> SurveyQuery(
        PatikaDbContext dbContext,
        bool tracking = false)
    {
        var query = tracking
            ? dbContext.TaxAllowanceSurveys
            : dbContext.TaxAllowanceSurveys.AsNoTracking();
        return query.Include(survey => survey.DeclarationRequirements);
    }

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

    private static IResult EmployeeLinkRequired() =>
        EndpointHelpers.ValidationProblem(
            [new ApiValidationError(
                "EMPLOYEE_LINK_REQUIRED",
                "A saját művelethez kapcsolt dolgozói profil szükséges.",
                "employeeId")]);
}
