using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaboratorioAI.Dati.Migrations
{
    /// <inheritdoc />
    public partial class PresenzeRegistroSoloAggiunta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 19/11/2026: il registro delle modifiche delle presenze è solo in aggiunta (nessun UPDATE né DELETE).
            migrationBuilder.Sql(@"
CREATE TRIGGER presenze.TR_RegistroModifiche_SoloAggiunta
ON presenze.RegistroModifiche
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'Il registro delle modifiche delle presenze e'' solo in aggiunta: non si modifica ne si cancella.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS presenze.TR_RegistroModifiche_SoloAggiunta");
        }
    }
}