using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Documenti del portale (19/11/2026). Si usa con ctx.Set&lt;LetturaDocumentoPortale&gt;().</summary>
public class ConfigurazioneLetturaDocumentoPortale : IEntityTypeConfiguration<LetturaDocumentoPortale>
{
    public void Configure(EntityTypeBuilder<LetturaDocumentoPortale> e)
    {
        e.ToTable("LettureDocumenti", "portale");
        e.HasKey(x => x.IdLetturaDocumentoPortale);
        e.Property(x => x.Origine).HasMaxLength(30).IsRequired();
        e.Property(x => x.DataAggiornamentoUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdClienteArcLab, x.IdEntry }).IsUnique();
    }
}
