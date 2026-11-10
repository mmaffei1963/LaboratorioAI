using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class VociDocumentiVenditaAzw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VociDocumentiVenditaAzw",
                schema: "dir",
                columns: table => new
                {
                    IdVoceDocumentoVenditaAzw = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoDocumento = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    AnnoDocumento = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataDocumento = table.Column<DateTime>(type: "date", nullable: false),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumeroRiga = table.Column<int>(type: "int", nullable: false),
                    CodiceArticolo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Sconto = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Importo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Voce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Commessa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VociDocumentiVenditaAzw", x => x.IdVoceDocumentoVenditaAzw);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VociDocumentiVenditaAzw_CodiceCliente",
                schema: "dir",
                table: "VociDocumentiVenditaAzw",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_VociDocumentiVenditaAzw_DataDocumento",
                schema: "dir",
                table: "VociDocumentiVenditaAzw",
                column: "DataDocumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VociDocumentiVenditaAzw",
                schema: "dir");
        }
    }
}
