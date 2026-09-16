using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Ordini di Accettazione emessi (schema "accettazione"). Registro anti-doppione: nessuna scrittura su ArcLab.

public class ConfigurazioneOrdineAccettazioneEmesso : IEntityTypeConfiguration<OrdineAccettazioneEmesso>
{
    public void Configure(EntityTypeBuilder<OrdineAccettazioneEmesso> e)
    {
        e.ToTable("OrdiniEmessi", "accettazione");
        e.HasKey(x => x.IdOrdineAccettazione);

        e.Property(x => x.CodLotto).HasMaxLength(20);
        e.Property(x => x.Cliente).HasMaxLength(300);
        e.Property(x => x.Origine).HasMaxLength(20);
        e.Property(x => x.DataEmissione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.Property(x => x.NomeFile).HasMaxLength(300);
        e.Property(x => x.PercorsoFile).HasMaxLength(500);
        e.Property(x => x.Hash).HasMaxLength(64);
        e.Property(x => x.StatoInvio).HasConversion<int>();
        e.Property(x => x.UltimoErrore).HasMaxLength(1000);

        // La chiave logica anti-doppione: un solo Ordine di Accettazione per lotto.
        e.HasIndex(x => x.IdLotto).IsUnique();
        e.HasIndex(x => x.StatoInvio);
    }
}
