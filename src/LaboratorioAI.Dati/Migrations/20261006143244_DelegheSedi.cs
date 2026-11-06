using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class DelegheSedi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TuttiInsediamenti",
                schema: "cons",
                table: "DelegheClienti",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DelegheInsediamenti",
                schema: "cons",
                columns: table => new
                {
                    IdDelegaInsediamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDelegaCliente = table.Column<int>(type: "int", nullable: false),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Sede = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Comune = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegheInsediamenti", x => x.IdDelegaInsediamento);
                    table.ForeignKey(
                        name: "FK_DelegheInsediamenti_DelegheClienti_IdDelegaCliente",
                        column: x => x.IdDelegaCliente,
                        principalSchema: "cons",
                        principalTable: "DelegheClienti",
                        principalColumn: "IdDelegaCliente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegheInsediamenti_IdDelegaCliente",
                schema: "cons",
                table: "DelegheInsediamenti",
                column: "IdDelegaCliente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegheInsediamenti",
                schema: "cons");

            migrationBuilder.DropColumn(
                name: "TuttiInsediamenti",
                schema: "cons",
                table: "DelegheClienti");
        }
    }
}
