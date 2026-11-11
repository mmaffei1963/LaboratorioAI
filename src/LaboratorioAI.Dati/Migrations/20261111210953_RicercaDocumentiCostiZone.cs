using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RicercaDocumentiCostiZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClienteFornitore",
                schema: "dir",
                table: "RegistrazioniAnalitiche",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodiceControparte",
                schema: "dir",
                table: "RegistrazioniAnalitiche",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RagioneSocialeControparte",
                schema: "dir",
                table: "RegistrazioniAnalitiche",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoricoZoneClienti",
                schema: "provv",
                columns: table => new
                {
                    IdStoricoZonaCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Zona = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataDal = table.Column<DateTime>(type: "date", nullable: false),
                    DataAl = table.Column<DateTime>(type: "date", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoricoZoneClienti", x => x.IdStoricoZonaCliente);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoricoZoneClienti_CodiceCliente_DataDal",
                schema: "provv",
                table: "StoricoZoneClienti",
                columns: new[] { "CodiceCliente", "DataDal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoricoZoneClienti",
                schema: "provv");

            migrationBuilder.DropColumn(
                name: "ClienteFornitore",
                schema: "dir",
                table: "RegistrazioniAnalitiche");

            migrationBuilder.DropColumn(
                name: "CodiceControparte",
                schema: "dir",
                table: "RegistrazioniAnalitiche");

            migrationBuilder.DropColumn(
                name: "RagioneSocialeControparte",
                schema: "dir",
                table: "RegistrazioniAnalitiche");
        }
    }
}
