using System.Text.Json;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>
/// Richiamo e simulazione di ritiro: dal lotto sospetto l'elenco dei lotti coinvolti, delle giacenze residue e dei clienti da avvisare
/// (Reg. CE 178/2002, art. 18). La simulazione non cambia nulla e resta nel registro con il tempo impiegato; il richiamo reale blocca anche i lotti.
/// </summary>
[Authorize]
[Route("Richiamo")]
public class RichiamoController : Controller
{
    private readonly ContestoTracciabilita _db;

    public RichiamoController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");

    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloIndiceRichiamo { Cerca = cerca };
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            m.Trovati = await (from l in _db.Lotti.AsNoTracking()
                               join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                               join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                               from f in fj.DefaultIfEmpty()
                               where l.Codice.Contains(t) || a.Descrizione.Contains(t) || (f != null && f.RagioneSociale.Contains(t))
                               orderby l.DataCreazione descending, l.IdLotto descending
                               select new RigaLottoRicerca { IdLotto = l.IdLotto, Codice = l.Codice, Articolo = a.Descrizione, Fornitore = f != null ? f.RagioneSociale : null, Tipo = l.Tipo })
                .Take(40).ToListAsync(ct);
        }
        m.Registro = await (from r in _db.Richiami.AsNoTracking()
                            join l in _db.Lotti.AsNoTracking() on r.IdLottoOrigine equals l.IdLotto
                            orderby r.DataAvvio descending
                            select new RigaRegistroRichiamo
                            {
                                IdRichiamo = r.IdRichiamo, DataAvvio = r.DataAvvio, Reale = r.Tipo == TipoRichiamo.Richiamo, Lotto = l.Codice,
                                Motivo = r.Motivo, Secondi = r.SecondiImpiegati, Esito = r.Esito
                            }).Take(50).ToListAsync(ct);
        return View(m);
    }

    /// <summary>Calcola il rapporto «in avanti» del lotto: lotti, giacenze residue e clienti con le loro consegne.</summary>
    private async Task<RapportoRichiamo?> CostruisciRapportoAsync(int idLotto, CancellationToken ct)
    {
        var origine = await (from l in _db.Lotti.AsNoTracking()
                             where l.IdLotto == idLotto
                             join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                             select new { l.Codice, a.Descrizione }).FirstOrDefaultAsync(ct);
        if (origine is null) return null;

        var consumi = await (from c in _db.ProduzioniConsumi.AsNoTracking()
                             join p in _db.Produzioni.AsNoTracking() on c.IdProduzione equals p.IdProduzione
                             where !p.Annullata
                             select new ArcoConsumo(p.IdProduzione, c.IdLottoIngresso, p.IdLottoProdotto, p.Giorno, c.Quantita)).ToListAsync(ct);
        var spedizioni = await (from sr in _db.SpedizioniRighe.AsNoTracking()
                                join sp0 in _db.Spedizioni.AsNoTracking() on sr.IdSpedizione equals sp0.IdSpedizione
                                where !sp0.Annullata
                                select new ArcoSpedizione(sp0.IdSpedizione, sr.IdLotto, sp0.IdCliente, sp0.DataSpedizione, sp0.NumeroDocumento, sr.Quantita)).ToListAsync(ct);
        var avanti = new MotoreRintracciabilita(consumi, spedizioni).Avanti(idLotto);

        var ids = avanti.Lotti.Select(x => x.IdLotto).ToList();
        var info = await (from l in _db.Lotti.AsNoTracking()
                          where ids.Contains(l.IdLotto)
                          join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                          select new { l.IdLotto, l.Codice, l.Tipo, l.Stato, Articolo = a.Descrizione, a.UnitaMisura }).ToDictionaryAsync(x => x.IdLotto, ct);
        var giacenze = (await _db.MovimentiLotto.AsNoTracking().Where(mv => ids.Contains(mv.IdLotto))
                .GroupBy(mv => mv.IdLotto).Select(g => new { Id = g.Key, S = g.Sum(x => x.Quantita) }).ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.S);
        var idClienti = avanti.Clienti.ToList();
        var clienti = await _db.Clienti.AsNoTracking().Where(c => idClienti.Contains(c.IdCliente)).ToDictionaryAsync(c => c.IdCliente, ct);

        var r = new RapportoRichiamo
        {
            IdLottoOrigine = idLotto, LottoOrigine = origine.Codice, ArticoloOrigine = origine.Descrizione,
            Produzioni = avanti.Produzioni.Count, Troncato = avanti.Troncato, DataOra = DateTime.UtcNow,
            Lotti = avanti.Lotti.Where(x => info.ContainsKey(x.IdLotto)).Select(x =>
            {
                var i = info[x.IdLotto];
                return new RigaLottoRapporto
                {
                    IdLotto = x.IdLotto, Livello = x.Livello, Codice = i.Codice, Articolo = i.Articolo, Tipo = i.Tipo.ToString(),
                    Giacenza = Math.Max(0m, giacenze.GetValueOrDefault(x.IdLotto)), UnitaMisura = i.UnitaMisura, Stato = i.Stato.ToString()
                };
            }).ToList()
        };
        foreach (var gruppo in avanti.Spedizioni.GroupBy(s => s.IdCliente))
        {
            var c = clienti.GetValueOrDefault(gruppo.Key);
            r.Clienti.Add(new ClienteRapporto
            {
                IdCliente = gruppo.Key, Nome = c?.RagioneSociale ?? "?", Email = c?.Email, Telefono = c?.Telefono,
                Indirizzo = c is null ? null : string.Join(", ", new[] { c.Indirizzo, string.Join(" ", new[] { c.Cap, c.Citta, c.Provincia }.Where(p => !string.IsNullOrWhiteSpace(p))) }.Where(p => !string.IsNullOrWhiteSpace(p))),
                Consegne = gruppo.OrderBy(s => s.Data).Select(s => new ConsegnaRapporto
                {
                    Data = s.Data.ToString("dd/MM/yyyy"), Documento = s.NumeroDocumento, Quantita = s.Quantita,
                    Lotto = info.TryGetValue(s.IdLotto, out var i) ? i.Codice : "?"
                }).ToList()
            });
        }
        r.Clienti = r.Clienti.OrderBy(c => c.Nome).ToList();
        return r;
    }

    [HttpGet("Lotto/{id:int}")]
    public async Task<IActionResult> Prepara(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var rapporto = await CostruisciRapportoAsync(id, ct);
        if (rapporto is null) return NotFound();
        return View(new ModelloPreparaRichiamo { Rapporto = rapporto, AvvioTicks = DateTime.UtcNow.Ticks });
    }

    [HttpPost("Lotto/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Esegui(int id, string tipo, string? motivo, long avvioTicks, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var reale = tipo == "richiamo";
        var testoMotivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (reale && testoMotivo is null)
        {
            TempData["Errore"] = "Per un richiamo scrivi il motivo.";
            return RedirectToAction(nameof(Prepara), new { id });
        }
        var rapporto = await CostruisciRapportoAsync(id, ct);
        if (rapporto is null) return NotFound();
        rapporto.Reale = reale;
        rapporto.Motivo = testoMotivo;
        rapporto.DataOra = DateTime.UtcNow;

        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).Select(i => (int?)i.IdInsediamento).FirstOrDefaultAsync(ct);
        var adesso = DateTime.UtcNow;
        var secondi = avvioTicks > 0 && avvioTicks <= adesso.Ticks ? (int)Math.Min(86400, (adesso - new DateTime(avvioTicks, DateTimeKind.Utc)).TotalSeconds) : (int?)null;
        var senzaEmail = rapporto.Clienti.Count(c => string.IsNullOrWhiteSpace(c.Email));
        var esito = $"{(reale ? "Richiamo" : "Simulazione")}: {rapporto.Lotti.Count} lotti, {rapporto.Clienti.Count} clienti da avvisare ({senzaEmail} senza e-mail), giacenza residua {rapporto.GiacenzaResidua:0.##}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var richiamo = new Richiamo
        {
            Tipo = reale ? TipoRichiamo.Richiamo : TipoRichiamo.Simulazione, IdLottoOrigine = id, Motivo = testoMotivo, DataAvvio = adesso,
            DataChiusura = reale ? null : adesso, SecondiImpiegati = secondi, Esito = esito, IdUtente = _db.IdUtenteCorrente,
            RapportoJson = JsonSerializer.Serialize(rapporto)
        };
        _db.Richiami.Add(richiamo);

        if (reale)
        {
            // Il lotto sospetto si blocca; i lotti che hanno raggiunto clienti risultano richiamati, gli altri bloccati.
            var idLotti = rapporto.Lotti.Select(l => l.IdLotto).ToList();
            var conClienti = (await (from r in _db.SpedizioniRighe.AsNoTracking()
                                     join sp in _db.Spedizioni.AsNoTracking() on r.IdSpedizione equals sp.IdSpedizione
                                     where !sp.Annullata && idLotti.Contains(r.IdLotto)
                                     select r.IdLotto).Distinct().ToListAsync(ct)).ToHashSet();
            var lotti = await _db.Lotti.Where(l => idLotti.Contains(l.IdLotto)).ToListAsync(ct);
            foreach (var l in lotti)
            {
                if (l.Stato == StatoLotto.Richiamato) continue;
                l.Stato = conClienti.Contains(l.IdLotto) ? StatoLotto.Richiamato : StatoLotto.Bloccato;
                l.MotivoBlocco = $"Richiamo del {adesso.ToLocalTime():dd/MM/yyyy}: {testoMotivo}";
            }
        }
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, IdInsediamento = sede, Azione = reale ? "RichiamoAvviato" : "SimulazioneRichiamo", Entita = "Lotto",
            IdEntita = id.ToString(), Dopo = esito, IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Messaggio"] = reale ? "Richiamo registrato: i lotti coinvolti sono stati bloccati." : "Simulazione registrata nel registro.";
        return RedirectToAction(nameof(Rapporto), new { id = richiamo.IdRichiamo });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Rapporto(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var r = await _db.Richiami.AsNoTracking().FirstOrDefaultAsync(x => x.IdRichiamo == id, ct);
        if (r?.RapportoJson is null) return NotFound();
        var rapporto = JsonSerializer.Deserialize<RapportoRichiamo>(r.RapportoJson);
        if (rapporto is null) return NotFound();
        ViewBag.Secondi = r.SecondiImpiegati;
        return View(rapporto);
    }
}
