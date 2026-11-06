using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class OpzioniVocePerModello : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpzioniVoce_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce",
                column: "IdModelloDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_OpzioniVoce_IdVoceChecklist_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce",
                columns: new[] { "IdVoceChecklist", "IdModelloDocumento" });

            migrationBuilder.AddForeignKey(
                name: "FK_OpzioniVoce_ModelliDocumento_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce",
                column: "IdModelloDocumento",
                principalSchema: "man",
                principalTable: "ModelliDocumento",
                principalColumn: "IdModelloDocumento",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpzioniVoce_ModelliDocumento_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce");

            migrationBuilder.DropIndex(
                name: "IX_OpzioniVoce_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce");

            migrationBuilder.DropIndex(
                name: "IX_OpzioniVoce_IdVoceChecklist_IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce");

            migrationBuilder.DropColumn(
                name: "IdModelloDocumento",
                schema: "man",
                table: "OpzioniVoce");
        }
    }
}
