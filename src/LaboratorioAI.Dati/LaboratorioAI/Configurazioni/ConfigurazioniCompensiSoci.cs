using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Compensi soci (21/10/2026): schema «soci». Definizione: claude/definizione-compensi-soci.md

public class ConfigurazioneSocio : IEntityTypeConfiguration<Socio>
{
    public void Configure(EntityTypeBuilder<Socio> e)
    {
        e.ToTable("Soci", "soci");
        e.HasKey(x => x.IdSocio);
        e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        e.Property(x => x.Email).HasMaxLength(255).IsRequired();
        e.Property(x => x.VoceCompensiAzw).HasMaxLength(10).IsRequired();
        e.Property(x => x.CommessaRsppAzw).HasMaxLength(20);
        e.Property(x => x.OperatoreTrasferteAzw).HasMaxLength(150);
        e.HasIndex(x => x.Email).IsUnique();
    }
}

public class ConfigurazioneAnnoCompensi : IEntityTypeConfiguration<AnnoCompensi>
{
    public void Configure(EntityTypeBuilder<AnnoCompensi> e)
    {
        e.ToTable("AnniCompensi", "soci");
        e.HasKey(x => x.IdAnnoCompensi);
        e.HasIndex(x => x.Anno).IsUnique();
        foreach (var p in new[] { nameof(AnnoCompensi.BaseTempoPieno), nameof(AnnoCompensi.CompensoAmministratore),
                     nameof(AnnoCompensi.CompensoResponsabileArea), nameof(AnnoCompensi.AssicurazioneAuto), nameof(AnnoCompensi.Premio),
                     nameof(AnnoCompensi.BaseLaboratorioManuale), nameof(AnnoCompensi.BaseLaboratorioAzwChiusura) })
            e.Property(p).HasPrecision(18, 2);
        e.Property(x => x.OreTempoPieno).HasPrecision(6, 2);
        e.Property(x => x.TrasferteDaAzw).HasDefaultValue(true).HasSentinel(true);   // righe già esistenti: vero
        e.Property(x => x.PercentualeRspp).HasPrecision(9, 4);
        e.Property(x => x.PercentualeIndennitaLaboratorio).HasPrecision(9, 4);
        e.Property(x => x.NotaBaseLaboratorio).HasMaxLength(500);
        e.Property(x => x.ChiusoDa).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(1000);
    }
}

public class ConfigurazionePercentualeVoceLaboratorio : IEntityTypeConfiguration<PercentualeVoceLaboratorio>
{
    public void Configure(EntityTypeBuilder<PercentualeVoceLaboratorio> e)
    {
        e.ToTable("PercentualiVociLaboratorio", "soci");
        e.HasKey(x => x.IdPercentualeVoceLaboratorio);
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Percentuale).HasPrecision(9, 4);
        e.HasIndex(x => new { x.Anno, x.Voce });
    }
}

public class ConfigurazioneCompensoSocioAnno : IEntityTypeConfiguration<CompensoSocioAnno>
{
    public void Configure(EntityTypeBuilder<CompensoSocioAnno> e)
    {
        e.ToTable("CompensiSociAnno", "soci");
        e.HasKey(x => x.IdCompensoSocioAnno);
        e.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.IdSocio).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Anno, x.IdSocio }).IsUnique();
        e.Property(x => x.OreSettimanali).HasPrecision(6, 2);
        e.Property(x => x.PercentualeAutoPropria).HasPrecision(9, 4);
        foreach (var p in new[] { nameof(CompensoSocioAnno.Premio), nameof(CompensoSocioAnno.RecuperoManuale),
                     nameof(CompensoSocioAnno.RettificaPercepito), nameof(CompensoSocioAnno.RsppAzwChiusura),
                     nameof(CompensoSocioAnno.PercepitoAzwChiusura), nameof(CompensoSocioAnno.TrasferteAzwChiusura),
                     nameof(CompensoSocioAnno.QuotaFissaAmministrazione) })
            e.Property(p).HasPrecision(18, 2);
        e.Property(x => x.NotaRettificaPercepito).HasMaxLength(500);
        e.Property(x => x.RipartizioneAree).HasMaxLength(200);
        e.Property(x => x.RipartizioneDifferenza).HasMaxLength(200);
        e.Property(x => x.Note).HasMaxLength(1000);
    }
}

public class ConfigurazionePremioAreaLaboratorio : IEntityTypeConfiguration<PremioAreaLaboratorio>
{
    public void Configure(EntityTypeBuilder<PremioAreaLaboratorio> e)
    {
        e.ToTable("PremiAreaLaboratorio", "soci");
        e.HasKey(x => x.IdPremioAreaLaboratorio);
        e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        e.Property(x => x.Importo).HasPrecision(18, 2);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasOne<Socio>().WithMany().HasForeignKey(x => x.IdSocio).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Anno);
    }
}

public class ConfigurazioneCategoriaRimborso : IEntityTypeConfiguration<CategoriaRimborso>
{
    public void Configure(EntityTypeBuilder<CategoriaRimborso> e)
    {
        e.ToTable("CategorieRimborsi", "soci");
        e.HasKey(x => x.IdCategoriaRimborso);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Maggiorazione).HasPrecision(9, 4);
    }
}

public class ConfigurazioneRimborsoSpesa : IEntityTypeConfiguration<RimborsoSpesa>
{
    public void Configure(EntityTypeBuilder<RimborsoSpesa> e)
    {
        e.ToTable("RimborsiSpese", "soci");
        e.HasKey(x => x.IdRimborsoSpesa);
        e.Property(x => x.Data).HasColumnType("date");
        e.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        e.Property(x => x.Importo).HasPrecision(18, 2);
        e.Property(x => x.Maggiorazione).HasPrecision(9, 4);
        e.Property(x => x.Km).HasPrecision(10, 1);
        e.Property(x => x.InseritoDa).HasMaxLength(255);
        e.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.IdSocio).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.IdCategoriaRimborso).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Ricevuta).WithOne().HasForeignKey<RicevutaRimborso>(x => x.IdRimborsoSpesa).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdSocio, x.Data });
    }
}

public class ConfigurazioneRicevutaRimborso : IEntityTypeConfiguration<RicevutaRimborso>
{
    public void Configure(EntityTypeBuilder<RicevutaRimborso> e)
    {
        e.ToTable("RicevuteRimborsi", "soci");
        e.HasKey(x => x.IdRicevutaRimborso);
        e.Property(x => x.NomeFile).HasMaxLength(255).IsRequired();
        e.Property(x => x.TipoContenuto).HasMaxLength(100).IsRequired();
        e.Property(x => x.Contenuto).IsRequired();
        e.HasIndex(x => x.IdRimborsoSpesa).IsUnique();
    }
}

public class ConfigurazioneTrasfertaAzw : IEntityTypeConfiguration<TrasfertaAzw>
{
    public void Configure(EntityTypeBuilder<TrasfertaAzw> e)
    {
        e.ToTable("TrasferteAzw", "soci");
        e.HasKey(x => x.IdTrasfertaAzw);
        e.Property(x => x.Operatore).HasMaxLength(150);
        e.Property(x => x.Data).HasColumnType("date");
        e.Property(x => x.Km).HasPrecision(10, 1);
        e.Property(x => x.CostoKm).HasPrecision(9, 4);
        e.Property(x => x.Autostrada).HasPrecision(18, 2);
        e.Property(x => x.VittoAlloggio).HasPrecision(18, 2);
        e.Property(x => x.CostiDiversi).HasPrecision(18, 2);
        e.Property(x => x.Mezzo).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Ignore(x => x.MezzoPrivato); e.Ignore(x => x.CostoChilometrico); e.Ignore(x => x.Rimborso);
        e.HasIndex(x => new { x.Data, x.Operatore });
    }
}
