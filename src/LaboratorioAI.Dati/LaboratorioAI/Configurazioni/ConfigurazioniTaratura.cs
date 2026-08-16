using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Taratura termometri: schema "taratura". I valori metrologici sono double
// (float in SQL) per riprodurre esattamente il calcolo del foglio storico.

public class ConfigurazioneCampioneRiferimento : IEntityTypeConfiguration<CampioneRiferimento>
{
    public void Configure(EntityTypeBuilder<CampioneRiferimento> e)
    {
        e.ToTable("CampioniRiferimento", "taratura");
        e.HasKey(x => x.IdCampione);

        e.Property(x => x.Identificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.TipoTermometro).HasMaxLength(255);
        e.Property(x => x.CodiceInterno).HasMaxLength(100);
        e.Property(x => x.CentroLat).HasMaxLength(100);
        e.Property(x => x.NumeroCertificato).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Identificazione);
        e.HasIndex(x => x.DataScadenza);
    }
}

public class ConfigurazionePuntoCertificato : IEntityTypeConfiguration<PuntoCertificato>
{
    public void Configure(EntityTypeBuilder<PuntoCertificato> e)
    {
        e.ToTable("PuntiCertificato", "taratura");
        e.HasKey(x => x.IdPunto);

        e.HasOne(x => x.Campione).WithMany(c => c.Punti)
         .HasForeignKey(x => x.IdCampione).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.IdCampione);
    }
}

public class ConfigurazioneTaratura : IEntityTypeConfiguration<Taratura>
{
    public void Configure(EntityTypeBuilder<Taratura> e)
    {
        e.ToTable("Tarature", "taratura");
        e.HasKey(x => x.IdTaratura);

        e.Property(x => x.StrumentoIdentificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.StrumentoCodice).HasMaxLength(100);
        e.Property(x => x.Committente).HasMaxLength(255);
        e.Property(x => x.NumeroRapporto).HasMaxLength(50);
        e.Property(x => x.RiferimentoIstruzione).HasMaxLength(100);
        e.Property(x => x.Responsabile).HasMaxLength(255);
        e.Property(x => x.DatiMisure).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.CampioneRiferimento).WithMany()
         .HasForeignKey(x => x.IdCampioneRiferimento).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataTaratura).IsDescending();
        e.HasIndex(x => x.DataScadenza);
        e.HasIndex(x => x.IdClienteArcLab);
    }
}

public class ConfigurazioneTaraturaIncubatore : IEntityTypeConfiguration<TaraturaIncubatore>
{
    public void Configure(EntityTypeBuilder<TaraturaIncubatore> e)
    {
        e.ToTable("TaratureIncubatori", "taratura");
        e.HasKey(x => x.IdTaratura);

        e.Property(x => x.MisuratoreIdentificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.MisuratoreCodice).HasMaxLength(100);
        e.Property(x => x.ApparecchiaturaIdentificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.ApparecchiaturaCodice).HasMaxLength(100);
        e.Property(x => x.NumeroRapporto).HasMaxLength(50);
        e.Property(x => x.RiferimentoIstruzione).HasMaxLength(100);
        e.Property(x => x.Responsabile).HasMaxLength(255);
        e.Property(x => x.DatiMisure).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.CampioneRiferimento).WithMany()
         .HasForeignKey(x => x.IdCampioneRiferimento).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataTaratura).IsDescending();
        e.HasIndex(x => x.DataScadenza);
    }
}

public class ConfigurazioneEsitoValidazioneTaratura : IEntityTypeConfiguration<EsitoValidazioneTaratura>
{
    public void Configure(EntityTypeBuilder<EsitoValidazioneTaratura> e)
    {
        e.ToTable("ValidazioniCalcolo", "taratura");
        e.HasKey(x => x.IdEsito);

        e.Property(x => x.VersioneSet).HasMaxLength(20).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.Property(x => x.Operatore).HasMaxLength(255);
        e.Property(x => x.Dettaglio).IsRequired();

        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataEsecuzione).IsDescending();
    }
}

public class ConfigurazioneTaraturaMappatura : IEntityTypeConfiguration<TaraturaMappatura>
{
    public void Configure(EntityTypeBuilder<TaraturaMappatura> e)
    {
        e.ToTable("Mappature", "taratura");
        e.HasKey(x => x.IdMappatura);

        e.Property(x => x.ApparecchiaturaIdentificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.ApparecchiaturaCodice).HasMaxLength(100);
        e.Property(x => x.NumeroRapporto).HasMaxLength(50);
        e.Property(x => x.RiferimentoIstruzione).HasMaxLength(100);
        e.Property(x => x.Responsabile).HasMaxLength(255);
        e.Property(x => x.DatiMisure).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.CampioneRiferimento).WithMany()
         .HasForeignKey(x => x.IdCampioneRiferimento).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataMappatura).IsDescending();
        e.HasIndex(x => x.DataScadenza);
    }
}

public class ConfigurazioneEsitoValidazioneMappatura : IEntityTypeConfiguration<EsitoValidazioneMappatura>
{
    public void Configure(EntityTypeBuilder<EsitoValidazioneMappatura> e)
    {
        e.ToTable("ValidazioniCalcoloMappatura", "taratura");
        e.HasKey(x => x.IdEsito);
        e.Property(x => x.VersioneSet).HasMaxLength(20).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.Property(x => x.Operatore).HasMaxLength(255);
        e.Property(x => x.Dettaglio).IsRequired();
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.DataEsecuzione).IsDescending();
    }
}

public class ConfigurazioneAnalisiMappatura : IEntityTypeConfiguration<AnalisiMappatura>
{
    public void Configure(EntityTypeBuilder<AnalisiMappatura> e)
    {
        e.ToTable("AnalisiMappature", "taratura");
        e.HasKey(x => x.IdAnalisi);
        e.Property(x => x.Firma).HasMaxLength(100).IsRequired();
        e.Property(x => x.Esito).HasMaxLength(255);
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.HasOne(x => x.Mappatura).WithMany().HasForeignKey(x => x.IdMappatura).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.IdMappatura, x.Firma });
    }
}

public class ConfigurazioneEsitoValidazioneIncubatore : IEntityTypeConfiguration<EsitoValidazioneIncubatore>
{
    public void Configure(EntityTypeBuilder<EsitoValidazioneIncubatore> e)
    {
        e.ToTable("ValidazioniCalcoloIncubatori", "taratura");
        e.HasKey(x => x.IdEsito);

        e.Property(x => x.VersioneSet).HasMaxLength(20).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.Property(x => x.Operatore).HasMaxLength(255);
        e.Property(x => x.Dettaglio).IsRequired();

        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataEsecuzione).IsDescending();
    }
}

public class ConfigurazioneAnalisiIncubatore : IEntityTypeConfiguration<AnalisiIncubatore>
{
    public void Configure(EntityTypeBuilder<AnalisiIncubatore> e)
    {
        e.ToTable("AnalisiIncubatori", "taratura");
        e.HasKey(x => x.IdAnalisi);

        e.Property(x => x.Firma).HasMaxLength(100).IsRequired();
        e.Property(x => x.Esito).HasMaxLength(255);
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);

        e.HasOne(x => x.Taratura).WithMany()
         .HasForeignKey(x => x.IdTaratura).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.IdTaratura, x.Firma });
    }
}

public class ConfigurazioneAnalisiTaratura : IEntityTypeConfiguration<AnalisiTaratura>
{
    public void Configure(EntityTypeBuilder<AnalisiTaratura> e)
    {
        e.ToTable("AnalisiTarature", "taratura");
        e.HasKey(x => x.IdAnalisi);

        e.Property(x => x.Firma).HasMaxLength(100).IsRequired();
        e.Property(x => x.Esito).HasMaxLength(255);
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);

        e.HasOne(x => x.Taratura).WithMany()
         .HasForeignKey(x => x.IdTaratura).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.IdTaratura, x.Firma });
    }
}
