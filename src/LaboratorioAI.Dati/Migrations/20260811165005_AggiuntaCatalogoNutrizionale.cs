using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AggiuntaCatalogoNutrizionale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nutri");

            migrationBuilder.CreateTable(
                name: "Fonti",
                schema: "nutri",
                columns: table => new
                {
                    IdFonte = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Descrizione = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Riferimento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrioritaDefault = table.Column<int>(type: "int", nullable: false),
                    Attivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fonti", x => x.IdFonte);
                });

            migrationBuilder.CreateTable(
                name: "MateriePrime",
                schema: "nutri",
                columns: table => new
                {
                    IdMateriaPrima = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Denominazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Fornitore = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodiceInterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriePrime", x => x.IdMateriaPrima);
                });

            migrationBuilder.CreateTable(
                name: "Ricette",
                schema: "nutri",
                columns: table => new
                {
                    IdRicetta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Denominazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: true),
                    IdInsediamentoArcLab = table.Column<int>(type: "int", nullable: true),
                    Committente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Lotto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitaConsumoG = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    UnitaConsumoPezzi = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: true),
                    CaloPesoPercentuale = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    PesoCottoKg = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricette", x => x.IdRicetta);
                });

            migrationBuilder.CreateTable(
                name: "ValoriNutrizionali",
                schema: "nutri",
                columns: table => new
                {
                    IdValoreNutrizionale = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMateriaPrima = table.Column<int>(type: "int", nullable: false),
                    Grassi = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    GrassiSaturi = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Carboidrati = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Zuccheri = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Proteine = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Sale = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Fibre = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Acqua = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    EnergiaKj = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    EnergiaKcal = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    IdFonte = table.Column<int>(type: "int", nullable: true),
                    Priorita = table.Column<int>(type: "int", nullable: false),
                    Corrente = table.Column<bool>(type: "bit", nullable: false),
                    DataValidita = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAcquisizione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RiferimentoDocumento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoriNutrizionali", x => x.IdValoreNutrizionale);
                    table.ForeignKey(
                        name: "FK_ValoriNutrizionali_Fonti_IdFonte",
                        column: x => x.IdFonte,
                        principalSchema: "nutri",
                        principalTable: "Fonti",
                        principalColumn: "IdFonte",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ValoriNutrizionali_MateriePrime_IdMateriaPrima",
                        column: x => x.IdMateriaPrima,
                        principalSchema: "nutri",
                        principalTable: "MateriePrime",
                        principalColumn: "IdMateriaPrima",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RapportiNutrizionali",
                schema: "nutri",
                columns: table => new
                {
                    IdRapporto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRicetta = table.Column<int>(type: "int", nullable: false),
                    DataRapporto = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true),
                    RedattoDa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Committente = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Denominazione = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Lotto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Risultati = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RicettaUsata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VersioneCalcolo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VersioneApplicazione = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NomeFileGenerato = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PercorsoDocumento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RapportiNutrizionali", x => x.IdRapporto);
                    table.ForeignKey(
                        name: "FK_RapportiNutrizionali_Ricette_IdRicetta",
                        column: x => x.IdRicetta,
                        principalSchema: "nutri",
                        principalTable: "Ricette",
                        principalColumn: "IdRicetta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RapportiNutrizionali_Utenti_IdUtente",
                        column: x => x.IdUtente,
                        principalSchema: "sec",
                        principalTable: "Utenti",
                        principalColumn: "IdUtente",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RigheRicetta",
                schema: "nutri",
                columns: table => new
                {
                    IdRigaRicetta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdRicetta = table.Column<int>(type: "int", nullable: false),
                    IdMateriaPrima = table.Column<int>(type: "int", nullable: false),
                    QuantitaKg = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    Ordine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheRicetta", x => x.IdRigaRicetta);
                    table.ForeignKey(
                        name: "FK_RigheRicetta_MateriePrime_IdMateriaPrima",
                        column: x => x.IdMateriaPrima,
                        principalSchema: "nutri",
                        principalTable: "MateriePrime",
                        principalColumn: "IdMateriaPrima",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RigheRicetta_Ricette_IdRicetta",
                        column: x => x.IdRicetta,
                        principalSchema: "nutri",
                        principalTable: "Ricette",
                        principalColumn: "IdRicetta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fonti_Tipo",
                schema: "nutri",
                table: "Fonti",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_MateriePrime_Denominazione",
                schema: "nutri",
                table: "MateriePrime",
                column: "Denominazione");

            migrationBuilder.CreateIndex(
                name: "IX_MateriePrime_IdClienteArcLab",
                schema: "nutri",
                table: "MateriePrime",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_MateriePrime_Tipo",
                schema: "nutri",
                table: "MateriePrime",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_RapportiNutrizionali_DataRapporto",
                schema: "nutri",
                table: "RapportiNutrizionali",
                column: "DataRapporto",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RapportiNutrizionali_IdRicetta",
                schema: "nutri",
                table: "RapportiNutrizionali",
                column: "IdRicetta");

            migrationBuilder.CreateIndex(
                name: "IX_RapportiNutrizionali_IdUtente",
                schema: "nutri",
                table: "RapportiNutrizionali",
                column: "IdUtente");

            migrationBuilder.CreateIndex(
                name: "IX_Ricette_Denominazione",
                schema: "nutri",
                table: "Ricette",
                column: "Denominazione");

            migrationBuilder.CreateIndex(
                name: "IX_Ricette_IdClienteArcLab",
                schema: "nutri",
                table: "Ricette",
                column: "IdClienteArcLab");

            migrationBuilder.CreateIndex(
                name: "IX_RigheRicetta_IdMateriaPrima",
                schema: "nutri",
                table: "RigheRicetta",
                column: "IdMateriaPrima");

            migrationBuilder.CreateIndex(
                name: "IX_RigheRicetta_IdRicetta",
                schema: "nutri",
                table: "RigheRicetta",
                column: "IdRicetta");

            migrationBuilder.CreateIndex(
                name: "IX_ValoriNutrizionali_IdFonte",
                schema: "nutri",
                table: "ValoriNutrizionali",
                column: "IdFonte");

            migrationBuilder.CreateIndex(
                name: "IX_ValoriNutrizionali_IdMateriaPrima_Corrente",
                schema: "nutri",
                table: "ValoriNutrizionali",
                columns: new[] { "IdMateriaPrima", "Corrente" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RapportiNutrizionali",
                schema: "nutri");

            migrationBuilder.DropTable(
                name: "RigheRicetta",
                schema: "nutri");

            migrationBuilder.DropTable(
                name: "ValoriNutrizionali",
                schema: "nutri");

            migrationBuilder.DropTable(
                name: "Ricette",
                schema: "nutri");

            migrationBuilder.DropTable(
                name: "Fonti",
                schema: "nutri");

            migrationBuilder.DropTable(
                name: "MateriePrime",
                schema: "nutri");
        }
    }
}
