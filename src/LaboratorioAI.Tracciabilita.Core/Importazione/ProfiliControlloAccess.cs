using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>Una riga della «Tabella controlli» del vecchio programma: per ogni matrice, quale riga di ogni tabella dei controlli si applica.</summary>
public sealed record RigaControlliMatrice(
    int IdMatrice,
    int? Etichettatura, int? Confezione, int? Temperatura, int? Organolettico,
    int? Prelievo, int? Certificati, int? Pallet);

/// <summary>Testo di una riga delle tabelle ControlloEtichettatura, ControlloConfezione, ecc.</summary>
public sealed record TestoControllo(TipoControllo Tipo, int Id, string Descrizione);

/// <summary>Matrici che hanno esattamente gli stessi controlli: diventano un solo profilo.</summary>
public sealed record GruppoProfilo(
    IReadOnlyList<int> IdMatrici,
    IReadOnlyDictionary<TipoControllo, string> Testi);

public static class ProfiliControlloAccess
{
    /// <summary>
    /// Raggruppa le matrici con gli stessi testi di controllo (non gli stessi numeri: due righe diverse
    /// con lo stesso testo sono lo stesso controllo). Un controllo mancante o senza testo non entra nel profilo.
    /// </summary>
    public static IReadOnlyList<GruppoProfilo> Raggruppa(
        IEnumerable<RigaControlliMatrice> righe,
        IEnumerable<TestoControllo> testi)
    {
        var perTipoId = testi
            .GroupBy(t => (t.Tipo, t.Id))
            .ToDictionary(g => g.Key, g => g.First().Descrizione.Trim());

        string? Testo(TipoControllo tipo, int? id) =>
            id is { } i && perTipoId.TryGetValue((tipo, i), out var d) && d.Length > 0 ? d : null;

        var gruppi = new Dictionary<string, (List<int> Matrici, Dictionary<TipoControllo, string> Testi)>();
        foreach (var r in righe.OrderBy(x => x.IdMatrice))
        {
            var t = new Dictionary<TipoControllo, string>();
            void Aggiungi(TipoControllo tipo, int? id)
            {
                if (Testo(tipo, id) is { } s) t[tipo] = s;
            }
            Aggiungi(TipoControllo.Etichettatura, r.Etichettatura);
            Aggiungi(TipoControllo.Confezione, r.Confezione);
            Aggiungi(TipoControllo.Temperatura, r.Temperatura);
            Aggiungi(TipoControllo.Organolettico, r.Organolettico);
            Aggiungi(TipoControllo.PrelievoAnalisi, r.Prelievo);
            Aggiungi(TipoControllo.Certificati, r.Certificati);
            Aggiungi(TipoControllo.Pallet, r.Pallet);

            var chiave = string.Join("|", t.OrderBy(x => x.Key).Select(x => $"{(int)x.Key}={x.Value.ToLowerInvariant()}"));
            if (!gruppi.TryGetValue(chiave, out var g))
                gruppi[chiave] = g = (new List<int>(), t);
            g.Matrici.Add(r.IdMatrice);
        }

        return gruppi.Values
            .OrderByDescending(g => g.Matrici.Count)
            .Select(g => new GruppoProfilo(g.Matrici, g.Testi))
            .ToList();
    }
}
