using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccettazioneLabEsterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CertificatoNonRichiesto",
                schema: "accettazione",
                table: "Campioni",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConsegnaPrevistaLabEstGiorni",
                schema: "accettazione",
                table: "Campioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInvioLab",
                schema: "accettazione",
                table: "Campioni",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdLabEsternoArcLab",
                schema: "accettazione",
                table: "Campioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabEsterno",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LuogoPrelievo",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteLabEsterno",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificatoNonRichiesto",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "ConsegnaPrevistaLabEstGiorni",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "DataInvioLab",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "IdLabEsternoArcLab",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "LabEsterno",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "LuogoPrelievo",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "NoteLabEsterno",
                schema: "accettazione",
                table: "Campioni");
        }
    }
}
