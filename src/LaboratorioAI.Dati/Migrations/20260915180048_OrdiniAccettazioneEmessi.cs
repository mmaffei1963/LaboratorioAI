using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class OrdiniAccettazioneEmessi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdiniEmessi",
                schema: "accettazione",
                columns: table => new
                {
                    IdOrdineAccettazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    CodLotto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NumeroCampioni = table.Column<int>(type: "int", nullable: false),
                    DataEmissione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    InviatoPortale = table.Column<bool>(type: "bit", nullable: false),
                    DataInvioPortale = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdiniEmessi", x => x.IdOrdineAccettazione);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniEmessi_IdLotto",
                schema: "accettazione",
                table: "OrdiniEmessi",
                column: "IdLotto",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdiniEmessi",
                schema: "accettazione");
        }
    }
}
