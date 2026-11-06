using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Andamento;

/// <summary>Cliente tolto dalla lista (riga valida più recente).</summary>
public sealed record UscitaCliente(string Cliente, MotivoUscitaCliente Motivo, string? Successore, DateTime Data);

/// <summary>
/// Clienti tolti dalla lista dei clienti da seguire (14/10/2026), funzioni pure.
/// <list type="bullet">
/// <item>Un cliente tolto che fattura o manda campioni in un mese successivo a quello dell'uscita è «rientrato»: torna in lista.</item>
/// <item>Un cliente acquisito da un altro nostro cliente (e non rientrato) somma il suo storico a quello del cliente che l'ha
/// assorbito, anche a catena (A acquisito da B, B acquisito da C → tutto su C): così il confronto tra periodi mostra se il
/// gruppo unito cresce o cala davvero.</item>
/// </list>
/// </summary>
public static class UsciteClienti
{
    private static int Mese(int anno, int mese) => anno * 12 + mese - 1;

    /// <summary>Clienti tolti che hanno avuto attività (fatturato o campioni) dopo il mese dell'uscita.</summary>
    public static HashSet<string> Rientrati(IEnumerable<UscitaCliente> uscite, IEnumerable<FatturatoMensile> fatturato, IEnumerable<CampioniMensili> campioni)
    {
        var meseUscita = uscite.ToDictionary(u => u.Cliente, u => Mese(u.Data.Year, u.Data.Month), StringComparer.Ordinal);
        var rientrati = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in fatturato)
            if (meseUscita.TryGetValue(f.Cliente, out var m) && Mese(f.Anno, f.Mese) > m && f.Laboratorio + f.Altro > 0m) rientrati.Add(f.Cliente);
        foreach (var c in campioni)
            if (meseUscita.TryGetValue(c.Cliente, out var m) && Mese(c.Anno, c.Mese) > m && c.Campioni > 0) rientrati.Add(c.Cliente);
        return rientrati;
    }

    /// <summary>Cliente in cui confluisce lo storico di ogni cliente acquisito (non rientrato), risolvendo le catene.</summary>
    public static Dictionary<string, string> Destinazioni(IEnumerable<UscitaCliente> uscite, IReadOnlySet<string> rientrati)
    {
        var diretti = uscite.Where(u => u.Motivo == MotivoUscitaCliente.AcquisitoDaCliente && !string.IsNullOrWhiteSpace(u.Successore)
                                       && !rientrati.Contains(u.Cliente) && u.Successore != u.Cliente)
            .ToDictionary(u => u.Cliente, u => u.Successore!, StringComparer.Ordinal);
        var finali = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (da, a) in diretti)
        {
            var dest = a;
            var visti = new HashSet<string>(StringComparer.Ordinal) { da };
            while (diretti.TryGetValue(dest, out var dopo) && visti.Add(dest)) dest = dopo;
            if (dest != da) finali[da] = dest;   // un giro chiuso (A→B→A) non si unisce
        }
        return finali;
    }

    /// <summary>Sposta lo storico dei clienti acquisiti sul cliente che li ha assorbiti (tranne <paramref name="escludi"/>).</summary>
    public static List<FatturatoMensile> Unisci(IEnumerable<FatturatoMensile> fatturato, IReadOnlyDictionary<string, string> destinazioni, string? escludi = null) =>
        fatturato.Select(f => f.Cliente != escludi && destinazioni.TryGetValue(f.Cliente, out var d) ? f with { Cliente = d } : f)
            .GroupBy(f => (f.Cliente, f.Anno, f.Mese))
            .Select(g => new FatturatoMensile(g.Key.Cliente, g.Key.Anno, g.Key.Mese, g.Sum(x => x.Laboratorio), g.Sum(x => x.Legionella), g.Sum(x => x.Altro)))
            .ToList();

    public static List<CampioniMensili> Unisci(IEnumerable<CampioniMensili> campioni, IReadOnlyDictionary<string, string> destinazioni, string? escludi = null) =>
        campioni.Select(c => c.Cliente != escludi && destinazioni.TryGetValue(c.Cliente, out var d) ? c with { Cliente = d } : c).ToList();
}
