using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ArchiviGestionaliCaselle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiviCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdArchivioCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Luogo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SistemaOperativo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Antivirus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Backup = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FrequenzaBackup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LuogoConservazione = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiviCompilazione", x => x.IdArchivioCompilazione);
                    table.ForeignKey(
                        name: "FK_ArchiviCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaselleEmailCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdCasellaEmailCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    Indirizzo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaselleEmailCompilazione", x => x.IdCasellaEmailCompilazione);
                    table.ForeignKey(
                        name: "FK_CaselleEmailCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GestionaliCompilazione",
                schema: "man",
                columns: table => new
                {
                    IdGestionaleCompilazione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCompilazione = table.Column<int>(type: "int", nullable: false),
                    Prodotto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MisureSicurezza = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Manutenzione = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Backup = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Frequenza = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LuogoConservazione = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Ordine = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    CreatoIlUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatoDa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GestionaliCompilazione", x => x.IdGestionaleCompilazione);
                    table.ForeignKey(
                        name: "FK_GestionaliCompilazione_Compilazioni_IdCompilazione",
                        column: x => x.IdCompilazione,
                        principalSchema: "man",
                        principalTable: "Compilazioni",
                        principalColumn: "IdCompilazione",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchiviAccessi",
                schema: "man",
                columns: table => new
                {
                    IdArchivioAccesso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArchivioCompilazione = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiviAccessi", x => x.IdArchivioAccesso);
                    table.ForeignKey(
                        name: "FK_ArchiviAccessi_ArchiviCompilazione_IdArchivioCompilazione",
                        column: x => x.IdArchivioCompilazione,
                        principalSchema: "man",
                        principalTable: "ArchiviCompilazione",
                        principalColumn: "IdArchivioCompilazione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchiviCategorieDati",
                schema: "man",
                columns: table => new
                {
                    IdArchivioCategoriaDati = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArchivioCompilazione = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiviCategorieDati", x => x.IdArchivioCategoriaDati);
                    table.ForeignKey(
                        name: "FK_ArchiviCategorieDati_ArchiviCompilazione_IdArchivioCompilazione",
                        column: x => x.IdArchivioCompilazione,
                        principalSchema: "man",
                        principalTable: "ArchiviCompilazione",
                        principalColumn: "IdArchivioCompilazione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchiviInteressati",
                schema: "man",
                columns: table => new
                {
                    IdArchivioInteressati = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArchivioCompilazione = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiviInteressati", x => x.IdArchivioInteressati);
                    table.ForeignKey(
                        name: "FK_ArchiviInteressati_ArchiviCompilazione_IdArchivioCompilazione",
                        column: x => x.IdArchivioCompilazione,
                        principalSchema: "man",
                        principalTable: "ArchiviCompilazione",
                        principalColumn: "IdArchivioCompilazione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaselleEmailFunzioni",
                schema: "man",
                columns: table => new
                {
                    IdCasellaEmailFunzione = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCasellaEmailCompilazione = table.Column<int>(type: "int", nullable: false),
                    IdFunzioneAziendale = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaselleEmailFunzioni", x => x.IdCasellaEmailFunzione);
                    table.ForeignKey(
                        name: "FK_CaselleEmailFunzioni_CaselleEmailCompilazione_IdCasellaEmailCompilazione",
                        column: x => x.IdCasellaEmailCompilazione,
                        principalSchema: "man",
                        principalTable: "CaselleEmailCompilazione",
                        principalColumn: "IdCasellaEmailCompilazione",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaselleEmailFunzioni_FunzioniAziendali_IdFunzioneAziendale",
                        column: x => x.IdFunzioneAziendale,
                        principalSchema: "man",
                        principalTable: "FunzioniAziendali",
                        principalColumn: "IdFunzioneAziendale",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchiviGestionali",
                schema: "man",
                columns: table => new
                {
                    IdArchivioGestionale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArchivioCompilazione = table.Column<int>(type: "int", nullable: false),
                    IdGestionaleCompilazione = table.Column<int>(type: "int", nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiviGestionali", x => x.IdArchivioGestionale);
                    table.ForeignKey(
                        name: "FK_ArchiviGestionali_ArchiviCompilazione_IdArchivioCompilazione",
                        column: x => x.IdArchivioCompilazione,
                        principalSchema: "man",
                        principalTable: "ArchiviCompilazione",
                        principalColumn: "IdArchivioCompilazione",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchiviGestionali_GestionaliCompilazione_IdGestionaleCompilazione",
                        column: x => x.IdGestionaleCompilazione,
                        principalSchema: "man",
                        principalTable: "GestionaliCompilazione",
                        principalColumn: "IdGestionaleCompilazione",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviAccessi_IdArchivioCompilazione_Ordine",
                schema: "man",
                table: "ArchiviAccessi",
                columns: new[] { "IdArchivioCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviCategorieDati_IdArchivioCompilazione_Ordine",
                schema: "man",
                table: "ArchiviCategorieDati",
                columns: new[] { "IdArchivioCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviCompilazione_IdCompilazione_Tipo_Ordine",
                schema: "man",
                table: "ArchiviCompilazione",
                columns: new[] { "IdCompilazione", "Tipo", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviGestionali_IdArchivioCompilazione_IdGestionaleCompilazione",
                schema: "man",
                table: "ArchiviGestionali",
                columns: new[] { "IdArchivioCompilazione", "IdGestionaleCompilazione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviGestionali_IdGestionaleCompilazione",
                schema: "man",
                table: "ArchiviGestionali",
                column: "IdGestionaleCompilazione");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiviInteressati_IdArchivioCompilazione_Ordine",
                schema: "man",
                table: "ArchiviInteressati",
                columns: new[] { "IdArchivioCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_CaselleEmailCompilazione_IdCompilazione_Ordine",
                schema: "man",
                table: "CaselleEmailCompilazione",
                columns: new[] { "IdCompilazione", "Ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_CaselleEmailFunzioni_IdCasellaEmailCompilazione_IdFunzioneAziendale",
                schema: "man",
                table: "CaselleEmailFunzioni",
                columns: new[] { "IdCasellaEmailCompilazione", "IdFunzioneAziendale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaselleEmailFunzioni_IdFunzioneAziendale",
                schema: "man",
                table: "CaselleEmailFunzioni",
                column: "IdFunzioneAziendale");

            migrationBuilder.CreateIndex(
                name: "IX_GestionaliCompilazione_IdCompilazione_Prodotto",
                schema: "man",
                table: "GestionaliCompilazione",
                columns: new[] { "IdCompilazione", "Prodotto" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiviAccessi",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ArchiviCategorieDati",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ArchiviGestionali",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ArchiviInteressati",
                schema: "man");

            migrationBuilder.DropTable(
                name: "CaselleEmailFunzioni",
                schema: "man");

            migrationBuilder.DropTable(
                name: "GestionaliCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "ArchiviCompilazione",
                schema: "man");

            migrationBuilder.DropTable(
                name: "CaselleEmailCompilazione",
                schema: "man");
        }
    }
}
