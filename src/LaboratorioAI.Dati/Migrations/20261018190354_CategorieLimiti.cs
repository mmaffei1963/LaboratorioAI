using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CategorieLimiti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategorieNormative",
                schema: "limiti",
                columns: table => new
                {
                    IdCategoriaNormativa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Famiglia = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Chiave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Attiva = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorieNormative", x => x.IdCategoriaNormativa);
                });

            migrationBuilder.CreateTable(
                name: "MatriciCategorie",
                schema: "limiti",
                columns: table => new
                {
                    IdAbbinamentoMatriceCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMatriceArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeMatrice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdCategoriaNormativa = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    PropostoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DataProposta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConfermatoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IdUtenteConferma = table.Column<int>(type: "int", nullable: true),
                    DataConferma = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatriciCategorie", x => x.IdAbbinamentoMatriceCategoria);
                    table.ForeignKey(
                        name: "FK_MatriciCategorie_CategorieNormative_IdCategoriaNormativa",
                        column: x => x.IdCategoriaNormativa,
                        principalSchema: "limiti",
                        principalTable: "CategorieNormative",
                        principalColumn: "IdCategoriaNormativa",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategorieNormative_Famiglia_Chiave",
                schema: "limiti",
                table: "CategorieNormative",
                columns: new[] { "Famiglia", "Chiave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatriciCategorie_IdCategoriaNormativa",
                schema: "limiti",
                table: "MatriciCategorie",
                column: "IdCategoriaNormativa");

            migrationBuilder.CreateIndex(
                name: "IX_MatriciCategorie_IdMatriceArcLab_Stato",
                schema: "limiti",
                table: "MatriciCategorie",
                columns: new[] { "IdMatriceArcLab", "Stato" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatriciCategorie",
                schema: "limiti");

            migrationBuilder.DropTable(
                name: "CategorieNormative",
                schema: "limiti");
        }
    }
}
