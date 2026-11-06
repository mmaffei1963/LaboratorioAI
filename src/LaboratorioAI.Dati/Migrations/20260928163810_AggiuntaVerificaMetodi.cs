using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaVerificaMetodi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TestoEstratto",
                schema: "labesterni",
                table: "Revisioni",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerificheMetodo",
                schema: "labesterni",
                columns: table => new
                {
                    IdVerifica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAccreditamento = table.Column<int>(type: "int", nullable: false),
                    IdLabEstMetodoArcLab = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdRevisioneVerificata = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataVerifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtenteVerifica = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificheMetodo", x => x.IdVerifica);
                    table.ForeignKey(
                        name: "FK_VerificheMetodo_Accreditamenti_IdAccreditamento",
                        column: x => x.IdAccreditamento,
                        principalSchema: "labesterni",
                        principalTable: "Accreditamenti",
                        principalColumn: "IdAccreditamento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificheMetodo_IdAccreditamento_IdLabEstMetodoArcLab",
                schema: "labesterni",
                table: "VerificheMetodo",
                columns: new[] { "IdAccreditamento", "IdLabEstMetodoArcLab" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificheMetodo",
                schema: "labesterni");

            migrationBuilder.DropColumn(
                name: "TestoEstratto",
                schema: "labesterni",
                table: "Revisioni");
        }
    }
}
