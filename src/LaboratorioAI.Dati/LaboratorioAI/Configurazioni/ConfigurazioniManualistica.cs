using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Modulo Manualistica (schema «man», 01/11/2026).
// I codici ArcLab e AZW sono riferimenti logici: nessun vincolo referenziale, nessuna scrittura sui gestionali.
// Le impostazioni del modulo non stanno qui: usano app.Configurazioni, come gli altri moduli.

public class ConfigurazioneTipoManuale : IEntityTypeConfiguration<TipoManuale>
{
    public void Configure(EntityTypeBuilder<TipoManuale> e)
    {
        e.ToTable("TipiManuale", "man");
        e.HasKey(x => x.IdTipoManuale);

        e.Property(x => x.Codice).HasMaxLength(30).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Codice).IsUnique();
    }
}

public class ConfigurazioneModelloDocumento : IEntityTypeConfiguration<ModelloDocumento>
{
    public void Configure(EntityTypeBuilder<ModelloDocumento> e)
    {
        e.ToTable("ModelliDocumento", "man");
        e.HasKey(x => x.IdModelloDocumento);

        e.Property(x => x.Codice).HasMaxLength(60).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        e.Property(x => x.CondizioneInclusione).HasMaxLength(200);
        e.Property(x => x.CodiceAttivita).HasMaxLength(30);
        e.Property(x => x.SiglaRegione).HasMaxLength(2).IsFixedLength();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.RichiedeMarcatori).HasDefaultValue(true);

        e.HasOne(x => x.TipoManuale).WithMany().HasForeignKey(x => x.IdTipoManuale).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Revisioni).WithOne(r => r.ModelloDocumento!).HasForeignKey(r => r.IdModelloDocumento).OnDelete(DeleteBehavior.Restrict);

        // Lo stesso codice può esistere più volte, differenziato per attività e regione.
        e.HasIndex(x => new { x.IdTipoManuale, x.Codice, x.CodiceAttivita, x.SiglaRegione }).IsUnique();
    }
}

public class ConfigurazioneRevisioneModello : IEntityTypeConfiguration<RevisioneModello>
{
    public void Configure(EntityTypeBuilder<RevisioneModello> e)
    {
        e.ToTable("RevisioniModello", "man");
        e.HasKey(x => x.IdRevisioneModello);

        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.ImprontaSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        e.Property(x => x.NomeFileOriginale).HasMaxLength(260);
        e.Property(x => x.DataEfficacia).HasColumnType("date");
        e.Property(x => x.NoteRevisione).HasMaxLength(4000);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasMany(x => x.Approvazioni).WithOne(a => a.RevisioneModello!).HasForeignKey(a => a.IdRevisioneModello).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdModelloDocumento, x.NumeroRevisione }).IsUnique();
        e.HasIndex(x => new { x.IdModelloDocumento, x.Stato });
    }
}

public class ConfigurazioneApprovazioneRevisione : IEntityTypeConfiguration<ApprovazioneRevisione>
{
    public void Configure(EntityTypeBuilder<ApprovazioneRevisione> e)
    {
        e.ToTable("ApprovazioniRevisione", "man");
        e.HasKey(x => x.IdApprovazioneRevisione);

        e.Property(x => x.Approvatore).HasMaxLength(200).IsRequired();
        e.Property(x => x.Esito).HasConversion<int>();
        e.Property(x => x.DataUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.Note).HasMaxLength(1000);

        // Una persona approva una revisione una volta sola: servono due persone diverse.
        e.HasIndex(x => new { x.IdRevisioneModello, x.Approvatore }).IsUnique();
    }
}

public class ConfigurazioneApprovatoreTipoManuale : IEntityTypeConfiguration<ApprovatoreTipoManuale>
{
    public void Configure(EntityTypeBuilder<ApprovatoreTipoManuale> e)
    {
        e.ToTable("ApprovatoriTipoManuale", "man");
        e.HasKey(x => x.IdApprovatoreTipoManuale);

        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.TipoManuale).WithMany().HasForeignKey(x => x.IdTipoManuale).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.Restrict);

        // Una persona compare una volta sola per tipo: togliere e rimettere riattiva la riga.
        e.HasIndex(x => new { x.IdTipoManuale, x.IdUtente }).IsUnique();
    }
}

public class ConfigurazioneVoceChecklist : IEntityTypeConfiguration<VoceChecklist>
{
    public void Configure(EntityTypeBuilder<VoceChecklist> e)
    {
        e.ToTable("VociChecklist", "man");
        e.HasKey(x => x.IdVoceChecklist);

        e.Property(x => x.Codice).HasMaxLength(80).IsRequired();   // il codice porta anche l'occorrenza nel blocco: «TAG#2»
        e.Property(x => x.Etichetta).HasMaxLength(200).IsRequired();
        e.Property(x => x.Gruppo).HasMaxLength(60);
        e.Property(x => x.TipoCampo).HasConversion<int>();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.TipoManuale).WithMany().HasForeignKey(x => x.IdTipoManuale).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Opzioni).WithOne(o => o.VoceChecklist!).HasForeignKey(o => o.IdVoceChecklist).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdTipoManuale, x.Codice }).IsUnique();
    }
}

public class ConfigurazioneOpzioneVoce : IEntityTypeConfiguration<OpzioneVoce>
{
    public void Configure(EntityTypeBuilder<OpzioneVoce> e)
    {
        e.ToTable("OpzioniVoce", "man");
        e.HasKey(x => x.IdOpzioneVoce);

        e.Property(x => x.Valore).HasMaxLength(400).IsRequired();
        e.Property(x => x.Etichetta).HasMaxLength(400);
        e.Property(x => x.ApprovataDa).HasMaxLength(200);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        // Una voce può valere per tutti i modelli (null) o per uno solo.
        e.HasOne(x => x.ModelloDocumento).WithMany()
            .HasForeignKey(x => x.IdModelloDocumento).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdVoceChecklist, x.Attivo, x.Ordine });
        e.HasIndex(x => new { x.IdVoceChecklist, x.IdModelloDocumento });
    }
}

public class ConfigurazioneFunzioneAziendale : IEntityTypeConfiguration<FunzioneAziendale>
{
    public void Configure(EntityTypeBuilder<FunzioneAziendale> e)
    {
        e.ToTable("FunzioniAziendali", "man");
        e.HasKey(x => x.IdFunzioneAziendale);

        e.Property(x => x.Codice).HasMaxLength(60).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.TipoManuale).WithMany().HasForeignKey(x => x.IdTipoManuale).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdTipoManuale, x.Codice }).IsUnique();
    }
}

public class ConfigurazioneQualificaAutorizzato : IEntityTypeConfiguration<QualificaAutorizzato>
{
    public void Configure(EntityTypeBuilder<QualificaAutorizzato> e)
    {
        e.ToTable("Qualifiche", "man");
        e.HasKey(x => x.IdQualifica);

        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Nome).IsUnique();
    }
}

public class ConfigurazioneCompilazione : IEntityTypeConfiguration<Compilazione>
{
    public void Configure(EntityTypeBuilder<Compilazione> e)
    {
        e.ToTable("Compilazioni", "man");
        e.HasKey(x => x.IdCompilazione);

        e.Property(x => x.CodiceClienteArcLab).HasMaxLength(30);
        e.Property(x => x.RagioneSociale).HasMaxLength(200).IsRequired();
        e.Property(x => x.PartitaIva).HasMaxLength(20);
        e.Property(x => x.CodiceFiscale).HasMaxLength(20);

        e.Property(x => x.SedeLegaleIndirizzo).HasMaxLength(300);
        e.Property(x => x.SedeLegaleCap).HasMaxLength(10);
        e.Property(x => x.SedeLegaleComune).HasMaxLength(100);
        e.Property(x => x.SedeLegaleProvincia).HasMaxLength(2).IsFixedLength();

        e.Property(x => x.Email).HasMaxLength(255);
        e.Property(x => x.Pec).HasMaxLength(255);
        e.Property(x => x.Telefono).HasMaxLength(50);

        e.Property(x => x.CodiceAteco).HasMaxLength(20);
        e.Property(x => x.AttivitaEsercitata).HasMaxLength(300);
        e.Property(x => x.SitiWeb).HasMaxLength(500);

        e.Property(x => x.CodiceUnivocoFatturazione).HasMaxLength(7);
        e.Property(x => x.ReferenteNome).HasMaxLength(100);
        e.Property(x => x.ReferenteCognome).HasMaxLength(100);

        e.Property(x => x.DataDocumenti).HasColumnType("date");
        e.Property(x => x.EmailCliente).HasMaxLength(255);
        e.Property(x => x.EmailConsulente).HasMaxLength(255);

        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.MotivoAnnullamento).HasMaxLength(500);

        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.ModificatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.TipoManuale).WithMany().HasForeignKey(x => x.IdTipoManuale).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Sedi).WithOne(s => s.Compilazione!).HasForeignKey(s => s.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Valori).WithOne(v => v.Compilazione!).HasForeignKey(v => v.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Autorizzati).WithOne(a => a.Compilazione!).HasForeignKey(a => a.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Responsabili).WithOne(r => r.Compilazione!).HasForeignKey(r => r.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Note).WithOne(n => n.Compilazione!).HasForeignKey(n => n.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Firme).WithOne(f => f.Compilazione!).HasForeignKey(f => f.IdCompilazione).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Documenti).WithOne(d => d.Compilazione!).HasForeignKey(d => d.IdCompilazione).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdTipoManuale, x.Stato });
        e.HasIndex(x => x.CodiceClienteArcLab);
        e.HasIndex(x => x.DataDocumenti);
    }
}

public class ConfigurazioneSedeCompilazione : IEntityTypeConfiguration<SedeCompilazione>
{
    public void Configure(EntityTypeBuilder<SedeCompilazione> e)
    {
        e.ToTable("SediCompilazione", "man");
        e.HasKey(x => x.IdSedeCompilazione);

        e.Property(x => x.CodiceInsediamentoArcLab).HasMaxLength(30);
        e.Property(x => x.Nome).HasMaxLength(255);
        e.Property(x => x.Indirizzo).HasMaxLength(300);
        e.Property(x => x.Cap).HasMaxLength(10);
        e.Property(x => x.Comune).HasMaxLength(100);
        e.Property(x => x.Provincia).HasMaxLength(2).IsFixedLength();
        e.Property(x => x.Attivo).HasDefaultValue(true);

        e.HasIndex(x => new { x.IdCompilazione, x.Ordine });
    }
}

public class ConfigurazioneValoreCompilazione : IEntityTypeConfiguration<ValoreCompilazione>
{
    public void Configure(EntityTypeBuilder<ValoreCompilazione> e)
    {
        e.ToTable("ValoriCompilazione", "man");
        e.HasKey(x => x.IdValoreCompilazione);

        e.Property(x => x.CodiceVoce).HasMaxLength(60).IsRequired();
        e.Property(x => x.Valore).HasMaxLength(4000);

        e.HasIndex(x => new { x.IdCompilazione, x.CodiceVoce }).IsUnique();
        // Le voci scritte a mano si elencano nel riepilogo e si propongono per le tendine.
        e.HasIndex(x => x.ScrittoAMano);
    }
}

public class ConfigurazioneAutorizzatoCompilazione : IEntityTypeConfiguration<AutorizzatoCompilazione>
{
    public void Configure(EntityTypeBuilder<AutorizzatoCompilazione> e)
    {
        e.ToTable("AutorizzatiCompilazione", "man");
        e.HasKey(x => x.IdAutorizzatoCompilazione);

        e.Property(x => x.Nome).HasMaxLength(100);
        e.Property(x => x.Cognome).HasMaxLength(100);
        e.Property(x => x.AccessoImmagini).HasMaxLength(300);
        e.Property(x => x.Attivo).HasDefaultValue(true);

        e.HasOne(x => x.Qualifica).WithMany().HasForeignKey(x => x.IdQualifica).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Funzioni).WithOne(f => f.Autorizzato!).HasForeignKey(f => f.IdAutorizzatoCompilazione).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdCompilazione, x.Ordine });
    }
}

public class ConfigurazioneAutorizzatoFunzione : IEntityTypeConfiguration<AutorizzatoFunzione>
{
    public void Configure(EntityTypeBuilder<AutorizzatoFunzione> e)
    {
        e.ToTable("AutorizzatiFunzioni", "man");
        e.HasKey(x => x.IdAutorizzatoFunzione);

        e.HasOne(x => x.Funzione).WithMany().HasForeignKey(x => x.IdFunzioneAziendale).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdAutorizzatoCompilazione, x.IdFunzioneAziendale }).IsUnique();
    }
}

public class ConfigurazioneResponsabileCompilazione : IEntityTypeConfiguration<ResponsabileCompilazione>
{
    public void Configure(EntityTypeBuilder<ResponsabileCompilazione> e)
    {
        e.ToTable("ResponsabiliCompilazione", "man");
        e.HasKey(x => x.IdResponsabileCompilazione);

        e.Property(x => x.CodiceModello).HasMaxLength(60).IsRequired();
        e.HasMany(x => x.Scelte).WithOne(s => s.Responsabile!).HasForeignKey(s => s.IdResponsabileCompilazione).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdCompilazione, x.CodiceModello, x.Ordine });
    }
}

public class ConfigurazioneSceltaResponsabile : IEntityTypeConfiguration<SceltaResponsabile>
{
    public void Configure(EntityTypeBuilder<SceltaResponsabile> e)
    {
        e.ToTable("ScelteResponsabile", "man");
        e.HasKey(x => x.IdSceltaResponsabile);

        e.Property(x => x.CodiceCampo).HasMaxLength(80).IsRequired();
        e.Property(x => x.Valore).HasMaxLength(1000);

        e.HasIndex(x => new { x.IdResponsabileCompilazione, x.CodiceCampo }).IsUnique();
    }
}

public class ConfigurazioneNotaCompilazione : IEntityTypeConfiguration<NotaCompilazione>
{
    public void Configure(EntityTypeBuilder<NotaCompilazione> e)
    {
        e.ToTable("NoteCompilazione", "man");
        e.HasKey(x => x.IdNotaCompilazione);

        e.Property(x => x.Sezione).HasMaxLength(200).IsRequired();
        e.Property(x => x.Testo).HasMaxLength(4000).IsRequired();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.IdCompilazione);
    }
}

public class ConfigurazioneContrattoFornitura : IEntityTypeConfiguration<ContrattoFornitura>
{
    public void Configure(EntityTypeBuilder<ContrattoFornitura> e)
    {
        e.ToTable("ContrattiFornitura", "man");
        e.HasKey(x => x.IdContrattoFornitura);

        e.Property(x => x.PrezzoDocumentazione).HasPrecision(10, 2);
        e.Property(x => x.PrezzoAssistenza).HasPrecision(10, 2);
        e.Property(x => x.Pagamento).HasConversion<int>();

        e.Property(x => x.RibaIstituto).HasMaxLength(200);
        e.Property(x => x.RibaAgenzia).HasMaxLength(200);
        e.Property(x => x.RibaIban).HasMaxLength(34);

        e.Property(x => x.Sdi).HasMaxLength(7);
        e.Property(x => x.Telefono).HasMaxLength(50);
        e.Property(x => x.EmailDocumentazione).HasMaxLength(255);

        e.Property(x => x.ConsensoProfilazione).HasConversion<int>();
        e.Property(x => x.ConsensoPresenza).HasConversion<int>();
        e.Property(x => x.ConsensoFotoVideo).HasConversion<int>();
        e.Property(x => x.ConsensoPromozionali).HasConversion<int>();

        e.Property(x => x.ConsulenteNome).HasMaxLength(200);
        e.Property(x => x.ConsulenteCellulare).HasMaxLength(50);
        e.Property(x => x.ConsulenteEmail).HasMaxLength(255);

        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        // Un contratto per compilazione.
        e.HasIndex(x => x.IdCompilazione).IsUnique();
    }
}

public class ConfigurazioneIntestatarioContratto : IEntityTypeConfiguration<IntestatarioContratto>
{
    public void Configure(EntityTypeBuilder<IntestatarioContratto> e)
    {
        e.ToTable("IntestatariContratto", "man");
        e.HasKey(x => x.IdIntestatarioContratto);

        e.Property(x => x.PartitaIva).HasMaxLength(20);
        e.Property(x => x.RagioneSociale).HasMaxLength(200).IsRequired();
        e.Property(x => x.Indirizzo).HasMaxLength(300);
        e.Property(x => x.Cap).HasMaxLength(10);
        e.Property(x => x.Comune).HasMaxLength(100);
        e.Property(x => x.Provincia).HasMaxLength(2).IsFixedLength();
        e.Property(x => x.CodiceFiscale).HasMaxLength(20);
        e.Property(x => x.CodiceClienteAzw).HasMaxLength(30);

        e.HasIndex(x => x.IdCompilazione).IsUnique();
    }
}

public class ConfigurazioneFirmaConsulente : IEntityTypeConfiguration<FirmaConsulente>
{
    public void Configure(EntityTypeBuilder<FirmaConsulente> e)
    {
        e.ToTable("FirmeConsulenti", "man");
        e.HasKey(x => x.IdFirmaConsulente);

        e.Property(x => x.UpnUtente).HasMaxLength(200).IsRequired();
        e.Property(x => x.Cellulare).HasMaxLength(50);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => new { x.UpnUtente, x.Attiva });
    }
}

public class ConfigurazioneFirmaCompilazione : IEntityTypeConfiguration<FirmaCompilazione>
{
    public void Configure(EntityTypeBuilder<FirmaCompilazione> e)
    {
        e.ToTable("FirmeCompilazione", "man");
        e.HasKey(x => x.IdFirmaCompilazione);

        e.Property(x => x.Ruolo).HasConversion<int>();
        e.Property(x => x.Modo).HasConversion<int>();
        e.Property(x => x.NomeFirmatario).HasMaxLength(200);
        e.Property(x => x.ImprontaSha256).HasMaxLength(64).IsFixedLength();
        e.Property(x => x.AcquisitaDa).HasMaxLength(200);
        e.Property(x => x.AcquisitaIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => new { x.IdCompilazione, x.Ruolo }).IsUnique();
    }
}

public class ConfigurazioneManualeGenerato : IEntityTypeConfiguration<ManualeGenerato>
{
    public void Configure(EntityTypeBuilder<ManualeGenerato> e)
    {
        e.ToTable("ManualiGenerati", "man");
        e.HasKey(x => x.IdManualeGenerato);

        e.Property(x => x.CodiceModello).HasMaxLength(60).IsRequired();
        e.Property(x => x.NomeFile).HasMaxLength(260).IsRequired();
        e.Property(x => x.Formato).HasConversion<int>();
        e.Property(x => x.PercorsoArchivio).HasMaxLength(500);
        e.Property(x => x.ImprontaSha256).HasMaxLength(64).IsFixedLength();
        e.Property(x => x.GeneratoDa).HasMaxLength(200);
        e.Property(x => x.GeneratoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasMany(x => x.Revisioni).WithOne(r => r.ManualeGenerato!).HasForeignKey(r => r.IdManualeGenerato).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdCompilazione, x.CodiceModello, x.NumeroLettera, x.Formato });
    }
}

public class ConfigurazioneManualeGeneratoRevisione : IEntityTypeConfiguration<ManualeGeneratoRevisione>
{
    public void Configure(EntityTypeBuilder<ManualeGeneratoRevisione> e)
    {
        e.ToTable("ManualiGeneratiRevisioni", "man");
        e.HasKey(x => x.IdManualeGeneratoRevisione);

        e.HasOne(x => x.RevisioneModello).WithMany().HasForeignKey(x => x.IdRevisioneModello).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdManualeGenerato, x.IdRevisioneModello }).IsUnique();
    }
}

public class ConfigurazioneInvioDocumenti : IEntityTypeConfiguration<InvioDocumenti>
{
    public void Configure(EntityTypeBuilder<InvioDocumenti> e)
    {
        e.ToTable("InviiDocumenti", "man");
        e.HasKey(x => x.IdInvioDocumenti);

        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Destinatari).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Formati).HasMaxLength(50);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.AnnullatoDa).HasMaxLength(200);
        e.Property(x => x.IdMessaggio).HasMaxLength(200);
        e.Property(x => x.Esito).HasMaxLength(2000);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        // Il servizio in background cerca gli invii programmati scaduti.
        e.HasIndex(x => new { x.Stato, x.InviareDopoUtc });
        e.HasIndex(x => x.IdCompilazione);
    }
}
