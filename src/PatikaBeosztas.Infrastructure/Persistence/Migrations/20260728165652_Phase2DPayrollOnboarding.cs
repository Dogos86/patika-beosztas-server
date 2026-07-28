using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated composite-key and index arrays.

namespace PatikaBeosztas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2DPayrollOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePayrollProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaxIdentificationNumberCiphertext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TaxIdentificationNumberHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmploymentStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollProfiles", x => x.Id);
                    table.UniqueConstraint("AK_EmployeePayrollProfiles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_EmployeePayrollProfiles_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayrollProfiles_Users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayrollProfiles_Users_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxAllowanceSurveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    FormVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RuleSetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceMetadata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MonthlyAllowancePreference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaritalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MarriageDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstMarriageStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FamilyAllowanceEligibleChildrenCount = table.Column<int>(type: "integer", nullable: false),
                    DependentStudentCount = table.Column<int>(type: "integer", nullable: false),
                    HasFetusAfterDay91 = table.Column<bool>(type: "boolean", nullable: false),
                    FetusEligibilityMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    HasDisabledDependent = table.Column<bool>(type: "boolean", nullable: false),
                    HasSharedCustodyChild = table.Column<bool>(type: "boolean", nullable: false),
                    FamilyAllowanceClaimMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OtherEligiblePersonClaimsPart = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsBiologicalOrAdoptiveMother = table.Column<bool>(type: "boolean", nullable: false),
                    MotherAllowanceQualifyingChildrenCount = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HasCurrentOwnChildOrFetusEligibleForFamilyAllowance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PersonalAllowanceEligibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PersonalAllowanceStartMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    HasOtherEmployerOrRegularPayer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Under25AllowanceOptOut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ForeignTaxResidencyOrSimilarForeignBenefit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeclaredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclaredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HrPayrollNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxAllowanceSurveys", x => x.Id);
                    table.UniqueConstraint("AK_TaxAllowanceSurveys_OrganizationId_EmployeeId_Id", x => new { x.OrganizationId, x.EmployeeId, x.Id });
                    table.UniqueConstraint("AK_TaxAllowanceSurveys_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_TaxAllowanceSurveys_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxAllowanceSurveys_Users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxAllowanceSurveys_Users_OrganizationId_DeclaredByUserId",
                        columns: x => new { x.OrganizationId, x.DeclaredByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxAllowanceSurveys_Users_OrganizationId_ReviewedByUserId",
                        columns: x => new { x.OrganizationId, x.ReviewedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxAllowanceSurveys_Users_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequiredDecision = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GeneratedByRuleVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    ManualOverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarationRequirements", x => x.Id);
                    table.UniqueConstraint("AK_TaxDeclarationRequirements_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_TaxDeclarationRequirements_Employees_OrganizationId_Employe~",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationRequirements_TaxAllowanceSurveys_Organization~",
                        columns: x => new { x.OrganizationId, x.EmployeeId, x.SurveyId },
                        principalTable: "TaxAllowanceSurveys",
                        principalColumns: new[] { "OrganizationId", "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationRequirements_Users_OrganizationId_CreatedByUs~",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationRequirements_Users_OrganizationId_UpdatedByUs~",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_CreatedByUserId",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_EmployeeId",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_EmployeeNumber",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_Status",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_TaxIdentificationNum~",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "TaxIdentificationNumberHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_OrganizationId_UpdatedByUserId",
                table: "EmployeePayrollProfiles",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_CreatedByUserId",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_DeclaredByUserId",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "DeclaredByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_EmployeeId_TaxYear_FormV~",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "EmployeeId", "TaxYear", "FormVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_EmployeeId_TaxYear_Status",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "EmployeeId", "TaxYear", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_ReviewedByUserId",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "ReviewedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_Status",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxAllowanceSurveys_OrganizationId_UpdatedByUserId",
                table: "TaxAllowanceSurveys",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationRequirements_OrganizationId_CreatedByUserId",
                table: "TaxDeclarationRequirements",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationRequirements_OrganizationId_EmployeeId_Status",
                table: "TaxDeclarationRequirements",
                columns: new[] { "OrganizationId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationRequirements_OrganizationId_EmployeeId_Survey~",
                table: "TaxDeclarationRequirements",
                columns: new[] { "OrganizationId", "EmployeeId", "SurveyId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationRequirements_OrganizationId_SurveyId_Status",
                table: "TaxDeclarationRequirements",
                columns: new[] { "OrganizationId", "SurveyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationRequirements_OrganizationId_UpdatedByUserId",
                table: "TaxDeclarationRequirements",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollProfiles");

            migrationBuilder.DropTable(
                name: "TaxDeclarationRequirements");

            migrationBuilder.DropTable(
                name: "TaxAllowanceSurveys");
        }
    }
}
#pragma warning restore CA1861
