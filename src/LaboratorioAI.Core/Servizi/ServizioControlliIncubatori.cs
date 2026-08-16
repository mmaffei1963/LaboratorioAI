using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Controlli deterministici anti-errore su una taratura di incubatore/cella,
/// indipendenti dall'AI: letture fuori scala, temperatura di esercizio fuori dal
/// certificato del CDR, poche letture, incertezza implausibile.
/// </summary>
public interface IServizioControlliIncubatori
{
    RisultatoControlliTaratura Controlla(DatiTaraturaIncubatore dati, RisultatoTaraturaIncubatore risultato);
}

/// <inheritdoc cref="IServizioControlliIncubatori" />
public class ServizioControlliIncubatori : IServizioControlliIncubatori
{
    private const int LettureMinime = 3;
    private const double ScartoLetturaRiferimento = 5.0; // |tw − t_rif| oltre cui è sospetto
    private const double IncertezzaMax = 2.0;            // U oltre cui è sospetto

    public RisultatoControlliTaratura Controlla(DatiTaraturaIncubatore d, RisultatoTaraturaIncubatore r)
    {
        var anomalie = new List<AnomaliaTaratura>();
        var ci = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string F(double v) => v.ToString("0.###", ci);

        int n = d.Letture.Count;
        if (n < LettureMinime)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Avviso,
                Messaggio = $"Solo {n} letture: poche per una stima affidabile della ripetibilità."
            });

        foreach (var l in d.Letture)
            if (Math.Abs(l.LetturaStrumento - l.TemperaturaRilevata) > ScartoLetturaRiferimento)
            {
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Errore, Punto = l.Posizione,
                    Messaggio = $"Lettura {F(l.LetturaStrumento)} °C molto lontana dal riferimento " +
                                $"{F(l.TemperaturaRilevata)} °C: possibile errore di battitura o unità."
                });
                break;
            }

        double certMin = d.Certificato.Min(c => c.TIndicata);
        double certMax = d.Certificato.Max(c => c.TIndicata);
        if (d.ValoreNominale < certMin - 1e-9 || d.ValoreNominale > certMax + 1e-9)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Avviso,
                Messaggio = $"Temperatura di esercizio {F(d.ValoreNominale)} °C fuori dall'intervallo del " +
                            $"certificato CDR [{F(certMin)}…{F(certMax)}] °C: la correzione è estrapolata."
            });

        if (d.Tolleranza <= 0)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Errore,
                Messaggio = "Limite di tolleranza non impostato: impossibile valutare la conformità."
            });

        if (r.IncertezzaEstesa <= 0 || r.IncertezzaEstesa > IncertezzaMax)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Avviso,
                Messaggio = $"Incertezza estesa {F(r.IncertezzaEstesa)} °C fuori dall'intervallo atteso."
            });

        return new RisultatoControlliTaratura { Anomalie = anomalie };
    }
}
