using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class VociPreventiviAmministrabili : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetodiPagamento",
                schema: "preventivi",
                columns: table => new
                {
                    IdMetodoPagamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descrizione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodiPagamento", x => x.IdMetodoPagamento);
                });

            migrationBuilder.UpdateData(
                schema: "preventivi",
                table: "ListinoAccessori",
                keyColumn: "IdVoceAccessorio",
                keyValue: 4,
                column: "Descrizione",
                value: "Prelievo eseguito da operatore MARE.A");

            migrationBuilder.InsertData(
                schema: "preventivi",
                table: "MetodiPagamento",
                columns: new[] { "IdMetodoPagamento", "Attivo", "Descrizione", "Ordine" },
                values: new object[] { 1, true, "Bonifico bancario vista fattura", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetodiPagamento",
                schema: "preventivi");

            migrationBuilder.UpdateData(
                schema: "preventivi",
                table: "ListinoAccessori",
                keyColumn: "IdVoceAccessorio",
                keyValue: 4,
                column: "Descrizione",
                value: "Campionamento eseguito da operatore MARE.A");
        }
    }
}
