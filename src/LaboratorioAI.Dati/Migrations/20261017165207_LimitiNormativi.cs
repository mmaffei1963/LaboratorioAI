using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class LimitiNormativi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "limiti");

            migrationBuilder.CreateTable(
                name: "FontiNormative",
                schema: "limiti",
                columns: table => new
                {
                    IdFonteNormativa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Famiglia = table.Column<int>(type: "int", nullable: false),
                    Titolo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Ente = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoFonte = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Attiva = table.Column<bool>(type: "bit", nullable: false),
                    DataUltimaVerifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EsitoUltimaVerifica = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreataDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaModificaDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FontiNormative", x => x.IdFonteNormativa);
                });

            migrationBuilder.CreateTable(
                name: "VersioniFonti",
                schema: "limiti",
                columns: table => new
                {
                    IdVersioneFonte = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdFonteNormativa = table.Column<int>(type: "int", nullable: false),
                    DataAggiornamentoDichiarata = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataConsultazione = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NomeFile = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    TipoContenuto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContenutoOriginale = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    TestoEstratto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impronta = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CaricataDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StatoEstrazione = table.Column<int>(type: "int", nullable: false),
                    DataEstrazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EsitoEstrazione = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SchedeProposte = table.Column<int>(type: "int", nullable: false),
                    SchedeScartate = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersioniFonti", x => x.IdVersioneFonte);
                    table.ForeignKey(
                        name: "FK_VersioniFonti_FontiNormative_IdFonteNormativa",
                        column: x => x.IdFonteNormativa,
                        principalSchema: "limiti",
                        principalTable: "FontiNormative",
                        principalColumn: "IdFonteNormativa",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LimitiNormativi",
                schema: "limiti",
                columns: table => new
                {
                    IdLimiteNormativo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceScheda = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Versione = table.Column<int>(type: "int", nullable: false),
                    IdLimitePrecedente = table.Column<int>(type: "int", nullable: true),
                    IdVersioneFonte = table.Column<int>(type: "int", nullable: true),
                    Famiglia = table.Column<int>(type: "int", nullable: false),
                    Parametro = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MatriceCategoria = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Operatore = table.Column<int>(type: "int", nullable: false),
                    Valore = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ValoreMax = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Unita = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    N = table.Column<int>(type: "int", nullable: true),
                    C = table.Column<int>(type: "int", nullable: true),
                    M = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    MMaiuscola = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TipoLimite = table.Column<int>(type: "int", nullable: false),
                    RegolaValutazione = table.Column<int>(type: "int", nullable: false),
                    Condizioni = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Riferimento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Citazione = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TestoSegnalazione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValidoDal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidoAl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PropostaDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdUtenteProposta = table.Column<int>(type: "int", nullable: true),
                    DataProposta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificataDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IdUtenteModifica = table.Column<int>(type: "int", nullable: true),
                    DataModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidataDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IdUtenteValidazione = table.Column<int>(type: "int", nullable: true),
                    DataValidazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NoteValidazione = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimitiNormativi", x => x.IdLimiteNormativo);
                    table.ForeignKey(
                        name: "FK_LimitiNormativi_LimitiNormativi_IdLimitePrecedente",
                        column: x => x.IdLimitePrecedente,
                        principalSchema: "limiti",
                        principalTable: "LimitiNormativi",
                        principalColumn: "IdLimiteNormativo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LimitiNormativi_VersioniFonti_IdVersioneFonte",
                        column: x => x.IdVersioneFonte,
                        principalSchema: "limiti",
                        principalTable: "VersioniFonti",
                        principalColumn: "IdVersioneFonte",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AbbinamentiLimiti",
                schema: "limiti",
                columns: table => new
                {
                    IdAbbinamentoLimite = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLimiteNormativo = table.Column<int>(type: "int", nullable: false),
                    IdParametroArcLab = table.Column<int>(type: "int", nullable: false),
                    NomeParametro = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdMatriceArcLab = table.Column<int>(type: "int", nullable: true),
                    NomeMatrice = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdControlloArcLab = table.Column<int>(type: "int", nullable: true),
                    NomeControllo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FattoreUnita = table.Column<decimal>(type: "decimal(18,9)", precision: 18, scale: 9, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbbinamentiLimiti", x => x.IdAbbinamentoLimite);
                    table.ForeignKey(
                        name: "FK_AbbinamentiLimiti_LimitiNormativi_IdLimiteNormativo",
                        column: x => x.IdLimiteNormativo,
                        principalSchema: "limiti",
                        principalTable: "LimitiNormativi",
                        principalColumn: "IdLimiteNormativo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbbinamentiLimiti_IdLimiteNormativo",
                schema: "limiti",
                table: "AbbinamentiLimiti",
                column: "IdLimiteNormativo");

            migrationBuilder.CreateIndex(
                name: "IX_AbbinamentiLimiti_IdParametroArcLab",
                schema: "limiti",
                table: "AbbinamentiLimiti",
                column: "IdParametroArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_FontiNormative_Famiglia",
                schema: "limiti",
                table: "FontiNormative",
                column: "Famiglia");

            migrationBuilder.CreateIndex(
                name: "IX_LimitiNormativi_CodiceScheda_Versione",
                schema: "limiti",
                table: "LimitiNormativi",
                columns: new[] { "CodiceScheda", "Versione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LimitiNormativi_Famiglia_Stato",
                schema: "limiti",
                table: "LimitiNormativi",
                columns: new[] { "Famiglia", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitiNormativi_IdLimitePrecedente",
                schema: "limiti",
                table: "LimitiNormativi",
                column: "IdLimitePrecedente");

            migrationBuilder.CreateIndex(
                name: "IX_LimitiNormativi_IdVersioneFonte",
                schema: "limiti",
                table: "LimitiNormativi",
                column: "IdVersioneFonte");

            migrationBuilder.CreateIndex(
                name: "IX_VersioniFonti_IdFonteNormativa_DataConsultazione",
                schema: "limiti",
                table: "VersioniFonti",
                columns: new[] { "IdFonteNormativa", "DataConsultazione" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbbinamentiLimiti",
                schema: "limiti");

            migrationBuilder.DropTable(
                name: "LimitiNormativi",
                schema: "limiti");

            migrationBuilder.DropTable(
                name: "VersioniFonti",
                schema: "limiti");

            migrationBuilder.DropTable(
                name: "FontiNormative",
                schema: "limiti");
        }
    }
}
