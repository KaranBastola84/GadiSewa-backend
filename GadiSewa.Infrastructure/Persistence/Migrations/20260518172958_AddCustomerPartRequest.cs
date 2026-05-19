using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadiSewa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPartRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByStaffId",
                table: "PartRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByCustomerId",
                table: "PartRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartRequests_RequestedByCustomerId",
                table: "PartRequests",
                column: "RequestedByCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartRequests_Customers_RequestedByCustomerId",
                table: "PartRequests",
                column: "RequestedByCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartRequests_Customers_RequestedByCustomerId",
                table: "PartRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartRequests_RequestedByCustomerId",
                table: "PartRequests");

            migrationBuilder.DropColumn(
                name: "RequestedByCustomerId",
                table: "PartRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByStaffId",
                table: "PartRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
