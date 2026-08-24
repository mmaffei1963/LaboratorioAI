using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Scadenziario tarature (schema "tarature").

public class ConfigurazioneTermometroEsterno : IEntityTypeConfiguration<TermometroEsterno>
{
    public void Configure(EntityTypeBuilder<TermometroEsterno> e)
    {
        e.ToTable("TermometriEsterni", "tarature");
        e.HasKey(x => x.IdTermometroEsterno);
        e.Property(x => x.Tipologia).HasMaxLength(100);
        e.Property(x => x.Cliente).HasMaxLength(255);
        e.Property(x => x.Riferimento).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.OperatoreConsegna).HasMaxLength(255);
        e.Property(x => x.RifFattura).HasMaxLength(100);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.NumeroRapporto);
        e.HasIndex(x => x.DataScadenza);
        e.HasIndex(x => x.IdTaraturaOrigine);
    }
}
