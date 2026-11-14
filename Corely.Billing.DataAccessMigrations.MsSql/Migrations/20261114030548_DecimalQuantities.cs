using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corely.Billing.DataAccessMigrations.MsSql.Migrations
{
    /// <inheritdoc />
    public partial class DecimalQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "Grants",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "GrantRequests",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ConsumptionEvents",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Quantity",
                table: "Grants",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,6)",
                oldPrecision: 19,
                oldScale: 6,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<long>(
                name: "Quantity",
                table: "GrantRequests",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,6)",
                oldPrecision: 19,
                oldScale: 6,
                oldNullable: true
            );

            migrationBuilder.AlterColumn<long>(
                name: "Quantity",
                table: "ConsumptionEvents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,6)",
                oldPrecision: 19,
                oldScale: 6
            );
        }
    }
}
