using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates inline migration metadata arrays.

namespace PatikaBeosztas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2BPlanningFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    RequiredCapability = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageRequirements", x => x.Id);
                    table.UniqueConstraint("AK_CoverageRequirements_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_CoverageRequirements_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCapabilities",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCapabilities", x => new { x.EmployeeId, x.Capability });
                    table.ForeignKey(
                        name: "FK_EmployeeCapabilities_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeShiftQuotaRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Minimum = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    Maximum = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeShiftQuotaRules", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeShiftQuotaRules_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_EmployeeShiftQuotaRules_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWorkProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractedMonthlyMinutes = table.Column<int>(type: "integer", nullable: false),
                    ContractedWeeklyMinutes = table.Column<int>(type: "integer", nullable: true),
                    StandardShiftMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumShiftMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaximumRegularShiftMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaximumDailyMinutes = table.Column<int>(type: "integer", nullable: false),
                    AllowsLongShift = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumLongShiftMinutes = table.Column<int>(type: "integer", nullable: true),
                    AllowsFullOpeningHoursShift = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsOvertime = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumOvertimeMinutesPerMonth = table.Column<int>(type: "integer", nullable: true),
                    AllowsOnCallDuty = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumOnCallAssignmentsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    AllowsStandby = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumStandbyAssignmentsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    AllowsSaturday = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumSaturdaysPerMonth = table.Column<int>(type: "integer", nullable: true),
                    AllowsSunday = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumSundaysPerMonth = table.Column<int>(type: "integer", nullable: true),
                    IncludeInAutoFill = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWorkProfiles", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeWorkProfiles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_EmployeeWorkProfiles_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WeekdayMask = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredCapability = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationShiftTemplates", x => x.Id);
                    table.UniqueConstraint("AK_LocationShiftTemplates_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_LocationShiftTemplates_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationWeeklyOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SundayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MondayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TuesdayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WednesdayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThursdayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FridayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SaturdayMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationWeeklyOpenings", x => x.Id);
                    table.UniqueConstraint("AK_LocationWeeklyOpenings_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_LocationWeeklyOpenings_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpeningIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationWeeklyOpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningIntervals_LocationWeeklyOpenings_OrganizationId_Loca~",
                        columns: x => new { x.OrganizationId, x.LocationWeeklyOpeningId },
                        principalTable: "LocationWeeklyOpenings",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "EmployeeCapabilities"
                    ("EmployeeId", "Capability", "OrganizationId", "AssignedAtUtc")
                SELECT "Id", 'Pharmacist', "OrganizationId", CURRENT_TIMESTAMP
                FROM "Employees"
                WHERE "CountsAsPharmacist" = TRUE
                   OR "ProfessionalRole" = 'PharmacyManager'
                ON CONFLICT ("EmployeeId", "Capability") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequirements_OrganizationId_LocationId_DayOfWeek_Is~",
                table: "CoverageRequirements",
                columns: new[] { "OrganizationId", "LocationId", "DayOfWeek", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageRequirements_OrganizationId_RequiredCapability_DayO~",
                table: "CoverageRequirements",
                columns: new[] { "OrganizationId", "RequiredCapability", "DayOfWeek", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCapabilities_OrganizationId_Capability_EmployeeId",
                table: "EmployeeCapabilities",
                columns: new[] { "OrganizationId", "Capability", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCapabilities_OrganizationId_EmployeeId",
                table: "EmployeeCapabilities",
                columns: new[] { "OrganizationId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftQuotaRules_OrganizationId_EmployeeId_Dimension~",
                table: "EmployeeShiftQuotaRules",
                columns: new[] { "OrganizationId", "EmployeeId", "Dimension", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftQuotaRules_OrganizationId_EmployeeId_IsActive",
                table: "EmployeeShiftQuotaRules",
                columns: new[] { "OrganizationId", "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkProfiles_OrganizationId_EmployeeId",
                table: "EmployeeWorkProfiles",
                columns: new[] { "OrganizationId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationShiftTemplates_OrganizationId_LocationId_IsActive",
                table: "LocationShiftTemplates",
                columns: new[] { "OrganizationId", "LocationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationWeeklyOpenings_OrganizationId_LocationId",
                table: "LocationWeeklyOpenings",
                columns: new[] { "OrganizationId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningIntervals_OrganizationId_LocationWeeklyOpeningId_Day~",
                table: "OpeningIntervals",
                columns: new[] { "OrganizationId", "LocationWeeklyOpeningId", "DayOfWeek", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageRequirements");

            migrationBuilder.DropTable(
                name: "EmployeeCapabilities");

            migrationBuilder.DropTable(
                name: "EmployeeShiftQuotaRules");

            migrationBuilder.DropTable(
                name: "EmployeeWorkProfiles");

            migrationBuilder.DropTable(
                name: "LocationShiftTemplates");

            migrationBuilder.DropTable(
                name: "OpeningIntervals");

            migrationBuilder.DropTable(
                name: "LocationWeeklyOpenings");
        }
    }
}
