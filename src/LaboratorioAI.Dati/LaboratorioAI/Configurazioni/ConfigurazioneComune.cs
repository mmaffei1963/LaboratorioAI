using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Elenco ISTAT dei comuni italiani (04/11/2026).</summary>
public class ConfigurazioneComune : IEntityTypeConfiguration<Comune>
{
    public void Configure(EntityTypeBuilder<Comune> e)
    {
        e.ToTable("Comuni", "app");
        e.HasKey(x => x.IdComune);

        e.Property(x => x.CodiceIstat).HasMaxLength(6).IsFixedLength().IsRequired();
        e.Property(x => x.Nome).HasMaxLength(60).IsRequired();
        e.Property(x => x.SiglaProvincia).HasMaxLength(2).IsFixedLength().IsRequired();
        e.Property(x => x.Provincia).HasMaxLength(60).IsRequired();
        e.Property(x => x.CodiceRegione).HasMaxLength(2).IsFixedLength().IsRequired();
        e.Property(x => x.Regione).HasMaxLength(40).IsRequired();
        e.Property(x => x.CodiceCatastale).HasMaxLength(4).IsFixedLength();

        // Proprieta' calcolata: sta nel codice, non nel database.
        e.Ignore(x => x.NomeConProvincia);

        e.HasMany(x => x.Cap).WithOne(c => c.Comune!)
            .HasForeignKey(c => c.IdComune).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.CodiceIstat).IsUnique();
        // Sei comuni hanno lo stesso nome in province diverse: la coppia e' unica, il nome da solo no.
        e.HasIndex(x => new { x.Nome, x.SiglaProvincia }).IsUnique();
        e.HasIndex(x => x.SiglaProvincia);
    }
}

/// <summary>I CAP di un comune: 8.570 righe per 7.904 comuni.</summary>
public class ConfigurazioneComuneCap : IEntityTypeConfiguration<ComuneCap>
{
    public void Configure(EntityTypeBuilder<ComuneCap> e)
    {
        e.ToTable("ComuniCap", "app");
        e.HasKey(x => x.IdComuneCap);

        e.Property(x => x.Cap).HasMaxLength(5).IsFixedLength().IsRequired();

        e.HasIndex(x => new { x.IdComune, x.Ordine });
        e.HasIndex(x => new { x.IdComune, x.Cap }).IsUnique();
        // Dal CAP al comune: serve quando l'indirizzo arriva gia' scritto da ArcLab.
        e.HasIndex(x => x.Cap);
    }
}
