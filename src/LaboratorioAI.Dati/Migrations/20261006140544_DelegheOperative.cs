using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DelegheOperative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DataSottoscrizione",
                schema: "cons",
                table: "DelegheClienti",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "CodiceFiscale",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Consulente",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsulenteAlbo",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsulenteQualifica",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEmissione",
                schema: "cons",
                table: "DelegheClienti",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Generata",
                schema: "cons",
                table: "DelegheClienti",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "cons",
                table: "DelegheClienti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdOrganicoArcLab",
                schema: "cons",
                table: "DelegheClienti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdRapportino",
                schema: "cons",
                table: "DelegheClienti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdUtenteCreazione",
                schema: "cons",
                table: "DelegheClienti",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegaleRappCognome",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegaleRappNome",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartitaIva",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PercorsoFirmata",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PercorsoPdf",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PercorsoWord",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SedeLegale",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SedeLegaleComune",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SedeOperativa",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SedeOperativaComune",
                schema: "cons",
                table: "DelegheClienti",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Stato",
                schema: "cons",
                table: "DelegheClienti",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ConsulentiDati",
                schema: "cons",
                columns: table => new
                {
                    IdConsulenteDati = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdOrganicoArcLab = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Qualifica = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IscrizioneAlbo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdUtenteModifica = table.Column<int>(type: "int", nullable: true),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsulentiDati", x => x.IdConsulenteDati);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegheClienti_IdClienteArcLab_IdOrganicoArcLab",
                schema: "cons",
                table: "DelegheClienti",
                columns: new[] { "IdClienteArcLab", "IdOrganicoArcLab" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsulentiDati_IdOrganicoArcLab",
                schema: "cons",
                table: "ConsulentiDati",
                column: "IdOrganicoArcLab",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsulentiDati",
                schema: "cons");

            migrationBuilder.DropIndex(
                name: "IX_DelegheClienti_IdClienteArcLab_IdOrganicoArcLab",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "CodiceFiscale",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "Consulente",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "ConsulenteAlbo",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "ConsulenteQualifica",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "DataEmissione",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "Generata",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "IdOrganicoArcLab",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "IdRapportino",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "IdUtenteCreazione",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "LegaleRappCognome",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "LegaleRappNome",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "PartitaIva",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "PercorsoFirmata",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "PercorsoPdf",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "PercorsoWord",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "SedeLegale",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "SedeLegaleComune",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "SedeOperativa",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "SedeOperativaComune",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.DropColumn(
                name: "Stato",
                schema: "cons",
                table: "DelegheClienti");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DataSottoscrizione",
                schema: "cons",
                table: "DelegheClienti",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);
        }
    }
}
