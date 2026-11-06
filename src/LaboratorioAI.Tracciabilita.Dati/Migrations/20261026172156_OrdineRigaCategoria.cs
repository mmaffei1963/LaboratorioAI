using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class OrdineRigaCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Attiva",
                schema: "trac",
                table: "OrdiniRighe",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdCategoria",
                schema: "trac",
                table: "OrdiniRighe",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniRighe_IdCategoria",
                schema: "trac",
                table: "OrdiniRighe",
                column: "IdCategoria");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdiniRighe_CategorieMateriaPrima_IdCategoria",
                schema: "trac",
                table: "OrdiniRighe",
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
                name: "FK_OrdiniRighe_CategorieMateriaPrima_IdCategoria",
                schema: "trac",
                table: "OrdiniRighe");

            migrationBuilder.DropIndex(
                name: "IX_OrdiniRighe_IdCategoria",
                schema: "trac",
                table: "OrdiniRighe");

            migrationBuilder.DropColumn(
                name: "Attiva",
                schema: "trac",
                table: "OrdiniRighe");

            migrationBuilder.DropColumn(
                name: "IdCategoria",
                schema: "trac",
                table: "OrdiniRighe");
        }
    }
}
