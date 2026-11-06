using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class CompensiSociTrasferte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperatoreTrasferteAzw",
                schema: "soci",
                table: "Soci",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrasferteAzwChiusura",
                schema: "soci",
                table: "CompensiSociAnno",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrasferteDaAzw",
                schema: "soci",
                table: "AnniCompensi",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "TrasferteAzw",
                schema: "soci",
                columns: table => new
                {
                    IdTrasfertaAzw = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdViaggioAzw = table.Column<int>(type: "int", nullable: false),
                    IdOperatoreAzw = table.Column<int>(type: "int", nullable: true),
                    Operatore = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    Km = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    CostoKm = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Autostrada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VittoAlloggio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostiDiversi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdMezzoAzw = table.Column<int>(type: "int", nullable: true),
                    Mezzo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TipoMezzo = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrasferteAzw", x => x.IdTrasfertaAzw);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrasferteAzw_Data_Operatore",
                schema: "soci",
                table: "TrasferteAzw",
                columns: new[] { "Data", "Operatore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrasferteAzw",
                schema: "soci");

            migrationBuilder.DropColumn(
                name: "OperatoreTrasferteAzw",
                schema: "soci",
                table: "Soci");

            migrationBuilder.DropColumn(
                name: "TrasferteAzwChiusura",
                schema: "soci",
                table: "CompensiSociAnno");

            migrationBuilder.DropColumn(
                name: "TrasferteDaAzw",
                schema: "soci",
                table: "AnniCompensi");
        }
    }
}
