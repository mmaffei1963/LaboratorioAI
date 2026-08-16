using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Controlli deterministici anti-errore su una taratura di bilancia.</summary>
public interface IServizioControlliBilancia
{
    RisultatoControlliTaratura Controlla(DatiBilancia dati, RisultatoBilancia risultato);
}

/// <inheritdoc cref="IServizioControlliBilancia" />
public class ServizioControlliBilancia : IServizioControlliBilancia
{
    public RisultatoControlliTaratura Controlla(DatiBilancia d, RisultatoBilancia r)
    {
        var anomalie = new List<AnomaliaTaratura>();
        var ci = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string F(double v) => v.ToString("0.######", ci);

        if (d.UnitaFormato <= 0)
            anomalie.Add(new AnomaliaTaratura { Livello = LivelloAnomalia.Errore, Messaggio = "Unità di formato (risoluzione) non impostata." });

        if (d.Ripetibilita.Count < 5)
            anomalie.Add(new AnomaliaTaratura { Livello = LivelloAnomalia.Avviso, Messaggio = $"Ripetibilità con {d.Ripetibilita.Count} letture (previste 5)." });

        if (d.Eccentricita.Letture.Count < 4)
            anomalie.Add(new AnomaliaTaratura { Livello = LivelloAnomalia.Avviso, Messaggio = "Prova di eccentricità con meno di 4 posizioni." });

        if (!r.EccentricitaConforme)
            anomalie.Add(new AnomaliaTaratura { Livello = LivelloAnomalia.Avviso, Messaggio = $"Eccentricità non superata: scostamento {F(r.EccentricitaScostamentoMax)} g ≥ 15 divisioni." });

        // Carichi di linearità entro la portata e ordinati.
        foreach (var p in d.Linearita)
        {
            if (p.CaricoNominale > d.Portata + 1e-9)
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Errore,
                    Messaggio = $"Carico {F(p.CaricoNominale)} g oltre la portata {F(d.Portata)} g."
                });
            if (p.CaricoNominale > 0 && Math.Abs(p.Lettura - p.ValoreConvenzionale) > 10 * d.UnitaFormato + 0.01 * p.CaricoNominale)
            {
                anomalie.Add(new AnomaliaTaratura
                {
                    Livello = LivelloAnomalia.Errore, Punto = F(p.CaricoNominale) + " g",
                    Messaggio = $"Errore d'indicazione {F(p.Lettura - p.ValoreConvenzionale)} g anomalo: verificare lettura/valore massa."
                });
            }
        }

        return new RisultatoControlliTaratura { Anomalie = anomalie };
    }
}
