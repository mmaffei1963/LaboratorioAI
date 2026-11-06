using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI;

/// <inheritdoc cref="IRepositorySigleLaboratori" />
public class RepositorySigleLaboratori : IRepositorySigleLaboratori
{
    private readonly ContestoLaboratorioAI _ctx;

    public RepositorySigleLaboratori(ContestoLaboratorioAI ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<SiglaLaboratorioEsterno>> ElencoAttiveAsync(CancellationToken ct = default) =>
        await _ctx.SigleLaboratoriEsterni.AsNoTracking()
            .Where(s => s.Attivo)
            .ToListAsync(ct);
}
