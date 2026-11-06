using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Manualistica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "man");

            migrationBuilder.CreateTable(
                name: "FirmeConsulenti",
                schema: "man",
                columns: table => new
                {
                    IdFirmaConsulente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UpnUtente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImmaginePng = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Cellulare = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Attiva = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmeConsulenti", x => x.IdFirmaConsulente);
                });

            migrationBuilder.CreateTable(
                name: "Qualifiche",
                schema: "man",
                columns: table => new
                {
                    IdQualifica = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RichiedeNominativo = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifiche", x => x.IdQualifica);
                });

            migrationBuilder.CreateTable(
                name: "TipiManuale",
                schema: "man",
                columns: table => new
                {
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipiManuale", x => x.IdTipoManuale);
                });

            migrationBuilder.CreateTable(
                name: "Compilazioni",
                schema: "man",
                columns: table => new
                {
                    IdCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false),
                    CodiceClienteArcLab = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SedeLegaleIndirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SedeLegaleCap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SedeLegaleComune = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SedeLegaleProvincia = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Pec = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CodiceAteco = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AttivitaEsercitata = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SitiWeb = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CodiceUnivocoFatturazione = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    ReferenteNome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenteCognome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenteComeLegaleRappresentante = table.Column<bool>(type: "bit", nullable: false),
                    NuovoCliente = table.Column<bool>(type: "bit", nullable: false),
                    DataDocumenti = table.Column<DateTime>(type: "date", nullable: false),
                    NumeroRevisioneDocumenti = table.Column<int>(type: "int", nullable: false),
                    CompilazioneManuale = table.Column<bool>(type: "bit", nullable: false),
                    EmailCliente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EmailConsulente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NoteAlCliente = table.Column<bool>(type: "bit", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    MotivoAnnullamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModificatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compilazioni", x => x.IdCompilazione);
                    table.ForeignKey(
                        name: "FK_Compilazioni_TipiManuale_IdTipoManuale",
                        column: x => x.IdTipoManuale,
                        principalSchema: "man",
                        principalTable: "TipiManuale",
                        principalColumn: "IdTipoManuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FunzioniAziendali",
                schema: "man",
                columns: table => new
                {
                    IdFunzioneAziendale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false),
                    Codice = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Facoltativa = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunzioniAziendali", x => x.IdFunzioneAziendale);
                    table.ForeignKey(
                        name: "FK_FunzioniAziendali_TipiManuale_IdTipoManuale",
                        column: x => x.IdTipoManuale,
                        principalSchema: "man",
                        principalTable: "TipiManuale",
                        principalColumn: "IdTipoManuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelliDocumento",
                schema: "man",
                columns: table => new
                {
                    IdModelloDocumento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false),
                    Codice = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CondizioneInclusione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodiceAttivita = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SiglaRegione = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelliDocumento", x => x.IdModelloDocumento);
                    table.ForeignKey(
                        name: "FK_ModelliDocumento_TipiManuale_IdTipoManuale",
                        column: x => x.IdTipoManuale,
                        principalSchema: "man",
                        principalTable: "TipiManuale",
                        principalColumn: "IdTipoManuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VociChecklist",
                schema: "man",
                columns: table => new
                {
                    IdVoceChecklist = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdTipoManuale = table.Column<int>(type: "int", nullable: false),
                    Codice = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Etichetta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gruppo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TipoCampo = table.Column<int>(type: "int", nullable: false),
                    Obbligatoria = table.Column<bool>(type: "bit", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VociChecklist", x => x.IdVoceChecklist);
                    table.ForeignKey(
                        name: "FK_VociChecklist_TipiManuale_IdTipoManuale",
                        column: x => x.IdTipoManuale,
                        principalSchema: "man",
                        principalTable: "TipiManuale",
                        principalColumn: "IdTipoManuale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutorizzatiCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdAutorizzatoCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    IdQualifica = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cognome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantita = table.Column<int>(type: "int", nullable: false),
                    AccessoImmagini = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizzatiCompilazione", x => x.IdAutorizzatoCompilazione);
                    table.ForeignKey(
                        name: "FK_AutorizzatiCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutorizzatiCompilazione_Qualifiche_IdQualifica",
                        column: x => x.IdQualifica,
                        principalSchema: "man",
                        principalTable: "Qualifiche",
                        principalColumn: "IdQualifica",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContrattiFornitura",
                schema: "man",
                columns: table => new
                {
                    IdContrattoFornitura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CompilazioneIdCompilazione = table.Column<int>(type: "int", nullable: true),
                    PrezzoDocumentazione = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PrezzoAssistenza = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AssistenzaAnnuale = table.Column<bool>(type: "bit", nullable: false),
                    Pagamento = table.Column<int>(type: "int", nullable: false),
                    RibaIstituto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RibaAgenzia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RibaIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Sdi = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailDocumentazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ConsensoProfilazione = table.Column<int>(type: "int", nullable: false),
                    ConsensoPresenza = table.Column<int>(type: "int", nullable: false),
                    ConsensoFotoVideo = table.Column<int>(type: "int", nullable: false),
                    ConsensoPromozionali = table.Column<int>(type: "int", nullable: false),
                    ConsulenteNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConsulenteCellulare = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConsulenteEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContrattiFornitura", x => x.IdContrattoFornitura);
                    table.ForeignKey(
                        name: "FK_ContrattiFornitura_Compilazioni_CompilazioneIdCompilazione",
                        column: x => x.CompilazioneIdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione");
                });

            migrationBuilder.CreateTable(
                name: "FirmeCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdFirmaCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    Ruolo = table.Column<int>(type: "int", nullable: false),
                    Modo = table.Column<int>(type: "int", nullable: false),
                    NomeFirmatario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImmaginePng = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImprontaSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    AcquisitaIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AcquisitaDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmeCompilazione", x => x.IdFirmaCompilazione);
                    table.ForeignKey(
                        name: "FK_FirmeCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntestatariContratto",
                schema: "man",
                columns: table => new
                {
                    IdIntestatarioContratto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CompilazioneIdCompilazione = table.Column<int>(type: "int", nullable: true),
                    PartitaIva = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Comune = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    CodiceFiscale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CodiceClienteAzw = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntestatariContratto", x => x.IdIntestatarioContratto);
                    table.ForeignKey(
                        name: "FK_IntestatariContratto_Compilazioni_CompilazioneIdCompilazione",
                        column: x => x.CompilazioneIdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione");
                });

            migrationBuilder.CreateTable(
                name: "InviiDocumenti",
                schema: "man",
                columns: table => new
                {
                    IdInvioDocumenti = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CompilazioneIdCompilazione = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Destinatari = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Formati = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InviareDopoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    InviatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnullatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AnnullatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdMessaggio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Esito = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviiDocumenti", x => x.IdInvioDocumenti);
                    table.ForeignKey(
                        name: "FK_InviiDocumenti_Compilazioni_CompilazioneIdCompilazione",
                        column: x => x.CompilazioneIdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione");
                });

            migrationBuilder.CreateTable(
                name: "ManualiGenerati",
                schema: "man",
                columns: table => new
                {
                    IdManualeGenerato = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CodiceModello = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NumeroLettera = table.Column<int>(type: "int", nullable: false),
                    NomeFile = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Formato = table.Column<int>(type: "int", nullable: false),
                    PercorsoArchivio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImprontaSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    GeneratoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    GeneratoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualiGenerati", x => x.IdManualeGenerato);
                    table.ForeignKey(
                        name: "FK_ManualiGenerati_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdNotaCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    Sezione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Testo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteCompilazione", x => x.IdNotaCompilazione);
                    table.ForeignKey(
                        name: "FK_NoteCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResponsabiliCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdResponsabileCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CodiceModello = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsabiliCompilazione", x => x.IdResponsabileCompilazione);
                    table.ForeignKey(
                        name: "FK_ResponsabiliCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SediCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdSedeCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CodiceInsediamentoArcLab = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Indirizzo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cap = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Comune = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SediCompilazione", x => x.IdSedeCompilazione);
                    table.ForeignKey(
                        name: "FK_SediCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValoriCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdValoreCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    CodiceVoce = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ScrittoAMano = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoriCompilazione", x => x.IdValoreCompilazione);
                    table.ForeignKey(
                        name: "FK_ValoriCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevisioniModello",
                schema: "man",
                columns: table => new
                {
                    IdRevisioneModello = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdModelloDocumento = table.Column<int>(type: "int", nullable: false),
                    NumeroRevisione = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    ContenutoDocx = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ImprontaSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    NomeFileOriginale = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    DataEfficacia = table.Column<DateTime>(type: "date", nullable: true),
                    NoteRevisione = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisioniModello", x => x.IdRevisioneModello);
                    table.ForeignKey(
                        name: "FK_RevisioniModello_ModelliDocumento_IdModelloDocumento",
                        column: x => x.IdModelloDocumento,
                        principalSchema: "man",
                        principalTable: "ModelliDocumento",
                        principalColumn: "IdModelloDocumento",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpzioniVoce",
                schema: "man",
                columns: table => new
                {
                    IdOpzioneVoce = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdVoceChecklist = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Etichetta = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Predefinita = table.Column<bool>(type: "bit", nullable: false),
                    DaCompilazione = table.Column<bool>(type: "bit", nullable: false),
                    IdCompilazioneOrigine = table.Column<int>(type: "int", nullable: true),
                    ApprovataDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovataIlUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpzioniVoce", x => x.IdOpzioneVoce);
                    table.ForeignKey(
                        name: "FK_OpzioniVoce_VociChecklist_IdVoceChecklist",
                        column: x => x.IdVoceChecklist,
                        principalSchema: "man",
                        principalTable: "VociChecklist",
                        principalColumn: "IdVoceChecklist",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutorizzatiFunzioni",
                schema: "man",
                columns: table => new
                {
                    IdAutorizzatoFunzione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAutorizzatoCompilazione = table.Column<int>(type: "int", nullable: false),
                    IdFunzioneAziendale = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizzatiFunzioni", x => x.IdAutorizzatoFunzione);
                    table.ForeignKey(
                        name: "FK_AutorizzatiFunzioni_AutorizzatiCompilazione_IdAutorizzatoCompilazione",
                        column: x => x.IdAutorizzatoCompilazione,
                        principalSchema: "man",
                        principalTable: "AutorizzatiCompilazione",
                        principalColumn: "IdAutorizzatoCompilazione",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutorizzatiFunzioni_FunzioniAziendali_IdFunzioneAziendale",
                        column: x => x.IdFunzioneAziendale,
                        principalSchema: "man",
                        principalTable: "FunzioniAziendali",
                        principalColumn: "IdFunzioneAziendale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScelteResponsabile",
                schema: "man",
                columns: table => new
                {
                    IdSceltaResponsabile = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdResponsabileCompilazione = table.Column<int>(type: "int", nullable: false),
                    CodiceCampo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScelteResponsabile", x => x.IdSceltaResponsabile);
                    table.ForeignKey(
                        name: "FK_ScelteResponsabile_ResponsabiliCompilazione_IdResponsabileCompilazione",
                        column: x => x.IdResponsabileCompilazione,
                        principalSchema: "man",
                        principalTable: "ResponsabiliCompilazione",
                        principalColumn: "IdResponsabileCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovazioniRevisione",
                schema: "man",
                columns: table => new
                {
                    IdApprovazioneRevisione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRevisioneModello = table.Column<int>(type: "int", nullable: false),
                    Approvatore = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Esito = table.Column<int>(type: "int", nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovazioniRevisione", x => x.IdApprovazioneRevisione);
                    table.ForeignKey(
                        name: "FK_ApprovazioniRevisione_RevisioniModello_IdRevisioneModello",
                        column: x => x.IdRevisioneModello,
                        principalSchema: "man",
                        principalTable: "RevisioniModello",
                        principalColumn: "IdRevisioneModello",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManualiGeneratiRevisioni",
                schema: "man",
                columns: table => new
                {
                    IdManualeGeneratoRevisione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdManualeGenerato = table.Column<int>(type: "int", nullable: false),
                    IdRevisioneModello = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualiGeneratiRevisioni", x => x.IdManualeGeneratoRevisione);
                    table.ForeignKey(
                        name: "FK_ManualiGeneratiRevisioni_ManualiGenerati_IdManualeGenerato",
                        column: x => x.IdManualeGenerato,
                        principalSchema: "man",
                        principalTable: "ManualiGenerati",
                        principalColumn: "IdManualeGenerato",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualiGeneratiRevisioni_RevisioniModello_IdRevisioneModello",
                        column: x => x.IdRevisioneModello,
                        principalSchema: "man",
                        principalTable: "RevisioniModello",
                        principalColumn: "IdRevisioneModello",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovazioniRevisione_IdRevisioneModello_Approvatore",
                schema: "man",
                table: "ApprovazioniRevisione",
                columns: new[] { "IdRevisioneModello", "Approvatore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutorizzatiCompilazione_IdCompilazione_Ordine",
                schema: "man",
                table: "AutorizzatiCompilazione",
                columns: new[] { "IdCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_AutorizzatiCompilazione_IdQualifica",
                schema: "man",
                table: "AutorizzatiCompilazione",
                column: "IdQualifica");

            migrationBuilder.CreateIndex(
                name: "IX_AutorizzatiFunzioni_IdAutorizzatoCompilazione_IdFunzioneAziendale",
                schema: "man",
                table: "AutorizzatiFunzioni",
                columns: new[] { "IdAutorizzatoCompilazione", "IdFunzioneAziendale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutorizzatiFunzioni_IdFunzioneAziendale",
                schema: "man",
                table: "AutorizzatiFunzioni",
                column: "IdFunzioneAziendale");

            migrationBuilder.CreateIndex(
                name: "IX_Compilazioni_CodiceClienteArcLab",
                schema: "man",
                table: "Compilazioni",
                column: "CodiceClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_Compilazioni_DataDocumenti",
                schema: "man",
                table: "Compilazioni",
                column: "DataDocumenti");

            migrationBuilder.CreateIndex(
                name: "IX_Compilazioni_IdTipoManuale_Stato",
                schema: "man",
                table: "Compilazioni",
                columns: new[] { "IdTipoManuale", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_ContrattiFornitura_CompilazioneIdCompilazione",
                schema: "man",
                table: "ContrattiFornitura",
                column: "CompilazioneIdCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_ContrattiFornitura_IdCompilazione",
                schema: "man",
                table: "ContrattiFornitura",
                column: "IdCompilazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmeCompilazione_IdCompilazione_Ruolo",
                schema: "man",
                table: "FirmeCompilazione",
                columns: new[] { "IdCompilazione", "Ruolo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmeConsulenti_UpnUtente_Attiva",
                schema: "man",
                table: "FirmeConsulenti",
                columns: new[] { "UpnUtente", "Attiva" });

            migrationBuilder.CreateIndex(
                name: "IX_FunzioniAziendali_IdTipoManuale_Codice",
                schema: "man",
                table: "FunzioniAziendali",
                columns: new[] { "IdTipoManuale", "Codice" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntestatariContratto_CompilazioneIdCompilazione",
                schema: "man",
                table: "IntestatariContratto",
                column: "CompilazioneIdCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_IntestatariContratto_IdCompilazione",
                schema: "man",
                table: "IntestatariContratto",
                column: "IdCompilazione",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviiDocumenti_CompilazioneIdCompilazione",
                schema: "man",
                table: "InviiDocumenti",
                column: "CompilazioneIdCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_InviiDocumenti_IdCompilazione",
                schema: "man",
                table: "InviiDocumenti",
                column: "IdCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_InviiDocumenti_Stato_InviareDopoUtc",
                schema: "man",
                table: "InviiDocumenti",
                columns: new[] { "Stato", "InviareDopoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualiGenerati_IdCompilazione_CodiceModello_NumeroLettera_Formato",
                schema: "man",
                table: "ManualiGenerati",
                columns: new[] { "IdCompilazione", "CodiceModello", "NumeroLettera", "Formato" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualiGeneratiRevisioni_IdManualeGenerato_IdRevisioneModello",
                schema: "man",
                table: "ManualiGeneratiRevisioni",
                columns: new[] { "IdManualeGenerato", "IdRevisioneModello" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManualiGeneratiRevisioni_IdRevisioneModello",
                schema: "man",
                table: "ManualiGeneratiRevisioni",
                column: "IdRevisioneModello");

            migrationBuilder.CreateIndex(
                name: "IX_ModelliDocumento_IdTipoManuale_Codice_CodiceAttivita_SiglaRegione",
                schema: "man",
                table: "ModelliDocumento",
                columns: new[] { "IdTipoManuale", "Codice", "CodiceAttivita", "SiglaRegione" },
                unique: true,
                filter: "[CodiceAttivita] IS NOT NULL AND [SiglaRegione] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NoteCompilazione_IdCompilazione",
                schema: "man",
                table: "NoteCompilazione",
                column: "IdCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_OpzioniVoce_IdVoceChecklist_Attivo_Ordine",
                schema: "man",
                table: "OpzioniVoce",
                columns: new[] { "IdVoceChecklist", "Attivo", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_Qualifiche_Nome",
                schema: "man",
                table: "Qualifiche",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResponsabiliCompilazione_IdCompilazione_CodiceModello_Ordine",
                schema: "man",
                table: "ResponsabiliCompilazione",
                columns: new[] { "IdCompilazione", "CodiceModello", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisioniModello_IdModelloDocumento_NumeroRevisione",
                schema: "man",
                table: "RevisioniModello",
                columns: new[] { "IdModelloDocumento", "NumeroRevisione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisioniModello_IdModelloDocumento_Stato",
                schema: "man",
                table: "RevisioniModello",
                columns: new[] { "IdModelloDocumento", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_ScelteResponsabile_IdResponsabileCompilazione_CodiceCampo",
                schema: "man",
                table: "ScelteResponsabile",
                columns: new[] { "IdResponsabileCompilazione", "CodiceCampo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SediCompilazione_IdCompilazione_Ordine",
                schema: "man",
                table: "SediCompilazione",
                columns: new[] { "IdCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_TipiManuale_Codice",
                schema: "man",
                table: "TipiManuale",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoriCompilazione_IdCompilazione_CodiceVoce",
                schema: "man",
                table: "ValoriCompilazione",
                columns: new[] { "IdCompilazione", "CodiceVoce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoriCompilazione_ScrittoAMano",
                schema: "man",
                table: "ValoriCompilazione",
                column: "ScrittoAMano");

            migrationBuilder.CreateIndex(
                name: "IX_VociChecklist_IdTipoManuale_Codice",
                schema: "man",
                table: "VociChecklist",
                columns: new[] { "IdTipoManuale", "Codice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovazioniRevisione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "AutorizzatiFunzioni",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ContrattiFornitura",
                schema: "man");

            migrationBuilder.DropTable(
                name: "FirmeCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "FirmeConsulenti",
                schema: "man");

            migrationBuilder.DropTable(
                name: "IntestatariContratto",
                schema: "man");

            migrationBuilder.DropTable(
                name: "InviiDocumenti",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ManualiGeneratiRevisioni",
                schema: "man");

            migrationBuilder.DropTable(
                name: "NoteCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "OpzioniVoce",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ScelteResponsabile",
                schema: "man");

            migrationBuilder.DropTable(
                name: "SediCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ValoriCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "AutorizzatiCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "FunzioniAziendali",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ManualiGenerati",
                schema: "man");

            migrationBuilder.DropTable(
                name: "RevisioniModello",
                schema: "man");

            migrationBuilder.DropTable(
                name: "VociChecklist",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ResponsabiliCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "Qualifiche",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ModelliDocumento",
                schema: "man");

            migrationBuilder.DropTable(
                name: "Compilazioni",
                schema: "man");

            migrationBuilder.DropTable(
                name: "TipiManuale",
                schema: "man");
        }
    }
}
