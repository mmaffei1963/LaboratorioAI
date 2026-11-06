using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class VerificaLimiti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GiriVerifica",
                schema: "limiti",
                columns: table => new
                {
                    IdGiroVerificaLimiti = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    IdFonteNormativa = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataRichiesta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataFine = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RichiestoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdUtenteRichiesta = table.Column<int>(type: "int", nullable: true),
                    Modello = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SchedeControllate = table.Column<int>(type: "int", nullable: false),
                    Esito = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiriVerifica", x => x.IdGiroVerificaLimiti);
                    table.ForeignKey(
                        name: "FK_GiriVerifica_FontiNormative_IdFonteNormativa",
                        column: x => x.IdFonteNormativa,
                        principalSchema: "limiti",
                        principalTable: "FontiNormative",
                        principalColumn: "IdFonteNormativa",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RilieviVerifica",
                schema: "limiti",
                columns: table => new
                {
                    IdRilievoVerificaLimite = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdGiroVerificaLimiti = table.Column<int>(type: "int", nullable: false),
                    IdLimiteNormativo = table.Column<int>(type: "int", nullable: false),
                    Gravita = table.Column<int>(type: "int", nullable: false),
                    Regola = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Citazione = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CitazioneTrovata = table.Column<bool>(type: "bit", nullable: true),
                    Chiuso = table.Column<bool>(type: "bit", nullable: false),
                    ChiusoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DataChiusura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaChiusura = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RilieviVerifica", x => x.IdRilievoVerificaLimite);
                    table.ForeignKey(
                        name: "FK_RilieviVerifica_GiriVerifica_IdGiroVerificaLimiti",
                        column: x => x.IdGiroVerificaLimiti,
                        principalSchema: "limiti",
                        principalTable: "GiriVerifica",
                        principalColumn: "IdGiroVerificaLimiti",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RilieviVerifica_LimitiNormativi_IdLimiteNormativo",
                        column: x => x.IdLimiteNormativo,
                        principalSchema: "limiti",
                        principalTable: "LimitiNormativi",
                        principalColumn: "IdLimiteNormativo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiriVerifica_IdFonteNormativa",
                schema: "limiti",
                table: "GiriVerifica",
                column: "IdFonteNormativa");

            migrationBuilder.CreateIndex(
                name: "IX_GiriVerifica_Stato",
                schema: "limiti",
                table: "GiriVerifica",
                column: "Stato");

            migrationBuilder.CreateIndex(
                name: "IX_RilieviVerifica_IdGiroVerificaLimiti",
                schema: "limiti",
                table: "RilieviVerifica",
                column: "IdGiroVerificaLimiti");

            migrationBuilder.CreateIndex(
                name: "IX_RilieviVerifica_IdLimiteNormativo_Chiuso",
                schema: "limiti",
                table: "RilieviVerifica",
                columns: new[] { "IdLimiteNormativo", "Chiuso" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RilieviVerifica",
                schema: "limiti");

            migrationBuilder.DropTable(
                name: "GiriVerifica",
                schema: "limiti");
        }
    }
}
