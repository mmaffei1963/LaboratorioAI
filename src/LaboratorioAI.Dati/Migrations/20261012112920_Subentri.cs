using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Subentri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampioniInsediamentiArcLab",
                schema: "clienti",
                columns: table => new
                {
                    IdCampioneInsediamentoArcLab = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Mese = table.Column<int>(type: "int", nullable: false),
                    Campioni = table.Column<int>(type: "int", nullable: false),
                    CampioniLegionella = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    PrimoCampione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoCampione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampioniInsediamentiArcLab", x => x.IdCampioneInsediamentoArcLab);
                });

            migrationBuilder.CreateTable(
                name: "InsediamentiArcLab",
                schema: "clienti",
                columns: table => new
                {
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NomeInsediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Citta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsediamentiArcLab", x => x.IdInsediamentoArcLab);
                });

            migrationBuilder.CreateTable(
                name: "SubentriInsediamenti",
                schema: "clienti",
                columns: table => new
                {
                    IdSubentroInsediamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInsediamentoPrecedente = table.Column<int>(type: "int", nullable: false),
                    IdInsediamentoSuccessivo = table.Column<int>(type: "int", nullable: true),
                    CodiceClientePrecedente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodiceClienteSuccessivo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Confermato = table.Column<bool>(type: "bit", nullable: false),
                    Manuale = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataDecisione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnullataDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubentriInsediamenti", x => x.IdSubentroInsediamento);
                });

            migrationBuilder.CreateTable(
                name: "VerifichePartitaIva",
                schema: "clienti",
                columns: table => new
                {
                    IdVerificaPartitaIva = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Paese = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valida = table.Column<bool>(type: "bit", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Errore = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataVerifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifichePartitaIva", x => x.IdVerificaPartitaIva);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampioniInsediamentiArcLab_IdInsediamento",
                schema: "clienti",
                table: "CampioniInsediamentiArcLab",
                column: "IdInsediamento");

            migrationBuilder.CreateIndex(
                name: "IX_InsediamentiArcLab_CodiceCliente",
                schema: "clienti",
                table: "InsediamentiArcLab",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_InsediamentiArcLab_IdInsediamento",
                schema: "clienti",
                table: "InsediamentiArcLab",
                column: "IdInsediamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubentriInsediamenti_IdInsediamentoPrecedente",
                schema: "clienti",
                table: "SubentriInsediamenti",
                column: "IdInsediamentoPrecedente");

            migrationBuilder.CreateIndex(
                name: "IX_VerifichePartitaIva_Paese_PartitaIva",
                schema: "clienti",
                table: "VerifichePartitaIva",
                columns: new[] { "Paese", "PartitaIva" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampioniInsediamentiArcLab",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "InsediamentiArcLab",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "SubentriInsediamenti",
                schema: "clienti");

            migrationBuilder.DropTable(
                name: "VerifichePartitaIva",
                schema: "clienti");
        }
    }
}
