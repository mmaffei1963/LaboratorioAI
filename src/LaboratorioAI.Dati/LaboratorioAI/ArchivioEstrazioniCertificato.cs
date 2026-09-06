using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI;

/// <inheritdoc cref="IArchivioEstrazioniCertificato" />
public class ArchivioEstrazioniCertificato : IArchivioEstrazioniCertificato
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web);

    public ArchivioEstrazioniCertificato(ContestoLaboratorioAI ctx, IContestoUtente utente)
    {
        _ctx = ctx;
        _utente = utente;
    }

    public async Task<EstrazioneCertificatoEsterno?> LeggiPerHashAsync(string hash, CancellationToken ct = default)
    {
        var riga = await _ctx.EstrazioniCertificato.AsNoTracking()
            .Where(x => x.Hash == hash)
            .OrderByDescending(x => x.DataCreazione)
            .FirstOrDefaultAsync(ct);
        return riga is null
            ? null
            : JsonSerializer.Deserialize<EstrazioneCertificatoEsterno>(riga.EstrazioneJson, Opzioni);
    }

    public async Task SalvaAsync(string hash, string? nomeFileOriginale, EstrazioneCertificatoEsterno estrazione,
        string? modelloAi, CancellationToken ct = default)
    {
        _ctx.EstrazioniCertificato.Add(new EstrazioneCertificatoArchiviata
        {
            Hash = hash,
            NomeFileOriginale = nomeFileOriginale,
            EstrazioneJson = JsonSerializer.Serialize(estrazione, Opzioni),
            ModelloAi = modelloAi,
            IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);
    }
}
