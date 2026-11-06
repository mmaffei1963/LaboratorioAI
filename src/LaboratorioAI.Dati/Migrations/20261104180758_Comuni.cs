using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Comuni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comuni",
                schema: "app",
                columns: table => new
                {
                    IdComune = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceIstat = table.Column<string>(type: "nchar(6)", fixedLength: true, maxLength: 6, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SiglaProvincia = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Provincia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CodiceRegione = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Regione = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodiceCatastale = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: true),
                    Cap = table.Column<string>(type: "nchar(5)", fixedLength: true, maxLength: 5, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comuni", x => x.IdComune);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comuni_CodiceIstat",
                schema: "app",
                table: "Comuni",
                column: "CodiceIstat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comuni_Nome_SiglaProvincia",
                schema: "app",
                table: "Comuni",
                columns: new[] { "Nome", "SiglaProvincia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comuni_SiglaProvincia",
                schema: "app",
                table: "Comuni",
                column: "SiglaProvincia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comuni",
                schema: "app");
        }
    }
}
