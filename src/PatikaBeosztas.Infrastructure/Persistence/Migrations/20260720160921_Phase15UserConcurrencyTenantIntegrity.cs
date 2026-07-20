using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates inline migration metadata arrays.

namespace PatikaBeosztas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15UserConcurrencyTenantIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeAllowedTimeTypes_Employees_EmployeeId",
                table: "EmployeeAllowedTimeTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeLocations_Employees_EmployeeId",
                table: "EmployeeLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeLocations_Locations_LocationId",
                table: "EmployeeLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTimeWindows_Employees_EmployeeId",
                table: "EmployeeTimeWindows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPermissions_Users_UserId",
                table: "UserPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTimeWindows_EmployeeId",
                table: "EmployeeTimeWindows");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLocations_LocationId",
                table: "EmployeeLocations");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Users_OrganizationId_Id",
                table: "Users",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Locations_OrganizationId_Id",
                table: "Locations",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Employees_OrganizationId_Id",
                table: "Employees",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_EmployeeId",
                table: "Users",
                columns: new[] { "OrganizationId", "EmployeeId" },
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_OrganizationId_UserId",
                table: "UserPermissions",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLocations_OrganizationId_EmployeeId",
                table: "EmployeeLocations",
                columns: new[] { "OrganizationId", "EmployeeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeAllowedTimeTypes_Employees_OrganizationId_EmployeeId",
                table: "EmployeeAllowedTimeTypes",
                columns: new[] { "OrganizationId", "EmployeeId" },
                principalTable: "Employees",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeLocations_Employees_OrganizationId_EmployeeId",
                table: "EmployeeLocations",
                columns: new[] { "OrganizationId", "EmployeeId" },
                principalTable: "Employees",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeLocations_Locations_OrganizationId_LocationId",
                table: "EmployeeLocations",
                columns: new[] { "OrganizationId", "LocationId" },
                principalTable: "Locations",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTimeWindows_Employees_OrganizationId_EmployeeId",
                table: "EmployeeTimeWindows",
                columns: new[] { "OrganizationId", "EmployeeId" },
                principalTable: "Employees",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermissions_Users_OrganizationId_UserId",
                table: "UserPermissions",
                columns: new[] { "OrganizationId", "UserId" },
                principalTable: "Users",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_OrganizationId_EmployeeId",
                table: "Users",
                columns: new[] { "OrganizationId", "EmployeeId" },
                principalTable: "Employees",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeAllowedTimeTypes_Employees_OrganizationId_EmployeeId",
                table: "EmployeeAllowedTimeTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeLocations_Employees_OrganizationId_EmployeeId",
                table: "EmployeeLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeLocations_Locations_OrganizationId_LocationId",
                table: "EmployeeLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTimeWindows_Employees_OrganizationId_EmployeeId",
                table: "EmployeeTimeWindows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPermissions_Users_OrganizationId_UserId",
                table: "UserPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_OrganizationId_EmployeeId",
                table: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Users_OrganizationId_Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_EmployeeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_OrganizationId_UserId",
                table: "UserPermissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Locations_OrganizationId_Id",
                table: "Locations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Employees_OrganizationId_Id",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLocations_OrganizationId_EmployeeId",
                table: "EmployeeLocations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTimeWindows_EmployeeId",
                table: "EmployeeTimeWindows",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLocations_LocationId",
                table: "EmployeeLocations",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeAllowedTimeTypes_Employees_EmployeeId",
                table: "EmployeeAllowedTimeTypes",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeLocations_Employees_EmployeeId",
                table: "EmployeeLocations",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeLocations_Locations_LocationId",
                table: "EmployeeLocations",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTimeWindows_Employees_EmployeeId",
                table: "EmployeeTimeWindows",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermissions_Users_UserId",
                table: "UserPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
#pragma warning restore CA1861
