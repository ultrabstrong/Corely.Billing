using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MsSql.Migrations
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
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "UsagePrices",
                columns: table => new
                {
                    Operation = table.Column<string>(
                        type: "nvarchar(100)",
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
                        type: "nvarchar(3)",
                        maxLength: 3,
                        nullable: false
                    ),
                    CreatedUtc = table.Column<DateTime>(
                        type: "DATETIME2",
                        nullable: false,
                        defaultValueSql: "(SYSUTCDATETIME())"
                    ),
                    ModifiedUtc = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePrices", x => x.Operation);
                }
            );
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
