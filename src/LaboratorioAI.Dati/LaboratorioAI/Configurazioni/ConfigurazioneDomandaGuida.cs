using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Domande a «Chiedi alla guida» (11/10/2026).</summary>
public class ConfigurazioneDomandaGuida : IEntityTypeConfiguration<DomandaGuida>
{
    public void Configure(EntityTypeBuilder<DomandaGuida> e)
    {
        e.ToTable("DomandeGuida", "log");
        e.HasKey(x => x.IdDomandaGuida);

        e.Property(x => x.DataOra).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.EmailUtente).HasMaxLength(256);
        e.Property(x => x.NomeUtente).HasMaxLength(200);
        e.Property(x => x.Percorso).HasMaxLength(300);
        e.Property(x => x.CodiceGuida).HasMaxLength(60);
        e.Property(x => x.Domanda).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Modello).HasMaxLength(60);
        e.Property(x => x.Errore).HasMaxLength(1000);
        e.Property(x => x.CostoStimato).HasPrecision(12, 6);

        e.HasIndex(x => x.DataOra).IsDescending();
        e.HasIndex(x => new { x.IdUtente, x.DataOra });
    }
}
