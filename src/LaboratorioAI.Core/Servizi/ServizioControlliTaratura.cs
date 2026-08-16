using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Controlli deterministici anti-errore su una taratura, indipendenti dall'AI:
/// intercettano gli errori grossolani più tipici (unità sbagliata, letture fuori
/// scala, correzione estrapolata, incertezza implausibile, poche letture).
/// </summary>
public interface IServizioControlliTaratura
{
    RisultatoControlliTaratura Controlla(DatiTaratura dati, RisultatoTaratura risultato);
}

/// <inheritdoc cref="IServizioControlliTaratura" />
public class ServizioControlliTaratura : IServizioControlliTaratura
{
    // Soglie di ragionevolezza (°C). Non sono limiti di conformità: servono solo
    // a segnalare probabili errori di inserimento.
    private const int LettureMinime = 3;
    private const double ScartoLetturaNominale = 5.0;   // |Tw − nominale| oltre cui è sospetto
    private const double DifferenzaMediaMax = 3.0;      // |Tw − Tr| medio oltre cui è sospetto
    private const double IncertezzaMax = 1.0;           // U oltre cui è sospetto

    public RisultatoControlliTaratura Controlla(DatiTaratura dati, RisultatoTaratura risultato)
    {
        var anomalie = new List<AnomaliaTaratura>();

        double certMin = dati.Certificato.Min(c => c.TIndicata);
        double certMax = dati.Certificato.Max(c => c.TIndicata);
        var perPunto = risultato.Punti.ToDictionary(p => p.Etichetta);

        foreach (var p in dati.Punti)
        {
            int n = p.Letture.Count;
            if (n == 0) continue;

            if (n < LettureMinime)
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Avviso, Punto = p.Etichetta,
                    Messaggio = $"Solo {n} letture: poche per una stima affidabile della ripetibilità."
                });

            foreach (var l in p.Letture)
            {
                if (Math.Abs(l.LetturaStrumento - l.TemperaturaNominale) > ScartoLetturaNominale)
                {
                    anomalie.Add(new AnomaliaTaratura
                    {
                        Livello = LivelloAnomalia.Errore, Punto = p.Etichetta,
                        Messaggio = $"Lettura {Fmt(l.LetturaStrumento)} °C molto lontana dal nominale " +
                                    $"{Fmt(l.TemperaturaNominale)} °C: possibile errore di battitura o unità."
                    });
                    break; // una segnalazione per punto basta
                }
            }

            double nomMin = p.Letture.Min(l => l.TemperaturaNominale);
            double nomMax = p.Letture.Max(l => l.TemperaturaNominale);
            if (nomMin < certMin - 1e-9 || nomMax > certMax + 1e-9)
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Avviso, Punto = p.Etichetta,
                    Messaggio = $"Punto fuori dall'intervallo del certificato CDR " +
                                $"[{Fmt(certMin)}…{Fmt(certMax)}] °C: la correzione è estrapolata."
                });

            if (perPunto.TryGetValue(p.Etichetta, out var r))
            {
                if (Math.Abs(r.DifferenzaMedia) > DifferenzaMediaMax)
                    anomalie.Add(new AnomaliaTaratura
                    {
                        Livello = LivelloAnomalia.Errore, Punto = p.Etichetta,
                        Messaggio = $"Differenza media {Fmt(r.DifferenzaMedia)} °C molto elevata: verificare i dati."
                    });

                if (r.IncertezzaEstesa <= 0 || r.IncertezzaEstesa > IncertezzaMax)
                    anomalie.Add(new AnomaliaTaratura
                    {
                        Livello = LivelloAnomalia.Avviso, Punto = p.Etichetta,
                        Messaggio = $"Incertezza estesa {Fmt(r.IncertezzaEstesa)} °C fuori dall'intervallo atteso."
                    });
            }
        }

        // Etichette di punto duplicate: le letture verrebbero mescolate.
        var duplicati = dati.Punti.GroupBy(p => p.Etichetta.Trim())
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var d in duplicati)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Errore, Punto = d,
                Messaggio = $"Etichetta di punto «{d}» ripetuta: le letture di punti diversi risultano unite."
            });

        return new RisultatoControlliTaratura { Anomalie = anomalie };
    }

    private static string Fmt(double v) =>
        v.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
}
