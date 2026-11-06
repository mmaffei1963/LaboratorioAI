using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>22/10/2026: campioni tolti dall'elenco dei campioni in ritardo.</summary>
public class ConfigurazioneCampioneNascostoRitardi : IEntityTypeConfiguration<CampioneNascostoRitardi>
{
    public void Configure(EntityTypeBuilder<CampioneNascostoRitardi> e)
    {
        e.ToTable("CampioniNascostiRitardi", "app");
        e.HasKey(x => x.IdCampioneNascostoRitardi);
        e.Property(x => x.NumeroCampione).HasMaxLength(30).IsRequired();
        e.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
        e.Property(x => x.NascostoDa).HasMaxLength(255).IsRequired();
        e.Property(x => x.RipristinatoDa).HasMaxLength(255);
        e.HasIndex(x => new { x.IdCampioneArcLab, x.Attivo });
    }
}
