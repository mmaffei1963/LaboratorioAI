using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Approvazione delle Condizioni Generali di Fornitura (schema contratti, 09/10/2026). Specifica v0.4.

public class ConfigurazioneRevisioneCondizioni : IEntityTypeConfiguration<RevisioneCondizioni>
{
    public void Configure(EntityTypeBuilder<RevisioneCondizioni> e)
    {
        e.ToTable("RevisioniCondizioni", "contratti");
        e.HasKey(x => x.IdRevisioneCondizioni);
        e.Property(x => x.Codice).HasMaxLength(200).IsRequired();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.NomeFilePdf).HasMaxLength(260);
        e.Property(x => x.ImprontaPdf).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.NomeUtentePubblicazione).HasMaxLength(200);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.Attivo).HasDefaultValue(true);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.NumeroRevisione, x.Attivo });
        e.HasMany(x => x.Clausole).WithOne(c => c.Revisione!)
            .HasForeignKey(c => c.IdRevisioneCondizioni).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneClausolaCondizioni : IEntityTypeConfiguration<ClausolaCondizioni>
{
    public void Configure(EntityTypeBuilder<ClausolaCondizioni> e)
    {
        e.ToTable("ClausoleCondizioni", "contratti");
        e.HasKey(x => x.IdClausolaCondizioni);
        e.Property(x => x.Articolo).HasMaxLength(20).IsRequired();
        e.Property(x => x.ContenutoBreve).HasMaxLength(500).IsRequired();
        e.Property(x => x.Attivo).HasDefaultValue(true);
        e.HasIndex(x => new { x.IdRevisioneCondizioni, x.Ordine });
    }
}

public class ConfigurazioneFirmatarioCliente : IEntityTypeConfiguration<FirmatarioCliente>
{
    public void Configure(EntityTypeBuilder<FirmatarioCliente> e)
    {
        e.ToTable("FirmatariClienti", "contratti");
        e.HasKey(x => x.IdFirmatarioCliente);
        e.Property(x => x.ClienteRagioneSociale).HasMaxLength(300);
        e.Property(x => x.ClientePartitaIva).HasMaxLength(30);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Cognome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Qualifica).HasMaxLength(150);
        e.Property(x => x.Email).HasMaxLength(254).IsRequired();
        e.Property(x => x.CodiceFiscale).HasMaxLength(16);
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.NomeUtenteDesignazione).HasMaxLength(200);
        e.Property(x => x.Attivo).HasDefaultValue(true);
        e.HasIndex(x => new { x.IdClienteArcLab, x.Attivo });
    }
}

public class ConfigurazioneRichiestaApprovazione : IEntityTypeConfiguration<RichiestaApprovazione>
{
    public void Configure(EntityTypeBuilder<RichiestaApprovazione> e)
    {
        e.ToTable("RichiesteApprovazione", "contratti");
        e.HasKey(x => x.IdRichiestaApprovazione);
        e.Property(x => x.ClienteRagioneSociale).HasMaxLength(300);
        e.Property(x => x.ClientePartitaIva).HasMaxLength(30);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.Modalita).HasConversion<int?>();
        e.Property(x => x.ImprontaCodiceLink).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.OffertaImpronta).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.OffertaRiferimento).HasMaxLength(100);
        e.Property(x => x.NomeDichiarato).HasMaxLength(100);
        e.Property(x => x.CognomeDichiarato).HasMaxLength(100);
        e.Property(x => x.QualificaDichiarata).HasMaxLength(150);
        e.Property(x => x.ImprontaTesti).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.NumeroRicevuta).HasMaxLength(20);
        e.Property(x => x.RicevutaImpronta).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.ErroreInvioRicevuta).HasMaxLength(1000);
        e.Property(x => x.ErroreInvioLink).HasMaxLength(1000);
        e.HasIndex(x => x.InvioAccodato).HasFilter("[InvioAccodato] IS NOT NULL");
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.NomeUtenteCreazione).HasMaxLength(200);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.ImprontaCodiceLink).IsUnique().HasFilter("[ImprontaCodiceLink] IS NOT NULL");
        e.HasIndex(x => new { x.IdClienteArcLab, x.IdRevisioneCondizioni });
        e.HasIndex(x => x.Stato);
        e.HasOne(x => x.Revisione).WithMany().HasForeignKey(x => x.IdRevisioneCondizioni).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Firmatario).WithMany().HasForeignKey(x => x.IdFirmatarioCliente).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Firme grafiche (modalità D): immagine e tracciato, a sola aggiunta.</summary>
public class ConfigurazioneFirmaGraficaApprovazione : IEntityTypeConfiguration<FirmaGraficaApprovazione>
{
    public void Configure(EntityTypeBuilder<FirmaGraficaApprovazione> e)
    {
        e.ToTable("FirmeGraficheApprovazione", "contratti");
        e.HasKey(x => x.IdFirmaGraficaApprovazione);
        e.Property(x => x.Tracciato).IsRequired();
        e.Property(x => x.Puntatore).HasMaxLength(20);
        e.Property(x => x.Impronta).HasMaxLength(64).IsUnicode(false).IsRequired();
        e.Property(x => x.DataUtc).HasColumnType("datetime2(7)");
        e.HasIndex(x => new { x.IdRichiestaApprovazione, x.Riquadro });
        e.HasOne<RichiestaApprovazione>().WithMany().HasForeignKey(x => x.IdRichiestaApprovazione).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Registro delle evidenze a sola aggiunta. Il divieto di UPDATE/DELETE è imposto anche da un trigger creato nella
/// migrazione (TR_EventiApprovazione_SolaAggiunta).
/// </summary>
public class ConfigurazioneEventoApprovazione : IEntityTypeConfiguration<EventoApprovazione>
{
    public void Configure(EntityTypeBuilder<EventoApprovazione> e)
    {
        e.ToTable("EventiApprovazione", "contratti", t => t.HasTrigger("TR_EventiApprovazione_SolaAggiunta"));
        e.HasKey(x => x.IdEventoApprovazione);
        e.Property(x => x.Tipo).HasMaxLength(60).IsRequired();
        e.Property(x => x.DataUtc).HasColumnType("datetime2(7)");
        e.Property(x => x.NomeUtente).HasMaxLength(200);
        e.Property(x => x.IndirizzoIp).HasMaxLength(64);
        e.Property(x => x.UserAgent).HasMaxLength(500);
        e.Property(x => x.ImprontaPrecedente).HasMaxLength(64).IsUnicode(false).IsRequired();
        e.Property(x => x.Impronta).HasMaxLength(64).IsUnicode(false).IsRequired();
        e.HasIndex(x => x.IdRichiestaApprovazione);
        e.HasIndex(x => x.IdClienteArcLab);
    }
}

/// <summary>Inviti al cliente a indicare il Firmatario (11/10/2026).</summary>
public class ConfigurazioneInvitoFirmatario : IEntityTypeConfiguration<InvitoFirmatario>
{
    public void Configure(EntityTypeBuilder<InvitoFirmatario> e)
    {
        e.ToTable("InvitiFirmatario", "contratti");
        e.HasKey(x => x.IdInvitoFirmatario);
        e.Property(x => x.ClienteRagioneSociale).HasMaxLength(300);
        e.Property(x => x.ClientePartitaIva).HasMaxLength(30);
        e.Property(x => x.EmailDestinatari).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.ImprontaCodiceLink).HasMaxLength(64).IsUnicode(false);
        e.Property(x => x.ErroreInvio).HasMaxLength(1000);
        e.Property(x => x.NomeIndicato).HasMaxLength(100);
        e.Property(x => x.CognomeIndicato).HasMaxLength(100);
        e.Property(x => x.QualificaIndicata).HasMaxLength(150);
        e.Property(x => x.EmailIndicata).HasMaxLength(256);
        e.Property(x => x.ErroreElaborazione).HasMaxLength(1000);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.NomeUtenteCreazione).HasMaxLength(200);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.ImprontaCodiceLink).IsUnique().HasFilter("[ImprontaCodiceLink] IS NOT NULL");
        e.HasIndex(x => x.InvioAccodato).HasFilter("[InvioAccodato] IS NOT NULL");
        e.HasIndex(x => new { x.IdClienteArcLab, x.Stato });
        e.HasIndex(x => x.Stato);
    }
}
