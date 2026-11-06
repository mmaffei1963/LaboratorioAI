using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ClientiTolti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientiTolti",
                schema: "clienti",
                columns: table => new
                {
                    IdClienteTolto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    CodiceClienteSuccessore = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataUscita = table.Column<DateTime>(type: "date", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeUtente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataRegistrazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnullataDa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientiTolti", x => x.IdClienteTolto);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientiTolti_CodiceCliente",
                schema: "clienti",
                table: "ClientiTolti",
                column: "CodiceCliente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientiTolti",
                schema: "clienti");
        }
    }
}
