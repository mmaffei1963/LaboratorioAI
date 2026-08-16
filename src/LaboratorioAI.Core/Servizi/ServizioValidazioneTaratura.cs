using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioValidazioneTaratura" />
public class ServizioValidazioneTaratura : IServizioValidazioneTaratura
{
    private readonly IServizioTaratura _calcolo;

    public ServizioValidazioneTaratura(IServizioTaratura calcolo) => _calcolo = calcolo;

    public EsitoValidazioneCalcoli Esegui(string versioneApplicazione)
    {
        double toll = SetRiferimentoTaratura.Tolleranza;
        var dettagli = new List<ConfrontoParametro>();
        var casi = new List<RiepilogoCasoValidazione>();

        foreach (var caso in SetRiferimentoTaratura.Casi())
        {
            var risultato = _calcolo.Calcola(caso.Dati);
            var perPunto = risultato.Punti.ToDictionary(p => p.Etichetta);
            int okCaso = 0, koCaso = 0;

            foreach (var atteso in caso.Attesi)
            {
                if (!perPunto.TryGetValue(atteso.Etichetta, out var calc))
                {
                    // Punto mancante: conta come non conforme su tutti i parametri.
                    dettagli.Add(Confronto(caso.Nome, atteso.Etichetta, "Punto presente", 0, 1, false));
                    koCaso++;
                    continue;
                }

                void Verifica(string nome, double c, double a)
                {
                    bool ok = Math.Abs(c - a) <= toll;
                    dettagli.Add(Confronto(caso.Nome, atteso.Etichetta, nome, c, a, ok));
                    if (ok) okCaso++; else koCaso++;
                }

                Verifica("Temperatura media Tr [°C]", calc.TemperaturaMediaRiferimento, atteso.TemperaturaMediaRiferimento);
                Verifica("Temperatura media Tw [°C]", calc.TemperaturaMediaStrumento, atteso.TemperaturaMediaStrumento);
                Verifica("Differenza media Tw−Tr [°C]", calc.DifferenzaMedia, atteso.DifferenzaMedia);
                Verifica("Incertezza estesa U [°C]", calc.IncertezzaEstesa, atteso.IncertezzaEstesa);
                Verifica("Fattore correzione [°C]", calc.FattoreCorrezione, atteso.FattoreCorrezione);
            }

            casi.Add(new RiepilogoCasoValidazione
            {
                Nome = caso.Nome, Descrizione = caso.Descrizione, Ok = okCaso, Ko = koCaso
            });
        }

        int ok = dettagli.Count(d => d.Ok);
        int ko = dettagli.Count - ok;

        return new EsitoValidazioneCalcoli
        {
            DataEsecuzione = DateTime.UtcNow,
            VersioneSet = SetRiferimentoTaratura.Versione,
            VersioneApplicazione = versioneApplicazione,
            Tolleranza = toll,
            Validato = ko == 0,
            NumeroOk = ok,
            NumeroKo = ko,
            Casi = casi,
            Dettagli = dettagli
        };
    }

    private static ConfrontoParametro Confronto(string caso, string punto, string par, double c, double a, bool ok) =>
        new() { Caso = caso, Punto = punto, Parametro = par, Calcolato = c, Atteso = a, Ok = ok };
}
