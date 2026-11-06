using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class UtentiERuoli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Utenti_ObjectIdEntra",
                schema: "sec",
                table: "Utenti");

            migrationBuilder.AlterColumn<Guid>(
                name: "ObjectIdEntra",
                schema: "sec",
                table: "Utenti",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "sec",
                table: "Utenti",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Attivo",
                schema: "sec",
                table: "Ruoli",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Ordine",
                schema: "sec",
                table: "Ruoli",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_ObjectIdEntra",
                schema: "sec",
                table: "Utenti",
                column: "ObjectIdEntra",
                unique: true,
                filter: "[ObjectIdEntra] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Utenti_ObjectIdEntra",
                schema: "sec",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "sec",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "Attivo",
                schema: "sec",
                table: "Ruoli");

            migrationBuilder.DropColumn(
                name: "Ordine",
                schema: "sec",
                table: "Ruoli");

            migrationBuilder.AlterColumn<Guid>(
                name: "ObjectIdEntra",
                schema: "sec",
                table: "Utenti",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_ObjectIdEntra",
                schema: "sec",
                table: "Utenti",
                column: "ObjectIdEntra",
                unique: true);
        }
    }
}
