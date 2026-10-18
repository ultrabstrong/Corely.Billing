using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MsSql.Migrations
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
                type: "bit",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "GrantRequests",
                columns: table => new
                {
                    GrantRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(
                        type: "DATETIME2",
                        nullable: false,
                        defaultValueSql: "(SYSUTCDATETIME())"
                    ),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Unit = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    RequestedByUserId = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    RequestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    DecidedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrantRequests", x => x.GrantRequestId);
                }
            );

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
