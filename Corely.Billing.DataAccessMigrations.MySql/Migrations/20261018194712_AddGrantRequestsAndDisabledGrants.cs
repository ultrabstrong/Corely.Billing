using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantRequestsAndDisabledGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Grants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder
                .CreateTable(
                    name: "GrantRequests",
                    columns: table => new
                    {
                        GrantRequestId = table.Column<Guid>(type: "char(36)", nullable: false),
                        CreatedUtc = table.Column<DateTime>(
                            type: "TIMESTAMP",
                            nullable: false,
                            defaultValueSql: "(UTC_TIMESTAMP)"
                        ),
                        AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                        Operation = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Unit = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Quantity = table.Column<long>(type: "bigint", nullable: false),
                        Note = table.Column<string>(
                            type: "varchar(500)",
                            maxLength: 500,
                            nullable: true
                        ),
                        RequestedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                        RequestedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        Status = table.Column<string>(
                            type: "varchar(16)",
                            maxLength: 16,
                            nullable: false
                        ),
                        DecidedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        DecidedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                        DecisionReason = table.Column<string>(
                            type: "varchar(500)",
                            maxLength: 500,
                            nullable: true
                        ),
                        GrantId = table.Column<Guid>(type: "char(36)", nullable: true),
                        TagsJson = table.Column<string>(type: "longtext", nullable: true),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_GrantRequests", x => x.GrantRequestId);
                    }
                )
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GrantRequests_AccountId_Status",
                table: "GrantRequests",
                columns: new[] { "AccountId", "Status" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GrantRequests");

            migrationBuilder.DropColumn(name: "Disabled", table: "Grants");
        }
    }
}
