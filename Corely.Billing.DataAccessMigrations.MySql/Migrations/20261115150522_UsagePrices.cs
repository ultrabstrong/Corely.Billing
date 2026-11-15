using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class UsagePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceAmount",
                table: "Grants",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "UnitPriceCurrency",
                table: "Grants",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "UsagePrices",
                    columns: table => new
                    {
                        Operation = table.Column<string>(
                            type: "varchar(100)",
                            maxLength: 100,
                            nullable: false
                        ),
                        Amount = table.Column<decimal>(
                            type: "decimal(19,6)",
                            precision: 19,
                            scale: 6,
                            nullable: false
                        ),
                        Currency = table.Column<string>(
                            type: "varchar(3)",
                            maxLength: 3,
                            nullable: false
                        ),
                        CreatedUtc = table.Column<DateTime>(
                            type: "TIMESTAMP",
                            nullable: false,
                            defaultValueSql: "(UTC_TIMESTAMP)"
                        ),
                        ModifiedUtc = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_UsagePrices", x => x.Operation);
                    }
                )
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UsagePrices");

            migrationBuilder.DropColumn(name: "UnitPriceAmount", table: "Grants");

            migrationBuilder.DropColumn(name: "UnitPriceCurrency", table: "Grants");
        }
    }
}
