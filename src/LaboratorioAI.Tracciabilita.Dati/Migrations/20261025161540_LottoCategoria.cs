using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class LottoCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCategoria",
                schema: "trac",
                table: "Lotti",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_IdCategoria",
                schema: "trac",
                table: "Lotti",
                column: "IdCategoria");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotti_CategorieMateriaPrima_IdCategoria",
                schema: "trac",
                table: "Lotti",
                column: "IdCategoria",
                principalSchema: "trac",
                principalTable: "CategorieMateriaPrima",
                principalColumn: "IdCategoria",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotti_CategorieMateriaPrima_IdCategoria",
                schema: "trac",
                table: "Lotti");

            migrationBuilder.DropIndex(
                name: "IX_Lotti_IdCategoria",
                schema: "trac",
                table: "Lotti");

            migrationBuilder.DropColumn(
                name: "IdCategoria",
                schema: "trac",
                table: "Lotti");
        }
    }
}
