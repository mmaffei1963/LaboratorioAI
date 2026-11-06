using System.Globalization;

namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>Numero d'ordine progressivo e continuo per azienda, senza anno.</summary>
public static class NumerazioneOrdini
{
    /// <summary>
    /// Il numero successivo a quelli già usati. Se non ci sono ordini si parte da <paramref name="primo"/>
    /// (al primo ordine l'azienda può continuare la numerazione del vecchio programma). I numeri non numerici si ignorano.
    /// </summary>
    public static int Prossimo(IEnumerable<string> numeriEsistenti, int primo = 1)
    {
        var massimo = 0;
        var qualcuno = false;
        foreach (var n in numeriEsistenti)
        {
            qualcuno = true;
            if (int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v > massimo) massimo = v;
        }
        if (!qualcuno || massimo == 0) return Math.Max(1, primo);
        return massimo + 1;
    }
}
