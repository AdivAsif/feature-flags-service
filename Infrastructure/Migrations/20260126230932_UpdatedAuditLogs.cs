using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousFeatureFlagState",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "NewFeatureFlagState",
                table: "AuditLogs",
                newName: "PreviousStateJson");

            migrationBuilder.AlterColumn<int>(
                name: "Action",
                table: "AuditLogs",
                type: "integer",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "NewStateJson",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewStateJson",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "PreviousStateJson",
                table: "AuditLogs",
                newName: "NewFeatureFlagState");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "PreviousFeatureFlagState",
                table: "AuditLogs",
                type: "text",
                nullable: true);
        }
    }
}
