using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioValidazioneIncubatori" />
public class ServizioValidazioneIncubatori : IServizioValidazioneIncubatori
{
    private readonly IServizioTaraturaIncubatori _calcolo;

    public ServizioValidazioneIncubatori(IServizioTaraturaIncubatori calcolo) => _calcolo = calcolo;

    public EsitoValidazioneCalcoli Esegui(string versioneApplicazione)
    {
        double toll = SetRiferimentoIncubatori.Tolleranza;
        var dettagli = new List<ConfrontoParametro>();
        var casi = new List<RiepilogoCasoValidazione>();

        foreach (var caso in SetRiferimentoIncubatori.Casi())
        {
            var r = _calcolo.Calcola(caso.Dati);
            var a = caso.Atteso;
            int ok = 0, ko = 0;

            void Verifica(string nome, double c, double atteso)
            {
                bool esito = Math.Abs(c - atteso) <= toll;
                dettagli.Add(new ConfrontoParametro
                {
                    Caso = caso.Nome, Punto = "—", Parametro = nome,
                    Calcolato = c, Atteso = atteso, Ok = esito
                });
                if (esito) ok++; else ko++;
            }

            Verifica("Temperatura media Tr [°C]", r.TemperaturaMediaRiferimento, a.TemperaturaMediaRiferimento);
            Verifica("Temperatura media Tw [°C]", r.TemperaturaMediaStrumento, a.TemperaturaMediaStrumento);
            Verifica("Incertezza estesa U [°C]", r.IncertezzaEstesa, a.IncertezzaEstesa);
            Verifica("Fattore correzione [°C]", r.FattoreCorrezione, a.FattoreCorrezione);
            Verifica("Allarme minimo [°C]", r.AllarmeMinimo, a.AllarmeMinimo);
            Verifica("Allarme massimo [°C]", r.AllarmeMassimo, a.AllarmeMassimo);

            // Giudizio di conformità: confronto esatto (1 = conforme, 0 = non conforme).
            bool giudizioOk = r.Conforme == a.Conforme;
            dettagli.Add(new ConfrontoParametro
            {
                Caso = caso.Nome, Punto = "—", Parametro = "Giudizio conformità",
                Calcolato = r.Conforme ? 1 : 0, Atteso = a.Conforme ? 1 : 0, Ok = giudizioOk
            });
            if (giudizioOk) ok++; else ko++;

            casi.Add(new RiepilogoCasoValidazione { Nome = caso.Nome, Descrizione = caso.Descrizione, Ok = ok, Ko = ko });
        }

        int totOk = dettagli.Count(d => d.Ok);
        int totKo = dettagli.Count - totOk;

        return new EsitoValidazioneCalcoli
        {
            DataEsecuzione = DateTime.UtcNow,
            VersioneSet = SetRiferimentoIncubatori.Versione,
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
