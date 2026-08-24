using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class ProposteControlli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "controlli");

            migrationBuilder.CreateTable(
                name: "Proposte",
                schema: "controlli",
                columns: table => new
                {
                    IdPropostaControllo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodiceLocale = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    Controllo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Origine = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataTrasmissione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NomeFileTracciato = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Attivo = table.Column<bool>(type: "bit", nullable: false),
                    DataCancellazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IdUtente = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposte", x => x.IdPropostaControllo);
                });

            migrationBuilder.CreateTable(
                name: "RigheMetodo",
                schema: "controlli",
                columns: table => new
                {
                    IdPropostaRigaMetodo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPropostaControllo = table.Column<int>(type: "int", nullable: false),
                    Progressivo = table.Column<int>(type: "int", nullable: false),
                    IdMetodoArcLab = table.Column<int>(type: "int", nullable: false),
                    CodMetodo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DenominazioneRiga = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CostoMetodoEuro = table.Column<decimal>(type: "money", nullable: true),
                    ScontoRiga = table.Column<double>(type: "float", nullable: false),
                    IdUnitaArcLab = table.Column<int>(type: "int", nullable: true),
                    UnitaMisura = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheMetodo", x => x.IdPropostaRigaMetodo);
                    table.ForeignKey(
                        name: "FK_RigheMetodo_Proposte_IdPropostaControllo",
                        column: x => x.IdPropostaControllo,
                        principalSchema: "controlli",
                        principalTable: "Proposte",
                        principalColumn: "IdPropostaControllo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposte_CodiceLocale",
                schema: "controlli",
                table: "Proposte",
                column: "CodiceLocale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposte_Stato_Attivo",
                schema: "controlli",
                table: "Proposte",
                columns: new[] { "Stato", "Attivo" });

            migrationBuilder.CreateIndex(
                name: "IX_RigheMetodo_IdPropostaControllo",
                schema: "controlli",
                table: "RigheMetodo",
                column: "IdPropostaControllo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RigheMetodo",
                schema: "controlli");

            migrationBuilder.DropTable(
                name: "Proposte",
                schema: "controlli");
        }
    }
}
