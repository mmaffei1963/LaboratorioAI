using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

public class ConfigurazioneCommerciale : IEntityTypeConfiguration<Commerciale>
{
    public void Configure(EntityTypeBuilder<Commerciale> e)
    {
        e.ToTable("Commerciali", "provv");
        e.HasKey(x => x.IdCommerciale);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Nome).IsUnique();
    }
}

public class ConfigurazioneCodiceVenditore : IEntityTypeConfiguration<CodiceVenditoreCommerciale>
{
    public void Configure(EntityTypeBuilder<CodiceVenditoreCommerciale> e)
    {
        e.ToTable("CodiciVenditore", "provv");
        e.HasKey(x => x.IdCodiceVenditore);
        e.Property(x => x.CodiceVenditore).HasMaxLength(10).IsRequired();
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CodiceVenditore, x.ValidoDal }).IsUnique();
    }
}

public class ConfigurazioneAzwFattura : IEntityTypeConfiguration<AzwFattura>
{
    public void Configure(EntityTypeBuilder<AzwFattura> e)
    {
        e.ToTable("AzwFatture", "provv");
        e.HasKey(x => x.IdAzwFattura);
        e.Property(x => x.Chiave).HasMaxLength(60).IsRequired();
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.RagioneSociale).HasMaxLength(255);
        e.Property(x => x.CodiceVenditore).HasMaxLength(10);
        e.Property(x => x.NettoPerimetro).HasPrecision(14, 2);
        e.Property(x => x.NettoTotale).HasPrecision(14, 2);
        e.Property(x => x.Lordo).HasPrecision(14, 2);
        e.HasIndex(x => x.Chiave).IsUnique();
        e.HasIndex(x => new { x.CodiceCliente, x.DataFattura });
    }
}

public class ConfigurazioneAzwIncasso : IEntityTypeConfiguration<AzwIncasso>
{
    public void Configure(EntityTypeBuilder<AzwIncasso> e)
    {
        e.ToTable("AzwIncassi", "provv");
        e.HasKey(x => x.IdAzwIncasso);
        e.Property(x => x.ChiaveFattura).HasMaxLength(60).IsRequired();
        e.Property(x => x.ImportoLordo).HasPrecision(14, 2);
        e.HasIndex(x => new { x.ChiaveFattura, x.DataIncasso });
        e.HasIndex(x => x.DataIncasso);
    }
}

public class ConfigurazioneAzwNotaCredito : IEntityTypeConfiguration<AzwNotaCredito>
{
    public void Configure(EntityTypeBuilder<AzwNotaCredito> e)
    {
        e.ToTable("AzwNoteCredito", "provv");
        e.HasKey(x => x.IdAzwNotaCredito);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Riferimento).HasMaxLength(60).IsRequired();
        e.Property(x => x.NettoPerimetro).HasPrecision(14, 2);
        e.Property(x => x.NettoTotale).HasPrecision(14, 2);
        e.HasIndex(x => new { x.CodiceCliente, x.Data });
    }
}

public class ConfigurazioneImportazioneAzw : IEntityTypeConfiguration<ImportazioneAzw>
{
    public void Configure(EntityTypeBuilder<ImportazioneAzw> e)
    {
        e.ToTable("ImportazioniAzw", "provv");
        e.HasKey(x => x.IdImportazione);
        e.Property(x => x.PercorsoFile).HasMaxLength(500).IsRequired();
        e.Property(x => x.Messaggio).HasMaxLength(2000);
        e.HasIndex(x => x.DataOra);
    }
}

public class ConfigurazioneElencoSviluppo : IEntityTypeConfiguration<VoceElencoSviluppo>
{
    public void Configure(EntityTypeBuilder<VoceElencoSviluppo> e)
    {
        e.ToTable("ElencoSviluppo", "provv");
        e.HasKey(x => x.IdElencoSviluppo);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Obiettivo).HasPrecision(14, 2);
        e.Property(x => x.Motivo).HasMaxLength(50);
        e.Property(x => x.AzionePrevista).HasMaxLength(100);
        e.Property(x => x.ApprovatoDa).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdCommerciale, x.Anno, x.CodiceCliente });
    }
}

public class ConfigurazioneTrimestreProvvigioni : IEntityTypeConfiguration<TrimestreProvvigioni>
{
    public void Configure(EntityTypeBuilder<TrimestreProvvigioni> e)
    {
        e.ToTable("Trimestri", "provv");
        e.HasKey(x => x.IdTrimestre);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(50).IsRequired();
        e.HasIndex(x => new { x.Anno, x.Numero }).IsUnique();
    }
}

public class ConfigurazioneMovimentoProvvigione : IEntityTypeConfiguration<MovimentoProvvigioneArchiviato>
{
    public void Configure(EntityTypeBuilder<MovimentoProvvigioneArchiviato> e)
    {
        e.ToTable("Movimenti", "provv");
        e.HasKey(x => x.IdMovimento);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Riferimento).HasMaxLength(60).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Voce).HasConversion<int>();
        e.Property(x => x.Categoria).HasConversion<int>();
        e.Property(x => x.Base).HasPrecision(14, 2);
        e.Property(x => x.Aliquota).HasPrecision(6, 4);
        e.Property(x => x.Importo).HasPrecision(14, 2);
        e.HasOne<TrimestreProvvigioni>().WithMany().HasForeignKey(x => x.IdTrimestre).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdTrimestre, x.IdCommerciale });
    }
}

public class ConfigurazionePremioProvvigioni : IEntityTypeConfiguration<PremioProvvigioni>
{
    public void Configure(EntityTypeBuilder<PremioProvvigioni> e)
    {
        e.ToTable("Premi", "provv");
        e.HasKey(x => x.IdPremio);
        e.Property(x => x.BaseIncassata).HasPrecision(14, 2);
        e.Property(x => x.Premio).HasPrecision(14, 2);
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdCommerciale, x.Anno }).IsUnique();
    }
}

public class ConfigurazioneConvenzioneProvvigioni : IEntityTypeConfiguration<ConvenzioneProvvigioni>
{
    public void Configure(EntityTypeBuilder<ConvenzioneProvvigioni> e)
    {
        e.ToTable("Convenzioni", "provv");
        e.HasKey(x => x.IdConvenzione);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Origine).HasConversion<int>();
        e.Property(x => x.Fonte).HasConversion<int>();
        e.Property(x => x.Descrizione).HasMaxLength(200);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdCommerciale, x.CodiceCliente });
    }
}

public class ConfigurazioneAzwRigaFattura : IEntityTypeConfiguration<AzwRigaFattura>
{
    public void Configure(EntityTypeBuilder<AzwRigaFattura> e)
    {
        e.ToTable("AzwRigheFattura", "provv");
        e.HasKey(x => x.IdAzwRigaFattura);
        e.Property(x => x.ChiaveFattura).HasMaxLength(60).IsRequired();
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20);
        e.Property(x => x.Netto).HasPrecision(14, 2);
        e.HasIndex(x => x.ChiaveFattura);
    }
}

public class ConfigurazioneAzwRigaNotaCredito : IEntityTypeConfiguration<AzwRigaNotaCredito>
{
    public void Configure(EntityTypeBuilder<AzwRigaNotaCredito> e)
    {
        e.ToTable("AzwRigheNoteCredito", "provv");
        e.HasKey(x => x.IdAzwRigaNotaCredito);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Riferimento).HasMaxLength(60).IsRequired();
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20);
        e.Property(x => x.Netto).HasPrecision(14, 2);
        e.HasIndex(x => x.CodiceCliente);
    }
}

public class ConfigurazioneBaseCalcolo : IEntityTypeConfiguration<BaseCalcolo>
{
    public void Configure(EntityTypeBuilder<BaseCalcolo> e)
    {
        e.ToTable("BasiCalcolo", "provv");
        e.HasKey(x => x.IdBaseCalcolo);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.Property(x => x.Elementi).HasMaxLength(2000).IsRequired();
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Nome);
    }
}

public class ConfigurazioneAccordoProvvigioni : IEntityTypeConfiguration<AccordoProvvigioni>
{
    public void Configure(EntityTypeBuilder<AccordoProvvigioni> e)
    {
        e.ToTable("Accordi", "provv");
        e.HasKey(x => x.IdAccordo);
        e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.RiferimentoDocumento).HasMaxLength(300);
        e.Property(x => x.FattoreOrario).HasPrecision(6, 4);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Voci).WithOne().HasForeignKey(x => x.IdAccordo).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Soglie).WithOne().HasForeignKey(x => x.IdAccordo).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdCommerciale, x.ValidoDal });
    }
}

public class ConfigurazioneVoceAccordo : IEntityTypeConfiguration<VoceAccordo>
{
    public void Configure(EntityTypeBuilder<VoceAccordo> e)
    {
        e.ToTable("VociAccordo", "provv");
        e.HasKey(x => x.IdVoceAccordo);
        e.Property(x => x.Voce).HasConversion<int>();
        e.Property(x => x.Percentuale).HasPrecision(7, 4);
        e.Property(x => x.Descrizione).HasMaxLength(1000);
        e.HasOne<BaseCalcolo>().WithMany().HasForeignKey(x => x.IdBaseCalcolo).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdAccordo, x.Voce }).IsUnique();
    }
}

public class ConfigurazioneSogliaAccordo : IEntityTypeConfiguration<SogliaAccordo>
{
    public void Configure(EntityTypeBuilder<SogliaAccordo> e)
    {
        e.ToTable("SoglieAccordo", "provv");
        e.HasKey(x => x.IdSogliaAccordo);
        e.Property(x => x.Soglia).HasPrecision(14, 2);
        e.Property(x => x.Premio).HasPrecision(14, 2);
    }
}

public class ConfigurazioneCommessaArcLab : IEntityTypeConfiguration<CommessaArcLab>
{
    public void Configure(EntityTypeBuilder<CommessaArcLab> e)
    {
        e.ToTable("CommesseArcLab", "provv");
        e.HasKey(x => x.IdCommessaArcLab);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.HasIndex(x => x.IdMansioneRiferimento);
        e.HasIndex(x => x.CodiceCliente);
    }
}

public class ConfigurazioneSegnalatore : IEntityTypeConfiguration<Segnalatore>
{
    public void Configure(EntityTypeBuilder<Segnalatore> e)
    {
        e.ToTable("Segnalatori", "provv");
        e.HasKey(x => x.IdSegnalatore);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Nome);
    }
}

public class ConfigurazioneCondizioneSegnalatore : IEntityTypeConfiguration<CondizioneSegnalatore>
{
    public void Configure(EntityTypeBuilder<CondizioneSegnalatore> e)
    {
        e.ToTable("CondizioniSegnalatori", "provv");
        e.HasKey(x => x.IdCondizioneSegnalatore);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Percentuale).HasPrecision(5, 2);
        e.Property(x => x.Ambito).HasConversion<int>();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Segnalatore>().WithMany(s => s.Condizioni).HasForeignKey(x => x.IdSegnalatore).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<BaseCalcolo>().WithMany().HasForeignKey(x => x.IdBaseCalcolo).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneSegnalazione : IEntityTypeConfiguration<Segnalazione>
{
    public void Configure(EntityTypeBuilder<Segnalazione> e)
    {
        e.ToTable("Segnalazioni", "provv");
        e.HasKey(x => x.IdSegnalazione);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<CondizioneSegnalatore>().WithMany().HasForeignKey(x => x.IdCondizioneSegnalatore).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.CodiceCliente);
    }
}

public class ConfigurazioneEccezioneAreaCommerciale : IEntityTypeConfiguration<EccezioneAreaCommerciale>
{
    public void Configure(EntityTypeBuilder<EccezioneAreaCommerciale> e)
    {
        e.ToTable("EccezioniArea", "provv");
        e.HasKey(x => x.IdEccezioneArea);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
        e.Property(x => x.CreataDa).HasMaxLength(100);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<BaseCalcolo>().WithMany().HasForeignKey(x => x.IdBaseCalcolo).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdCommerciale, x.CodiceCliente });
    }
}

public class ConfigurazioneMovimentoSegnalatore : IEntityTypeConfiguration<MovimentoSegnalatoreArchiviato>
{
    public void Configure(EntityTypeBuilder<MovimentoSegnalatoreArchiviato> e)
    {
        e.ToTable("MovimentiSegnalatori", "provv");
        e.HasKey(x => x.IdMovimentoSegnalatore);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Riferimento).HasMaxLength(60).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Base).HasPrecision(14, 2);
        e.Property(x => x.Aliquota).HasPrecision(7, 4);
        e.Property(x => x.Importo).HasPrecision(14, 2);
        e.HasOne<TrimestreProvvigioni>().WithMany().HasForeignKey(x => x.IdTrimestre).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdTrimestre, x.IdSegnalatore });
    }
}
