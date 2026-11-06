// Rinnovo delle tarature dei termometri clienti (03/11/2026). Dopo il salvataggio di una taratura nuova per un
// cliente il programma propone le sue tarature ancora «in uso» della stessa tipologia: si spuntano quelle che la
// nuova sostituisce e non compaiono più tra le scadenze. Il programma non sceglie da solo: nell'archivio non c'è
// un'identificazione del singolo termometro (due sonde dello stesso cliente hanno gli stessi dati).

const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const dataIt = d => d ? new Date(d).toLocaleDateString('it-IT') : '—';

/**
 * Mostra nel contenitore le tarature da spuntare.
 * @param {HTMLElement} contenitore
 * @param {Array} candidati  [{ idTermometroEsterno, numeroRapporto, tipologia, cliente, riferimento, dataTaratura, dataScadenza }]
 * @param {string} urlRinnova  POST { ids: [...] }
 * @param {Function} [alTermine]  chiamata dopo il salvataggio (con il numero di rinnovate) o la chiusura (0)
 */
export function mostraRinnovo(contenitore, candidati, urlRinnova, alTermine) {
    if (!contenitore) return;
    if (!candidati || candidati.length === 0) { contenitore.innerHTML = ''; contenitore.classList.add('nascosto'); return; }
    const unica = candidati.length === 1;
    contenitore.innerHTML = `
        <h3>Questa taratura rinnova una di queste?</h3>
        <p class="sottotesto">Tarature ancora in uso dello stesso cliente e della stessa tipologia. Spunta quelle sostituite
        da questa: non compariranno più tra le scadenze (restano in archivio come «rinnovate»).</p>
        <div class="tabella-scorrevole"><table class="tabella"><thead><tr>
            <th></th><th class="numerico">N° rapporto</th><th>Tipologia</th><th>Cliente</th><th>Rif.</th><th>Taratura</th><th>Scadenza</th>
        </tr></thead><tbody>
        ${candidati.map(c => `<tr>
            <td><input type="checkbox" value="${c.idTermometroEsterno}" ${unica ? 'checked' : ''} /></td>
            <td class="numerico">${c.numeroRapporto ?? '—'}</td><td>${esc(c.tipologia)}</td><td>${esc(c.cliente)}</td>
            <td>${esc(c.riferimento)}</td><td>${dataIt(c.dataTaratura)}</td><td>${dataIt(c.dataScadenza)}</td></tr>`).join('')}
        </tbody></table></div>
        <div class="azioni-filtri">
            <div style="flex:1"></div>
            <button type="button" class="pulsante pulsante-tenue" data-azione="nessuna">Nessuna</button>
            <button type="button" class="pulsante" data-azione="rinnova">Segna come rinnovate</button>
        </div>`;
    contenitore.classList.remove('nascosto');
    const chiudi = (n) => { contenitore.innerHTML = ''; contenitore.classList.add('nascosto'); if (alTermine) alTermine(n); };
    contenitore.querySelector('[data-azione="nessuna"]').addEventListener('click', () => chiudi(0));
    contenitore.querySelector('[data-azione="rinnova"]').addEventListener('click', async (ev) => {
        const ids = [...contenitore.querySelectorAll('input[type=checkbox]:checked')].map(x => Number(x.value));
        if (ids.length === 0) { chiudi(0); return; }
        ev.target.disabled = true;
        try {
            const r = await fetch(urlRinnova, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ids }) });
            if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
            const e = await r.json();
            chiudi(e.rinnovate ?? ids.length);
        } catch (err) { alert(err.message); ev.target.disabled = false; }
    });
}
