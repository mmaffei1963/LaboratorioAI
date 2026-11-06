using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AnalisiPacchettiConsulenza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataUltimaModifica",
                schema: "cons",
                table: "PacchettiConsulenza",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimaModificaDa",
                schema: "cons",
                table: "PacchettiConsulenza",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnalisiPacchettiConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdAnalisiPacchetto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPacchettoConsulenza = table.Column<int>(type: "int", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    IdControlliOrigine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Modalita = table.Column<int>(type: "int", nullable: false),
                    QuantitaAnnua = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Prezzo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisiPacchettiConsulenza", x => x.IdAnalisiPacchetto);
                    table.ForeignKey(
                        name: "FK_AnalisiPacchettiConsulenza_PacchettiConsulenza_IdPacchettoConsulenza",
                        column: x => x.IdPacchettoConsulenza,
                        principalSchema: "cons",
                        principalTable: "PacchettiConsulenza",
                        principalColumn: "IdPacchettoConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalisiPreventiviConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdAnalisiPreventivoConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPreventivoConsulenza = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    IdControlliOrigine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Modalita = table.Column<int>(type: "int", nullable: false),
                    QuantitaAnnua = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    OriginePrezzo = table.Column<int>(type: "int", nullable: false),
                    PrezzoBase = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisiPreventiviConsulenza", x => x.IdAnalisiPreventivoConsulenza);
                    table.ForeignKey(
                        name: "FK_AnalisiPreventiviConsulenza_PreventiviConsulenza_IdPreventivoConsulenza",
                        column: x => x.IdPreventivoConsulenza,
                        principalSchema: "cons",
                        principalTable: "PreventiviConsulenza",
                        principalColumn: "IdPreventivoConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetodiAnalisiConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdMetodoAnalisiConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAnalisiPreventivoConsulenza = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdMetodoArcLab = table.Column<int>(type: "int", nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Denominazione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RiferimentoNormativo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Accreditato = table.Column<bool>(type: "bit", nullable: false),
                    IdLabEsternoArcLab = table.Column<int>(type: "int", nullable: true),
                    LabEsterno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LabEsternoFrase = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodiAnalisiConsulenza", x => x.IdMetodoAnalisiConsulenza);
                    table.ForeignKey(
                        name: "FK_MetodiAnalisiConsulenza_AnalisiPreventiviConsulenza_IdAnalisiPreventivoConsulenza",
                        column: x => x.IdAnalisiPreventivoConsulenza,
                        principalSchema: "cons",
                        principalTable: "AnalisiPreventiviConsulenza",
                        principalColumn: "IdAnalisiPreventivoConsulenza",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiPacchettiConsulenza_IdPacchettoConsulenza",
                schema: "cons",
                table: "AnalisiPacchettiConsulenza",
                column: "IdPacchettoConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisiPreventiviConsulenza_IdPreventivoConsulenza",
                schema: "cons",
                table: "AnalisiPreventiviConsulenza",
                column: "IdPreventivoConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_MetodiAnalisiConsulenza_IdAnalisiPreventivoConsulenza",
                schema: "cons",
                table: "MetodiAnalisiConsulenza",
                column: "IdAnalisiPreventivoConsulenza");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisiPacchettiConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "MetodiAnalisiConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "AnalisiPreventiviConsulenza",
                schema: "cons");

            migrationBuilder.DropColumn(
                name: "DataUltimaModifica",
                schema: "cons",
                table: "PacchettiConsulenza");

            migrationBuilder.DropColumn(
                name: "UltimaModificaDa",
                schema: "cons",
                table: "PacchettiConsulenza");
        }
    }
}
