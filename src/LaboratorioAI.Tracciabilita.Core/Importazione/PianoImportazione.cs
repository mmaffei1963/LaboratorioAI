using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>Una riga di magazzino pronta per essere scritta, con fornitore e articolo già risolti dalle scelte di Mirko.</summary>
public sealed record RigaPronta(
    int Numero,
    string ChiaveFornitore,        // codice del fornitore nell'anagrafica Access, oppure NUOVO:<nome>
    string NomeArticolo,
    string CodiceLotto,
    DateOnly DataArrivo,
    string? TipoDocumento,
    string? NumeroDocumento,
    DateOnly? DataDocumento,
    decimal Quantita,
    string UnitaMisura,
    DateOnly? DataChiusura,        // vuota = lotto ancora in magazzino
    bool ChiusuraDaDataNonAttendibile,
    string? EnteBio,
    // Dalla tabella Ordine (facoltativi).
    int? IdMatrice = null,
    DateOnly? DataScadenza = null,
    DateOnly? DataProduzioneFornitore = null,
    GiudizioAccettazione Giudizio = GiudizioAccettazione.Accettato,
    string? Note = null,
    IReadOnlyList<ControlloPronto>? Controlli = null);

public sealed record RigaScartata(int Numero, string Motivo);

public sealed record FornitoreNuovo(string Chiave, string RagioneSociale);

public sealed class PianoImportazione
{
    public required IReadOnlyList<RigaPronta> Righe { get; init; }
    public required IReadOnlyList<RigaScartata> Scartate { get; init; }
    public required IReadOnlyList<FornitoreNuovo> FornitoriNuovi { get; init; }

    /// <summary>Diciture di fornitore o articolo senza una scelta nei file: l'importazione non parte finché ce ne sono.</summary>
    public required IReadOnlyList<string> NonRisolte { get; init; }

    /// <summary>Categorie, profili di controllo e categoria di ogni articolo (vuoto se i dati non vengono da Ordine).</summary>
    public PianoCategorie Categorie { get; init; } = PianoCategorie.Vuoto;

    public int LottiDistinti => Righe.Select(r => (r.ChiaveFornitore, r.NomeArticolo.ToLowerInvariant(), r.CodiceLotto.ToLowerInvariant())).Distinct().Count();
}

/// <summary>
/// Trasforma le righe del vecchio magazzino in cose da scrivere, applicando le scelte dei due file.
/// Storico: un lotto con data di impegno è già stato usato, quindi entra chiuso (carico e uscita di chiusura);
/// resta in magazzino solo quello senza impegno. Nessun consumo viene inventato.
/// </summary>
public static class PianificaImportazione
{
    public static PianoImportazione Costruisci(
        DatiAccess dati,
        IReadOnlyList<SceltaFornitore> scelteFornitori,
        IReadOnlyList<SceltaArticolo> scelteArticoli,
        DateTime oggi)
    {
        var perFornitore = scelteFornitori
            .GroupBy(s => s.Dicitura.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Scelta, StringComparer.OrdinalIgnoreCase);
        var perArticolo = scelteArticoli
            .GroupBy(s => s.Dicitura.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var nonRisolte = new List<string>();
        var nuovi = new Dictionary<string, FornitoreNuovo>(StringComparer.OrdinalIgnoreCase);
        var pronte = new List<RigaPronta>();
        var scartate = new List<RigaScartata>();

        foreach (var r in dati.Magazzino)
        {
            var forn = r.Fornitore?.Trim();
            var art = r.Articolo?.Trim();
            var lotto = r.Lotto?.Trim();

            if (string.IsNullOrEmpty(forn)) { scartate.Add(new(r.Numero, "Fornitore vuoto")); continue; }
            if (string.IsNullOrEmpty(art)) { scartate.Add(new(r.Numero, "Articolo vuoto")); continue; }
            if (string.IsNullOrEmpty(lotto)) { scartate.Add(new(r.Numero, "Codice lotto vuoto")); continue; }

            // Fornitore
            if (!perFornitore.TryGetValue(forn, out var sceltaF) || string.IsNullOrWhiteSpace(sceltaF))
            {
                NonRisolta(nonRisolte, "Fornitore", forn);
                continue;
            }
            string chiaveF;
            if (sceltaF.Equals(FileAbbinamenti.Salta, StringComparison.OrdinalIgnoreCase))
            {
                scartate.Add(new(r.Numero, "Fornitore escluso (SALTA)"));
                continue;
            }
            if (sceltaF.Equals(FileAbbinamenti.Nuovo, StringComparison.OrdinalIgnoreCase))
            {
                chiaveF = "NUOVO:" + NomiNormalizzati.ChiaveFornitore(forn);
                nuovi.TryAdd(chiaveF, new FornitoreNuovo(chiaveF, forn));
            }
            else if (TrovaCodice(dati.Fornitori, sceltaF) is { } codiceCanonico)
            {
                chiaveF = codiceCanonico;
            }
            else
            {
                NonRisolta(nonRisolte, "Fornitore (codice inesistente)", $"{forn} → {sceltaF}");
                continue;
            }

            // Articolo
            if (!perArticolo.TryGetValue(art, out var sceltaA) || (string.IsNullOrWhiteSpace(sceltaA.NomeArticolo) && !sceltaA.Salta))
            {
                NonRisolta(nonRisolte, "Articolo", art);
                continue;
            }
            if (sceltaA.Salta) { scartate.Add(new(r.Numero, "Articolo escluso (SALTA)")); continue; }

            // Quantità e date
            if (r.Quantita < 0) { scartate.Add(new(r.Numero, "Quantità negativa")); continue; }

            var limite = oggi.AddDays(31);
            var docOk = r.DataDocumento is { } dd && dd.Year >= 2004 && dd <= limite;
            var impOk = r.DataImpegno is { } di && di.Year >= 2004 && di <= oggi.AddYears(1);
            DateTime? arrivo = docOk ? r.DataDocumento : (impOk ? r.DataImpegno : null);
            if (arrivo is null) { scartate.Add(new(r.Numero, "Nessuna data attendibile")); continue; }

            DateOnly? chiusura = null;
            var nonAttendibile = false;
            if (r.DataImpegno is { } imp)
            {
                // Impegno plausibile e non anteriore all'arrivo: è la data di chiusura. Altrimenti si chiude alla data di arrivo.
                if (impOk && imp >= arrivo) chiusura = DateOnly.FromDateTime(imp);
                else { chiusura = DateOnly.FromDateTime(arrivo.Value); nonAttendibile = true; }
            }

            pronte.Add(new RigaPronta(
                r.Numero, chiaveF, sceltaA.NomeArticolo.Trim(), lotto, DateOnly.FromDateTime(arrivo.Value),
                Vuoto(r.TipoDocumento), Vuoto(r.NumeroDocumento), r.DataDocumento is { } d && docOk ? DateOnly.FromDateTime(d) : null,
                r.Quantita ?? 0,
                LetturaOrdine.Unita(r.UnitaMisura) ?? (string.IsNullOrWhiteSpace(sceltaA.UnitaMisura) ? "kg" : sceltaA.UnitaMisura.Trim()),
                chiusura, nonAttendibile, Vuoto(r.EnteBio),
                r.IdMatrice, DataOpzionale(LetturaOrdine.Data(r.DataScadenza)), DataOpzionale(LetturaOrdine.Data(r.DataProduzione)),
                LetturaOrdine.Giudizio(r.Giudizio), Vuoto(r.Note), LetturaOrdine.Controlli(r)));
        }

        return new PianoImportazione
        {
            Righe = pronte,
            Scartate = scartate,
            FornitoriNuovi = nuovi.Values.ToList(),
            NonRisolte = nonRisolte,
            Categorie = PianificaCategorie.Costruisci(dati, pronte)
        };
    }

    /// <summary>
    /// Trova il codice del fornitore anche se Excel ha tolto gli zeri iniziali (le partite IVA come 04119190371 diventano 4119190371).
    /// Se più fornitori restano ambigui non decide: lo farà la persona.
    /// </summary>
    public static string? TrovaCodice(IReadOnlyList<FornitoreAccess> fornitori, string scelta)
    {
        var s = scelta.Trim();
        var esatto = fornitori.FirstOrDefault(f => f.Codice.Equals(s, StringComparison.OrdinalIgnoreCase));
        if (esatto is not null) return esatto.Codice;

        var senzaZeri = s.TrimStart('0');
        if (senzaZeri.Length == 0) return null;
        var simili = fornitori.Where(f => f.Codice.TrimStart('0').Equals(senzaZeri, StringComparison.OrdinalIgnoreCase)).ToList();
        return simili.Count == 1 ? simili[0].Codice : null;
    }

    private static void NonRisolta(List<string> elenco, string tipo, string dicitura)
    {
        var voce = $"{tipo}: {dicitura}";
        if (!elenco.Contains(voce)) elenco.Add(voce);
    }

    private static DateOnly? DataOpzionale(DateOnly? d) => d;

    private static string? Vuoto(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
}
