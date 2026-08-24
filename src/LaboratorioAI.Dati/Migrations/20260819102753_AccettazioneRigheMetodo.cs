using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class AccettazioneRigheMetodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Metodo",
                schema: "accettazione",
                table: "Prove",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdRigaMetodoArcLab",
                schema: "accettazione",
                table: "Prove",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaboratorioEsterno",
                schema: "accettazione",
                table: "Prove",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Parametro",
                schema: "accettazione",
                table: "Prove",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerbalePrelievo",
                schema: "accettazione",
                table: "Lotti",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vettore",
                schema: "accettazione",
                table: "Lotti",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraLegionella",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CondizioniTrasporto",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataPrevistaConsegna",
                schema: "accettazione",
                table: "Campioni",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtichettaCliente",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTermometroArcLab",
                schema: "accettazione",
                table: "Campioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identificativo",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Imballaggio",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProceduraPrelievo",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RitardoAnalisiGiorni",
                schema: "accettazione",
                table: "Campioni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiglaCliente",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TempRicezione",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Termometro",
                schema: "accettazione",
                table: "Campioni",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdRigaMetodoArcLab",
                schema: "accettazione",
                table: "Prove");

            migrationBuilder.DropColumn(
                name: "LaboratorioEsterno",
                schema: "accettazione",
                table: "Prove");

            migrationBuilder.DropColumn(
                name: "Parametro",
                schema: "accettazione",
                table: "Prove");

            migrationBuilder.DropColumn(
                name: "VerbalePrelievo",
                schema: "accettazione",
                table: "Lotti");

            migrationBuilder.DropColumn(
                name: "Vettore",
                schema: "accettazione",
                table: "Lotti");

            migrationBuilder.DropColumn(
                name: "CameraLegionella",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "CondizioniTrasporto",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "DataPrevistaConsegna",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "EtichettaCliente",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "IdTermometroArcLab",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "Identificativo",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "Imballaggio",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "ProceduraPrelievo",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "RitardoAnalisiGiorni",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "SiglaCliente",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "TempRicezione",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.DropColumn(
                name: "Termometro",
                schema: "accettazione",
                table: "Campioni");

            migrationBuilder.AlterColumn<string>(
                name: "Metodo",
                schema: "accettazione",
                table: "Prove",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
