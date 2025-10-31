using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiGenteEnLinea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Soft_Delete_To_Empleador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Ofertantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Ofertantes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Ofertantes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Ofertantes");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Ofertantes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Ofertantes");
        }
    }
}
