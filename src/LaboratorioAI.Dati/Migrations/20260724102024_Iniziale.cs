using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Iniziale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "log");

            migrationBuilder.EnsureSchema(
                name: "sec");

            migrationBuilder.EnsureSchema(
                name: "portale");

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "log",
                columns: table => new
                {
                    IdVoce = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    EmailUtente = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Azione = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEntita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdEntita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Esito = table.Column<int>(type: "int", nullable: false),
                    Dettaglio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.IdVoce);
                });

            migrationBuilder.CreateTable(
                name: "Configurazioni",
                schema: "app",
                columns: table => new
                {
                    IdConfigurazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chiave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtenteModifica = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurazioni", x => x.IdConfigurazione);
                });

            migrationBuilder.CreateTable(
                name: "Permessi",
                schema: "sec",
                columns: table => new
                {
                    IdPermesso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permessi", x => x.IdPermesso);
                });

            migrationBuilder.CreateTable(
                name: "Ruoli",
                schema: "sec",
                columns: table => new
                {
                    IdRuolo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DiSistema = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ruoli", x => x.IdRuolo);
                });

            migrationBuilder.CreateTable(
                name: "SessioniPortale",
                schema: "portale",
                columns: table => new
                {
                    IdSessione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdCliente = table.Column<int>(type: "int", nullable: true),
                    CodiceContabilita = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Operatore = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Scadenza = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataRevoca = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessioniPortale", x => x.IdSessione);
                });

            migrationBuilder.CreateTable(
                name: "TentativiAccesso",
                schema: "portale",
                columns: table => new
                {
                    IdTentativo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataOra = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CodiceContabilita = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IndirizzoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Riuscito = table.Column<bool>(type: "bit", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TentativiAccesso", x => x.IdTentativo);
                });

            migrationBuilder.CreateTable(
                name: "Utenti",
                schema: "sec",
                columns: table => new
                {
                    IdUtente = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObjectIdEntra = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NomeCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimoAccesso = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utenti", x => x.IdUtente);
                });

            migrationBuilder.CreateTable(
                name: "RuoliPermessi",
                schema: "sec",
                columns: table => new
                {
                    IdRuolo = table.Column<int>(type: "int", nullable: false),
                    IdPermesso = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuoliPermessi", x => new { x.IdRuolo, x.IdPermesso });
                    table.ForeignKey(
                        name: "FK_RuoliPermessi_Permessi_IdPermesso",
                        column: x => x.IdPermesso,
                        principalSchema: "sec",
                        principalTable: "Permessi",
                        principalColumn: "IdPermesso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuoliPermessi_Ruoli_IdRuolo",
                        column: x => x.IdRuolo,
                        principalSchema: "sec",
                        principalTable: "Ruoli",
                        principalColumn: "IdRuolo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Analisi",
                schema: "app",
                columns: table => new
                {
                    IdAnalisi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCampione = table.Column<int>(type: "int", nullable: false),
                    IdCertificato = table.Column<int>(type: "int", nullable: true),
                    Modalita = table.Column<int>(type: "int", nullable: false),
                    DataAnalisi = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DenominazioneCampione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Matrice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataRicezione = table.Column<DateOnly>(type: "date", nullable: true),
                    NumeroProve = table.Column<int>(type: "int", nullable: true),
                    Giudizio = table.Column<int>(type: "int", nullable: false),
                    Testo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelloAi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenInput = table.Column<int>(type: "int", nullable: true),
                    TokenOutput = table.Column<int>(type: "int", nullable: true),
                    CostoStimato = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analisi", x => x.IdAnalisi);
                    table.ForeignKey(
                        name: "FK_Analisi_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RegistroDvr",
                schema: "app",
                columns: table => new
                {
                    IdVoce = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCliente = table.Column<int>(type: "int", nullable: true),
                    IdInsediamento = table.Column<int>(type: "int", nullable: true),
                    RagioneSociale = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NomeInsediamento = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Regione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataGenerazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    NomeFile = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PercorsoArchivio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Esito = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdLotto = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroDvr", x => x.IdVoce);
                    table.ForeignKey(
                        name: "FK_RegistroDvr_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UtentiRuoli",
                schema: "sec",
                columns: table => new
                {
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    IdRuolo = table.Column<int>(type: "int", nullable: false),
                    DataAssegnazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtenteAssegnante = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtentiRuoli", x => new { x.IdUtente, x.IdRuolo });
                    table.ForeignKey(
                        name: "FK_UtentiRuoli_Ruoli_IdRuolo",
                        column: x => x.IdRuolo,
                        principalSchema: "sec",
                        principalTable: "Ruoli",
                        principalColumn: "IdRuolo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UtentiRuoli_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analisi_Campione_Data",
                schema: "app",
                table: "Analisi",
                columns: new[] { "IdCampione", "DataAnalisi" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Analisi_DataAnalisi",
                schema: "app",
                table: "Analisi",
                column: "DataAnalisi",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Analisi_Giudizio",
                schema: "app",
                table: "Analisi",
                column: "Giudizio");

            migrationBuilder.CreateIndex(
                name: "IX_Analisi_IdUtente",
                schema: "app",
                table: "Analisi",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Area_Azione",
                schema: "log",
                table: "Audit",
                columns: new[] { "Area", "Azione" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_DataOra",
                schema: "log",
                table: "Audit",
                column: "DataOra",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Audit_IdUtente_DataOra",
                schema: "log",
                table: "Audit",
                columns: new[] { "IdUtente", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_Configurazioni_Chiave",
                schema: "app",
                table: "Configurazioni",
                column: "Chiave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permessi_Codice",
                schema: "sec",
                table: "Permessi",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDvr_DataGenerazione",
                schema: "app",
                table: "RegistroDvr",
                column: "DataGenerazione",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDvr_IdCliente",
                schema: "app",
                table: "RegistroDvr",
                column: "IdCliente");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDvr_IdLotto",
                schema: "app",
                table: "RegistroDvr",
                column: "IdLotto");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroDvr_IdUtente",
                schema: "app",
                table: "RegistroDvr",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_Ruoli_Codice",
                schema: "sec",
                table: "Ruoli",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuoliPermessi_IdPermesso",
                schema: "sec",
                table: "RuoliPermessi",
                column: "IdPermesso");

            migrationBuilder.CreateIndex(
                name: "IX_SessioniPortale_Scadenza",
                schema: "portale",
                table: "SessioniPortale",
                column: "Scadenza");

            migrationBuilder.CreateIndex(
                name: "IX_SessioniPortale_Token",
                schema: "portale",
                table: "SessioniPortale",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TentativiAccesso_CodiceContabilita_DataOra",
                schema: "portale",
                table: "TentativiAccesso",
                columns: new[] { "CodiceContabilita", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_TentativiAccesso_IndirizzoIp_DataOra",
                schema: "portale",
                table: "TentativiAccesso",
                columns: new[] { "IndirizzoIp", "DataOra" });

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_Email",
                schema: "sec",
                table: "Utenti",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_ObjectIdEntra",
                schema: "sec",
                table: "Utenti",
                column: "ObjectIdEntra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtentiRuoli_IdRuolo",
                schema: "sec",
                table: "UtentiRuoli",
                column: "IdRuolo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analisi",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Audit",
                schema: "log");

            migrationBuilder.DropTable(
                name: "Configurazioni",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RegistroDvr",
                schema: "app");

            migrationBuilder.DropTable(
                name: "RuoliPermessi",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "SessioniPortale",
                schema: "portale");

            migrationBuilder.DropTable(
                name: "TentativiAccesso",
                schema: "portale");

            migrationBuilder.DropTable(
                name: "UtentiRuoli",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Permessi",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Ruoli",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Utenti",
                schema: "sec");
        }
    }
}
