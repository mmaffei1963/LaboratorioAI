using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Preferiti della home per utente (schema app).</summary>
public class ConfigurazionePreferitoUtente : IEntityTypeConfiguration<PreferitoUtente>
{
    public void Configure(EntityTypeBuilder<PreferitoUtente> e)
    {
        e.ToTable("PreferitiUtenti", "app");
        e.HasKey(x => x.IdPreferitoUtente);
        e.Property(x => x.Url).HasMaxLength(200).IsRequired();
        e.Property(x => x.Attivo).HasDefaultValue(true);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdUtente, x.Url }).IsUnique();
        e.HasOne<Utente>().WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.Restrict);
    }
}
