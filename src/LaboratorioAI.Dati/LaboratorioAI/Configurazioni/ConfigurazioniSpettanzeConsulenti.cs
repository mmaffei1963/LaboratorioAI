using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Spettanze dei consulenti a percentuale (19/10/2026), schema provv.

public class ConfigurazioneConsulentePercentuale : IEntityTypeConfiguration<ConsulentePercentuale>
{
    public void Configure(EntityTypeBuilder<ConsulentePercentuale> e)
    {
        e.ToTable("ConsulentiPercentuale", "provv");
        e.HasKey(x => x.IdConsulentePercentuale);
        e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        e.Property(x => x.CodiceZona).HasMaxLength(20).IsRequired();
        e.Property(x => x.CodiceCommessa).HasMaxLength(20).IsRequired();
        e.Property(x => x.VoceCosto).HasMaxLength(10);
        e.Property(x => x.CodiceFornitore).HasMaxLength(20);
        e.Property(x => x.CentriDocenzeOrarie).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);
    }
}

public class ConfigurazioneContrattoConsulente : IEntityTypeConfiguration<ContrattoConsulente>
{
    public void Configure(EntityTypeBuilder<ContrattoConsulente> e)
    {
        e.ToTable("ContrattiConsulenti", "provv");
        e.HasKey(x => x.IdContrattoConsulente);
        e.Property(x => x.Riferimento).HasMaxLength(300);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.ModificatoDa).HasMaxLength(150).IsRequired();
        foreach (var p in new[] { nameof(ContrattoConsulente.PercConsulenza), nameof(ContrattoConsulente.PercCampionamento), nameof(ContrattoConsulente.PercCampionamentoEsterne),
                     nameof(ContrattoConsulente.PercAnalisiClienti), nameof(ContrattoConsulente.PercPrimoAnnoServizi),
                     nameof(ContrattoConsulente.PercFormazioneAltri), nameof(ContrattoConsulente.PercDocenza), nameof(ContrattoConsulente.PercTarature) })
            e.Property(p).HasPrecision(6, 3);
        e.HasOne<ConsulentePercentuale>().WithMany().HasForeignKey(x => x.IdConsulentePercentuale).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneVociGruppoSpettanze : IEntityTypeConfiguration<VociGruppoSpettanze>
{
    public void Configure(EntityTypeBuilder<VociGruppoSpettanze> e)
    {
        e.ToTable("VociGruppiSpettanze", "provv");
        e.HasKey(x => x.IdVociGruppoSpettanze);
        e.Property(x => x.Voci).HasMaxLength(1000).IsRequired();
        e.Property(x => x.ModificatoDa).HasMaxLength(150).IsRequired();
        e.HasIndex(x => x.Gruppo).IsUnique();
    }
}

public class ConfigurazioneAzwFatturaConsulente : IEntityTypeConfiguration<AzwFatturaConsulente>
{
    public void Configure(EntityTypeBuilder<AzwFatturaConsulente> e)
    {
        e.ToTable("AzwFattureConsulenti", "provv");
        e.HasKey(x => x.IdAzwFatturaConsulente);
        e.Property(x => x.CodiceFornitore).HasMaxLength(20).IsRequired();
        e.Property(x => x.Documento).HasMaxLength(60).IsRequired();
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Importo).HasPrecision(14, 2);
        e.HasIndex(x => new { x.CodiceFornitore, x.DataRegistrazione });
    }
}

public class ConfigurazioneAnnoSpettanzeChiuso : IEntityTypeConfiguration<AnnoSpettanzeChiuso>
{
    public void Configure(EntityTypeBuilder<AnnoSpettanzeChiuso> e)
    {
        e.ToTable("AnniSpettanzeChiusi", "provv");
        e.HasKey(x => x.IdAnnoSpettanzeChiuso);
        e.Property(x => x.Maturato).HasPrecision(14, 2);
        e.Property(x => x.Acconti).HasPrecision(14, 2);
        e.Property(x => x.DocenzeOrarie).HasPrecision(14, 2);
        e.Property(x => x.Conguaglio).HasPrecision(14, 2);
        e.Property(x => x.ChiusoDa).HasMaxLength(150).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.HasIndex(x => new { x.IdConsulentePercentuale, x.Anno }).IsUnique();
        e.HasOne<ConsulentePercentuale>().WithMany().HasForeignKey(x => x.IdConsulentePercentuale).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneMovimentoSpettanzaArchiviato : IEntityTypeConfiguration<MovimentoSpettanzaArchiviato>
{
    public void Configure(EntityTypeBuilder<MovimentoSpettanzaArchiviato> e)
    {
        e.ToTable("MovimentiSpettanze", "provv");
        e.HasKey(x => x.IdMovimentoSpettanza);
        e.Property(x => x.Voce).HasMaxLength(2).IsRequired();
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Documento).HasMaxLength(60).IsRequired();
        e.Property(x => x.VoceAzw).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20);
        e.Property(x => x.Origine).HasMaxLength(20).IsRequired();
        e.Property(x => x.Base).HasPrecision(14, 2);
        e.Property(x => x.Percentuale).HasPrecision(6, 3);
        e.Property(x => x.Importo).HasPrecision(14, 2);
        e.HasIndex(x => x.IdAnnoSpettanzeChiuso);
        e.HasOne<AnnoSpettanzeChiuso>().WithMany().HasForeignKey(x => x.IdAnnoSpettanzeChiuso).OnDelete(DeleteBehavior.Restrict);
    }
}
