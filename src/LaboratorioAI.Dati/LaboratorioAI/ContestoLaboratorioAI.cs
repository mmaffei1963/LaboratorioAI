using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI;

/// <summary>
/// Contesto del database proprio dell'applicazione.
///
/// Contiene esclusivamente i dati di cui il sistema è proprietario.
/// I dati gestionali (campioni, prove, commesse, clienti) risiedono in
/// ArcLab e vengono letti tramite <see cref="ArcLab"/>, mai da qui.
/// </summary>
public class ContestoLaboratorioAI : DbContext
{
    public ContestoLaboratorioAI(DbContextOptions<ContestoLaboratorioAI> opzioni)
        : base(opzioni) { }

    // ── Sicurezza ──────────────────────────────────────────────────
    public DbSet<Utente> Utenti => Set<Utente>();
    public DbSet<Ruolo> Ruoli => Set<Ruolo>();
    public DbSet<Permesso> Permessi => Set<Permesso>();
    public DbSet<UtenteRuolo> UtentiRuoli => Set<UtenteRuolo>();
    public DbSet<RuoloPermesso> RuoliPermessi => Set<RuoloPermesso>();

    // ── Dominio ────────────────────────────────────────────────────
    public DbSet<AnalisiAi> Analisi => Set<AnalisiAi>();
    public DbSet<EstrazioneCertificatoArchiviata> EstrazioniCertificato => Set<EstrazioneCertificatoArchiviata>();
    public DbSet<VoceRegistroDvr> RegistroDvr => Set<VoceRegistroDvr>();
    public DbSet<DvrPiscina> DvrPiscine => Set<DvrPiscina>();
    public DbSet<Configurazione> Configurazioni => Set<Configurazione>();

    // ── Nutrizionale (schema nutri) ────────────────────────────────
    public DbSet<MateriaPrima> MateriePrime => Set<MateriaPrima>();
    public DbSet<ValoreNutrizionale> ValoriNutrizionali => Set<ValoreNutrizionale>();
    public DbSet<Fonte> Fonti => Set<Fonte>();
    public DbSet<Ricetta> Ricette => Set<Ricetta>();
    public DbSet<RigaRicetta> RigheRicetta => Set<RigaRicetta>();
    public DbSet<RapportoNutrizionale> RapportiNutrizionali => Set<RapportoNutrizionale>();
    public DbSet<VerificaNutrizionale> VerificheNutrizionali => Set<VerificaNutrizionale>();

    // ── Taratura termometri (schema taratura) ──────────────────────
    public DbSet<CampioneRiferimento> CampioniRiferimento => Set<CampioneRiferimento>();
    public DbSet<PuntoCertificato> PuntiCertificato => Set<PuntoCertificato>();
    public DbSet<Taratura> Tarature => Set<Taratura>();
    public DbSet<EsitoValidazioneTaratura> ValidazioniCalcolo => Set<EsitoValidazioneTaratura>();
    public DbSet<AnalisiTaratura> AnalisiTarature => Set<AnalisiTaratura>();
    public DbSet<TaraturaIncubatore> TaratureIncubatori => Set<TaraturaIncubatore>();
    public DbSet<EsitoValidazioneIncubatore> ValidazioniCalcoloIncubatori => Set<EsitoValidazioneIncubatore>();
    public DbSet<AnalisiIncubatore> AnalisiIncubatori => Set<AnalisiIncubatore>();
    public DbSet<TaraturaMappatura> Mappature => Set<TaraturaMappatura>();
    public DbSet<EsitoValidazioneMappatura> ValidazioniCalcoloMappatura => Set<EsitoValidazioneMappatura>();
    public DbSet<AnalisiMappatura> AnalisiMappature => Set<AnalisiMappatura>();

    // ── Taratura bilance (schema bilance) ──────────────────────────
    public DbSet<Pesiera> Pesiere => Set<Pesiera>();
    public DbSet<MassaCampione> MasseCampione => Set<MassaCampione>();
    public DbSet<TaraturaBilancia> TaratureBilance => Set<TaraturaBilancia>();
    public DbSet<EsitoValidazioneBilancia> ValidazioniCalcoloBilance => Set<EsitoValidazioneBilancia>();
    public DbSet<AnalisiBilancia> AnalisiBilance => Set<AnalisiBilancia>();

    // ── Monitoraggio TempLog Econorma ──────────────────────────────
    public DbSet<MappaturaCanaleTempLog> MappatureCanaliTempLog => Set<MappaturaCanaleTempLog>();
    public DbSet<CaricamentoTempLog> CaricamentiTempLog => Set<CaricamentoTempLog>();
    public DbSet<SincronizzazioneSondeTempLog> SincronizzazioniSondeTempLog => Set<SincronizzazioneSondeTempLog>();

    // ── Scadenziario tarature ──────────────────────────────────────
    public DbSet<TermometroEsterno> TermometriEsterni => Set<TermometroEsterno>();

    // ── Accettazione campioni (staging) ────────────────────────────
    public DbSet<AccettazioneLotto> AccettazioneLotti => Set<AccettazioneLotto>();
    public DbSet<AccettazioneCampione> AccettazioneCampioni => Set<AccettazioneCampione>();
    public DbSet<AccettazioneProva> AccettazioneProve => Set<AccettazioneProva>();
    public DbSet<ContatoreAccettazione> ContatoriAccettazione => Set<ContatoreAccettazione>();
    public DbSet<PropostaControllo> ProposteControllo => Set<PropostaControllo>();
    public DbSet<PropostaRigaMetodo> ProposteRigheMetodo => Set<PropostaRigaMetodo>();

    // ── Preventivi (schema preventivi) ─────────────────────────────
    public DbSet<Preventivo> Preventivi => Set<Preventivo>();
    public DbSet<RigaPreventivo> RighePreventivo => Set<RigaPreventivo>();
    public DbSet<RigaMetodoPreventivo> RigheMetodoPreventivo => Set<RigaMetodoPreventivo>();
    public DbSet<VoceListinoAccessorio> ListinoAccessori => Set<VoceListinoAccessorio>();

    // ── Portale ────────────────────────────────────────────────────
    public DbSet<SessionePortale> SessioniPortale => Set<SessionePortale>();
    public DbSet<TentativoAccesso> TentativiAccesso => Set<TentativoAccesso>();

    // ── Audit ──────────────────────────────────────────────────────
    public DbSet<VoceAudit> Audit => Set<VoceAudit>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Le configurazioni sono in classi separate nella cartella Configurazioni/
        mb.ApplyConfigurationsFromAssembly(typeof(ContestoLaboratorioAI).Assembly);

        base.OnModelCreating(mb);
    }
}
