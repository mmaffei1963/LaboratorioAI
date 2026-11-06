using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>
/// Popolamento iniziale dell'anagrafica di raccordo fra i laboratori esterni attivi
/// (ArcLab, LabEsterno.Attivo = 1) e il loro accreditamento ACCREDIA corrente.
///
/// I numeri sono stati verificati a mano il 28/09/2026 cercando ogni laboratorio sul
/// portale ACCREDIA (i vecchi numeri scritti nel nome in ArcLab sono spesso superati
/// dalla rinumerazione 2025). Eseguito all'avvio, idempotente sul IdLabEsternoArcLab:
/// aggiunge solo i laboratori non ancora presenti, non tocca quelli già registrati
/// (un operatore potrebbe averne corretto numero/sede dall'interfaccia).
/// </summary>
public static class PopolamentoAccreditamentiEsterni
{
    public static async Task EseguiAsync(ContestoLaboratorioAI ctx, CancellationToken ct = default)
    {
        var esistenti = await ctx.Set<AccreditamentoLabEsterno>()
            .Select(a => a.IdLabEsternoArcLab)
            .ToListAsync(ct);

        foreach (var (idLabEsterno, nome, numero, sede, note) in Catalogo())
        {
            if (esistenti.Contains(idLabEsterno)) continue;

            ctx.Set<AccreditamentoLabEsterno>().Add(new AccreditamentoLabEsterno
            {
                IdLabEsternoArcLab = idLabEsterno,
                NomeLaboratorio = nome,
                NumeroAccreditamento = numero,
                Sede = sede,
                Note = note,
                DataCreazione = DateTime.UtcNow,
                DataUltimaModifica = DateTime.UtcNow
            });
        }

        await ctx.SaveChangesAsync(ct);
    }

    private static IEnumerable<(int IdLabEsterno, string Nome, string Numero, string Sede, string? Note)> Catalogo() =>
    [
        (128267681,   "Agribiosearch S.N.C.",              "01131", "A", null),
        (1548178828,  "ALS Italia S.r.l.",                 "00141", "A", null),
        (-914812082,  "ARPAM",                             "00245", "C", null),
        (1679306732,  "BioAgriFood S.r.l.",                "00602", "A", null),
        (-867474552,  "Centro Ricerche Marine",            "00419", "A", null),
        (1107789459,  "Chelab s.r.l.",                     "00051", "A", null),
        (1403269539,  "EPTA NORD Srl",                     "00253", "A", null),
        (1592551070,  "Genesi Srl",                        "00624", "A",
            "Indirizzo ACCREDIA (Calcinaia, PI) diverso da quello in ArcLab (Cascine di Buti, PI): stessa provincia, probabile trasferimento sede. Da verificare."),
        (1028307649,  "Gruppo C.S.A. Spa",                 "00164", "A", null),
        (-164473245,  "IZS delle Venezie",                 "00139", "A", null),
        (1596617663,  "IZS dell'Umbria e delle Marche",    "00196", "D", null),
        (1674573663,  "Protex Italia Srl",                 "01281", "A",
            "In ArcLab esistono due voci LabEsterno duplicate per questo laboratorio (Id 1679914391 e 1674573663, stesso indirizzo): monitorata questa seconda voce."),
        (1679914393,  "SICURAL SRL CONSORTILE",            "00112", "A",
            "Indirizzo ACCREDIA (Via Portofino) diverso da quello in ArcLab (Via Dismano), stessa città Cesena: probabile trasferimento sede. Da verificare."),
        (-1792263698, "WHITE LAB - L.A.V. srl",            "00398", "A", null),
        (1679914392,  "WHITE LAB S.r.l.",                  "00165", "A", null)
    ];
}
