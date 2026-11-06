using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Rapportini di consulenza (schema "cons"). Nessuna scrittura su ArcLab: gli id ArcLab sono solo riferimenti.

public class ConfigurazioneRapportino : IEntityTypeConfiguration<Rapportino>
{
    public void Configure(EntityTypeBuilder<Rapportino> e)
    {
        e.ToTable("Rapportini", "cons");
        e.HasKey(x => x.IdRapportino);

        e.Property(x => x.RagioneSociale).HasMaxLength(150).IsRequired();
        e.Property(x => x.NomeInsediamento).HasMaxLength(255);
        e.Property(x => x.IndirizzoInsediamento).HasMaxLength(400);
        e.Property(x => x.EmailInsediamento).HasMaxLength(255);
        e.Property(x => x.DataIntervento).HasColumnType("date");

        e.Property(x => x.Consulente).HasMaxLength(120).IsRequired();
        e.Property(x => x.CognomeConsulente).HasMaxLength(50);
        e.Property(x => x.CommessaConsulente).HasMaxLength(100);

        e.Property(x => x.DescrizioneCommessa).HasMaxLength(255);
        e.Property(x => x.PrezzoCommessa).HasPrecision(10, 2);
        e.Property(x => x.Voce).HasMaxLength(20);
        e.Property(x => x.DescrizioneFattura).HasMaxLength(250).IsRequired();

        e.Property(x => x.Ore).HasPrecision(6, 2);
        e.Property(x => x.Luogo).HasConversion<int>();
        e.Property(x => x.ModalitaImporto).HasConversion<int>();
        e.Property(x => x.TariffaOraria).HasPrecision(10, 2);
        e.Property(x => x.Importo).HasPrecision(10, 2);
        e.Property(x => x.ImportoCorretto).HasPrecision(10, 2);
        e.Property(x => x.MotivoCorrezione).HasMaxLength(500);
        e.Property(x => x.ModalitaFatturazione).HasConversion<int>();

        e.Property(x => x.DataUltimoScalo).HasColumnType("date");
        e.Property(x => x.OreResiduePrima).HasPrecision(6, 2);
        e.Property(x => x.NoteInterne).HasMaxLength(1000);

        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.NotaContestazione).HasMaxLength(1000);
        e.Property(x => x.MotivoAnnullamento).HasMaxLength(500);

        e.Property(x => x.NomeFilePdf).HasMaxLength(300);
        e.Property(x => x.PercorsoPdf).HasMaxLength(500);
        e.Property(x => x.FilePalomar).HasMaxLength(500);

        e.Property(x => x.AttivitaSvolta).HasMaxLength(4000);
        e.Property(x => x.RiferimentoOfferta).HasMaxLength(300);
        e.Property(x => x.RiferimentoDelega).HasMaxLength(300);
        e.Property(x => x.FonteInformazioni).HasMaxLength(1000);
        e.Property(x => x.NotaPianoAnalitico).HasMaxLength(1000);
        e.Property(x => x.FollowUpAzione).HasMaxLength(1000);
        e.Property(x => x.FollowUpResponsabile).HasMaxLength(20);
        e.Property(x => x.FollowUpTermine).HasMaxLength(100);
        e.HasMany(x => x.Documenti).WithOne().HasForeignKey(d => d.IdRapportino).OnDelete(DeleteBehavior.Restrict);

        e.Ignore(x => x.ImportoDaFatturare);
        e.Ignore(x => x.OreResidueDopo);
        e.Ignore(x => x.SoloRendicontato);
        e.Ignore(x => x.Modificabile);

        e.HasIndex(x => x.Stato);
        e.HasIndex(x => new { x.IdClienteArcLab, x.DataIntervento });
        e.HasIndex(x => new { x.IdOrganicoArcLab, x.DataIntervento });
    }
}

public class ConfigurazioneConsulenteUtente : IEntityTypeConfiguration<ConsulenteUtente>
{
    public void Configure(EntityTypeBuilder<ConsulenteUtente> e)
    {
        e.ToTable("ConsulentiUtenti", "cons");
        e.HasKey(x => x.IdConsulenteUtente);
        e.HasIndex(x => x.IdUtente).IsUnique();
    }
}

public class ConfigurazioneVoceIncarico : IEntityTypeConfiguration<VoceIncarico>
{
    public void Configure(EntityTypeBuilder<VoceIncarico> e)
    {
        e.ToTable("VociIncarico", "cons");
        e.HasKey(x => x.IdVoceIncarico);
        e.Property(x => x.Voce).HasMaxLength(20).IsRequired();

        // Fasce riprese dalla query Access di prefatturazione consulenze (decisione 4/10/2026:
        // 1159 corretto in 159; 118–119 senza voce, voluto).
        e.HasData(
            new VoceIncarico { IdVoceIncarico = 1,  IdTipoIncaricoDa = 93,  IdTipoIncaricoA = 94,  Voce = "626" },
            new VoceIncarico { IdVoceIncarico = 2,  IdTipoIncaricoDa = 95,  IdTipoIncaricoA = 95,  Voce = "RSP" },
            new VoceIncarico { IdVoceIncarico = 3,  IdTipoIncaricoDa = 96,  IdTipoIncaricoA = 111, Voce = "626" },
            new VoceIncarico { IdVoceIncarico = 4,  IdTipoIncaricoDa = 112, IdTipoIncaricoA = 113, Voce = "HAC" },
            new VoceIncarico { IdVoceIncarico = 5,  IdTipoIncaricoDa = 114, IdTipoIncaricoA = 117, Voce = "AT" },
            new VoceIncarico { IdVoceIncarico = 6,  IdTipoIncaricoDa = 120, IdTipoIncaricoA = 123, Voce = "RIF" },
            new VoceIncarico { IdVoceIncarico = 7,  IdTipoIncaricoDa = 124, IdTipoIncaricoA = 125, Voce = "PRY" },
            new VoceIncarico { IdVoceIncarico = 8,  IdTipoIncaricoDa = 126, IdTipoIncaricoA = 153, Voce = "F62" },
            new VoceIncarico { IdVoceIncarico = 9,  IdTipoIncaricoDa = 154, IdTipoIncaricoA = 158, Voce = "FHA" },
            new VoceIncarico { IdVoceIncarico = 10, IdTipoIncaricoDa = 159, IdTipoIncaricoA = 183, Voce = "FON" });
    }
}

public class ConfigurazioneRapportinoDocumento : IEntityTypeConfiguration<RapportinoDocumento>
{
    public void Configure(EntityTypeBuilder<RapportinoDocumento> e)
    {
        e.ToTable("RapportiniDocumenti", "cons");
        e.HasKey(x => x.IdRapportinoDocumento);
        e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        e.Property(x => x.Revisione).HasMaxLength(30);
        e.Property(x => x.DataDocumento).HasColumnType("date");
        e.Property(x => x.Azione).HasConversion<int>();
        e.Property(x => x.Consegna).HasConversion<int?>();
        e.HasIndex(x => x.IdRapportino);
    }
}

public class ConfigurazioneDocumentoTipo : IEntityTypeConfiguration<DocumentoTipo>
{
    public void Configure(EntityTypeBuilder<DocumentoTipo> e)
    {
        e.ToTable("DocumentiTipo", "cons");
        e.HasKey(x => x.IdDocumentoTipo);
        e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        e.Property(x => x.Area).HasMaxLength(50);
        e.HasData(
            new DocumentoTipo { IdDocumentoTipo = 1, Nome = "Manuale di autocontrollo (HACCP)", Area = "HACCP", RichiamaLaboratorio = true, Ordine = 10 },
            new DocumentoTipo { IdDocumentoTipo = 2, Nome = "Piano di autocontrollo analitico", Area = "HACCP", RichiamaLaboratorio = true, PianoAnalitico = true, Ordine = 20 },
            new DocumentoTipo { IdDocumentoTipo = 3, Nome = "Procedura di richiamo/ritiro prodotti", Area = "HACCP", RichiamaLaboratorio = true, Ordine = 30 },
            new DocumentoTipo { IdDocumentoTipo = 4, Nome = "Documento di valutazione del rischio Legionella", Area = "Igiene", RichiamaLaboratorio = true, Ordine = 40 },
            new DocumentoTipo { IdDocumentoTipo = 5, Nome = "Documento di valutazione dei rischi (D.Lgs. 81/08)", Area = "Sicurezza", Ordine = 50 },
            new DocumentoTipo { IdDocumentoTipo = 6, Nome = "Schede e registri di autocontrollo", Area = "HACCP", Ordine = 60 });
    }
}

public class ConfigurazioneDelegaCliente : IEntityTypeConfiguration<DelegaCliente>
{
    public void Configure(EntityTypeBuilder<DelegaCliente> e)
    {
        e.ToTable("DelegheClienti", "cons");
        e.HasKey(x => x.IdDelegaCliente);
        e.Property(x => x.RagioneSociale).HasMaxLength(150).IsRequired();
        e.Property(x => x.Estremi).HasMaxLength(300).IsRequired();
        e.Property(x => x.DataSottoscrizione).HasColumnType("date");
        e.Property(x => x.DataFine).HasColumnType("date");
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.Consulente).HasMaxLength(120);
        e.Property(x => x.ConsulenteQualifica).HasMaxLength(150);
        e.Property(x => x.ConsulenteAlbo).HasMaxLength(200);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.DataEmissione).HasColumnType("date");
        e.Property(x => x.SedeLegale).HasMaxLength(300);
        e.Property(x => x.SedeLegaleComune).HasMaxLength(100);
        e.Property(x => x.PartitaIva).HasMaxLength(30);
        e.Property(x => x.CodiceFiscale).HasMaxLength(30);
        e.Property(x => x.SedeOperativa).HasMaxLength(400);
        e.Property(x => x.SedeOperativaComune).HasMaxLength(100);
        e.Property(x => x.LegaleRappNome).HasMaxLength(80);
        e.Property(x => x.LegaleRappCognome).HasMaxLength(80);
        e.Property(x => x.PercorsoWord).HasMaxLength(500);
        e.Property(x => x.PercorsoPdf).HasMaxLength(500);
        e.Property(x => x.PercorsoFirmata).HasMaxLength(500);
        e.HasIndex(x => x.IdClienteArcLab);
        e.HasIndex(x => new { x.IdClienteArcLab, x.IdOrganicoArcLab });
        e.HasMany(x => x.Insediamenti).WithOne().HasForeignKey(i => i.IdDelegaCliente).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneConsulenteDati : IEntityTypeConfiguration<ConsulenteDati>
{
    public void Configure(EntityTypeBuilder<ConsulenteDati> e)
    {
        e.ToTable("ConsulentiDati", "cons");
        e.HasKey(x => x.IdConsulenteDati);
        e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        e.Property(x => x.Qualifica).HasMaxLength(150);
        e.Property(x => x.IscrizioneAlbo).HasMaxLength(200);
        e.Property(x => x.DataModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.IdOrganicoArcLab).IsUnique();
    }
}

public class ConfigurazioneDelegaInsediamento : IEntityTypeConfiguration<DelegaInsediamento>
{
    public void Configure(EntityTypeBuilder<DelegaInsediamento> e)
    {
        e.ToTable("DelegheInsediamenti", "cons");
        e.HasKey(x => x.IdDelegaInsediamento);
        e.Property(x => x.Nome).HasMaxLength(255).IsRequired();
        e.Property(x => x.Sede).HasMaxLength(400);
        e.Property(x => x.Comune).HasMaxLength(100);
        e.HasIndex(x => x.IdDelegaCliente);
    }
}
