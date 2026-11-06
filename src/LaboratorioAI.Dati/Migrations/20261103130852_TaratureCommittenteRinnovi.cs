using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class TaratureCommittenteRinnovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataRinnovo",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTermometroRinnovo",
                schema: "tarature",
                table: "TermometriEsterni",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Committente",
                schema: "taratura",
                table: "TaratureIncubatori",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdClienteArcLab",
                schema: "taratura",
                table: "TaratureIncubatori",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "TaratureIncubatori",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insediamento",
                schema: "taratura",
                table: "TaratureIncubatori",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Committente",
                schema: "bilance",
                table: "TaratureBilance",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdClienteArcLab",
                schema: "bilance",
                table: "TaratureBilance",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "bilance",
                table: "TaratureBilance",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insediamento",
                schema: "bilance",
                table: "TaratureBilance",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "Tarature",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insediamento",
                schema: "taratura",
                table: "Tarature",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Committente",
                schema: "taratura",
                table: "Mappature",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdClienteArcLab",
                schema: "taratura",
                table: "Mappature",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "Mappature",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Insediamento",
                schema: "taratura",
                table: "Mappature",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataRinnovo",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "IdTermometroRinnovo",
                schema: "tarature",
                table: "TermometriEsterni");

            migrationBuilder.DropColumn(
                name: "Committente",
                schema: "taratura",
                table: "TaratureIncubatori");

            migrationBuilder.DropColumn(
                name: "IdClienteArcLab",
                schema: "taratura",
                table: "TaratureIncubatori");

            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "TaratureIncubatori");

            migrationBuilder.DropColumn(
                name: "Insediamento",
                schema: "taratura",
                table: "TaratureIncubatori");

            migrationBuilder.DropColumn(
                name: "Committente",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "IdClienteArcLab",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "Insediamento",
                schema: "bilance",
                table: "TaratureBilance");

            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "Tarature");

            migrationBuilder.DropColumn(
                name: "Insediamento",
                schema: "taratura",
                table: "Tarature");

            migrationBuilder.DropColumn(
                name: "Committente",
                schema: "taratura",
                table: "Mappature");

            migrationBuilder.DropColumn(
                name: "IdClienteArcLab",
                schema: "taratura",
                table: "Mappature");

            migrationBuilder.DropColumn(
                name: "IdInsediamentoArcLab",
                schema: "taratura",
                table: "Mappature");

            migrationBuilder.DropColumn(
                name: "Insediamento",
                schema: "taratura",
                table: "Mappature");
        }
    }
}
