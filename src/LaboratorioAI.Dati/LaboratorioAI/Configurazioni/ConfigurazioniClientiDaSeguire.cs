using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// «Clienti da seguire» (schema "clienti"). Nessuna scrittura su ArcLab né su AZW: i codici cliente sono solo riferimenti.

public class ConfigurazioneCampioneMensileArcLab : IEntityTypeConfiguration<CampioneMensileArcLab>
{
    public void Configure(EntityTypeBuilder<CampioneMensileArcLab> e)
    {
        e.ToTable("CampioniMensiliArcLab", "clienti");
        e.HasKey(x => x.IdCampioneMensileArcLab);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Controllo).HasMaxLength(200);
        e.Property(x => x.ValoreListino).HasPrecision(14, 2);
        e.HasIndex(x => x.CodiceCliente);
        e.HasIndex(x => new { x.Anno, x.Mese });
    }
}

public class ConfigurazioneImpostazioniAndamento : IEntityTypeConfiguration<ImpostazioniAndamento>
{
    public void Configure(EntityTypeBuilder<ImpostazioniAndamento> e)
    {
        e.ToTable("ImpostazioniAndamento", "clienti");
        e.HasKey(x => x.IdImpostazioniAndamento);
        e.Property(x => x.MinimoFatturato).HasPrecision(12, 2);
        e.Property(x => x.SogliaRosso).HasPrecision(6, 2);
        e.Property(x => x.SogliaGiallo).HasPrecision(6, 2);
        // Default −20 anche sulla colonna: la riga già esistente riceve lo stesso valore usato finora (soglia del rosso).
        e.Property(x => x.LegionellaSogliaCalo).HasPrecision(6, 2).HasDefaultValue(-20m);
        e.Property(x => x.ValoreAnalisiPerse).HasPrecision(12, 2);
        e.Property(x => x.SoloConsulenzaMinimo).HasPrecision(12, 2);
        e.Property(x => x.SoloConsulenzaLabMassimo).HasPrecision(12, 2);
        e.Property(x => x.TestoControlloLegionella).HasMaxLength(50).IsRequired();
        e.Property(x => x.VociLegionella).HasMaxLength(100).IsRequired();
        e.Property(x => x.UltimaModificaDa).HasMaxLength(100);
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class ConfigurazioneAzioneCliente : IEntityTypeConfiguration<AzioneCliente>
{
    public void Configure(EntityTypeBuilder<AzioneCliente> e)
    {
        e.ToTable("AzioniClienti", "clienti");
        e.HasKey(x => x.IdAzioneCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.DataAzione).HasColumnType("date");
        e.Property(x => x.ProssimoContatto).HasColumnType("date");
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Esito).HasConversion<int>();
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.NomeUtente).HasMaxLength(100);
        e.Property(x => x.AnnullataDa).HasMaxLength(100);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceCliente);
        e.HasIndex(x => x.ProssimoContatto);
    }
}

public class ConfigurazioneAssegnazioneCliente : IEntityTypeConfiguration<AssegnazioneCliente>
{
    public void Configure(EntityTypeBuilder<AssegnazioneCliente> e)
    {
        e.ToTable("AssegnazioniClienti", "clienti");
        e.HasKey(x => x.IdAssegnazioneCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.Motivo).HasMaxLength(1000);
        e.Property(x => x.FatturatoLabAllaProposta).HasPrecision(12, 2);
        e.Property(x => x.PropostaDa).HasMaxLength(100);
        e.Property(x => x.DecisaDa).HasMaxLength(100);
        e.Property(x => x.NotaDecisione).HasMaxLength(1000);
        e.Property(x => x.DataInizio).HasColumnType("date");
        e.Property(x => x.DataFine).HasColumnType("date");
        e.Property(x => x.DataProposta).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceCliente);
        e.HasOne<Commerciale>().WithMany().HasForeignKey(x => x.IdCommerciale).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneAttributiCliente : IEntityTypeConfiguration<AttributiCliente>
{
    public void Configure(EntityTypeBuilder<AttributiCliente> e)
    {
        e.ToTable("AttributiClienti", "clienti");
        e.HasKey(x => x.IdAttributiCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.UltimaModificaDa).HasMaxLength(100);
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceCliente).IsUnique();
    }
}

// ── Cambi di gestione (subentri, 12/10/2026) ──────────────────────────

public class ConfigurazioneInsediamentoArcLab : IEntityTypeConfiguration<InsediamentoArcLab>
{
    public void Configure(EntityTypeBuilder<InsediamentoArcLab> e)
    {
        e.ToTable("InsediamentiArcLab", "clienti");
        e.HasKey(x => x.IdInsediamentoArcLab);
        e.Property(x => x.CodiceCliente).HasMaxLength(20);
        e.Property(x => x.RagioneSociale).HasMaxLength(200).IsRequired();
        e.Property(x => x.PartitaIva).HasMaxLength(30);
        e.Property(x => x.NomeInsediamento).HasMaxLength(255);
        e.Property(x => x.Indirizzo).HasMaxLength(255);
        e.Property(x => x.Citta).HasMaxLength(100);
        e.Property(x => x.Telefono).HasMaxLength(60);
        e.HasIndex(x => x.IdInsediamento).IsUnique();
        e.HasIndex(x => x.CodiceCliente);
    }
}

public class ConfigurazioneCampioneInsediamentoArcLab : IEntityTypeConfiguration<CampioneInsediamentoArcLab>
{
    public void Configure(EntityTypeBuilder<CampioneInsediamentoArcLab> e)
    {
        e.ToTable("CampioniInsediamentiArcLab", "clienti");
        e.HasKey(x => x.IdCampioneInsediamentoArcLab);
        e.Property(x => x.Valore).HasPrecision(14, 2);
        e.HasIndex(x => x.IdInsediamento);
    }
}

public class ConfigurazioneSubentroInsediamento : IEntityTypeConfiguration<SubentroInsediamento>
{
    public void Configure(EntityTypeBuilder<SubentroInsediamento> e)
    {
        e.ToTable("SubentriInsediamenti", "clienti");
        e.HasKey(x => x.IdSubentroInsediamento);
        e.Property(x => x.CodiceClientePrecedente).HasMaxLength(20);
        e.Property(x => x.CodiceClienteSuccessivo).HasMaxLength(20);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.NomeUtente).HasMaxLength(100);
        e.Property(x => x.AnnullataDa).HasMaxLength(100);
        e.Property(x => x.DataDecisione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.IdInsediamentoPrecedente);
    }
}

public class ConfigurazioneVerificaPartitaIva : IEntityTypeConfiguration<VerificaPartitaIva>
{
    public void Configure(EntityTypeBuilder<VerificaPartitaIva> e)
    {
        e.ToTable("VerifichePartitaIva", "clienti");
        e.HasKey(x => x.IdVerificaPartitaIva);
        e.Property(x => x.Paese).HasMaxLength(2).IsRequired();
        e.Property(x => x.PartitaIva).HasMaxLength(20).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(300);
        e.Property(x => x.Indirizzo).HasMaxLength(500);
        e.Property(x => x.Errore).HasMaxLength(300);
        e.Property(x => x.DataVerifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.Paese, x.PartitaIva });
    }
}

public class ConfigurazionePrestazioneInsediamentoArcLab : IEntityTypeConfiguration<PrestazioneInsediamentoArcLab>
{
    public void Configure(EntityTypeBuilder<PrestazioneInsediamentoArcLab> e)
    {
        e.ToTable("PrestazioniInsediamentiArcLab", "clienti");
        e.HasKey(x => x.IdPrestazioneInsediamentoArcLab);
        e.Property(x => x.Valore).HasPrecision(14, 2);
        e.HasIndex(x => x.IdInsediamento);
    }
}

public class ConfigurazioneClienteTolto : IEntityTypeConfiguration<ClienteTolto>
{
    public void Configure(EntityTypeBuilder<ClienteTolto> e)
    {
        e.ToTable("ClientiTolti", "clienti");
        e.HasKey(x => x.IdClienteTolto);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.CodiceClienteSuccessore).HasMaxLength(20);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.NomeUtente).HasMaxLength(100);
        e.Property(x => x.AnnullataDa).HasMaxLength(100);
        e.Property(x => x.DataUscita).HasColumnType("date");
        e.HasIndex(x => x.CodiceCliente);
    }
}
