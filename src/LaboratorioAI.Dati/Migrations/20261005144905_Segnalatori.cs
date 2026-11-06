using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Segnalatori : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Segnalatori",
                schema: "provv",
                columns: table => new
                {
                    IdSegnalatore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Segnalatori", x => x.IdSegnalatore);
                });

            migrationBuilder.CreateTable(
                name: "SegnalazioniClienti",
                schema: "provv",
                columns: table => new
                {
                    IdSegnalazioneCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCommerciale = table.Column<int>(type: "int", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdSegnalatore = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegnalazioniClienti", x => x.IdSegnalazioneCliente);
                    table.ForeignKey(
                        name: "FK_SegnalazioniClienti_Commerciali_IdCommerciale",
                        column: x => x.IdCommerciale,
                        principalSchema: "provv",
                        principalTable: "Commerciali",
                        principalColumn: "IdCommerciale",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SegnalazioniClienti_Segnalatori_IdSegnalatore",
                        column: x => x.IdSegnalatore,
                        principalSchema: "provv",
                        principalTable: "Segnalatori",
                        principalColumn: "IdSegnalatore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Segnalatori_Nome",
                schema: "provv",
                table: "Segnalatori",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_SegnalazioniClienti_IdCommerciale_CodiceCliente",
                schema: "provv",
                table: "SegnalazioniClienti",
                columns: new[] { "IdCommerciale", "CodiceCliente" });

            migrationBuilder.CreateIndex(
                name: "IX_SegnalazioniClienti_IdSegnalatore",
                schema: "provv",
                table: "SegnalazioniClienti",
                column: "IdSegnalatore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SegnalazioniClienti",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "Segnalatori",
                schema: "provv");
        }
    }
}
