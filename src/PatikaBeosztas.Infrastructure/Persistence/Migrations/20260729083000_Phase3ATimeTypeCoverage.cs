using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatikaBeosztas.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PatikaDbContext))]
[Migration("20260729083000_Phase3ATimeTypeCoverage")]
public sealed class Phase3ATimeTypeCoverage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeType",
            table: "LocationShiftTemplates",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Work");

        migrationBuilder.AddColumn<string>(
            name: "TimeType",
            table: "CoverageRequirements",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Work");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TimeType",
            table: "LocationShiftTemplates");

        migrationBuilder.DropColumn(
            name: "TimeType",
            table: "CoverageRequirements");
    }
}
