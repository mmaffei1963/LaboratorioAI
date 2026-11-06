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

public class ConfigurazioneOrdinePrivato : IEntityTypeConfiguration<OrdinePrivato>
{
    public void Configure(EntityTypeBuilder<OrdinePrivato> e)
    {
        e.ToTable("OrdiniPrivati", "accettazione");
        e.HasKey(x => x.IdOrdinePrivato);
        e.Property(x => x.CodLotto).HasMaxLength(20);
        e.Property(x => x.Cliente).HasMaxLength(300);
        e.Property(x => x.EmailRapporto).HasMaxLength(320);
        e.Property(x => x.Consegna).HasConversion<int>();
        e.Property(x => x.FirmatoIl).HasColumnType("date");
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.NomeUtente).HasMaxLength(100);
        e.Property(x => x.UltimaModificaDa).HasMaxLength(100);
        e.Property(x => x.NomeFile).HasMaxLength(300);
        e.Property(x => x.PercorsoFile).HasMaxLength(500);
        e.Property(x => x.Hash).HasMaxLength(64);
        e.HasIndex(x => x.IdLotto).IsUnique();
        e.HasIndex(x => x.IdClienteArcLab);
    }
}
