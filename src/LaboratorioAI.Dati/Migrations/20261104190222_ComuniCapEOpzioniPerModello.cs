using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ComuniCapEOpzioniPerModello : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cap",
                schema: "app",
                table: "Comuni");

            migrationBuilder.CreateTable(
                name: "ComuniCap",
                schema: "app",
                columns: table => new
                {
                    IdComuneCap = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdComune = table.Column<int>(type: "int", nullable: false),
                    Cap = table.Column<string>(type: "nchar(5)", fixedLength: true, maxLength: 5, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComuniCap", x => x.IdComuneCap);
                    table.ForeignKey(
                        name: "FK_ComuniCap_Comuni_IdComune",
                        column: x => x.IdComune,
                        principalSchema: "app",
                        principalTable: "Comuni",
                        principalColumn: "IdComune",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComuniCap_Cap",
                schema: "app",
                table: "ComuniCap",
                column: "Cap");

            migrationBuilder.CreateIndex(
                name: "IX_ComuniCap_IdComune_Cap",
                schema: "app",
                table: "ComuniCap",
                columns: new[] { "IdComune", "Cap" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComuniCap_IdComune_Ordine",
                schema: "app",
                table: "ComuniCap",
                columns: new[] { "IdComune", "Ordine" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComuniCap",
                schema: "app");

            migrationBuilder.AddColumn<string>(
                name: "Cap",
                schema: "app",
                table: "Comuni",
                type: "nchar(5)",
                fixedLength: true,
                maxLength: 5,
                nullable: true);
        }
    }
}
