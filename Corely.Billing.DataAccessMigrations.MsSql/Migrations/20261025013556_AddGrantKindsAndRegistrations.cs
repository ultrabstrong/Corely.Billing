using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MsSql.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantKindsAndRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Grants_AccountId", table: "Grants");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Grants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100
            );

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Grants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true
            );

            migrationBuilder.Sql("EXEC(N'UPDATE Grants SET Kind = ''Consumable''')");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Grants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "GrantRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100
            );

            migrationBuilder.AlterColumn<long>(
                name: "Quantity",
                table: "GrantRequests",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint"
            );

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "GrantRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true
            );

            migrationBuilder.Sql("EXEC(N'UPDATE GrantRequests SET Kind = ''Consumable''')");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "GrantRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true
            );

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(
                        type: "DATETIME2",
                        nullable: false,
                        defaultValueSql: "(SYSUTCDATETIME())"
                    ),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Reference = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    Description = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    RegisteredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.RegistrationId);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Grants_AccountId_Operation",
                table: "Grants",
                columns: new[] { "AccountId", "Operation" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_AccountId_Operation_Reference",
                table: "Registrations",
                columns: new[] { "AccountId", "Operation", "Reference" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_GrantId_ReleasedUtc",
                table: "Registrations",
                columns: new[] { "GrantId", "ReleasedUtc" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Registrations");

            migrationBuilder.DropIndex(name: "IX_Grants_AccountId_Operation", table: "Grants");

            migrationBuilder.DropColumn(name: "Kind", table: "Grants");

            migrationBuilder.DropColumn(name: "Kind", table: "GrantRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Grants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "GrantRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<long>(
                name: "Quantity",
                table: "GrantRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Grants_AccountId",
                table: "Grants",
                column: "AccountId"
            );
        }
    }
}
