using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccettazioneCodLotto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodLotto",
                schema: "accettazione",
                table: "Lotti",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contatori",
                schema: "accettazione",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ProssimoValore = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contatori", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "accettazione",
                table: "Contatori",
                columns: new[] { "Id", "ProssimoValore" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_CodLotto",
                schema: "accettazione",
                table: "Lotti",
                column: "CodLotto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contatori",
                schema: "accettazione");

            migrationBuilder.DropIndex(
                name: "IX_Lotti_CodLotto",
                schema: "accettazione",
                table: "Lotti");

            migrationBuilder.DropColumn(
                name: "CodLotto",
                schema: "accettazione",
                table: "Lotti");
        }
    }
}
