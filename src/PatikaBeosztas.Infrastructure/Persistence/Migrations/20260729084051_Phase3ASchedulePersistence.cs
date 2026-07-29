using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates inline composite-key and index arrays.

namespace PatikaBeosztas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ASchedulePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BasedOnScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedRevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GenerationOptionsSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    InputSnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CloneIdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewRequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewRequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulePlans", x => x.Id);
                    table.UniqueConstraint("AK_SchedulePlans_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_SchedulePlans_OrganizationId_BasedOnScheduleId",
                        columns: x => new { x.OrganizationId, x.BasedOnScheduleId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_ApprovedByUserId",
                        columns: x => new { x.OrganizationId, x.ApprovedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_ArchivedByUserId",
                        columns: x => new { x.OrganizationId, x.ArchivedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_PublishedByUserId",
                        columns: x => new { x.OrganizationId, x.PublishedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_ReviewRequestedByUserId",
                        columns: x => new { x.OrganizationId, x.ReviewRequestedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchedulePlans_Users_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleGenerationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationRequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeterministicSeed = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    InputSnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SolverStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SolverStatisticsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ObjectiveValue = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RedactedError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeConcurrencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleGenerationRuns", x => x.Id);
                    table.UniqueConstraint("AK_ScheduleGenerationRuns_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ScheduleGenerationRuns_SchedulePlans_OrganizationId_Schedul~",
                        columns: x => new { x.OrganizationId, x.SchedulePlanId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleGenerationRuns_Users_OrganizationId_RequestedByUser~",
                        columns: x => new { x.OrganizationId, x.RequestedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedByRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacesShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.UniqueConstraint("AK_ShiftAssignments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ScheduleGenerationRuns_OrganizationId_Gene~",
                        columns: x => new { x.OrganizationId, x.GeneratedByRunId },
                        principalTable: "ScheduleGenerationRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_SchedulePlans_OrganizationId_SchedulePlanId",
                        columns: x => new { x.OrganizationId, x.SchedulePlanId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ShiftAssignments_OrganizationId_ReplacesSh~",
                        columns: x => new { x.OrganizationId, x.ReplacesShiftId },
                        principalTable: "ShiftAssignments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Users_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedSuggestionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExclusionScope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedSuggestionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedSuggestionDecisions_ScheduleGenerationRuns_Organiz~",
                        columns: x => new { x.OrganizationId, x.GenerationRunId },
                        principalTable: "ScheduleGenerationRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedSuggestionDecisions_SchedulePlans_OrganizationId_S~",
                        columns: x => new { x.OrganizationId, x.SchedulePlanId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedSuggestionDecisions_ShiftAssignments_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.ShiftAssignmentId },
                        principalTable: "ShiftAssignments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedSuggestionDecisions_Users_OrganizationId_ActorUser~",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ResolutionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleIssues", x => x.Id);
                    table.UniqueConstraint("AK_ScheduleIssues_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_Employees_OrganizationId_EmployeeId",
                        columns: x => new { x.OrganizationId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_Locations_OrganizationId_LocationId",
                        columns: x => new { x.OrganizationId, x.LocationId },
                        principalTable: "Locations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_ScheduleGenerationRuns_OrganizationId_Genera~",
                        columns: x => new { x.OrganizationId, x.GenerationRunId },
                        principalTable: "ScheduleGenerationRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_SchedulePlans_OrganizationId_SchedulePlanId",
                        columns: x => new { x.OrganizationId, x.SchedulePlanId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_ShiftAssignments_OrganizationId_ShiftAssignm~",
                        columns: x => new { x.OrganizationId, x.ShiftAssignmentId },
                        principalTable: "ShiftAssignments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleIssues_Users_OrganizationId_ResolutionByUserId",
                        columns: x => new { x.OrganizationId, x.ResolutionByUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftExplanations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchedulePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScoreComponentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AlternativesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftExplanations_ScheduleGenerationRuns_OrganizationId_Gen~",
                        columns: x => new { x.OrganizationId, x.GenerationRunId },
                        principalTable: "ScheduleGenerationRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftExplanations_SchedulePlans_OrganizationId_SchedulePlan~",
                        columns: x => new { x.OrganizationId, x.SchedulePlanId },
                        principalTable: "SchedulePlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftExplanations_ShiftAssignments_OrganizationId_ShiftAssi~",
                        columns: x => new { x.OrganizationId, x.ShiftAssignmentId },
                        principalTable: "ShiftAssignments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftSegments_ShiftAssignments_OrganizationId_ShiftAssignme~",
                        columns: x => new { x.OrganizationId, x.ShiftAssignmentId },
                        principalTable: "ShiftAssignments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedSuggestionDecisions_OrganizationId_ActorUserId",
                table: "GeneratedSuggestionDecisions",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedSuggestionDecisions_OrganizationId_GenerationRunId",
                table: "GeneratedSuggestionDecisions",
                columns: new[] { "OrganizationId", "GenerationRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedSuggestionDecisions_OrganizationId_SchedulePlanId_~",
                table: "GeneratedSuggestionDecisions",
                columns: new[] { "OrganizationId", "SchedulePlanId", "ShiftAssignmentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedSuggestionDecisions_OrganizationId_ShiftAssignment~",
                table: "GeneratedSuggestionDecisions",
                columns: new[] { "OrganizationId", "ShiftAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGenerationRuns_OrganizationId_IdempotencyKeyHash",
                table: "ScheduleGenerationRuns",
                columns: new[] { "OrganizationId", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGenerationRuns_OrganizationId_RequestedByUserId",
                table: "ScheduleGenerationRuns",
                columns: new[] { "OrganizationId", "RequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGenerationRuns_OrganizationId_SchedulePlanId",
                table: "ScheduleGenerationRuns",
                columns: new[] { "OrganizationId", "SchedulePlanId" },
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGenerationRuns_OrganizationId_ScopeConcurrencyKey",
                table: "ScheduleGenerationRuns",
                columns: new[] { "OrganizationId", "ScopeConcurrencyKey" },
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleGenerationRuns_OrganizationId_Status_RequestedAtUtc",
                table: "ScheduleGenerationRuns",
                columns: new[] { "OrganizationId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_Code_Date",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "Code", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_EmployeeId",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_GenerationRunId",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "GenerationRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_LocationId",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_ResolutionByUserId",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "ResolutionByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_SchedulePlanId_Severity_IsRes~",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "SchedulePlanId", "Severity", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIssues_OrganizationId_ShiftAssignmentId",
                table: "ScheduleIssues",
                columns: new[] { "OrganizationId", "ShiftAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_ApprovedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "ApprovedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_ArchivedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "ArchivedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_BasedOnScheduleId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "BasedOnScheduleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_CloneIdempotencyKeyHash",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "CloneIdempotencyKeyHash" },
                unique: true,
                filter: "\"CloneIdempotencyKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_CreatedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_PeriodStart_PeriodEnd_Publishe~",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "PeriodStart", "PeriodEnd", "PublishedRevisionNumber" },
                unique: true,
                filter: "\"PublishedRevisionNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_PeriodStart_PeriodEnd_Status",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "PeriodStart", "PeriodEnd", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_PublishedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "PublishedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_ReviewRequestedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "ReviewRequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePlans_OrganizationId_UpdatedByUserId",
                table: "SchedulePlans",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_CreatedByUserId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_EmployeeId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_GeneratedByRunId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "GeneratedByRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_LocationId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_ReplacesShiftId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "ReplacesShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_SchedulePlanId_Date_Employe~",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "SchedulePlanId", "Date", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_SchedulePlanId_LocationId_D~",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "SchedulePlanId", "LocationId", "Date", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_OrganizationId_UpdatedByUserId",
                table: "ShiftAssignments",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExplanations_OrganizationId_GenerationRunId",
                table: "ShiftExplanations",
                columns: new[] { "OrganizationId", "GenerationRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExplanations_OrganizationId_SchedulePlanId",
                table: "ShiftExplanations",
                columns: new[] { "OrganizationId", "SchedulePlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExplanations_OrganizationId_ShiftAssignmentId",
                table: "ShiftExplanations",
                columns: new[] { "OrganizationId", "ShiftAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSegments_OrganizationId_ShiftAssignmentId_StartTime",
                table: "ShiftSegments",
                columns: new[] { "OrganizationId", "ShiftAssignmentId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedSuggestionDecisions");

            migrationBuilder.DropTable(
                name: "ScheduleIssues");

            migrationBuilder.DropTable(
                name: "ShiftExplanations");

            migrationBuilder.DropTable(
                name: "ShiftSegments");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "ScheduleGenerationRuns");

            migrationBuilder.DropTable(
                name: "SchedulePlans");
        }
    }
}
#pragma warning restore CA1861
