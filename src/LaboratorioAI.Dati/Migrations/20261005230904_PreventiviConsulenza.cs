using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PreventiviConsulenza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttivitaConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdAttivitaConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoIncarico = table.Column<int>(type: "int", nullable: false),
                    IdTipoCommessa = table.Column<int>(type: "int", nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModalitaPrezzo = table.Column<int>(type: "int", nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TestoOfferta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestoSuccessivi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrezzoProposto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PrezzoSuccessiviProposto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttivitaConsulenza", x => x.IdAttivitaConsulenza);
                });

            migrationBuilder.CreateTable(
                name: "AzwClienti",
                schema: "provv",
                columns: table => new
                {
                    IdAzwCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Citta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Pec = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CodiceSdi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzwClienti", x => x.IdAzwCliente);
                });

            migrationBuilder.CreateTable(
                name: "PacchettiConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdPacchettoConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacchettiConsulenza", x => x.IdPacchettoConsulenza);
                });

            migrationBuilder.CreateTable(
                name: "PreventiviConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdPreventivoConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anno = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    CodiceClienteAzw = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Citta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Pec = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CodiceSdi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: true),
                    SedeNome = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SedeIndirizzo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdContattoArcLab = table.Column<int>(type: "int", nullable: true),
                    ReferenteNome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenteMansione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenteTelefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReferenteEmail = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SettoreAttivita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdPacchettoOrigine = table.Column<int>(type: "int", nullable: true),
                    Oggetto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataOfferta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValiditaGiorni = table.Column<int>(type: "int", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurataAnni = table.Column<int>(type: "int", nullable: false),
                    NumeroRate = table.Column<int>(type: "int", nullable: false),
                    CondizioniPagamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevisioneCondizioni = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdUtenteConsulente = table.Column<int>(type: "int", nullable: true),
                    IdOrganicoConsulente = table.Column<int>(type: "int", nullable: true),
                    ConsulenteNome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdCommerciale = table.Column<int>(type: "int", nullable: true),
                    TotalePrimoAnno = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TotaleAnniSuccessivi = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataAccettazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiviConsulenza", x => x.IdPreventivoConsulenza);
                });

            migrationBuilder.CreateTable(
                name: "ContenutiAttivita",
                schema: "cons",
                columns: table => new
                {
                    IdContenutoAttivita = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAttivitaConsulenza = table.Column<int>(type: "int", nullable: false),
                    Chiave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Testo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Predefinito = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContenutiAttivita", x => x.IdContenutoAttivita);
                    table.ForeignKey(
                        name: "FK_ContenutiAttivita_AttivitaConsulenza_IdAttivitaConsulenza",
                        column: x => x.IdAttivitaConsulenza,
                        principalSchema: "cons",
                        principalTable: "AttivitaConsulenza",
                        principalColumn: "IdAttivitaConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RighePacchettoConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdRigaPacchetto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPacchettoConsulenza = table.Column<int>(type: "int", nullable: false),
                    IdAttivitaConsulenza = table.Column<int>(type: "int", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    DescrizioneLibera = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Contenuti = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ModalitaPrezzo = table.Column<int>(type: "int", nullable: true),
                    Quantita = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PrezzoSuccessivi = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Informativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RighePacchettoConsulenza", x => x.IdRigaPacchetto);
                    table.ForeignKey(
                        name: "FK_RighePacchettoConsulenza_AttivitaConsulenza_IdAttivitaConsulenza",
                        column: x => x.IdAttivitaConsulenza,
                        principalSchema: "cons",
                        principalTable: "AttivitaConsulenza",
                        principalColumn: "IdAttivitaConsulenza",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RighePacchettoConsulenza_PacchettiConsulenza_IdPacchettoConsulenza",
                        column: x => x.IdPacchettoConsulenza,
                        principalSchema: "cons",
                        principalTable: "PacchettiConsulenza",
                        principalColumn: "IdPacchettoConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RighePreventivoConsulenza",
                schema: "cons",
                columns: table => new
                {
                    IdRigaPreventivoConsulenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPreventivoConsulenza = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdAttivitaConsulenza = table.Column<int>(type: "int", nullable: false),
                    IdTipoIncarico = table.Column<int>(type: "int", nullable: false),
                    IdTipoCommessa = table.Column<int>(type: "int", nullable: true),
                    DescrizioneAttivita = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DescrizioneLibera = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TestoOfferta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestoSuccessivi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contenuti = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ModalitaPrezzo = table.Column<int>(type: "int", nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Quantita = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Prezzo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PrezzoSuccessivi = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Informativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RighePreventivoConsulenza", x => x.IdRigaPreventivoConsulenza);
                    table.ForeignKey(
                        name: "FK_RighePreventivoConsulenza_AttivitaConsulenza_IdAttivitaConsulenza",
                        column: x => x.IdAttivitaConsulenza,
                        principalSchema: "cons",
                        principalTable: "AttivitaConsulenza",
                        principalColumn: "IdAttivitaConsulenza",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RighePreventivoConsulenza_PreventiviConsulenza_IdPreventivoConsulenza",
                        column: x => x.IdPreventivoConsulenza,
                        principalSchema: "cons",
                        principalTable: "PreventiviConsulenza",
                        principalColumn: "IdPreventivoConsulenza",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttivitaConsulenza_IdTipoIncarico",
                schema: "cons",
                table: "AttivitaConsulenza",
                column: "IdTipoIncarico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AzwClienti_CodiceCliente",
                schema: "provv",
                table: "AzwClienti",
                column: "CodiceCliente");

            migrationBuilder.CreateIndex(
                name: "IX_ContenutiAttivita_IdAttivitaConsulenza",
                schema: "cons",
                table: "ContenutiAttivita",
                column: "IdAttivitaConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiviConsulenza_Anno_Numero",
                schema: "cons",
                table: "PreventiviConsulenza",
                columns: new[] { "Anno", "Numero" },
                unique: true,
                filter: "[Numero] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RighePacchettoConsulenza_IdAttivitaConsulenza",
                schema: "cons",
                table: "RighePacchettoConsulenza",
                column: "IdAttivitaConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_RighePacchettoConsulenza_IdPacchettoConsulenza",
                schema: "cons",
                table: "RighePacchettoConsulenza",
                column: "IdPacchettoConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_RighePreventivoConsulenza_IdAttivitaConsulenza",
                schema: "cons",
                table: "RighePreventivoConsulenza",
                column: "IdAttivitaConsulenza");

            migrationBuilder.CreateIndex(
                name: "IX_RighePreventivoConsulenza_IdPreventivoConsulenza",
                schema: "cons",
                table: "RighePreventivoConsulenza",
                column: "IdPreventivoConsulenza");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzwClienti",
                schema: "provv");

            migrationBuilder.DropTable(
                name: "ContenutiAttivita",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "RighePacchettoConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "RighePreventivoConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "PacchettiConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "AttivitaConsulenza",
                schema: "cons");

            migrationBuilder.DropTable(
                name: "PreventiviConsulenza",
                schema: "cons");
        }
    }
}
