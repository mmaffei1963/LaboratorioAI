using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class InviiLaboratoriEsterni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContattiLaboratoriEsterni",
                schema: "accettazione",
                columns: table => new
                {
                    IdContattoLaboratorioEsterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLabEsternoArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeLaboratorio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EmailDestinatari = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmailCopia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContattiLaboratoriEsterni", x => x.IdContattoLaboratorioEsterno);
                });

            migrationBuilder.CreateTable(
                name: "InviiLaboratoriEsterni",
                schema: "accettazione",
                columns: table => new
                {
                    IdInvioLaboratorioEsterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLabEsternoArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeLaboratorio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Dal = table.Column<DateTime>(type: "date", nullable: false),
                    Al = table.Column<DateTime>(type: "date", nullable: false),
                    Destinatari = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Copia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Oggetto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Testo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NomeFile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileExcel = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    NumeroCampioni = table.Column<int>(type: "int", nullable: false),
                    NumeroProve = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Riuscito = table.Column<bool>(type: "bit", nullable: false),
                    Errore = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataInvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviiLaboratoriEsterni", x => x.IdInvioLaboratorioEsterno);
                });

            migrationBuilder.CreateTable(
                name: "RigheInvioLaboratorioEsterno",
                schema: "accettazione",
                columns: table => new
                {
                    IdRigaInvioLaboratorioEsterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInvioLaboratorioEsterno = table.Column<int>(type: "int", nullable: false),
                    IdCampioneArcLab = table.Column<int>(type: "int", nullable: false),
                    IdProvaArcLab = table.Column<int>(type: "int", nullable: false),
                    NumeroCampione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheInvioLaboratorioEsterno", x => x.IdRigaInvioLaboratorioEsterno);
                    table.ForeignKey(
                        name: "FK_RigheInvioLaboratorioEsterno_InviiLaboratoriEsterni_IdInvioLaboratorioEsterno",
                        column: x => x.IdInvioLaboratorioEsterno,
                        principalSchema: "accettazione",
                        principalTable: "InviiLaboratoriEsterni",
                        principalColumn: "IdInvioLaboratorioEsterno",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContattiLaboratoriEsterni_IdLabEsternoArcLab",
                schema: "accettazione",
                table: "ContattiLaboratoriEsterni",
                column: "IdLabEsternoArcLab",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviiLaboratoriEsterni_IdLabEsternoArcLab_DataInvio",
                schema: "accettazione",
                table: "InviiLaboratoriEsterni",
                columns: new[] { "IdLabEsternoArcLab", "DataInvio" });

            migrationBuilder.CreateIndex(
                name: "IX_RigheInvioLaboratorioEsterno_IdInvioLaboratorioEsterno",
                schema: "accettazione",
                table: "RigheInvioLaboratorioEsterno",
                column: "IdInvioLaboratorioEsterno");

            migrationBuilder.CreateIndex(
                name: "IX_RigheInvioLaboratorioEsterno_IdProvaArcLab",
                schema: "accettazione",
                table: "RigheInvioLaboratorioEsterno",
                column: "IdProvaArcLab");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContattiLaboratoriEsterni",
                schema: "accettazione");

            migrationBuilder.DropTable(
                name: "RigheInvioLaboratorioEsterno",
                schema: "accettazione");

            migrationBuilder.DropTable(
                name: "InviiLaboratoriEsterni",
                schema: "accettazione");
        }
    }
}
