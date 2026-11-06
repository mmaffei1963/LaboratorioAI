using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreventiviConsulenzaBlocchi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueFasi",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase1Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase1Titolo",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Condizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "Fase2Titolo",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "TotaleFase2AnniSuccessivi",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.DropColumn(
                name: "TotaleFase2PrimoAnno",
                schema: "cons",
                table: "PreventiviConsulenza");

            migrationBuilder.RenameColumn(
                name: "Fase",
                schema: "cons",
                table: "RighePreventivoConsulenza",
                newName: "Blocco");

            migrationBuilder.CreateTable(
                name: "BlocchiPreventivoConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdBloccoPreventivoConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPreventivoConsulenza = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Titolo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlocchiPreventivoConsulenza", x => x.IdBloccoPreventivoConsulenza);
                    table.ForeignKey(
                        name: "FK_BlocchiPreventivoConsulenza_PreventiviConsulenza_IdPreventivoConsulenza",
                        column: x => x.IdPreventivoConsulenza,
                        principalSchema: "cons",
                        principalTable: "PreventiviConsulenza",
                        principalColumn: "IdPreventivoConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlocchiPreventivoConsulenza_IdPreventivoConsulenza_Numero",
                schema: "cons",
                table: "BlocchiPreventivoConsulenza",
                columns: new[] { "IdPreventivoConsulenza", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlocchiPreventivoConsulenza",
                schema: "cons");

            migrationBuilder.RenameColumn(
                name: "Blocco",
                schema: "cons",
                table: "RighePreventivoConsulenza",
                newName: "Fase");

            migrationBuilder.AddColumn<bool>(
                name: "DueFasi",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Fase1Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase1Titolo",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Condizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Descrizione",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fase2Titolo",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotaleFase2AnniSuccessivi",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotaleFase2PrimoAnno",
                schema: "cons",
                table: "PreventiviConsulenza",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
