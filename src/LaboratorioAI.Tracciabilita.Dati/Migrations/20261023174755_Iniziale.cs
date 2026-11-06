using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LaboratorioAI.Tracciabilita.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Iniziale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trac");

            migrationBuilder.CreateTable(
                name: "Allergeni",
                schema: "trac",
                columns: table => new
                {
                    IdAllergene = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergeni", x => x.IdAllergene);
                });

            migrationBuilder.CreateTable(
                name: "Aziende",
                schema: "trac",
                columns: table => new
                {
                    IdAzienda = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Settore = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataAttivazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataInizioChiusura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataFineConservazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxInsediamenti = table.Column<int>(type: "int", nullable: false),
                    MaxUtenti = table.Column<int>(type: "int", nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aziende", x => x.IdAzienda);
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "trac",
                columns: table => new
                {
                    IdAudit = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: true),
                    IdInsediamento = table.Column<int>(type: "int", nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Azione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Entita = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdEntita = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Prima = table.Column<string>(type: "nvarchar(max)", maxLength: 300, nullable: true),
                    Dopo = table.Column<string>(type: "nvarchar(max)", maxLength: 300, nullable: true),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.IdAudit);
                    table.ForeignKey(
                        name: "FK_Audit_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampiAggiuntivi",
                schema: "trac",
                columns: table => new
                {
                    IdCampo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Entita = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Obbligatorio = table.Column<bool>(type: "bit", nullable: false),
                    Opzioni = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampiAggiuntivi", x => x.IdCampo);
                    table.ForeignKey(
                        name: "FK_CampiAggiuntivi_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clienti",
                schema: "trac",
                columns: table => new
                {
                    IdCliente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Citta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Provincia = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CodiceEsterno = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clienti", x => x.IdCliente);
                    table.ForeignKey(
                        name: "FK_Clienti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fornitori",
                schema: "trac",
                columns: table => new
                {
                    IdFornitore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    RagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Citta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Provincia = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CodiceEsterno = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornitori", x => x.IdFornitore);
                    table.ForeignKey(
                        name: "FK_Fornitori_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImpostazioniAziende",
                schema: "trac",
                columns: table => new
                {
                    IdImpostazioni = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    FormatoLotto = table.Column<int>(type: "int", nullable: false),
                    IncludiSiglaSedeNelLotto = table.Column<bool>(type: "bit", nullable: false),
                    PiuLottiAlGiorno = table.Column<bool>(type: "bit", nullable: false),
                    QuantitaConsumiObbligatorie = table.Column<bool>(type: "bit", nullable: false),
                    CriterioProposta = table.Column<int>(type: "int", nullable: false),
                    GiorniPreavvisoScadenza = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpostazioniAziende", x => x.IdImpostazioni);
                    table.ForeignKey(
                        name: "FK_ImpostazioniAziende_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Insediamenti",
                schema: "trac",
                columns: table => new
                {
                    IdInsediamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insediamenti", x => x.IdInsediamento);
                    table.ForeignKey(
                        name: "FK_Insediamenti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfiliControllo",
                schema: "trac",
                columns: table => new
                {
                    IdProfiloControllo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfiliControllo", x => x.IdProfiloControllo);
                    table.ForeignKey(
                        name: "FK_ProfiliControllo_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Utenti",
                schema: "trac",
                columns: table => new
                {
                    IdUtente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ImprontaPassword = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    EGestore = table.Column<bool>(type: "bit", nullable: false),
                    UltimoAccesso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utenti", x => x.IdUtente);
                    table.ForeignKey(
                        name: "FK_Utenti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValoriCampi",
                schema: "trac",
                columns: table => new
                {
                    IdValoreCampo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdCampo = table.Column<int>(type: "int", nullable: false),
                    IdEntita = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoriCampi", x => x.IdValoreCampo);
                    table.ForeignKey(
                        name: "FK_ValoriCampi_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValoriCampi_CampiAggiuntivi_IdCampo",
                        column: x => x.IdCampo,
                        principalSchema: "trac",
                        principalTable: "CampiAggiuntivi",
                        principalColumn: "IdCampo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContatoriLotti",
                schema: "trac",
                columns: table => new
                {
                    IdContatore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: false),
                    Ultimo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatoriLotti", x => x.IdContatore);
                    table.ForeignKey(
                        name: "FK_ContatoriLotti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContatoriLotti_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ordini",
                schema: "trac",
                columns: table => new
                {
                    IdOrdine = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DataOrdine = table.Column<DateOnly>(type: "date", nullable: false),
                    DataConsegnaPrevista = table.Column<DateOnly>(type: "date", nullable: true),
                    IdFornitore = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ordini", x => x.IdOrdine);
                    table.ForeignKey(
                        name: "FK_Ordini_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ordini_Fornitori_IdFornitore",
                        column: x => x.IdFornitore,
                        principalSchema: "trac",
                        principalTable: "Fornitori",
                        principalColumn: "IdFornitore",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ordini_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Spedizioni",
                schema: "trac",
                columns: table => new
                {
                    IdSpedizione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdCliente = table.Column<int>(type: "int", nullable: false),
                    DataSpedizione = table.Column<DateOnly>(type: "date", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataDocumento = table.Column<DateOnly>(type: "date", nullable: true),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spedizioni", x => x.IdSpedizione);
                    table.ForeignKey(
                        name: "FK_Spedizioni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Spedizioni_Clienti_IdCliente",
                        column: x => x.IdCliente,
                        principalSchema: "trac",
                        principalTable: "Clienti",
                        principalColumn: "IdCliente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Spedizioni_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trasferimenti",
                schema: "trac",
                columns: table => new
                {
                    IdTrasferimento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamentoOrigine = table.Column<int>(type: "int", nullable: false),
                    IdInsediamentoDestinazione = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trasferimenti", x => x.IdTrasferimento);
                    table.CheckConstraint("CK_Trasferimenti_SediDiverse", "[IdInsediamentoOrigine] <> [IdInsediamentoDestinazione]");
                    table.ForeignKey(
                        name: "FK_Trasferimenti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trasferimenti_Insediamenti_IdInsediamentoDestinazione",
                        column: x => x.IdInsediamentoDestinazione,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trasferimenti_Insediamenti_IdInsediamentoOrigine",
                        column: x => x.IdInsediamentoOrigine,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategorieMateriaPrima",
                schema: "trac",
                columns: table => new
                {
                    IdCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdProfiloControllo = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorieMateriaPrima", x => x.IdCategoria);
                    table.ForeignKey(
                        name: "FK_CategorieMateriaPrima_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategorieMateriaPrima_ProfiliControllo_IdProfiloControllo",
                        column: x => x.IdProfiloControllo,
                        principalSchema: "trac",
                        principalTable: "ProfiliControllo",
                        principalColumn: "IdProfiloControllo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfiliControlloRighe",
                schema: "trac",
                columns: table => new
                {
                    IdProfiloControlloRiga = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdProfiloControllo = table.Column<int>(type: "int", nullable: false),
                    Controllo = table.Column<int>(type: "int", nullable: false),
                    Obbligatorio = table.Column<bool>(type: "bit", nullable: false),
                    Istruzione = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfiliControlloRighe", x => x.IdProfiloControlloRiga);
                    table.ForeignKey(
                        name: "FK_ProfiliControlloRighe_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfiliControlloRighe_ProfiliControllo_IdProfiloControllo",
                        column: x => x.IdProfiloControllo,
                        principalSchema: "trac",
                        principalTable: "ProfiliControllo",
                        principalColumn: "IdProfiloControllo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Esportazioni",
                schema: "trac",
                columns: table => new
                {
                    IdEsportazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    DataRichiesta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPronta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Scadenza = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PercorsoFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImprontaSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Errore = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Esportazioni", x => x.IdEsportazione);
                    table.ForeignKey(
                        name: "FK_Esportazioni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Esportazioni_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "trac",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UtentiInsediamenti",
                schema: "trac",
                columns: table => new
                {
                    IdUtenteInsediamento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: true),
                    Ruolo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtentiInsediamenti", x => x.IdUtenteInsediamento);
                    table.ForeignKey(
                        name: "FK_UtentiInsediamenti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtentiInsediamenti_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtentiInsediamenti_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "trac",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Articoli",
                schema: "trac",
                columns: table => new
                {
                    IdArticolo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Codice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Bio = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articoli", x => x.IdArticolo);
                    table.ForeignKey(
                        name: "FK_Articoli_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articoli_CategorieMateriaPrima_IdCategoria",
                        column: x => x.IdCategoria,
                        principalSchema: "trac",
                        principalTable: "CategorieMateriaPrima",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArticoliAllergeni",
                schema: "trac",
                columns: table => new
                {
                    IdArticoloAllergene = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    IdAllergene = table.Column<int>(type: "int", nullable: false),
                    SoloTracce = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticoliAllergeni", x => x.IdArticoloAllergene);
                    table.ForeignKey(
                        name: "FK_ArticoliAllergeni_Allergeni_IdAllergene",
                        column: x => x.IdAllergene,
                        principalSchema: "trac",
                        principalTable: "Allergeni",
                        principalColumn: "IdAllergene",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticoliAllergeni_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticoliAllergeni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArticoliFornitori",
                schema: "trac",
                columns: table => new
                {
                    IdArticoloFornitore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    IdFornitore = table.Column<int>(type: "int", nullable: false),
                    CodiceDelFornitore = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Preferito = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticoliFornitori", x => x.IdArticoloFornitore);
                    table.ForeignKey(
                        name: "FK_ArticoliFornitori_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticoliFornitori_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticoliFornitori_Fornitori_IdFornitore",
                        column: x => x.IdFornitore,
                        principalSchema: "trac",
                        principalTable: "Fornitori",
                        principalColumn: "IdFornitore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lotti",
                schema: "trac",
                columns: table => new
                {
                    IdLotto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    IdFornitore = table.Column<int>(type: "int", nullable: true),
                    Codice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    GiornoLottoInterno = table.Column<DateOnly>(type: "date", nullable: true),
                    DataProduzioneFornitore = table.Column<DateOnly>(type: "date", nullable: true),
                    DataScadenza = table.Column<DateOnly>(type: "date", nullable: true),
                    ETmc = table.Column<bool>(type: "bit", nullable: false),
                    EnteBio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MotivoBlocco = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdAccettazione = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotti", x => x.IdLotto);
                    table.ForeignKey(
                        name: "FK_Lotti_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lotti_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lotti_Fornitori_IdFornitore",
                        column: x => x.IdFornitore,
                        principalSchema: "trac",
                        principalTable: "Fornitori",
                        principalColumn: "IdFornitore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrdiniRighe",
                schema: "trac",
                columns: table => new
                {
                    IdOrdineRiga = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdOrdine = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Colli = table.Column<int>(type: "int", nullable: true),
                    Prezzo = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdiniRighe", x => x.IdOrdineRiga);
                    table.ForeignKey(
                        name: "FK_OrdiniRighe_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdiniRighe_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdiniRighe_Ordini_IdOrdine",
                        column: x => x.IdOrdine,
                        principalSchema: "trac",
                        principalTable: "Ordini",
                        principalColumn: "IdOrdine",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ricette",
                schema: "trac",
                columns: table => new
                {
                    IdRicetta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Attiva = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricette", x => x.IdRicetta);
                    table.ForeignKey(
                        name: "FK_Ricette_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ricette_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LottiInUso",
                schema: "trac",
                columns: table => new
                {
                    IdLottoInUso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: false),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    Inizio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fine = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LottiInUso", x => x.IdLottoInUso);
                    table.ForeignKey(
                        name: "FK_LottiInUso_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LottiInUso_CategorieMateriaPrima_IdCategoria",
                        column: x => x.IdCategoria,
                        principalSchema: "trac",
                        principalTable: "CategorieMateriaPrima",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LottiInUso_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LottiInUso_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Richiami",
                schema: "trac",
                columns: table => new
                {
                    IdRichiamo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    IdLottoOrigine = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataAvvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataChiusura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SecondiImpiegati = table.Column<int>(type: "int", nullable: true),
                    RapportoJson = table.Column<string>(type: "nvarchar(max)", maxLength: 300, nullable: true),
                    Esito = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Richiami", x => x.IdRichiamo);
                    table.ForeignKey(
                        name: "FK_Richiami_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Richiami_Lotti_IdLottoOrigine",
                        column: x => x.IdLottoOrigine,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpedizioniRighe",
                schema: "trac",
                columns: table => new
                {
                    IdSpedizioneRiga = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdSpedizione = table.Column<int>(type: "int", nullable: false),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpedizioniRighe", x => x.IdSpedizioneRiga);
                    table.ForeignKey(
                        name: "FK_SpedizioniRighe_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpedizioniRighe_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpedizioniRighe_Spedizioni_IdSpedizione",
                        column: x => x.IdSpedizione,
                        principalSchema: "trac",
                        principalTable: "Spedizioni",
                        principalColumn: "IdSpedizione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrasferimentiRighe",
                schema: "trac",
                columns: table => new
                {
                    IdTrasferimentoRiga = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdTrasferimento = table.Column<int>(type: "int", nullable: false),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrasferimentiRighe", x => x.IdTrasferimentoRiga);
                    table.ForeignKey(
                        name: "FK_TrasferimentiRighe_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrasferimentiRighe_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrasferimentiRighe_Trasferimenti_IdTrasferimento",
                        column: x => x.IdTrasferimento,
                        principalSchema: "trac",
                        principalTable: "Trasferimenti",
                        principalColumn: "IdTrasferimento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Accettazioni",
                schema: "trac",
                columns: table => new
                {
                    IdAccettazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdOrdineRiga = table.Column<int>(type: "int", nullable: true),
                    IdFornitore = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    DataArrivo = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataDocumento = table.Column<DateOnly>(type: "date", nullable: true),
                    Vettore = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Giudizio = table.Column<int>(type: "int", nullable: false),
                    NoteReclami = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdLotto = table.Column<int>(type: "int", nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accettazioni", x => x.IdAccettazione);
                    table.ForeignKey(
                        name: "FK_Accettazioni_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accettazioni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accettazioni_Fornitori_IdFornitore",
                        column: x => x.IdFornitore,
                        principalSchema: "trac",
                        principalTable: "Fornitori",
                        principalColumn: "IdFornitore",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accettazioni_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accettazioni_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accettazioni_OrdiniRighe_IdOrdineRiga",
                        column: x => x.IdOrdineRiga,
                        principalSchema: "trac",
                        principalTable: "OrdiniRighe",
                        principalColumn: "IdOrdineRiga",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RicetteVersioni",
                schema: "trac",
                columns: table => new
                {
                    IdRicettaVersione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdRicetta = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ValidaDal = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidaAl = table.Column<DateOnly>(type: "date", nullable: true),
                    QuantitaProdottaTeorica = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicetteVersioni", x => x.IdRicettaVersione);
                    table.ForeignKey(
                        name: "FK_RicetteVersioni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteVersioni_Ricette_IdRicetta",
                        column: x => x.IdRicetta,
                        principalSchema: "trac",
                        principalTable: "Ricette",
                        principalColumn: "IdRicetta",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccettazioniControlli",
                schema: "trac",
                columns: table => new
                {
                    IdAccettazioneControllo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdAccettazione = table.Column<int>(type: "int", nullable: false),
                    Controllo = table.Column<int>(type: "int", nullable: false),
                    Conforme = table.Column<bool>(type: "bit", nullable: true),
                    Valore = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccettazioniControlli", x => x.IdAccettazioneControllo);
                    table.ForeignKey(
                        name: "FK_AccettazioniControlli_Accettazioni_IdAccettazione",
                        column: x => x.IdAccettazione,
                        principalSchema: "trac",
                        principalTable: "Accettazioni",
                        principalColumn: "IdAccettazione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccettazioniControlli_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allegati",
                schema: "trac",
                columns: table => new
                {
                    IdAllegato = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    NomeFile = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TipoContenuto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Percorso = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Dimensione = table.Column<long>(type: "bigint", nullable: false),
                    ImprontaSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ScadenzaValidita = table.Column<DateOnly>(type: "date", nullable: true),
                    IdAccettazione = table.Column<int>(type: "int", nullable: true),
                    IdLotto = table.Column<int>(type: "int", nullable: true),
                    IdFornitore = table.Column<int>(type: "int", nullable: true),
                    IdSpedizione = table.Column<int>(type: "int", nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCaricamento = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allegati", x => x.IdAllegato);
                    table.ForeignKey(
                        name: "FK_Allegati_Accettazioni_IdAccettazione",
                        column: x => x.IdAccettazione,
                        principalSchema: "trac",
                        principalTable: "Accettazioni",
                        principalColumn: "IdAccettazione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allegati_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allegati_Fornitori_IdFornitore",
                        column: x => x.IdFornitore,
                        principalSchema: "trac",
                        principalTable: "Fornitori",
                        principalColumn: "IdFornitore",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allegati_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Allegati_Spedizioni_IdSpedizione",
                        column: x => x.IdSpedizione,
                        principalSchema: "trac",
                        principalTable: "Spedizioni",
                        principalColumn: "IdSpedizione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Produzioni",
                schema: "trac",
                columns: table => new
                {
                    IdProduzione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdRicettaVersione = table.Column<int>(type: "int", nullable: false),
                    IdArticolo = table.Column<int>(type: "int", nullable: false),
                    IdLottoProdotto = table.Column<int>(type: "int", nullable: false),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: false),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produzioni", x => x.IdProduzione);
                    table.ForeignKey(
                        name: "FK_Produzioni_Articoli_IdArticolo",
                        column: x => x.IdArticolo,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Produzioni_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Produzioni_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Produzioni_Lotti_IdLottoProdotto",
                        column: x => x.IdLottoProdotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Produzioni_RicetteVersioni_IdRicettaVersione",
                        column: x => x.IdRicettaVersione,
                        principalSchema: "trac",
                        principalTable: "RicetteVersioni",
                        principalColumn: "IdRicettaVersione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RicetteRighe",
                schema: "trac",
                columns: table => new
                {
                    IdRicettaRiga = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdRicettaVersione = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: true),
                    IdArticoloSemilavorato = table.Column<int>(type: "int", nullable: true),
                    QuantitaTeorica = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicetteRighe", x => x.IdRicettaRiga);
                    table.CheckConstraint("CK_RicetteRighe_Ingrediente", "([IdCategoria] IS NOT NULL AND [IdArticoloSemilavorato] IS NULL) OR ([IdCategoria] IS NULL AND [IdArticoloSemilavorato] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RicetteRighe_Articoli_IdArticoloSemilavorato",
                        column: x => x.IdArticoloSemilavorato,
                        principalSchema: "trac",
                        principalTable: "Articoli",
                        principalColumn: "IdArticolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteRighe_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteRighe_CategorieMateriaPrima_IdCategoria",
                        column: x => x.IdCategoria,
                        principalSchema: "trac",
                        principalTable: "CategorieMateriaPrima",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RicetteRighe_RicetteVersioni_IdRicettaVersione",
                        column: x => x.IdRicettaVersione,
                        principalSchema: "trac",
                        principalTable: "RicetteVersioni",
                        principalColumn: "IdRicettaVersione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentiLotto",
                schema: "trac",
                columns: table => new
                {
                    IdMovimento = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdInsediamento = table.Column<int>(type: "int", nullable: false),
                    IdLotto = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DataMovimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdAccettazione = table.Column<int>(type: "int", nullable: true),
                    IdProduzione = table.Column<int>(type: "int", nullable: true),
                    IdSpedizione = table.Column<int>(type: "int", nullable: true),
                    IdTrasferimento = table.Column<int>(type: "int", nullable: true),
                    IdMovimentoStornato = table.Column<long>(type: "bigint", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentiLotto", x => x.IdMovimento);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Accettazioni_IdAccettazione",
                        column: x => x.IdAccettazione,
                        principalSchema: "trac",
                        principalTable: "Accettazioni",
                        principalColumn: "IdAccettazione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Insediamenti_IdInsediamento",
                        column: x => x.IdInsediamento,
                        principalSchema: "trac",
                        principalTable: "Insediamenti",
                        principalColumn: "IdInsediamento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Lotti_IdLotto",
                        column: x => x.IdLotto,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_MovimentiLotto_IdMovimentoStornato",
                        column: x => x.IdMovimentoStornato,
                        principalSchema: "trac",
                        principalTable: "MovimentiLotto",
                        principalColumn: "IdMovimento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Produzioni_IdProduzione",
                        column: x => x.IdProduzione,
                        principalSchema: "trac",
                        principalTable: "Produzioni",
                        principalColumn: "IdProduzione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Spedizioni_IdSpedizione",
                        column: x => x.IdSpedizione,
                        principalSchema: "trac",
                        principalTable: "Spedizioni",
                        principalColumn: "IdSpedizione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentiLotto_Trasferimenti_IdTrasferimento",
                        column: x => x.IdTrasferimento,
                        principalSchema: "trac",
                        principalTable: "Trasferimenti",
                        principalColumn: "IdTrasferimento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProduzioniConsumi",
                schema: "trac",
                columns: table => new
                {
                    IdProduzioneConsumo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAzienda = table.Column<int>(type: "int", nullable: false),
                    IdProduzione = table.Column<int>(type: "int", nullable: false),
                    IdCategoria = table.Column<int>(type: "int", nullable: true),
                    IdLottoIngresso = table.Column<int>(type: "int", nullable: false),
                    Quantita = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Origine = table.Column<int>(type: "int", nullable: false),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProduzioniConsumi", x => x.IdProduzioneConsumo);
                    table.ForeignKey(
                        name: "FK_ProduzioniConsumi_Aziende_IdAzienda",
                        column: x => x.IdAzienda,
                        principalSchema: "trac",
                        principalTable: "Aziende",
                        principalColumn: "IdAzienda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProduzioniConsumi_CategorieMateriaPrima_IdCategoria",
                        column: x => x.IdCategoria,
                        principalSchema: "trac",
                        principalTable: "CategorieMateriaPrima",
                        principalColumn: "IdCategoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProduzioniConsumi_Lotti_IdLottoIngresso",
                        column: x => x.IdLottoIngresso,
                        principalSchema: "trac",
                        principalTable: "Lotti",
                        principalColumn: "IdLotto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProduzioniConsumi_Produzioni_IdProduzione",
                        column: x => x.IdProduzione,
                        principalSchema: "trac",
                        principalTable: "Produzioni",
                        principalColumn: "IdProduzione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "trac",
                table: "Allergeni",
                columns: new[] { "IdAllergene", "Codice", "Nome", "Ordine" },
                values: new object[,]
                {
                    { 1, "GLUTINE", "Cereali contenenti glutine", 1 },
                    { 2, "CROSTACEI", "Crostacei e prodotti a base di crostacei", 2 },
                    { 3, "UOVA", "Uova e prodotti a base di uova", 3 },
                    { 4, "PESCE", "Pesce e prodotti a base di pesce", 4 },
                    { 5, "ARACHIDI", "Arachidi e prodotti a base di arachidi", 5 },
                    { 6, "SOIA", "Soia e prodotti a base di soia", 6 },
                    { 7, "LATTE", "Latte e prodotti a base di latte (incluso lattosio)", 7 },
                    { 8, "FRUTTA_GUSCIO", "Frutta a guscio", 8 },
                    { 9, "SEDANO", "Sedano e prodotti a base di sedano", 9 },
                    { 10, "SENAPE", "Senape e prodotti a base di senape", 10 },
                    { 11, "SESAMO", "Semi di sesamo e prodotti a base di semi di sesamo", 11 },
                    { 12, "SOLFITI", "Anidride solforosa e solfiti (oltre 10 mg/kg o 10 mg/l)", 12 },
                    { 13, "LUPINI", "Lupini e prodotti a base di lupini", 13 },
                    { 14, "MOLLUSCHI", "Molluschi e prodotti a base di molluschi", 14 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdArticolo",
                schema: "trac",
                table: "Accettazioni",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdAzienda",
                schema: "trac",
                table: "Accettazioni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdFornitore",
                schema: "trac",
                table: "Accettazioni",
                column: "IdFornitore");

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdInsediamento",
                schema: "trac",
                table: "Accettazioni",
                column: "IdInsediamento");

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdLotto",
                schema: "trac",
                table: "Accettazioni",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_Accettazioni_IdOrdineRiga",
                schema: "trac",
                table: "Accettazioni",
                column: "IdOrdineRiga");

            migrationBuilder.CreateIndex(
                name: "IX_AccettazioniControlli_IdAccettazione",
                schema: "trac",
                table: "AccettazioniControlli",
                column: "IdAccettazione");

            migrationBuilder.CreateIndex(
                name: "IX_AccettazioniControlli_IdAzienda",
                schema: "trac",
                table: "AccettazioniControlli",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Allegati_IdAccettazione",
                schema: "trac",
                table: "Allegati",
                column: "IdAccettazione");

            migrationBuilder.CreateIndex(
                name: "IX_Allegati_IdAzienda",
                schema: "trac",
                table: "Allegati",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Allegati_IdFornitore",
                schema: "trac",
                table: "Allegati",
                column: "IdFornitore");

            migrationBuilder.CreateIndex(
                name: "IX_Allegati_IdLotto",
                schema: "trac",
                table: "Allegati",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_Allegati_IdSpedizione",
                schema: "trac",
                table: "Allegati",
                column: "IdSpedizione");

            migrationBuilder.CreateIndex(
                name: "IX_Allergeni_Codice",
                schema: "trac",
                table: "Allergeni",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articoli_IdAzienda",
                schema: "trac",
                table: "Articoli",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Articoli_IdCategoria",
                schema: "trac",
                table: "Articoli",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliAllergeni_IdAllergene",
                schema: "trac",
                table: "ArticoliAllergeni",
                column: "IdAllergene");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliAllergeni_IdArticolo",
                schema: "trac",
                table: "ArticoliAllergeni",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliAllergeni_IdAzienda",
                schema: "trac",
                table: "ArticoliAllergeni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliFornitori_IdArticolo",
                schema: "trac",
                table: "ArticoliFornitori",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliFornitori_IdAzienda",
                schema: "trac",
                table: "ArticoliFornitori",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ArticoliFornitori_IdFornitore",
                schema: "trac",
                table: "ArticoliFornitori",
                column: "IdFornitore");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_IdAzienda_DataOra",
                schema: "trac",
                table: "Audit",
                columns: new[] { "IdAzienda", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_CampiAggiuntivi_IdAzienda",
                schema: "trac",
                table: "CampiAggiuntivi",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_CategorieMateriaPrima_IdAzienda",
                schema: "trac",
                table: "CategorieMateriaPrima",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_CategorieMateriaPrima_IdProfiloControllo",
                schema: "trac",
                table: "CategorieMateriaPrima",
                column: "IdProfiloControllo");

            migrationBuilder.CreateIndex(
                name: "IX_Clienti_IdAzienda",
                schema: "trac",
                table: "Clienti",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ContatoriLotti_IdAzienda",
                schema: "trac",
                table: "ContatoriLotti",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ContatoriLotti_IdInsediamento_Giorno",
                schema: "trac",
                table: "ContatoriLotti",
                columns: new[] { "IdInsediamento", "Giorno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Esportazioni_IdAzienda",
                schema: "trac",
                table: "Esportazioni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Esportazioni_IdUtente",
                schema: "trac",
                table: "Esportazioni",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_Fornitori_IdAzienda",
                schema: "trac",
                table: "Fornitori",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ImpostazioniAziende_IdAzienda",
                schema: "trac",
                table: "ImpostazioniAziende",
                column: "IdAzienda",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Insediamenti_IdAzienda_Sigla",
                schema: "trac",
                table: "Insediamenti",
                columns: new[] { "IdAzienda", "Sigla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_IdArticolo",
                schema: "trac",
                table: "Lotti",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_IdAzienda_Codice",
                schema: "trac",
                table: "Lotti",
                columns: new[] { "IdAzienda", "Codice" });

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_IdAzienda_IdArticolo_IdFornitore_Codice_GiornoLottoInterno",
                schema: "trac",
                table: "Lotti",
                columns: new[] { "IdAzienda", "IdArticolo", "IdFornitore", "Codice", "GiornoLottoInterno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotti_IdFornitore",
                schema: "trac",
                table: "Lotti",
                column: "IdFornitore");

            migrationBuilder.CreateIndex(
                name: "IX_LottiInUso_IdAzienda",
                schema: "trac",
                table: "LottiInUso",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_LottiInUso_IdCategoria",
                schema: "trac",
                table: "LottiInUso",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_LottiInUso_IdInsediamento_IdCategoria_Fine",
                schema: "trac",
                table: "LottiInUso",
                columns: new[] { "IdInsediamento", "IdCategoria", "Fine" });

            migrationBuilder.CreateIndex(
                name: "IX_LottiInUso_IdLotto",
                schema: "trac",
                table: "LottiInUso",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdAccettazione",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdAccettazione");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdAzienda",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdInsediamento",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdInsediamento");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdLotto_IdInsediamento",
                schema: "trac",
                table: "MovimentiLotto",
                columns: new[] { "IdLotto", "IdInsediamento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdMovimentoStornato",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdMovimentoStornato",
                unique: true,
                filter: "[IdMovimentoStornato] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdProduzione",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdProduzione");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdSpedizione",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdSpedizione");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentiLotto_IdTrasferimento",
                schema: "trac",
                table: "MovimentiLotto",
                column: "IdTrasferimento");

            migrationBuilder.CreateIndex(
                name: "IX_Ordini_IdAzienda_Numero",
                schema: "trac",
                table: "Ordini",
                columns: new[] { "IdAzienda", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ordini_IdFornitore",
                schema: "trac",
                table: "Ordini",
                column: "IdFornitore");

            migrationBuilder.CreateIndex(
                name: "IX_Ordini_IdInsediamento",
                schema: "trac",
                table: "Ordini",
                column: "IdInsediamento");

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniRighe_IdArticolo",
                schema: "trac",
                table: "OrdiniRighe",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniRighe_IdAzienda",
                schema: "trac",
                table: "OrdiniRighe",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_OrdiniRighe_IdOrdine",
                schema: "trac",
                table: "OrdiniRighe",
                column: "IdOrdine");

            migrationBuilder.CreateIndex(
                name: "IX_Produzioni_IdArticolo",
                schema: "trac",
                table: "Produzioni",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_Produzioni_IdAzienda",
                schema: "trac",
                table: "Produzioni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Produzioni_IdInsediamento_Giorno",
                schema: "trac",
                table: "Produzioni",
                columns: new[] { "IdInsediamento", "Giorno" });

            migrationBuilder.CreateIndex(
                name: "IX_Produzioni_IdLottoProdotto",
                schema: "trac",
                table: "Produzioni",
                column: "IdLottoProdotto");

            migrationBuilder.CreateIndex(
                name: "IX_Produzioni_IdRicettaVersione",
                schema: "trac",
                table: "Produzioni",
                column: "IdRicettaVersione");

            migrationBuilder.CreateIndex(
                name: "IX_ProduzioniConsumi_IdAzienda",
                schema: "trac",
                table: "ProduzioniConsumi",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ProduzioniConsumi_IdCategoria",
                schema: "trac",
                table: "ProduzioniConsumi",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_ProduzioniConsumi_IdLottoIngresso",
                schema: "trac",
                table: "ProduzioniConsumi",
                column: "IdLottoIngresso");

            migrationBuilder.CreateIndex(
                name: "IX_ProduzioniConsumi_IdProduzione",
                schema: "trac",
                table: "ProduzioniConsumi",
                column: "IdProduzione");

            migrationBuilder.CreateIndex(
                name: "IX_ProfiliControllo_IdAzienda",
                schema: "trac",
                table: "ProfiliControllo",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ProfiliControlloRighe_IdAzienda",
                schema: "trac",
                table: "ProfiliControlloRighe",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ProfiliControlloRighe_IdProfiloControllo",
                schema: "trac",
                table: "ProfiliControlloRighe",
                column: "IdProfiloControllo");

            migrationBuilder.CreateIndex(
                name: "IX_Ricette_IdArticolo",
                schema: "trac",
                table: "Ricette",
                column: "IdArticolo");

            migrationBuilder.CreateIndex(
                name: "IX_Ricette_IdAzienda",
                schema: "trac",
                table: "Ricette",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRighe_IdArticoloSemilavorato",
                schema: "trac",
                table: "RicetteRighe",
                column: "IdArticoloSemilavorato");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRighe_IdAzienda",
                schema: "trac",
                table: "RicetteRighe",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRighe_IdCategoria",
                schema: "trac",
                table: "RicetteRighe",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteRighe_IdRicettaVersione",
                schema: "trac",
                table: "RicetteRighe",
                column: "IdRicettaVersione");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteVersioni_IdAzienda",
                schema: "trac",
                table: "RicetteVersioni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_RicetteVersioni_IdRicetta",
                schema: "trac",
                table: "RicetteVersioni",
                column: "IdRicetta");

            migrationBuilder.CreateIndex(
                name: "IX_Richiami_IdAzienda",
                schema: "trac",
                table: "Richiami",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Richiami_IdLottoOrigine",
                schema: "trac",
                table: "Richiami",
                column: "IdLottoOrigine");

            migrationBuilder.CreateIndex(
                name: "IX_Spedizioni_IdAzienda",
                schema: "trac",
                table: "Spedizioni",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Spedizioni_IdCliente",
                schema: "trac",
                table: "Spedizioni",
                column: "IdCliente");

            migrationBuilder.CreateIndex(
                name: "IX_Spedizioni_IdInsediamento_DataSpedizione",
                schema: "trac",
                table: "Spedizioni",
                columns: new[] { "IdInsediamento", "DataSpedizione" });

            migrationBuilder.CreateIndex(
                name: "IX_SpedizioniRighe_IdAzienda",
                schema: "trac",
                table: "SpedizioniRighe",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_SpedizioniRighe_IdLotto",
                schema: "trac",
                table: "SpedizioniRighe",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_SpedizioniRighe_IdSpedizione",
                schema: "trac",
                table: "SpedizioniRighe",
                column: "IdSpedizione");

            migrationBuilder.CreateIndex(
                name: "IX_Trasferimenti_IdAzienda",
                schema: "trac",
                table: "Trasferimenti",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_Trasferimenti_IdInsediamentoDestinazione",
                schema: "trac",
                table: "Trasferimenti",
                column: "IdInsediamentoDestinazione");

            migrationBuilder.CreateIndex(
                name: "IX_Trasferimenti_IdInsediamentoOrigine",
                schema: "trac",
                table: "Trasferimenti",
                column: "IdInsediamentoOrigine");

            migrationBuilder.CreateIndex(
                name: "IX_TrasferimentiRighe_IdAzienda",
                schema: "trac",
                table: "TrasferimentiRighe",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_TrasferimentiRighe_IdLotto",
                schema: "trac",
                table: "TrasferimentiRighe",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_TrasferimentiRighe_IdTrasferimento",
                schema: "trac",
                table: "TrasferimentiRighe",
                column: "IdTrasferimento");

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_Email",
                schema: "trac",
                table: "Utenti",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_IdAzienda",
                schema: "trac",
                table: "Utenti",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_UtentiInsediamenti_IdAzienda",
                schema: "trac",
                table: "UtentiInsediamenti",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_UtentiInsediamenti_IdInsediamento",
                schema: "trac",
                table: "UtentiInsediamenti",
                column: "IdInsediamento");

            migrationBuilder.CreateIndex(
                name: "IX_UtentiInsediamenti_IdUtente",
                schema: "trac",
                table: "UtentiInsediamenti",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_ValoriCampi_IdAzienda",
                schema: "trac",
                table: "ValoriCampi",
                column: "IdAzienda");

            migrationBuilder.CreateIndex(
                name: "IX_ValoriCampi_IdCampo",
                schema: "trac",
                table: "ValoriCampi",
                column: "IdCampo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccettazioniControlli",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Allegati",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ArticoliAllergeni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ArticoliFornitori",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Audit",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ContatoriLotti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Esportazioni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ImpostazioniAziende",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "LottiInUso",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "MovimentiLotto",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ProduzioniConsumi",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ProfiliControlloRighe",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "RicetteRighe",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Richiami",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "SpedizioniRighe",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "TrasferimentiRighe",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "UtentiInsediamenti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ValoriCampi",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Allergeni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Accettazioni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Produzioni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Spedizioni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Trasferimenti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Utenti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "CampiAggiuntivi",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "OrdiniRighe",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Lotti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "RicetteVersioni",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Clienti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Ordini",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Ricette",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Fornitori",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Insediamenti",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Articoli",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "CategorieMateriaPrima",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "ProfiliControllo",
                schema: "trac");

            migrationBuilder.DropTable(
                name: "Aziende",
                schema: "trac");
        }
    }
}
