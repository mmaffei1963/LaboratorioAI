namespace LaboratorioAI.Tracciabilita.Core.Importazione;

public enum EsitoAbbinamento
{
    /// <summary>Stesso nome a meno di maiuscole, punteggiatura e forma societaria.</summary>
    Sicuro = 1,
    /// <summary>Somiglia molto a un solo candidato: da confermare.</summary>
    Probabile = 2,
    /// <summary>Nessun candidato convincente: lo sceglie una persona.</summary>
    Nessuno = 3
}

public sealed record FornitoreNoto(string Codice, string RagioneSociale);

public sealed record PropostaFornitore(string Dicitura, int Righe, FornitoreNoto? Candidato, EsitoAbbinamento Esito, double Punteggio);

public sealed record GruppoArticolo(string Chiave, IReadOnlyList<string> Varianti, int Righe);

/// <summary>Propone gli abbinamenti dei nomi scritti a mano. Non decide niente: produce proposte per la schermata di conferma.</summary>
public static class AbbinamentoNomi
{
    public const double SogliaProbabile = 0.6;
    public const double ScartoMinimo = 0.15;

    public static PropostaFornitore ProponiFornitore(string dicitura, int righe, IReadOnlyList<FornitoreNoto> anagrafica)
    {
        var chiave = NomiNormalizzati.ChiaveFornitore(dicitura);
        if (chiave.Length > 0)
        {
            var uguali = anagrafica.Where(f => NomiNormalizzati.ChiaveFornitore(f.RagioneSociale) == chiave).ToList();
            if (uguali.Count == 1) return new PropostaFornitore(dicitura, righe, uguali[0], EsitoAbbinamento.Sicuro, 1);

            // Stesso nome presente più volte in anagrafica (doppione): il nome è giusto ma quale riga tenere lo conferma una persona.
            if (uguali.Count > 1) return new PropostaFornitore(dicitura, righe, uguali[0], EsitoAbbinamento.Probabile, 1);
        }

        var classifica = anagrafica
            .Select(f => (F: f, P: NomiNormalizzati.Somiglianza(dicitura, f.RagioneSociale, comeFornitore: true)))
            .OrderByDescending(x => x.P)
            .Take(2)
            .ToList();

        if (classifica.Count > 0 && classifica[0].P >= SogliaProbabile &&
            (classifica.Count == 1 || classifica[0].P - classifica[1].P >= ScartoMinimo))
            return new PropostaFornitore(dicitura, righe, classifica[0].F, EsitoAbbinamento.Probabile, classifica[0].P);

        return new PropostaFornitore(dicitura, righe, null, EsitoAbbinamento.Nessuno, classifica.Count > 0 ? classifica[0].P : 0);
    }

    /// <summary>Riunisce le diciture che differiscono solo per maiuscole, punteggiatura o ordine delle parole.</summary>
    public static IReadOnlyList<GruppoArticolo> RaggruppaArticoli(IEnumerable<(string Descrizione, int Righe)> diciture) =>
        diciture
            .Where(d => !string.IsNullOrWhiteSpace(d.Descrizione))
            .GroupBy(d => NomiNormalizzati.ChiaveArticolo(d.Descrizione))
            .Select(g => new GruppoArticolo(
                g.Key,
                g.OrderByDescending(x => x.Righe).Select(x => x.Descrizione.Trim()).Distinct().ToList(),
                g.Sum(x => x.Righe)))
            .OrderByDescending(g => g.Righe)
            .ToList();

    /// <summary>Coppie di gruppi diversi ma molto simili: probabili duplicati da far unire a una persona.</summary>
    public static IReadOnlyList<(GruppoArticolo A, GruppoArticolo B, double Punteggio)> DuplicatiProbabili(
        IReadOnlyList<GruppoArticolo> gruppi, double soglia = 0.75)
    {
        var coppie = new List<(GruppoArticolo, GruppoArticolo, double)>();
        for (var i = 0; i < gruppi.Count; i++)
            for (var j = i + 1; j < gruppi.Count; j++)
            {
                var p = NomiNormalizzati.Somiglianza(gruppi[i].Chiave, gruppi[j].Chiave, comeFornitore: false);
                if (p >= soglia) coppie.Add((gruppi[i], gruppi[j], p));
            }
        return coppie.OrderByDescending(c => c.Item3).ToList();
    }
}
