using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Controlli deterministici anti-errore su una mappatura, indipendenti dall'AI.</summary>
public interface IServizioControlliMappatura
{
    RisultatoControlliTaratura Controlla(DatiMappatura dati, RisultatoMappatura risultato);
}

/// <inheritdoc cref="IServizioControlliMappatura" />
public class ServizioControlliMappatura : IServizioControlliMappatura
{
    private const int LettureMinimePerRipiano = 3;
    private const double ScartoNominale = 5.0;

    public RisultatoControlliTaratura Controlla(DatiMappatura d, RisultatoMappatura r)
    {
        var anomalie = new List<AnomaliaTaratura>();
        var ci = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string F(double v) => v.ToString("0.###", ci);

        if (d.Tolleranza <= 0)
            anomalie.Add(new AnomaliaTaratura { Livello = LivelloAnomalia.Errore, Messaggio = "Limite di tolleranza non impostato." });

        if (r.IncertezzaEstesa >= d.Tolleranza && d.Tolleranza > 0)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Errore,
                Messaggio = $"Incertezza estesa ({F(r.IncertezzaEstesa)} °C) ≥ tolleranza ({F(d.Tolleranza)} °C): intervallo ammesso non valido."
            });

        foreach (var l in d.Letture)
            if (Math.Abs(l.TemperaturaRilevata - d.ValoreNominale) > ScartoNominale)
            {
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Errore, Punto = l.Posizione,
                    Messaggio = $"Lettura {F(l.TemperaturaRilevata)} °C molto lontana dalla temperatura di esercizio " +
                                $"{F(d.ValoreNominale)} °C: possibile errore."
                });
                break;
            }

        foreach (var rip in r.Ripiani)
            if (rip.NumeroLetture < LettureMinimePerRipiano)
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Avviso, Punto = rip.Posizione,
                    Messaggio = $"Ripiano {rip.Posizione}: solo {rip.NumeroLetture} letture (previste 5)."
                });

        double certMin = d.Certificato.Min(c => c.TIndicata), certMax = d.Certificato.Max(c => c.TIndicata);
        if (d.ValoreNominale < certMin - 1e-9 || d.ValoreNominale > certMax + 1e-9)
            anomalie.Add(new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Avviso,
                Messaggio = $"Temperatura di esercizio fuori dall'intervallo del certificato CDR [{F(certMin)}…{F(certMax)}] °C."
            });

        return new RisultatoControlliTaratura { Anomalie = anomalie };
    }
}
