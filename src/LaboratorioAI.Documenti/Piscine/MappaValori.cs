// LaboratorioAI.Documenti/Piscine/MappaValori.cs
//
// Corrispondenza fra le proprietà di SchedaPiscina e i tag del modello Word,
// comprese le formattazioni (enum -> testo, sì/no, date, elenchi tabellari).
//
// È l'unico punto in cui vivono queste corrispondenze: il GeneratorePiscine
// non conosce i nomi dei tag né le stringhe di visualizzazione, chiede a
// MappaValori i valori già pronti. Le stringhe restituite per gli elenchi a
// discesa COINCIDONO con le opzioni definite nel modello, così il valore
// impostato è sempre una scelta valida del controllo.

using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>
/// Valori pronti per la compilazione del modello, raccolti per categoria di
/// controllo contenuto.
/// </summary>
public record ValoriModello
{
    /// <summary>Tag -> testo, per controlli di testo, data ed elenco a discesa.</summary>
    public required IReadOnlyDictionary<string, string?> Testi { get; init; }

    /// <summary>Tag <c>chk_*</c> -> stato della casella.</summary>
    public required IReadOnlyDictionary<string, bool> Caselle { get; init; }

    /// <summary>Tag <c>se_*</c> -> mantenere (true) o rimuovere (false) la sezione.</summary>
    public required IReadOnlyDictionary<string, bool> Blocchi { get; init; }

    /// <summary>Tag <c>tab_*</c> -> righe, ciascuna come celle nell'ordine di colonna.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string?>>> Tabelle { get; init; }
}

/// <summary>Costruisce i valori del modello a partire da una <see cref="SchedaPiscina"/>.</summary>
public static class MappaValori
{
    // ---- Formattazioni di base ----------------------------------------------

    /// <summary>Rende un booleano come «Sì»/«No», coerente con gli elenchi Sì/No del modello.</summary>
    public static string SiNo(bool valore) => valore ? "Sì" : "No";

    /// <summary>Formatta una data nel formato del modello (dd/MM/yyyy).</summary>
    public static string? Data(DateOnly? data) => data?.ToString("dd/MM/yyyy");

    /// <summary>Testo dell'opzione per la collocazione dell'impianto.</summary>
    public static string? Testo(CollocazioneImpianto? c) => c switch
    {
        CollocazioneImpianto.Coperto => "Coperto (indoor)",
        CollocazioneImpianto.Scoperto => "Scoperto (outdoor)",
        CollocazioneImpianto.Misto => "Misto indoor/outdoor",
        _ => null,
    };

    /// <summary>Testo dell'opzione per il sistema di ricircolo.</summary>
    public static string? Testo(SistemaRicircolo? s) => s switch
    {
        SistemaRicircolo.Sfioro => "A sfioro (skimmer)",
        SistemaRicircolo.Bordo => "A bordo (sfioratori perimetrali)",
        SistemaRicircolo.Misto => "Misto",
        _ => null,
    };

    /// <summary>Testo dell'opzione per la conformità di griglie e bocchettoni.</summary>
    public static string? Testo(StatoConformitaGriglie? g) => g switch
    {
        StatoConformitaGriglie.Conformi => "Conformi / adeguate",
        StatoConformitaGriglie.NonConformi => "Non conformi - da adeguare",
        StatoConformitaGriglie.DaVerificare => "Da verificare",
        _ => null,
    };

    /// <summary>Testo dell'opzione per il trattamento dell'acqua.</summary>
    public static string? Testo(TrattamentoAcqua? t) => t switch
    {
        TrattamentoAcqua.Cloro => "Cloro",
        TrattamentoAcqua.Sale => "Sale (elettrolisi salina)",
        TrattamentoAcqua.OssigenoAttivo => "Ossigeno attivo",
        TrattamentoAcqua.UvOzono => "UV / Ozono in combinazione",
        TrattamentoAcqua.Altro => "Altro",
        _ => null,
    };

    /// <summary>Testo dell'opzione per la presenza dell'assistente bagnanti.</summary>
    public static string? Testo(PresenzaBagnino? b) => b switch
    {
        PresenzaBagnino.Continuativo => "Sì, con turni continuativi",
        PresenzaBagnino.FasceOrarie => "Sì, solo in fasce orarie",
        PresenzaBagnino.Assente => "No",
        _ => null,
    };

    /// <summary>Testo dell'opzione per la tipologia di struttura.</summary>
    public static string? Testo(TipoStruttura? t) => t switch
    {
        TipoStruttura.Albergo => "Albergo",
        TipoStruttura.VillaggioTuristico => "Villaggio turistico",
        TipoStruttura.Residence => "Residence",
        TipoStruttura.Agriturismo => "Agriturismo con piscina",
        TipoStruttura.Altro => "Altro",
        _ => null,
    };

    /// <summary>Testo dell'opzione per la tipologia di utenza.</summary>
    public static string? Testo(TipoUtenza? u) => u switch
    {
        TipoUtenza.Adulti => "Adulti",
        TipoUtenza.Bambini => "Bambini",
        TipoUtenza.Mista => "Mista adulti e bambini",
        _ => null,
    };

    // ---- Mappatura completa -------------------------------------------------

    /// <summary>Costruisce tutti i valori del modello dalla scheda.</summary>
    public static ValoriModello Mappa(SchedaPiscina s) => new()
    {
        Testi = Testi(s),
        Caselle = Caselle(s),
        Blocchi = Blocchi(s),
        Tabelle = Tabelle(s),
    };

    private static IReadOnlyDictionary<string, string?> Testi(SchedaPiscina s) => new Dictionary<string, string?>
    {
        // Anagrafica (ArcLab)
        ["ragione_sociale"] = s.Anagrafica.RagioneSociale,
        ["partita_iva"] = s.Anagrafica.PartitaIva,
        ["indirizzo"] = s.Anagrafica.Indirizzo,
        ["comune_provincia"] = s.Anagrafica.ComuneProvincia,
        ["regione"] = s.Anagrafica.Regione,
        ["rif_pratica"] = s.Anagrafica.RiferimentoPratica,
        // Sopralluogo
        ["tipo_struttura"] = Testo(s.TipoStruttura),
        ["numero_vasche"] = s.NumeroVasche?.ToString(),
        ["collocazione_impianto"] = Testo(s.Collocazione),
        ["tipo_utenza"] = Testo(s.TipoUtenza),
        ["sistema_ricircolo"] = Testo(s.SistemaRicircolo),
        ["griglie_conformi"] = Testo(s.GriglieConformi),
        ["trattamento_acqua"] = Testo(s.TrattamentoAcqua),
        ["presenza_idromassaggio"] = SiNo(s.PresenzaIdromassaggio),
        ["presenza_bagnino"] = Testo(s.PresenzaBagnino),
        ["presenza_dae"] = SiNo(s.PresenzaDae),
        ["responsabile_piscina"] = s.ResponsabilePiscina,
        ["addetto_impianti"] = s.AddettoImpianti,
        ["rischio_residuo"] = s.RischioResiduo,
        // Documento (DB LaboratorioAI)
        ["data_valutazione"] = Data(s.DataValutazione),
        ["revisione"] = s.Revisione,
        ["valutatore"] = s.Valutatore,
    };

    private static IReadOnlyDictionary<string, bool> Caselle(SchedaPiscina s) => new Dictionary<string, bool>
    {
        ["chk_griglie"] = s.Misure.Griglie,
        ["chk_cartelli"] = s.Misure.Cartelli,
        ["chk_salvagente"] = s.Misure.Salvagente,
        ["chk_pronto_soccorso"] = s.Misure.ProntoSoccorso,
        ["chk_dae"] = s.Misure.Dae,
        ["chk_regolamento"] = s.Misure.Regolamento,
        ["chk_registro"] = s.Misure.Registro,
        ["chk_manutenzione"] = s.Misure.Manutenzione,
        ["chk_formazione"] = s.Misure.Formazione,
    };

    private static IReadOnlyDictionary<string, bool> Blocchi(SchedaPiscina s) => new Dictionary<string, bool>
    {
        ["se_impianto_scoperto"] = s.ImpiantoScoperto,
        ["se_impianto_coperto"] = s.ImpiantoCoperto,
        ["se_vasca_idromassaggio"] = s.HaVascaIdromassaggio,
        ["se_assenza_bagnino"] = s.AssenzaBagnino,
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string?>>> Tabelle(SchedaPiscina s) =>
        new Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>>
        {
            ["tab_vasche"] = s.Vasche
                .Select(v => (IReadOnlyList<string?>)new List<string?>
                    { v.Denominazione, v.Tipo, v.Dimensioni, v.Profondita, v.Volume, v.Temperatura, v.Capienza })
                .ToList(),

            ["tab_parametri_controllo"] = s.ParametriControllo
                .Select(p => (IReadOnlyList<string?>)new List<string?>
                    { p.Parametro, p.UnitaMisura, p.Limite, p.Frequenza, p.Metodo })
                .ToList(),

            ["tab_punti_prelievo"] = s.PuntiPrelievo
                .Select(p => (IReadOnlyList<string?>)new List<string?>
                    { p.Punto, p.Ubicazione, p.Frequenza, p.EsitoStorico })
                .ToList(),

            ["tab_manutenzione"] = s.Manutenzione
                .Select(m => (IReadOnlyList<string?>)new List<string?>
                    { m.Attivita, m.Frequenza, m.Responsabile, Data(m.ProssimaScadenza) })
                .ToList(),
        };
}
