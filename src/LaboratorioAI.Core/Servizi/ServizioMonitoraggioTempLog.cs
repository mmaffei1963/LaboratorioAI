using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Applica i limiti di taratura alle letture del file TempLog ed estrae le escursioni.</summary>
public interface IServizioMonitoraggioTempLog
{
    RisultatoMonitoraggio Monitora(FileTempLog file, IReadOnlyList<LimitiCanale> limiti);
}

/// <inheritdoc />
///
/// <remarks>
/// Riproduce la logica del foglio Econorma: per ogni canale una lettura è
/// "sopra il massimo" se &gt; Max, "sotto il minimo" se &lt; Min, altrimenti in range.
/// I limiti (Min = LimInfInt, Max = LimSupInt) provengono dalla taratura in ArcLab.
/// A differenza del foglio storico NON altera i dati: rileva ed elenca le escursioni.
/// </remarks>
public class ServizioMonitoraggioTempLog : IServizioMonitoraggioTempLog
{
    public RisultatoMonitoraggio Monitora(FileTempLog file, IReadOnlyList<LimitiCanale> limiti)
    {
        var mappaLimiti = limiti
            .GroupBy(l => l.Canale, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var esiti = new List<EsitoCanale>();
        var tutteEscursioni = new List<Escursione>();
        var daSistemare = new List<string>();

        foreach (var canale in file.Canali)
        {
            var letture = file.Letture.TryGetValue(canale, out var l) ? l : new List<LetturaTempLog>();
            mappaLimiti.TryGetValue(canale, out var lim);

            bool limitiOk = lim is { LimitiDisponibili: true };
            if (!limitiOk) daSistemare.Add(canale);

            var valutate = new List<LetturaValutata>(letture.Count);
            foreach (var r in letture)
            {
                StatoLettura stato;
                if (r.Valore is null) stato = StatoLettura.Vuota;
                else if (!limitiOk) stato = StatoLettura.SenzaLimiti;
                else if (r.Valore > lim!.Max!.Value) stato = StatoLettura.SopraMax;
                else if (r.Valore < lim.Min!.Value) stato = StatoLettura.SottoMin;
                else stato = StatoLettura.InRange;
                valutate.Add(new LetturaValutata { Ora = r.Ora, Valore = r.Valore, Stato = stato });
            }

            var valori = letture.Where(x => x.Valore.HasValue).Select(x => x.Valore!.Value).ToList();
            int nSopra = valutate.Count(x => x.Stato == StatoLettura.SopraMax);
            int nSotto = valutate.Count(x => x.Stato == StatoLettura.SottoMin);

            var escursioni = limitiOk
                ? EstraiEscursioni(canale, lim!, valutate, file.DataDal)
                : new List<Escursione>();
            tutteEscursioni.AddRange(escursioni);

            esiti.Add(new EsitoCanale
            {
                Canale = canale,
                IdStrumento = lim?.IdStrumento,
                Sonda = lim?.Sonda,
                Ambiente = lim?.Ambiente,
                VN = lim?.VN,
                Min = lim?.Min,
                Max = lim?.Max,
                LimitiDisponibili = limitiOk,
                MotivoMancanza = limitiOk ? null : (lim?.MotivoMancanza ?? "Canale non mappato o senza taratura in ArcLab."),
                Letture = valutate,
                NumeroLetture = valori.Count,
                NumeroSopra = nSopra,
                NumeroSotto = nSotto,
                ValoreMedio = valori.Count > 0 ? valori.Average() : null,
                ValoreMin = valori.Count > 0 ? valori.Min() : null,
                ValoreMax = valori.Count > 0 ? valori.Max() : null,
                Conforme = limitiOk && escursioni.Count == 0,
                Escursioni = escursioni
            });
        }

        int monitorati = esiti.Count(e => e.LimitiDisponibili);
        int conEscursioni = esiti.Count(e => e.Escursioni.Count > 0);

        return new RisultatoMonitoraggio
        {
            DataDal = file.DataDal,
            DataAl = file.DataAl,
            NomeFile = file.NomeFile,
            Canali = esiti,
            Escursioni = tutteEscursioni,
            DaSistemare = daSistemare,
            NumeroCanaliMonitorati = monitorati,
            NumeroCanaliConEscursioni = conEscursioni,
            TuttoConforme = monitorati > 0 && conEscursioni == 0
        };
    }

    /// <summary>Raggruppa le letture fuori limite consecutive dello stesso tipo in escursioni.</summary>
    private static List<Escursione> EstraiEscursioni(string canale, LimitiCanale lim, IReadOnlyList<LetturaValutata> letture, DateTime? data)
    {
        var risultato = new List<Escursione>();
        int i = 0;
        while (i < letture.Count)
        {
            var stato = letture[i].Stato;
            if (stato != StatoLettura.SopraMax && stato != StatoLettura.SottoMin) { i++; continue; }

            int j = i;
            while (j + 1 < letture.Count && letture[j + 1].Stato == stato) j++;

            var blocco = letture.Skip(i).Take(j - i + 1).Where(x => x.Valore.HasValue).ToList();
            bool sopra = stato == StatoLettura.SopraMax;
            double limite = sopra ? lim.Max!.Value : lim.Min!.Value;
            double peggiore = sopra ? blocco.Max(x => x!.Valore!.Value) : blocco.Min(x => x!.Valore!.Value);

            risultato.Add(new Escursione
            {
                Canale = canale,
                Sonda = lim.Sonda,
                Ambiente = lim.Ambiente,
                Data = data,
                Tipo = sopra ? "Sopra il massimo" : "Sotto il minimo",
                OraInizio = letture[i].Ora,
                OraFine = letture[j].Ora,
                NumeroLetture = blocco.Count,
                ValorePeggiore = peggiore,
                LimiteSuperato = limite,
                ScostamentoMax = Math.Abs(peggiore - limite)
            });
            i = j + 1;
        }
        return risultato;
    }
}
