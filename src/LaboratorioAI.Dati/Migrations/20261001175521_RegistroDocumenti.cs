using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RegistroDocumenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistroDocumenti",
                schema: "log",
                columns: table => new
                {
                    IdRegistroDocumento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ambito = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdEsecuzione = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Riferimento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdLotto = table.Column<int>(type: "int", nullable: true),
                    Esito = table.Column<int>(type: "int", nullable: false),
                    Messaggio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FileOrigine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileArchivio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePalomar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PalomarVerificato = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroDocumenti", x => x.IdRegistroDocumento);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDocumenti_Ambito_DataOra",
                schema: "log",
                table: "RegistroDocumenti",
                columns: new[] { "Ambito", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDocumenti_IdEsecuzione",
                schema: "log",
                table: "RegistroDocumenti",
                column: "IdEsecuzione");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistroDocumenti",
                schema: "log");
        }
    }
}
