using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ApprovatoriManualistica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovatoriTipoManuale",
                schema: "man",
                columns: table => new
                {
                    IdApprovatoreTipoManuale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovatoriTipoManuale", x => x.IdApprovatoreTipoManuale);
                    table.ForeignKey(
                        name: "FK_ApprovatoriTipoManuale_TipiManuale_IdTipoManuale",
                        column: x => x.IdTipoManuale,
                        principalSchema: "man",
                        principalTable: "TipiManuale",
                        principalColumn: "IdTipoManuale",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovatoriTipoManuale_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovatoriTipoManuale_IdTipoManuale_IdUtente",
                schema: "man",
                table: "ApprovatoriTipoManuale",
                columns: new[] { "IdTipoManuale", "IdUtente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovatoriTipoManuale_IdUtente",
                schema: "man",
                table: "ApprovatoriTipoManuale",
                column: "IdUtente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovatoriTipoManuale",
                schema: "man");
        }
    }
}
