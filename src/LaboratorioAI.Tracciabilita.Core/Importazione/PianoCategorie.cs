using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

public sealed record ProfiloPronto(
    string Nome,
    IReadOnlyList<(TipoControllo Tipo, string Istruzione)> Righe,
    IReadOnlyList<string> Categorie);

/// <summary>
/// Categorie (le «matrici» del vecchio programma), a quale categoria appartiene ogni articolo
/// e profili di controllo (matrici con gli stessi controlli → un solo profilo).
/// </summary>
public sealed class PianoCategorie
{
    public static readonly PianoCategorie Vuoto = new();

    public IReadOnlyList<string> Categorie { get; init; } = Array.Empty<string>();
    /// <summary>Numero della matrice nel vecchio programma → nome della categoria.</summary>
    public IReadOnlyDictionary<int, string> CategoriaPerMatrice { get; init; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<string, string> CategoriaPerArticolo { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ProfiloPronto> Profili { get; init; } = Array.Empty<ProfiloPronto>();
    public IReadOnlyDictionary<string, string> ProfiloPerCategoria { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Segnalazioni { get; init; } = Array.Empty<string>();
}

public static class PianificaCategorie
{
    public static PianoCategorie Costruisci(DatiAccess dati, IReadOnlyList<RigaPronta> righe)
    {
        var segnalazioni = new List<string>();
        var nomePerId = new Dictionary<int, string>();
        foreach (var m in dati.MatriciConId ?? Array.Empty<MatriceAccess>())
            if (!string.IsNullOrWhiteSpace(m.Nome)) nomePerId[m.IdMatrice] = m.Nome.Trim();

        // Nel vecchio programma una matrice rinominata «xFarina 00 in sacchi(era 32)» è la vecchia matrice numero 32:
        // le righe che puntano ancora al 32 appartengono a «Farina 00 in sacchi».
        var nomiScartati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in (dati.MatriciConId ?? Array.Empty<MatriceAccess>()).ToList())
        {
            var mt = System.Text.RegularExpressions.Regex.Match(m.Nome ?? "", @"^x?\s*(?<nome>.+?)\s*\(era\s+(?<n>\d+)\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!mt.Success) continue;
            var pulito = mt.Groups["nome"].Value.Trim();
            var vecchioId = int.Parse(mt.Groups["n"].Value);
            nomiScartati.Add(m.Nome!.Trim());
            nomePerId[m.IdMatrice] = pulito;
            if (!nomePerId.ContainsKey(vecchioId))
            {
                nomePerId[vecchioId] = pulito;
                segnalazioni.Add($"La matrice «{m.Nome!.Trim()}» è la vecchia #{vecchioId}: le righe che puntano a #{vecchioId} sono state assegnate a «{pulito}».");
            }
        }

        var usoPerId = righe.Where(r => r.IdMatrice is > 0).GroupBy(r => r.IdMatrice!.Value).ToDictionary(g => g.Key, g => g.Count());
        foreach (var id in usoPerId.Keys.Where(i => !nomePerId.ContainsKey(i)).OrderBy(i => i))
        {
            nomePerId[id] = $"Matrice senza nome (#{id})";
            segnalazioni.Add($"La matrice #{id} è usata da {usoPerId[id]} righe ma non ha un nome nella tabella Matrici: creata come «Matrice senza nome (#{id})».");
        }

        var categorie = nomePerId.Values.Concat(dati.Matrici.Select(m => m.Trim()).Where(m => !nomiScartati.Contains(m)))
            .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        var doppie = nomePerId.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        foreach (var g in doppie)
            segnalazioni.Add($"Il nome «{g.Key}» è usato da più matrici ({string.Join(", ", g.Select(x => "#" + x.Key))}): unite in un'unica categoria.");

        // Articolo → categoria più frequente tra le sue righe.
        var perArticolo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in righe.Where(r => r.IdMatrice is > 0 and { } i && nomePerId.ContainsKey(i)).GroupBy(r => r.NomeArticolo, StringComparer.OrdinalIgnoreCase))
        {
            var conteggi = g.GroupBy(r => nomePerId[r.IdMatrice!.Value], StringComparer.OrdinalIgnoreCase)
                .Select(x => (Nome: x.Key, N: x.Count())).OrderByDescending(x => x.N).ThenBy(x => x.Nome, StringComparer.OrdinalIgnoreCase).ToList();
            perArticolo[g.Key] = conteggi[0].Nome;
            if (conteggi.Count > 1)
                segnalazioni.Add($"L'articolo «{g.Key}» compare in più matrici: {string.Join(", ", conteggi.Select(x => $"{x.Nome} ({x.N})"))} → scelta «{conteggi[0].Nome}».");
        }

        // Profili.
        var gruppi = ProfiliControlloAccess.Raggruppa(dati.RigheControlli ?? Array.Empty<RigaControlliMatrice>(), dati.TestiControlli ?? Array.Empty<TestoControllo>());
        var gruppoPerId = new Dictionary<int, int>();
        for (var i = 0; i < gruppi.Count; i++)
            foreach (var id in gruppi[i].IdMatrici) gruppoPerId[id] = i;

        var gruppoPerCategoria = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in nomePerId.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            // Se la categoria ha più matrici con profili diversi vale quella più usata.
            var scelta = g.Select(kv => kv.Key).Where(gruppoPerId.ContainsKey)
                .OrderByDescending(id => usoPerId.GetValueOrDefault(id)).ThenBy(id => id).Cast<int?>().FirstOrDefault();
            if (scelta is { } id) gruppoPerCategoria[g.Key] = gruppoPerId[id];
        }
        foreach (var c in categorie.Where(c => !gruppoPerCategoria.ContainsKey(c)))
            segnalazioni.Add($"La categoria «{c}» non ha controlli nella «Tabella controlli»: resta senza profilo.");

        var profili = new List<ProfiloPronto>();
        var profiloPerCategoria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nomiUsati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < gruppi.Count; i++)
        {
            var assegnate = gruppoPerCategoria.Where(kv => kv.Value == i).Select(kv => kv.Key)
                .OrderByDescending(c => nomePerId.Where(n => n.Value.Equals(c, StringComparison.OrdinalIgnoreCase)).Sum(n => usoPerId.GetValueOrDefault(n.Key)))
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            if (assegnate.Count == 0) continue;

            var nome = assegnate.Count == 1 ? assegnate[0] : $"{assegnate[0]} e altre {assegnate.Count - 1}";
            var finale = nome; var k = 2;
            while (!nomiUsati.Add(finale)) finale = $"{nome} ({k++})";

            var righeProfilo = gruppi[i].Testi.OrderBy(t => t.Key)
                .Where(t => !LetturaOrdine.ControlloNonRichiesto(t.Value))
                .Select(t => (t.Key, t.Value)).ToList();
            profili.Add(new ProfiloPronto(finale, righeProfilo, assegnate));
            foreach (var c in assegnate) profiloPerCategoria[c] = finale;
        }

        return new PianoCategorie
        {
            Categorie = categorie,
            CategoriaPerMatrice = nomePerId,
            CategoriaPerArticolo = perArticolo,
            Profili = profili,
            ProfiloPerCategoria = profiloPerCategoria,
            Segnalazioni = segnalazioni
        };
    }
}
