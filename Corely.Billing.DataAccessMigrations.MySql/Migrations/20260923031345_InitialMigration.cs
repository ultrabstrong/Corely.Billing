using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "ConsumptionEvents",
                    columns: table => new
                    {
                        ConsumptionId = table.Column<Guid>(type: "char(36)", nullable: false),
                        CreatedUtc = table.Column<DateTime>(
                            type: "TIMESTAMP",
                            nullable: false,
                            defaultValueSql: "(UTC_TIMESTAMP)"
                        ),
                        AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                        Quantity = table.Column<long>(type: "bigint", nullable: false),
                        Unit = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Operation = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Provider = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        UtcTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        CorrelationId = table.Column<Guid>(type: "char(36)", nullable: false),
                        IdempotencyKey = table.Column<string>(
                            type: "varchar(400)",
                            maxLength: 400,
                            nullable: false
                        ),
                        GrantId = table.Column<Guid>(type: "char(36)", nullable: false),
                        FinalizedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        Outcome = table.Column<string>(
                            type: "varchar(20)",
                            maxLength: 20,
                            nullable: true
                        ),
                        UserId = table.Column<Guid>(type: "char(36)", nullable: true),
                        TagsJson = table.Column<string>(type: "longtext", nullable: true),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ConsumptionEvents", x => x.ConsumptionId);
                    }
                )
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "Grants",
                    columns: table => new
                    {
                        GrantId = table.Column<Guid>(type: "char(36)", nullable: false),
                        CreatedUtc = table.Column<DateTime>(
                            type: "TIMESTAMP",
                            nullable: false,
                            defaultValueSql: "(UTC_TIMESTAMP)"
                        ),
                        AccountId = table.Column<Guid>(type: "char(36)", nullable: false),
                        Quantity = table.Column<long>(type: "bigint", nullable: false),
                        Unit = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Operation = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        ValidFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        ValidToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        TagsJson = table.Column<string>(type: "longtext", nullable: true),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Grants", x => x.GrantId);
                    }
                )
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEvents_AccountId_CorrelationId",
                table: "ConsumptionEvents",
                columns: new[] { "AccountId", "CorrelationId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEvents_AccountId_IdempotencyKey",
                table: "ConsumptionEvents",
                columns: new[] { "AccountId", "IdempotencyKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Grants_AccountId",
                table: "Grants",
                column: "AccountId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ConsumptionEvents");

            migrationBuilder.DropTable(name: "Grants");
        }
    }
}
