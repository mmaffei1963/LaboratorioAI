using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RicetteIngredienti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Etichetta",
                schema: "trac",
                table: "RicetteRighe",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LottiPerFornitore",
                schema: "trac",
                table: "RicetteRighe",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RicetteRigheCategorie",
                schema: "trac",
                columns: table => new
                {
                    IdRicettaRigaCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdRicettaRiga = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicetteRigheCategorie", x => x.IdRicettaRigaCategoria);
                    table.ForeignKey(
                        name: "FK_RicetteRigheCategorie_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteRigheCategorie_CategorieMateriaPrima_IdCategoria",
                        column: x => x.IdCategoria,
                        principalSchema: "trac",
                        principalTable: "CategorieMateriaPrima",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteRigheCategorie_RicetteRighe_IdRicettaRiga",
                        column: x => x.IdRicettaRiga,
                        principalSchema: "trac",
                        principalTable: "RicetteRighe",
                        principalColumn: "IdRicettaRiga",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRigheCategorie_IdAzienda",
                schema: "trac",
                table: "RicetteRigheCategorie",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRigheCategorie_IdCategoria",
                schema: "trac",
                table: "RicetteRigheCategorie",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRigheCategorie_IdRicettaRiga_IdCategoria",
                schema: "trac",
                table: "RicetteRigheCategorie",
                columns: new[] { "IdRicettaRiga", "IdCategoria" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RicetteRigheCategorie",
                schema: "trac");

            migrationBuilder.DropColumn(
                name: "Etichetta",
                schema: "trac",
                table: "RicetteRighe");

            migrationBuilder.DropColumn(
                name: "LottiPerFornitore",
                schema: "trac",
                table: "RicetteRighe");
        }
    }
}
