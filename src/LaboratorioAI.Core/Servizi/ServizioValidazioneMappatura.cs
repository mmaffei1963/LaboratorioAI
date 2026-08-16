using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioValidazioneMappatura" />
public class ServizioValidazioneMappatura : IServizioValidazioneMappatura
{
    private readonly IServizioMappaturaIncubatori _calcolo;

    public ServizioValidazioneMappatura(IServizioMappaturaIncubatori calcolo) => _calcolo = calcolo;

    public EsitoValidazioneCalcoli Esegui(string versioneApplicazione)
    {
        double toll = SetRiferimentoMappatura.Tolleranza;
        var dettagli = new List<ConfrontoParametro>();
        var casi = new List<RiepilogoCasoValidazione>();

        foreach (var caso in SetRiferimentoMappatura.Casi())
        {
            var r = _calcolo.Calcola(caso.Dati);
            var a = caso.Atteso;
            int ok = 0, ko = 0;

            void Verifica(string nome, double c, double atteso)
            {
                bool esito = Math.Abs(c - atteso) <= toll;
                dettagli.Add(new ConfrontoParametro { Caso = caso.Nome, Punto = "—", Parametro = nome, Calcolato = c, Atteso = atteso, Ok = esito });
                if (esito) ok++; else ko++;
            }

            Verifica("Temperatura media Tr [°C]", r.TemperaturaMedia, a.TemperaturaMedia);
            Verifica("Scarto tipo della media [°C]", r.ScartoTipoMedia, a.ScartoTipoMedia);
            Verifica("Incertezza estesa U [°C]", r.IncertezzaEstesa, a.IncertezzaEstesa);
            Verifica("Differenza dal nominale [°C]", r.DifferenzaNominale, a.DifferenzaNominale);
            Verifica("Intervallo minimo [°C]", r.IntervalloMinimo, a.IntervalloMinimo);
            Verifica("Intervallo massimo [°C]", r.IntervalloMassimo, a.IntervalloMassimo);

            foreach (var (pos, atteso) in a.MediePosizioni)
            {
                var rip = r.Ripiani.FirstOrDefault(x => x.Posizione == pos);
                Verifica($"Media ripiano {pos} [°C]", rip?.Media ?? double.NaN, atteso);
            }

            casi.Add(new RiepilogoCasoValidazione { Nome = caso.Nome, Descrizione = caso.Descrizione, Ok = ok, Ko = ko });
        }

        int totOk = dettagli.Count(d => d.Ok);
        int totKo = dettagli.Count - totOk;

        return new EsitoValidazioneCalcoli
        {
            DataEsecuzione = DateTime.UtcNow,
            VersioneSet = SetRiferimentoMappatura.Versione,
            VersioneApplicazione = versioneApplicazione,
            Tolleranza = toll,
            Validato = totKo == 0,
            NumeroOk = totOk,
            NumeroKo = totKo,
            Casi = casi,
            Dettagli = dettagli
        };
    }
}
