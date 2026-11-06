using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class InvitiFirmatario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitiFirmatario",
                schema: "contratti",
                columns: table => new
                {
                    IdInvitoFirmatario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdClienteArcLab = table.Column<int>(type: "int", nullable: false),
                    ClienteRagioneSociale = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientePartitaIva = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmailDestinatari = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdRevisioneCondizioni = table.Column<int>(type: "int", nullable: false),
                    Stato = table.Column<int>(type: "int", nullable: false),
                    ImprontaCodiceLink = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ScadenzaLink = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvioAccodato = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataInvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataApertura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroPromemoria = table.Column<int>(type: "int", nullable: false),
                    DataUltimoPromemoria = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErroreInvio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NomeIndicato = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CognomeIndicato = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QualificaIndicata = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmailIndicata = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DataIndicazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdFirmatarioCliente = table.Column<int>(type: "int", nullable: true),
                    IdRichiestaApprovazione = table.Column<int>(type: "int", nullable: true),
                    DataElaborazione = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErroreElaborazione = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdUtenteCreazione = table.Column<int>(type: "int", nullable: true),
                    NomeUtenteCreazione = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCreazione = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DataUltimaModifica = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitiFirmatario", x => x.IdInvitoFirmatario);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitiFirmatario_IdClienteArcLab_Stato",
                schema: "contratti",
                table: "InvitiFirmatario",
                columns: new[] { "IdClienteArcLab", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_InvitiFirmatario_ImprontaCodiceLink",
                schema: "contratti",
                table: "InvitiFirmatario",
                column: "ImprontaCodiceLink",
                unique: true,
                filter: "[ImprontaCodiceLink] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvitiFirmatario_InvioAccodato",
                schema: "contratti",
                table: "InvitiFirmatario",
                column: "InvioAccodato",
                filter: "[InvioAccodato] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvitiFirmatario_Stato",
                schema: "contratti",
                table: "InvitiFirmatario",
                column: "Stato");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitiFirmatario",
                schema: "contratti");
        }
    }
}
