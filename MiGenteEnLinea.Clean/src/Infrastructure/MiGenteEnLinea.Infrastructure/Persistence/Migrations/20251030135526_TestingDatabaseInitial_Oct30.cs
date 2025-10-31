using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiGenteEnLinea.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestingDatabaseInitial_Oct30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Remuneraciones_Empleados",
                table: "Remuneraciones");

            migrationBuilder.DropTable(
                name: "Empleado");

            migrationBuilder.AlterColumn<string>(
                name: "userID",
                table: "Remuneraciones",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<decimal>(
                name: "monto",
                table: "Remuneraciones",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "empleadoID",
                table: "Remuneraciones",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "userID",
                table: "Remuneraciones",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<decimal>(
                name: "monto",
                table: "Remuneraciones",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "empleadoID",
                table: "Remuneraciones",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "Empleado",
                columns: table => new
                {
                    empleadoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Activo = table.Column<bool>(type: "bit", nullable: true),
                    alias = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Apellido = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    contactoEmergencia = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    contrato = table.Column<bool>(type: "bit", nullable: true),
                    diasPago = table.Column<int>(type: "int", nullable: true),
                    direccion = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: true),
                    estadoCivil = table.Column<int>(type: "int", nullable: true),
                    fechaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fechaRegistro = table.Column<DateTime>(type: "datetime", nullable: true),
                    fechaSalida = table.Column<DateTime>(type: "datetime", nullable: true),
                    foto = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    identificacion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    montoExtra1 = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    montoExtra2 = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    montoExtra3 = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    motivoBaja = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    municipio = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    nacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Nombre = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    periodoPago = table.Column<int>(type: "int", nullable: true),
                    posicion = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    prestaciones = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    provincia = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    remuneracionExtra1 = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    remuneracionExtra2 = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    remuneracionExtra3 = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    salario = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    telefono1 = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    telefono2 = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    telefonoEmergencia = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    tss = table.Column<bool>(type: "bit", nullable: true),
                    userID = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleado", x => x.empleadoID);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Remuneraciones_Empleados",
                table: "Remuneraciones",
                column: "empleadoID",
                principalTable: "Empleado",
                principalColumn: "empleadoID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
