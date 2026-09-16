using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ModuloPreventivi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "preventivi");

            migrationBuilder.CreateTable(
                name: "ListinoAccessori",
                schema: "preventivi",
                columns: table => new
                {
                    IdVoceAccessorio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Prezzo = table.Column<decimal>(type: "money", nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListinoAccessori", x => x.IdVoceAccessorio);
                });

            migrationBuilder.CreateTable(
                name: "Preventivi",
                schema: "preventivi",
                columns: table => new
                {
                    IdPreventivo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<int>(type: "int", nullable: true),
                    Anno = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    ClienteRagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ClientePartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClienteIndirizzo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ClienteCap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ClienteCitta = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ClienteProvincia = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ClienteEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Oggetto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataOfferta = table.Column<DateTime>(type: "date", nullable: true),
                    ValiditaGiorni = table.Column<int>(type: "int", nullable: false),
                    CondizioniPagamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevisioneCondizioni = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TotaleImponibile = table.Column<decimal>(type: "money", nullable: false),
                    AliquotaIva = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RichiedeApprovazione = table.Column<bool>(type: "bit", nullable: false),
                    IdUtenteApprovatore = table.Column<int>(type: "int", nullable: true),
                    DataApprovazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtenteCreatore = table.Column<int>(type: "int", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preventivi", x => x.IdPreventivo);
                });

            migrationBuilder.CreateTable(
                name: "Righe",
                schema: "preventivi",
                columns: table => new
                {
                    IdRigaPreventivo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPreventivo = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    IdPropostaControllo = table.Column<int>(type: "int", nullable: true),
                    IdVoceAccessorio = table.Column<int>(type: "int", nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginePrezzo = table.Column<int>(type: "int", nullable: false),
                    PrezzoBase = table.Column<decimal>(type: "money", nullable: false),
                    ScontoPercentuale = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Quantita = table.Column<int>(type: "int", nullable: false),
                    PrezzoFinale = table.Column<decimal>(type: "money", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Righe", x => x.IdRigaPreventivo);
                    table.ForeignKey(
                        name: "FK_Righe_Preventivi_IdPreventivo",
                        column: x => x.IdPreventivo,
                        principalSchema: "preventivi",
                        principalTable: "Preventivi",
                        principalColumn: "IdPreventivo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigheMetodo",
                schema: "preventivi",
                columns: table => new
                {
                    IdRigaMetodoPreventivo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRigaPreventivo = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdMetodoArcLab = table.Column<int>(type: "int", nullable: true),
                    Libero = table.Column<bool>(type: "bit", nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DenominazioneRiga = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RiferimentoNormativo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CostoMetodoEuro = table.Column<decimal>(type: "money", nullable: true),
                    Accreditato = table.Column<bool>(type: "bit", nullable: false),
                    CifreSignificative = table.Column<int>(type: "int", nullable: true),
                    MaxDecimali = table.Column<int>(type: "int", nullable: true),
                    IdLabEsternoArcLab = table.Column<int>(type: "int", nullable: true),
                    LabEsterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LabEsternoFrase = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheMetodo", x => x.IdRigaMetodoPreventivo);
                    table.ForeignKey(
                        name: "FK_RigheMetodo_Righe_IdRigaPreventivo",
                        column: x => x.IdRigaPreventivo,
                        principalSchema: "preventivi",
                        principalTable: "Righe",
                        principalColumn: "IdRigaPreventivo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "preventivi",
                table: "ListinoAccessori",
                columns: new[] { "IdVoceAccessorio", "Attivo", "Codice", "Descrizione", "Prezzo", "UnitaMisura" },
                values: new object[,]
                {
                    { 1, true, "GIUDIZIO_CONF", "Giudizio di conformità sul rapporto di prova", 10m, "cad" },
                    { 2, true, "RIEMISSIONE_RDP", "Riemissione rapporto di prova su richiesta del cliente", 20m, "cad" },
                    { 3, true, "CONSERVAZIONE", "Conservazione campione oltre 20 giorni per cause del cliente", 0.50m, "giorno" },
                    { 4, true, "CAMPIONAMENTO", "Campionamento eseguito da operatore MARE.A", 0m, "cad" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListinoAccessori_Codice",
                schema: "preventivi",
                table: "ListinoAccessori",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preventivi_Anno_Numero",
                schema: "preventivi",
                table: "Preventivi",
                columns: new[] { "Anno", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_Preventivi_IdClienteArcLab",
                schema: "preventivi",
                table: "Preventivi",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_Preventivi_Stato_Attivo",
                schema: "preventivi",
                table: "Preventivi",
                columns: new[] { "Stato", "Attivo" });

            migrationBuilder.CreateIndex(
                name: "IX_Righe_IdPreventivo",
                schema: "preventivi",
                table: "Righe",
                column: "IdPreventivo");

            migrationBuilder.CreateIndex(
                name: "IX_RigheMetodo_IdRigaPreventivo",
                schema: "preventivi",
                table: "RigheMetodo",
                column: "IdRigaPreventivo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListinoAccessori",
                schema: "preventivi");

            migrationBuilder.DropTable(
                name: "RigheMetodo",
                schema: "preventivi");

            migrationBuilder.DropTable(
                name: "Righe",
                schema: "preventivi");

            migrationBuilder.DropTable(
                name: "Preventivi",
                schema: "preventivi");
        }
    }
}
