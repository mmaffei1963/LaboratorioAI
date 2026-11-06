namespace LaboratorioAI.Core.Provvigioni;

/// <summary>Una segnalazione con la sua condizione economica, pronta per il calcolo.</summary>
/// <param name="Aliquota">Frazione: 0,05 = 5%.</param>
/// <param name="MesiInterruzione">Dopo tanti mesi senza fatture nell'ambito il compenso si ferma (definitivamente).</param>
/// <param name="AnniDurataMassima">Se indicato, nessun compenso sulle fatture dopo tanti anni dall'inizio.</param>
public sealed record SegnalazioneCompenso(
    int IdSegnalazione, string Cliente, DateTime DataInizio, DateTime? DataFine,
    decimal Aliquota, int MesiInterruzione, int? AnniDurataMassima);

/// <summary>Fattura del cliente con il netto che rientra nell'ambito della segnalazione (commessa o area).</summary>
public sealed record FatturaSegnalazione(int IdSegnalazione, string ChiaveFattura, DateTime Data, decimal NettoAmbito, decimal Lordo);

/// <summary>Nota di credito del cliente con il netto (positivo) che rientra nell'ambito della segnalazione.</summary>
public sealed record NotaSegnalazione(int IdSegnalazione, string Riferimento, DateTime Data, decimal NettoAmbito);

public sealed record InputCompensiSegnalatori(
    IReadOnlyList<SegnalazioneCompenso> Segnalazioni,
    IReadOnlyList<FatturaSegnalazione> Fatture,
    IReadOnlyList<IncassoFattura> Incassi,
    IReadOnlyList<NotaSegnalazione> Note,
    DateTime? Oggi = null);

public sealed record MovimentoSegnalatore(
    string Trimestre, int IdSegnalazione, string Cliente, string Riferimento, DateTime Data,
    TipoMovimento Tipo, decimal Base, decimal Aliquota, decimal Importo);

/// <param name="FineCompenso">Per segnalazione: data in cui il compenso si è fermato (interruzione o durata massima), se è successo.</param>
public sealed record RisultatoCompensiSegnalatori(
    IReadOnlyList<MovimentoSegnalatore> Movimenti,
    IReadOnlyDictionary<int, DateTime> FineCompenso,
    IReadOnlyList<string> Avvisi);

/// <summary>
/// Compensi ai segnalatori. Funzione pura, nessun accesso a database.
///  - Contano le fatture del cliente nell'ambito della segnalazione, dalla data di inizio (ed entro la data di fine).
///  - Continuità: se fra una fattura e la precedente (o l'inizio, per la prima) passano più di <c>MesiInterruzione</c>
///    mesi, il compenso si ferma lì e le fatture successive non contano più. Stesso effetto per la durata massima.
///  - Il compenso matura sull'incassato: quota imponibile = incasso lordo × netto nell'ambito ÷ lordo della fattura.
///    Fatture non pagate (insoluti) non generano compenso; gli storni di incasso lo riducono.
///  - Note di credito: tolte nel trimestre della nota, se cadono nel periodo in cui il compenso è attivo.
/// Arrotondamento bancario al centesimo sul singolo movimento.
/// </summary>
public sealed class MotoreCompensiSegnalatori
{
    public RisultatoCompensiSegnalatori Calcola(InputCompensiSegnalatori input)
    {
        var movimenti = new List<MovimentoSegnalatore>();
        var fine = new Dictionary<int, DateTime>();
        var avvisi = new List<string>();
        var incassi = input.Incassi.ToLookup(i => i.ChiaveFattura, StringComparer.Ordinal);
        var fatture = input.Fatture.ToLookup(f => f.IdSegnalazione);
        var note = input.Note.ToLookup(n => n.IdSegnalazione);
        var oggi = (input.Oggi ?? DateTime.Today).Date;

        foreach (var s in input.Segnalazioni)
        {
            var inizio = s.DataInizio.Date;
            var limite = s.DataFine?.Date ?? DateTime.MaxValue.Date;
            if (s.AnniDurataMassima is int anni && anni > 0)
            {
                var fineDurata = inizio.AddYears(anni).AddDays(-1);
                if (fineDurata < limite) limite = fineDurata;
            }

            // Fatture che contano: in ordine di data, finché non c'è un'interruzione.
            var valide = new List<FatturaSegnalazione>();
            var ultima = inizio;
            DateTime? stop = null;
            var candidate = fatture[s.IdSegnalazione].Where(x => x.NettoAmbito != 0m && x.Data.Date >= inizio)
                .OrderBy(x => x.Data).ThenBy(x => x.ChiaveFattura, StringComparer.Ordinal).ToList();
            foreach (var f in candidate)
            {
                var d = f.Data.Date;
                if (d > limite) { stop ??= limite; break; }
                if (s.MesiInterruzione > 0 && d > ultima.AddMonths(s.MesiInterruzione))
                {
                    stop = ultima.AddMonths(s.MesiInterruzione);
                    break;
                }
                valide.Add(f);
                ultima = d;
            }
            if (stop is null && s.DataFine is null && s.MesiInterruzione > 0)
            {
                // Nessuna fattura dopo l'ultima: il compenso si ferma comunque passato il periodo di interruzione.
                var scadenza = ultima.AddMonths(s.MesiInterruzione);
                if (scadenza < oggi) stop = scadenza;
            }
            var fineAttivo = stop ?? limite;
            if (stop is DateTime st) fine[s.IdSegnalazione] = st;

            foreach (var f in valide)
            {
                if (f.Lordo <= 0m)
                {
                    avvisi.Add($"Fattura {f.ChiaveFattura} senza importo lordo: compenso non calcolato.");
                    continue;
                }
                foreach (var inc in incassi[f.ChiaveFattura])
                {
                    var quota = inc.ImportoLordo * f.NettoAmbito / f.Lordo;
                    var b = Arrotonda(quota);
                    var imp = Arrotonda(quota * s.Aliquota);
                    if (b == 0m && imp == 0m) continue;
                    movimenti.Add(new MovimentoSegnalatore(Trimestre(inc.DataIncasso), s.IdSegnalazione, s.Cliente, f.ChiaveFattura,
                        inc.DataIncasso, TipoMovimento.Incasso, b, s.Aliquota, imp));
                }
            }

            var noteValide = note[s.IdSegnalazione].Where(x => x.NettoAmbito != 0m && x.Data.Date >= inizio && x.Data.Date <= fineAttivo).ToList();
            foreach (var n in noteValide)
            {
                var b = -Arrotonda(Math.Abs(n.NettoAmbito));
                movimenti.Add(new MovimentoSegnalatore(Trimestre(n.Data), s.IdSegnalazione, s.Cliente, n.Riferimento,
                    n.Data, TipoMovimento.NotaCredito, b, s.Aliquota, -Arrotonda(Math.Abs(n.NettoAmbito) * s.Aliquota)));
            }
        }

        var ordinati = movimenti.OrderBy(m => m.Data).ThenBy(m => m.Cliente, StringComparer.Ordinal)
            .ThenBy(m => m.Riferimento, StringComparer.Ordinal).ToList();
        return new RisultatoCompensiSegnalatori(ordinati, fine, avvisi.Distinct().ToList());
    }

    private static decimal Arrotonda(decimal v) => Math.Round(v, 2, MidpointRounding.ToEven);

    private static string Trimestre(DateTime d) => $"{d.Year}-T{(d.Month - 1) / 3 + 1}";
}
