using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class Presenze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "presenze");

            migrationBuilder.CreateTable(
                name: "ImpostazioniPresenze",
                schema: "presenze",
                columns: table => new
                {
                    IdImpostazione = table.Column<int>(type: "int", nullable: false),
                    InizioConteggi = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpostazioniPresenze", x => x.IdImpostazione);
                });

            migrationBuilder.CreateTable(
                name: "Sedi",
                schema: "presenze",
                columns: table => new
                {
                    IdSede = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Attiva = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sedi", x => x.IdSede);
                });

            migrationBuilder.CreateTable(
                name: "CalendarioAziendale",
                schema: "presenze",
                columns: table => new
                {
                    IdGiornoCalendario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSede = table.Column<int>(type: "int", nullable: true),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ore = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarioAziendale", x => x.IdGiornoCalendario);
                    table.CheckConstraint("CK_CalendarioAziendale_Ore", "[Ore] IS NULL OR ([Ore] > 0 AND [Ore] <= 24)");
                    table.CheckConstraint("CK_CalendarioAziendale_Tipo", "[Tipo] IN ('FE', 'CH')");
                    table.ForeignKey(
                        name: "FK_CalendarioAziendale_Sedi_IdSede",
                        column: x => x.IdSede,
                        principalSchema: "presenze",
                        principalTable: "Sedi",
                        principalColumn: "IdSede",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lavoratori",
                schema: "presenze",
                columns: table => new
                {
                    IdLavoratore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUtente = table.Column<int>(type: "int", nullable: false),
                    IdSede = table.Column<int>(type: "int", nullable: false),
                    Cognome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Colore = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    AttivoDal = table.Column<DateOnly>(type: "date", nullable: true),
                    OreLunedi = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreMartedi = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreMercoledi = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreGiovedi = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreVenerdi = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreSabato = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    OreDomenica = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lavoratori", x => x.IdLavoratore);
                    table.CheckConstraint("CK_Lavoratori_Ore", "[OreLunedi] BETWEEN 0 AND 24 AND [OreMartedi] BETWEEN 0 AND 24 AND [OreMercoledi] BETWEEN 0 AND 24 AND [OreGiovedi] BETWEEN 0 AND 24 AND [OreVenerdi] BETWEEN 0 AND 24 AND [OreSabato] BETWEEN 0 AND 24 AND [OreDomenica] BETWEEN 0 AND 24");
                    table.ForeignKey(
                        name: "FK_Lavoratori_Sedi_IdSede",
                        column: x => x.IdSede,
                        principalSchema: "presenze",
                        principalTable: "Sedi",
                        principalColumn: "IdSede",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lavoratori_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assenze",
                schema: "presenze",
                columns: table => new
                {
                    IdAssenza = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLavoratore = table.Column<int>(type: "int", nullable: false),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: false),
                    Causale = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Ore = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnnullamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assenze", x => x.IdAssenza);
                    table.CheckConstraint("CK_Assenze_Causale", "[Causale] IN ('FER', 'PER', '104', 'SEG', 'MAL', 'MAT', 'DS', 'ALT')");
                    table.CheckConstraint("CK_Assenze_Ore", "[Ore] >= 0 AND [Ore] <= 24");
                    table.ForeignKey(
                        name: "FK_Assenze_Lavoratori_IdLavoratore",
                        column: x => x.IdLavoratore,
                        principalSchema: "presenze",
                        principalTable: "Lavoratori",
                        principalColumn: "IdLavoratore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistroModifiche",
                schema: "presenze",
                columns: table => new
                {
                    IdModifica = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuandoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUtenteAutore = table.Column<int>(type: "int", nullable: true),
                    IdLavoratore = table.Column<int>(type: "int", nullable: true),
                    Azione = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: true),
                    Prima = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dopo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroModifiche", x => x.IdModifica);
                    table.ForeignKey(
                        name: "FK_RegistroModifiche_Lavoratori_IdLavoratore",
                        column: x => x.IdLavoratore,
                        principalSchema: "presenze",
                        principalTable: "Lavoratori",
                        principalColumn: "IdLavoratore",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistroModifiche_Utenti_IdUtenteAutore",
                        column: x => x.IdUtenteAutore,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Timbrature",
                schema: "presenze",
                columns: table => new
                {
                    IdTimbratura = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLavoratore = table.Column<int>(type: "int", nullable: false),
                    DataOraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Giorno = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    Luogo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Annullata = table.Column<bool>(type: "bit", nullable: false),
                    DataAnnullamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnnullamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timbrature", x => x.IdTimbratura);
                    table.CheckConstraint("CK_Timbrature_Origine", "[Origine] IN ('App', 'Manuale')");
                    table.CheckConstraint("CK_Timbrature_Tipo", "[Tipo] IN ('E', 'U')");
                    table.ForeignKey(
                        name: "FK_Timbrature_Lavoratori_IdLavoratore",
                        column: x => x.IdLavoratore,
                        principalSchema: "presenze",
                        principalTable: "Lavoratori",
                        principalColumn: "IdLavoratore",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "presenze",
                table: "ImpostazioniPresenze",
                columns: new[] { "IdImpostazione", "InizioConteggi" },
                values: new object[] { 1, new DateOnly(2027, 1, 1) });

            migrationBuilder.InsertData(
                schema: "presenze",
                table: "Sedi",
                columns: new[] { "IdSede", "Attiva", "Nome" },
                values: new object[,]
                {
                    { 1, true, "Cattolica" },
                    { 2, true, "Pesaro" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assenze_Giorno",
                schema: "presenze",
                table: "Assenze",
                column: "Giorno");

            migrationBuilder.CreateIndex(
                name: "IX_Assenze_IdLavoratore_Giorno",
                schema: "presenze",
                table: "Assenze",
                columns: new[] { "IdLavoratore", "Giorno" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarioAziendale_IdSede_Giorno",
                schema: "presenze",
                table: "CalendarioAziendale",
                columns: new[] { "IdSede", "Giorno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lavoratori_Cognome",
                schema: "presenze",
                table: "Lavoratori",
                column: "Cognome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lavoratori_IdSede",
                schema: "presenze",
                table: "Lavoratori",
                column: "IdSede");

            migrationBuilder.CreateIndex(
                name: "IX_Lavoratori_IdUtente",
                schema: "presenze",
                table: "Lavoratori",
                column: "IdUtente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroModifiche_IdLavoratore_Giorno",
                schema: "presenze",
                table: "RegistroModifiche",
                columns: new[] { "IdLavoratore", "Giorno" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroModifiche_IdUtenteAutore",
                schema: "presenze",
                table: "RegistroModifiche",
                column: "IdUtenteAutore");

            migrationBuilder.CreateIndex(
                name: "IX_RegistroModifiche_QuandoUtc",
                schema: "presenze",
                table: "RegistroModifiche",
                column: "QuandoUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sedi_Nome",
                schema: "presenze",
                table: "Sedi",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Timbrature_Giorno",
                schema: "presenze",
                table: "Timbrature",
                column: "Giorno");

            migrationBuilder.CreateIndex(
                name: "IX_Timbrature_IdLavoratore_Giorno",
                schema: "presenze",
                table: "Timbrature",
                columns: new[] { "IdLavoratore", "Giorno" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assenze",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "CalendarioAziendale",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "ImpostazioniPresenze",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "RegistroModifiche",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "Timbrature",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "Lavoratori",
                schema: "presenze");

            migrationBuilder.DropTable(
                name: "Sedi",
                schema: "presenze");
        }
    }
}
