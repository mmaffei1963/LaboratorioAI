using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class RichiesteAnalisiPrivati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RichiesteAnalisiPrivati",
                schema: "preventivi",
                columns: table => new
                {
                    IdRichiestaAnalisiPrivato = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    NomeCognome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RitiroInLaboratorio = table.Column<bool>(type: "bit", nullable: false),
                    Pagamento = table.Column<int>(type: "int", nullable: false),
                    IdMatriceArcLab = table.Column<int>(type: "int", nullable: true),
                    DescrizioneCampione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PuntoPrelievo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataOraPrelievo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdImballaggioArcLab = table.Column<int>(type: "int", nullable: true),
                    Contenitore = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContenitoreDelLaboratorio = table.Column<bool>(type: "bit", nullable: true),
                    TemperaturaRicevimento = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    TempiConsegnaGiorni = table.Column<int>(type: "int", nullable: false),
                    GiudizioRichiesto = table.Column<bool>(type: "bit", nullable: false),
                    NormaGiudizio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplementoGiudizio = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AliquotaIva = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotaleImponibile = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotaleIvaInclusa = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtenteCreatore = table.Column<int>(type: "int", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RichiesteAnalisiPrivati", x => x.IdRichiestaAnalisiPrivato);
                });

            migrationBuilder.CreateTable(
                name: "RigheRichiestaAnalisiPrivati",
                schema: "preventivi",
                columns: table => new
                {
                    IdRigaRichiestaAnalisiPrivato = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRichiestaAnalisiPrivato = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    IdCommessaArcLab = table.Column<int>(type: "int", nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PrezzoImponibile = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PrezzoIvaInclusa = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TempiGiorni = table.Column<int>(type: "int", nullable: false),
                    MetodiJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheRichiestaAnalisiPrivati", x => x.IdRigaRichiestaAnalisiPrivato);
                    table.ForeignKey(
                        name: "FK_RigheRichiestaAnalisiPrivati_RichiesteAnalisiPrivati_IdRichiestaAnalisiPrivato",
                        column: x => x.IdRichiestaAnalisiPrivato,
                        principalSchema: "preventivi",
                        principalTable: "RichiesteAnalisiPrivati",
                        principalColumn: "IdRichiestaAnalisiPrivato",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RichiesteAnalisiPrivati_Anno_Numero",
                schema: "preventivi",
                table: "RichiesteAnalisiPrivati",
                columns: new[] { "Anno", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RigheRichiestaAnalisiPrivati_IdRichiestaAnalisiPrivato",
                schema: "preventivi",
                table: "RigheRichiestaAnalisiPrivati",
                column: "IdRichiestaAnalisiPrivato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RigheRichiestaAnalisiPrivati",
                schema: "preventivi");

            migrationBuilder.DropTable(
                name: "RichiesteAnalisiPrivati",
                schema: "preventivi");
        }
    }
}
