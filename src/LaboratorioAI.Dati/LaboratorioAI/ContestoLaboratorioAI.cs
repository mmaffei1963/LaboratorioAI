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
    public DbSet<PreferitoUtente> PreferitiUtenti => Set<PreferitoUtente>();
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

    // ── Ordini di Accettazione emessi (registro anti-doppione, schema accettazione) ──
    public DbSet<OrdineAccettazioneEmesso> OrdiniAccettazioneEmessi => Set<OrdineAccettazioneEmesso>();
    public DbSet<OrdinePrivato> OrdiniPrivati => Set<OrdinePrivato>();

    // ── Certificati esterni da importare (schema certificati) ──────
    public DbSet<CertificatoEsternoDaImportare> CertificatiEsterniDaImportare => Set<CertificatoEsternoDaImportare>();
    public DbSet<SiglaLaboratorioEsterno> SigleLaboratoriEsterni => Set<SiglaLaboratorioEsterno>();

    // ── Registro di controllo dei documenti per Palomar (schema log, append-only) ──
    public DbSet<RegistroDocumento> RegistroDocumenti => Set<RegistroDocumento>();

    // ── Accreditamento ACCREDIA laboratori esterni (schema labesterni) ────
    public DbSet<AccreditamentoLabEsterno> AccreditamentiEsterni => Set<AccreditamentoLabEsterno>();
    public DbSet<RevisioneAccreditamentoEsterno> RevisioniAccreditamentoEsterno => Set<RevisioneAccreditamentoEsterno>();
    public DbSet<VerificaMetodoAccreditamento> VerificheMetodoAccreditamento => Set<VerificaMetodoAccreditamento>();
    public DbSet<ProvaSospesaLabEsterno> ProveSospeseLabEsterni => Set<ProvaSospesaLabEsterno>();
    public DbSet<DenominazioneMetodoVerificata> DenominazioniMetodoVerificate => Set<DenominazioneMetodoVerificata>();

    // ── Condizioni Generali e firme dei clienti (schema contratti, 09/10/2026) ──
    public DbSet<RevisioneCondizioni> RevisioniCondizioni => Set<RevisioneCondizioni>();
    public DbSet<ClausolaCondizioni> ClausoleCondizioni => Set<ClausolaCondizioni>();
    public DbSet<FirmatarioCliente> FirmatariClienti => Set<FirmatarioCliente>();
    public DbSet<RichiestaApprovazione> RichiesteApprovazione => Set<RichiestaApprovazione>();
    public DbSet<EventoApprovazione> EventiApprovazione => Set<EventoApprovazione>();
    public DbSet<InvitoFirmatario> InvitiFirmatario => Set<InvitoFirmatario>();
    public DbSet<FirmaGraficaApprovazione> FirmeGraficheApprovazione => Set<FirmaGraficaApprovazione>();

    // ── Preventivi (schema preventivi) ─────────────────────────────
    public DbSet<Preventivo> Preventivi => Set<Preventivo>();
    // 05/11/2026: richieste di analisi dei clienti privati (Mod. Com. 05B)
    public DbSet<RichiestaAnalisiPrivato> RichiesteAnalisiPrivati => Set<RichiestaAnalisiPrivato>();
    public DbSet<RigaRichiestaAnalisiPrivato> RigheRichiestaAnalisiPrivati => Set<RigaRichiestaAnalisiPrivato>();
    // 06/11/2026: invio ai laboratori esterni dell'elenco campioni
    public DbSet<ContattoLaboratorioEsterno> ContattiLaboratoriEsterni => Set<ContattoLaboratorioEsterno>();
    public DbSet<InvioLaboratorioEsterno> InviiLaboratoriEsterni => Set<InvioLaboratorioEsterno>();
    public DbSet<RigaInvioLaboratorioEsterno> RigheInvioLaboratorioEsterno => Set<RigaInvioLaboratorioEsterno>();
    public DbSet<RigaPreventivo> RighePreventivo => Set<RigaPreventivo>();
    public DbSet<RigaMetodoPreventivo> RigheMetodoPreventivo => Set<RigaMetodoPreventivo>();
    public DbSet<VoceListinoAccessorio> ListinoAccessori => Set<VoceListinoAccessorio>();
    public DbSet<MetodoPagamento> MetodiPagamento => Set<MetodoPagamento>();

    // ── Portale ────────────────────────────────────────────────────
    public DbSet<SessionePortale> SessioniPortale => Set<SessionePortale>();
    public DbSet<TentativoAccesso> TentativiAccesso => Set<TentativoAccesso>();

    // ── Audit ──────────────────────────────────────────────────────
    public DbSet<VoceAudit> Audit => Set<VoceAudit>();
    /// <summary>Domande a «Chiedi alla guida» (11/10/2026).</summary>
    public DbSet<DomandaGuida> DomandeGuida => Set<DomandaGuida>();

    /// <summary>Elenco ISTAT dei comuni italiani: normalizza i luoghi nei documenti.</summary>
    public DbSet<Comune> Comuni => Set<Comune>();

    /// <summary>I CAP dei comuni: uno solo per quasi tutti, 82 per Roma.</summary>
    public DbSet<ComuneCap> ComuniCap => Set<ComuneCap>();

    // ── Provvigioni commerciali (schema provv) ─────────────────────
    public DbSet<Commerciale> Commerciali => Set<Commerciale>();
    public DbSet<CodiceVenditoreCommerciale> CodiciVenditore => Set<CodiceVenditoreCommerciale>();
    public DbSet<AzwFattura> AzwFatture => Set<AzwFattura>();
    public DbSet<AzwIncasso> AzwIncassi => Set<AzwIncasso>();
    public DbSet<AzwNotaCredito> AzwNoteCredito => Set<AzwNotaCredito>();
    public DbSet<ImportazioneAzw> ImportazioniAzw => Set<ImportazioneAzw>();
    public DbSet<VoceElencoSviluppo> ElencoSviluppo => Set<VoceElencoSviluppo>();
    public DbSet<ConvenzioneProvvigioni> Convenzioni => Set<ConvenzioneProvvigioni>();
    public DbSet<AzwRigaFattura> AzwRigheFattura => Set<AzwRigaFattura>();
    public DbSet<AzwRigaNotaCredito> AzwRigheNoteCredito => Set<AzwRigaNotaCredito>();
    public DbSet<BaseCalcolo> BasiCalcolo => Set<BaseCalcolo>();
    public DbSet<AccordoProvvigioni> AccordiProvvigioni => Set<AccordoProvvigioni>();
    public DbSet<VoceAccordo> VociAccordo => Set<VoceAccordo>();
    public DbSet<SogliaAccordo> SoglieAccordo => Set<SogliaAccordo>();
    public DbSet<CommessaArcLab> CommesseArcLab => Set<CommessaArcLab>();
    public DbSet<Segnalatore> Segnalatori => Set<Segnalatore>();
    public DbSet<CondizioneSegnalatore> CondizioniSegnalatori => Set<CondizioneSegnalatore>();
    public DbSet<AttivitaConsulenza> AttivitaConsulenza => Set<AttivitaConsulenza>();
    public DbSet<ContenutoAttivita> ContenutiAttivita => Set<ContenutoAttivita>();
    public DbSet<PacchettoConsulenza> PacchettiConsulenza => Set<PacchettoConsulenza>();
    public DbSet<RigaPacchettoConsulenza> RighePacchettoConsulenza => Set<RigaPacchettoConsulenza>();
    public DbSet<PreventivoConsulenza> PreventiviConsulenza => Set<PreventivoConsulenza>();
    public DbSet<RigaPreventivoConsulenza> RighePreventivoConsulenza => Set<RigaPreventivoConsulenza>();
    public DbSet<BloccoPreventivoConsulenza> BlocchiPreventivoConsulenza => Set<BloccoPreventivoConsulenza>();
    public DbSet<AnalisiPacchettoConsulenza> AnalisiPacchettiConsulenza => Set<AnalisiPacchettoConsulenza>();
    public DbSet<AnalisiPreventivoConsulenza> AnalisiPreventiviConsulenza => Set<AnalisiPreventivoConsulenza>();
    public DbSet<MetodoAnalisiConsulenza> MetodiAnalisiConsulenza => Set<MetodoAnalisiConsulenza>();
    public DbSet<AzwCliente> AzwClienti => Set<AzwCliente>();

    // Limiti normativi (schema limiti, 17/10/2026)
    public DbSet<FonteNormativa> FontiNormative => Set<FonteNormativa>();
    public DbSet<VersioneFonte> VersioniFonti => Set<VersioneFonte>();
    public DbSet<LimiteNormativo> LimitiNormativi => Set<LimiteNormativo>();
    public DbSet<AbbinamentoLimite> AbbinamentiLimiti => Set<AbbinamentoLimite>();
    public DbSet<GiroVerificaLimiti> GiriVerificaLimiti => Set<GiroVerificaLimiti>();
    public DbSet<RilievoVerificaLimite> RilieviVerificaLimiti => Set<RilievoVerificaLimite>();
    public DbSet<CategoriaNormativa> CategorieNormative => Set<CategoriaNormativa>();
    public DbSet<AbbinamentoMatriceCategoria> MatriciCategorie => Set<AbbinamentoMatriceCategoria>();
    public DbSet<TempoMassimoProva> TempiMassimiProva => Set<TempoMassimoProva>();
    public DbSet<GiudizioDaRiportare> GiudiziDaRiportare => Set<GiudizioDaRiportare>();
    public DbSet<ConsulentePercentuale> ConsulentiPercentuale => Set<ConsulentePercentuale>();
    public DbSet<ContrattoConsulente> ContrattiConsulenti => Set<ContrattoConsulente>();
    public DbSet<VociGruppoSpettanze> VociGruppiSpettanze => Set<VociGruppoSpettanze>();
    public DbSet<AzwFatturaConsulente> AzwFattureConsulenti => Set<AzwFatturaConsulente>();
    public DbSet<AnnoSpettanzeChiuso> AnniSpettanzeChiusi => Set<AnnoSpettanzeChiuso>();
    public DbSet<MovimentoSpettanzaArchiviato> MovimentiSpettanze => Set<MovimentoSpettanzaArchiviato>();

    // Cruscotto Direzione (13/10/2026)
    public DbSet<MovimentoAnaliticoMensile> MovimentiAnalitici => Set<MovimentoAnaliticoMensile>();
    public DbSet<RegistrazioneAnalitica> RegistrazioniAnalitiche => Set<RegistrazioneAnalitica>();
    // 10/11/2026: voci dei documenti di vendita AZW (ricerca fatture per descrizione)
    public DbSet<VoceDocumentoVenditaAzw> VociDocumentiVenditaAzw => Set<VoceDocumentoVenditaAzw>();
    /// <summary>11/11/2026: storico della zona AZW (responsabile) dei clienti, aggiornato a ogni importazione.</summary>
    public DbSet<StoricoZonaCliente> StoricoZoneClienti => Set<StoricoZonaCliente>();
    public DbSet<ClassificazioneVoce> ClassificazioniVoci => Set<ClassificazioneVoce>();
    public DbSet<CentroCostoAzw> CentriCostoAzw => Set<CentroCostoAzw>();
    public DbSet<CommessaAzw> CommesseAzw => Set<CommessaAzw>();
    public DbSet<FatturatoClienteMensile> FatturatoClienti => Set<FatturatoClienteMensile>();
    public DbSet<CommentoAiDirezione> CommentiAiDirezione => Set<CommentoAiDirezione>();

    // Avviso campioni in ritardo (22/10/2026)
    public DbSet<CampioneNascostoRitardi> CampioniNascostiRitardi => Set<CampioneNascostoRitardi>();

    // Compensi soci (21/10/2026, schema soci)
    public DbSet<Socio> Soci => Set<Socio>();
    public DbSet<AnnoCompensi> AnniCompensi => Set<AnnoCompensi>();
    public DbSet<PercentualeVoceLaboratorio> PercentualiVociLaboratorio => Set<PercentualeVoceLaboratorio>();
    public DbSet<CompensoSocioAnno> CompensiSociAnno => Set<CompensoSocioAnno>();
    public DbSet<PremioAreaLaboratorio> PremiAreaLaboratorio => Set<PremioAreaLaboratorio>();
    public DbSet<CategoriaRimborso> CategorieRimborsi => Set<CategoriaRimborso>();
    public DbSet<RimborsoSpesa> RimborsiSpese => Set<RimborsoSpesa>();
    public DbSet<RicevutaRimborso> RicevuteRimborsi => Set<RicevutaRimborso>();
    public DbSet<TrasfertaAzw> TrasferteAzw => Set<TrasfertaAzw>();

    // Clienti da seguire (schema clienti)
    public DbSet<CampioneMensileArcLab> CampioniMensiliArcLab => Set<CampioneMensileArcLab>();
    // Cambi di gestione (subentri, 12/10/2026)
    public DbSet<InsediamentoArcLab> InsediamentiArcLab => Set<InsediamentoArcLab>();
    public DbSet<CampioneInsediamentoArcLab> CampioniInsediamentiArcLab => Set<CampioneInsediamentoArcLab>();
    public DbSet<SubentroInsediamento> SubentriInsediamenti => Set<SubentroInsediamento>();
    public DbSet<VerificaPartitaIva> VerifichePartitaIva => Set<VerificaPartitaIva>();
    public DbSet<PrestazioneInsediamentoArcLab> PrestazioniInsediamentiArcLab => Set<PrestazioneInsediamentoArcLab>();
    public DbSet<ImpostazioniAndamento> ImpostazioniAndamento => Set<ImpostazioniAndamento>();
    public DbSet<AzioneCliente> AzioniClienti => Set<AzioneCliente>();
    public DbSet<AssegnazioneCliente> AssegnazioniClienti => Set<AssegnazioneCliente>();
    public DbSet<AttributiCliente> AttributiClienti => Set<AttributiCliente>();
    public DbSet<ClienteTolto> ClientiTolti => Set<ClienteTolto>();
    public DbSet<ResponsabileCliente> ResponsabiliCliente => Set<ResponsabileCliente>();
    public DbSet<Segnalazione> Segnalazioni => Set<Segnalazione>();
    public DbSet<EccezioneAreaCommerciale> EccezioniArea => Set<EccezioneAreaCommerciale>();
    public DbSet<MovimentoSegnalatoreArchiviato> MovimentiSegnalatori => Set<MovimentoSegnalatoreArchiviato>();
    public DbSet<TrimestreProvvigioni> TrimestriProvvigioni => Set<TrimestreProvvigioni>();
    public DbSet<MovimentoProvvigioneArchiviato> MovimentiProvvigioni => Set<MovimentoProvvigioneArchiviato>();
    public DbSet<PremioProvvigioni> PremiProvvigioni => Set<PremioProvvigioni>();

    // Rapportini di consulenza (schema cons)
    public DbSet<Rapportino> Rapportini => Set<Rapportino>();
    public DbSet<ConsulenteUtente> ConsulentiUtenti => Set<ConsulenteUtente>();
    public DbSet<VoceIncarico> VociIncarico => Set<VoceIncarico>();
    public DbSet<RapportinoDocumento> RapportiniDocumenti => Set<RapportinoDocumento>();
    public DbSet<DocumentoTipo> DocumentiTipo => Set<DocumentoTipo>();
    public DbSet<DelegaCliente> DelegheClienti => Set<DelegaCliente>();
    public DbSet<ConsulenteDati> ConsulentiDati => Set<ConsulenteDati>();
    public DbSet<DelegaInsediamento> DelegheInsediamenti => Set<DelegaInsediamento>();

    // ── Manualistica (schema man, 01/11/2026) ──────────────────────
    public DbSet<TipoManuale> TipiManuale => Set<TipoManuale>();
    public DbSet<ModelloDocumento> ModelliDocumento => Set<ModelloDocumento>();
    public DbSet<RevisioneModello> RevisioniModello => Set<RevisioneModello>();
    public DbSet<ApprovazioneRevisione> ApprovazioniRevisione => Set<ApprovazioneRevisione>();
    public DbSet<ApprovatoreTipoManuale> ApprovatoriTipoManuale => Set<ApprovatoreTipoManuale>();
    public DbSet<VoceChecklist> VociChecklist => Set<VoceChecklist>();

    /// <summary>Gli archivi del cliente, cartacei e informatici, e cosa contengono.</summary>
    public DbSet<ArchivioCompilazione> ArchiviCompilazione => Set<ArchivioCompilazione>();
    public DbSet<ArchivioAccesso> ArchiviAccessi => Set<ArchivioAccesso>();
    public DbSet<ArchivioInteressati> ArchiviInteressati => Set<ArchivioInteressati>();
    public DbSet<ArchivioCategoriaDati> ArchiviCategorieDati => Set<ArchivioCategoriaDati>();

    /// <summary>I gestionali del cliente, scritti una volta e installati dove serve.</summary>
    public DbSet<GestionaleCompilazione> GestionaliCompilazione => Set<GestionaleCompilazione>();
    public DbSet<ArchivioGestionale> ArchiviGestionali => Set<ArchivioGestionale>();

    /// <summary>Le caselle di posta del cliente e le funzioni che vi accedono.</summary>
    public DbSet<CasellaEmailCompilazione> CaselleEmailCompilazione => Set<CasellaEmailCompilazione>();
    public DbSet<CasellaEmailFunzione> CaselleEmailFunzioni => Set<CasellaEmailFunzione>();
    public DbSet<OpzioneVoce> OpzioniVoce => Set<OpzioneVoce>();
    public DbSet<FunzioneAziendale> FunzioniAziendali => Set<FunzioneAziendale>();
    public DbSet<QualificaAutorizzato> Qualifiche => Set<QualificaAutorizzato>();
    public DbSet<Compilazione> Compilazioni => Set<Compilazione>();
    public DbSet<SedeCompilazione> SediCompilazione => Set<SedeCompilazione>();
    public DbSet<ValoreCompilazione> ValoriCompilazione => Set<ValoreCompilazione>();
    public DbSet<AutorizzatoCompilazione> AutorizzatiCompilazione => Set<AutorizzatoCompilazione>();
    public DbSet<AutorizzatoFunzione> AutorizzatiFunzioni => Set<AutorizzatoFunzione>();
    public DbSet<ResponsabileCompilazione> ResponsabiliCompilazione => Set<ResponsabileCompilazione>();
    public DbSet<SceltaResponsabile> ScelteResponsabile => Set<SceltaResponsabile>();
    public DbSet<NotaCompilazione> NoteCompilazione => Set<NotaCompilazione>();
    public DbSet<ContrattoFornitura> ContrattiFornitura => Set<ContrattoFornitura>();
    public DbSet<IntestatarioContratto> IntestatariContratto => Set<IntestatarioContratto>();
    public DbSet<FirmaConsulente> FirmeConsulenti => Set<FirmaConsulente>();
    public DbSet<FirmaCompilazione> FirmeCompilazione => Set<FirmaCompilazione>();
    public DbSet<ManualeGenerato> ManualiGenerati => Set<ManualeGenerato>();
    public DbSet<ManualeGeneratoRevisione> ManualiGeneratiRevisioni => Set<ManualeGeneratoRevisione>();
    public DbSet<InvioDocumenti> InviiDocumenti => Set<InvioDocumenti>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Le configurazioni sono in classi separate nella cartella Configurazioni/
        mb.ApplyConfigurationsFromAssembly(typeof(ContestoLaboratorioAI).Assembly);

        base.OnModelCreating(mb);
    }
}
