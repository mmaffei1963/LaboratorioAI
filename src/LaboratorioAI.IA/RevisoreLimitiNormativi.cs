using System.Globalization;
using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.IA;

/// <summary>
/// Revisione con l'AI delle schede validate di una fonte (18/10/2026, «quattro occhi»). L'AI legge il testo ufficiale
/// salvato e, per ogni scheda, dice se è conforme, dubbia o errata, citando la frase della norma. Non modifica nulla:
/// crea rilievi da chiudere con una nota. Modello: Anthropic:ModelloRevisione (vuoto = quello normale).
/// </summary>
public class RevisoreLimitiNormativi
{
    private const int TokenUscita = 16000, TimeoutSecondi = 600, SchedePerChiamata = 40;
    private readonly IClientAnthropic _ia;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly OpzioniAnthropic _opzioni;
    private readonly ILogger<RevisoreLimitiNormativi> _log;

    public RevisoreLimitiNormativi(IClientAnthropic ia, ContestoLaboratorioAI ctx, IOptions<OpzioniAnthropic> opzioni, ILogger<RevisoreLimitiNormativi> log)
    {
        _ia = ia; _ctx = ctx; _opzioni = opzioni.Value; _log = log;
    }

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
    };

    public class GiudizioAi
    {
        public int Id { get; set; }
        public string? Esito { get; set; }
        public string? Motivo { get; set; }
        public string? Citazione { get; set; }
    }

    private const string Istruzioni = """
        Sei il REVISORE indipendente delle «schede limite» di un laboratorio di prova accreditato ISO/IEC 17025 (MARE.A srl).
        Nel messaggio di sistema c'è il TESTO UFFICIALE della norma. Qui sotto trovi, in JSON, le schede già validate da
        un tecnico: ognuna dice per un parametro e una categoria qual è il limite (operatore, valore, unità, piano n/c/m/M,
        tipo di limite, condizioni) e riporta la frase della norma da cui è tratto.

        Controlla OGNI scheda SOLO sul testo fornito (non usare la memoria) e verifica:
        - il valore e l'unità di misura corrispondono al testo, per quel parametro e quella categoria;
        - l'operatore è giusto (≤, <, ≥, intervallo, assente in … g);
        - n, c, m (campo m_piccola), M (campo M_grande), se presenti, sono quelli della norma;
        - il tipo di limite è coerente (Sicurezza = superamento rende non conforme; IgieneProcesso; Indicatore;
          LineaGuida; Riferimento);
        - la categoria/matrice e le condizioni (fase, «immessi sul mercato durante la conservabilità», ecc.) sono corrette;
        - la citazione sostiene davvero il limite.

        Rispondi ESCLUSIVAMENTE con un array JSON, un oggetto per scheda, nello stesso ordine:
        { "id": numero della scheda, "esito": "Conforme" | "Dubbio" | "Errato",
          "motivo": "frase breve: cosa non torna e cosa dice la norma (vuoto se conforme)",
          "citazione": "frase del testo COPIATA PAROLA PER PAROLA che giustifica il giudizio (per le righe di tabella copia la riga con i « | »)" }
        Usa «Errato» solo se il testo dice chiaramente altro; «Dubbio» se il testo è ambiguo o la scheda è incompleta.
        """;

    public async Task EseguiAsync(int idGiro, CancellationToken ct)
    {
        var giro = await _ctx.GiriVerificaLimiti.FirstAsync(g => g.IdGiroVerificaLimiti == idGiro, ct);
        var versioni = _ctx.VersioniFonti.Where(v => v.IdFonteNormativa == giro.IdFonteNormativa).Select(v => (int?)v.IdVersioneFonte);
        var schede = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(s => (s.Stato == StatoSchedaLimite.Validata || s.Stato == StatoSchedaLimite.DaRiverificare) && versioni.Contains(s.IdVersioneFonte))
            .OrderBy(s => s.CodiceScheda).ToListAsync(ct);
        var modello = string.IsNullOrWhiteSpace(_opzioni.ModelloRevisione) ? _opzioni.Modello : _opzioni.ModelloRevisione!;
        giro.Modello = modello;
        int conformi = 0, dubbi = 0, errati = 0, senzaGiudizio = 0, tokenIn = 0, tokenOut = 0;

        // le schede possono venire da versioni diverse della fonte: si rivede ognuna sul testo da cui è stata tratta
        foreach (var gruppo in schede.GroupBy(s => s.IdVersioneFonte!.Value))
        {
            var v = await _ctx.VersioniFonti.AsNoTracking().Where(x => x.IdVersioneFonte == gruppo.Key)
                .Select(x => new { x.TestoEstratto, Titolo = x.Fonte!.Titolo, x.DataAggiornamentoDichiarata }).FirstAsync(ct);
            var sistema = $"FONTE: {v.Titolo}\nAggiornata al: {v.DataAggiornamentoDichiarata:dd/MM/yyyy}\n\n=== TESTO ===\n{v.TestoEstratto}";
            var testoNorm = RegoleLimiti.NormalizzaTesto(v.TestoEstratto);
            var testoComp = RegoleLimiti.Compatta(testoNorm);

            foreach (var blocco in gruppo.Chunk(SchedePerChiamata))
            {
                var elenco = blocco.Select(s => new
                {
                    id = s.IdLimiteNormativo, codice = s.CodiceScheda, famiglia = RegoleLimiti.CodiceFamiglia(s.Famiglia),
                    parametro = s.Parametro, categoria = s.MatriceCategoria, operatore = s.Operatore.ToString(),
                    valore = s.Valore, valoreMax = s.ValoreMax, unita = s.Unita, n = s.N, c = s.C, m_piccola = s.M, M_grande = s.MMaiuscola,
                    tipoLimite = s.TipoLimite.ToString(), regola = s.RegolaValutazione.ToString(), condizioni = s.Condizioni,
                    riferimento = s.Riferimento, citazione = s.Citazione
                });
                giro.Esito = $"Revisione in corso: {conformi + dubbi + errati}/{schede.Count} schede…";
                await _ctx.SaveChangesAsync(ct);
                var risposta = await _ia.InterrogaLungaAsync(sistema, Istruzioni + "\n\nSCHEDE:\n" + JsonSerializer.Serialize(elenco),
                    TokenUscita, TimeoutSecondi, ct, modello);
                tokenIn += risposta.TokenInput + risposta.TokenCacheScritti + risposta.TokenCacheLetti;
                tokenOut += risposta.TokenOutput;
                var giudizi = LeggiGiudizi(risposta.Testo);
                foreach (var s in blocco)
                {
                    var g = giudizi.FirstOrDefault(x => x.Id == s.IdLimiteNormativo);
                    var esito = (g?.Esito ?? "").Trim().ToLowerInvariant();
                    var gravita = esito.StartsWith("conf") ? GravitaRilievo.Conforme : esito.StartsWith("err") ? GravitaRilievo.Errato
                        : esito.StartsWith("dub") ? GravitaRilievo.Dubbio : (GravitaRilievo?)null;
                    if (gravita is null) { senzaGiudizio++; gravita = GravitaRilievo.Dubbio; }
                    var trovata = string.IsNullOrWhiteSpace(g?.Citazione) ? (bool?)null : RegoleLimiti.CitazionePresente(g!.Citazione, testoNorm, testoComp);
                    var motivo = g is null ? "Il revisore non ha restituito un giudizio per questa scheda." : (string.IsNullOrWhiteSpace(g.Motivo) ? "—" : g.Motivo.Trim());
                    if (trovata == false && gravita != GravitaRilievo.Conforme) motivo += " (la frase indicata dal revisore non è stata trovata nel testo: verificare)";
                    _ctx.RilieviVerificaLimiti.Add(new RilievoVerificaLimite
                    {
                        IdGiroVerificaLimiti = giro.IdGiroVerificaLimiti, IdLimiteNormativo = s.IdLimiteNormativo, Gravita = gravita.Value,
                        Regola = "revisione-ai", Motivo = motivo.Length > 2000 ? motivo[..2000] : motivo,
                        Citazione = g?.Citazione is { Length: > 2000 } c ? c[..2000] : g?.Citazione, CitazioneTrovata = trovata,
                        Chiuso = gravita == GravitaRilievo.Conforme   // i conformi restano come prova, già chiusi
                    });
                    switch (gravita) { case GravitaRilievo.Conforme: conformi++; break; case GravitaRilievo.Errato: errati++; break; default: dubbi++; break; }
                }
            }
        }
        giro.Stato = StatoGiroVerifica.Completato;
        giro.DataFine = DateTime.UtcNow;
        giro.SchedeControllate = schede.Count;
        giro.Esito = $"{schede.Count} schede riviste: {conformi} conformi, {dubbi} dubbie, {errati} errate"
            + (senzaGiudizio > 0 ? $" ({senzaGiudizio} senza giudizio del revisore)" : "") + $". Modello {modello}, token {tokenIn:N0}+{tokenOut:N0}.";
        await _ctx.SaveChangesAsync(ct);
        _log.LogInformation("Revisione limiti giro {Id}: {Esito}", idGiro, giro.Esito);
    }

    public static List<GiudizioAi> LeggiGiudizi(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return [];
        var i = testo.IndexOf('['); var f = testo.LastIndexOf(']');
        if (i >= 0 && f > i)
        {
            try { return JsonSerializer.Deserialize<List<GiudizioAi>>(testo[i..(f + 1)], Opzioni) ?? []; }
            catch (JsonException) { }
        }
        // risposta troncata: si recuperano gli oggetti completi
        if (i < 0) return [];
        var corpo = testo[i..];
        for (var k = corpo.LastIndexOf('}'); k > 0; k = corpo.LastIndexOf('}', k - 1))
        {
            try { return JsonSerializer.Deserialize<List<GiudizioAi>>(corpo[..(k + 1)] + "]", Opzioni) ?? []; }
            catch (JsonException) { }
        }
        return [];
    }
}
