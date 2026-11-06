using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Schema «scambio»: tabelle lette da Palomar per riportare dati in ArcLab (18/10/2026).</summary>
public class ConfigurazioneGiudizioDaRiportare : IEntityTypeConfiguration<GiudizioDaRiportare>
{
    public void Configure(EntityTypeBuilder<GiudizioDaRiportare> e)
    {
        e.ToTable("GiudiziDaRiportare", "scambio");
        e.HasKey(x => x.IdGiudizioDaRiportare);
        e.Property(x => x.NumeroCampione).HasMaxLength(50);
        e.Property(x => x.CodGiudizio).HasMaxLength(255);
        e.Property(x => x.GiudizioTesto).HasMaxLength(2000);
        e.Property(x => x.ColoreLimiti).HasMaxLength(20);
        e.Property(x => x.ValidatoDa).HasMaxLength(150).IsRequired();
        e.Property(x => x.AnnullatoDa).HasMaxLength(150);
        e.Property(x => x.Nota).HasMaxLength(1000);
        // Anti-doppione: una sola validazione non annullata per campione (Stato 3 = Annullato).
        e.HasIndex(x => x.IdCampione).IsUnique().HasFilter("[Stato] <> 3").HasDatabaseName("UX_GiudiziDaRiportare_IdCampione_Attivo");
        e.HasIndex(x => x.Stato);
    }
}
