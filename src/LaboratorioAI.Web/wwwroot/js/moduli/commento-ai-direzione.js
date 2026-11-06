// Commento AI del cruscotto Direzione (13/10/2026): il programma calcola i numeri, l'AI li commenta.
// Ogni commento resta in archivio con i filtri e con i dati inviati all'AI.
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const dataOra = s => s ? new Date(s).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';

/** Markdown minimo e sicuro (il testo viene prima neutralizzato): titoli ##, elenchi -, grassetto **. */
function markdown(testo) {
    const inline = t => esc(t).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    let html = '', inLista = false;
    const chiudi = () => { if (inLista) { html += '</ul>'; inLista = false; } };
    for (const grezza of (testo || '').split('\n')) {
        const r = grezza.trim();
        if (!r) { chiudi(); continue; }
        const titolo = r.match(/^#{1,4}\s+(.*)$/);
        if (titolo) { chiudi(); html += `<h3 style="margin:1rem 0 0.3rem">${inline(titolo[1])}</h3>`; continue; }
        const punto = r.match(/^[-*•]\s+(.*)$/) || r.match(/^\d+[.)]\s+(.*)$/);
        if (punto) { if (!inLista) { html += '<ul style="margin:0.2rem 0 0.4rem 1.2rem">'; inLista = true; } html += `<li>${inline(punto[1])}</li>`; continue; }
        chiudi(); html += `<p style="margin:0.3rem 0">${inline(r)}</p>`;
    }
    chiudi();
    return html;
}

/**
 * Monta il riquadro nel contenitore.
 * @param {{pagina: 'territorio'|'indicatori', contenitore: HTMLElement, filtri: () => object}} opzioni
 */
export function montaCommentoAi({ pagina, contenitore, filtri }) {
    contenitore.innerHTML = `
        <div class="barra-risultati">
            <h2 style="margin:0">Commento AI</h2>
            <button type="button" class="pulsante" data-ca="genera">Genera commento sui dati mostrati</button>
        </div>
        <p class="nota" style="margin-top:0.4rem">L'AI commenta i numeri calcolati dal programma con i filtri attuali (compresi i nomi dei primi 30 clienti per elenco):
            non calcola nulla e ogni affermazione si verifica nelle tabelle. Ci vogliono 20–40 secondi; costo di qualche centesimo. I commenti restano in archivio.</p>
        <div data-ca="stato" class="sottotesto"></div>
        <article data-ca="testo" style="display:none;border-left:3px solid #6b4fbb;padding:0.2rem 0 0.2rem 1rem;margin:0.6rem 0"></article>
        <div data-ca="piede" class="sottotesto"></div>
        <details data-ca="dati" style="display:none;margin-top:0.4rem"><summary class="sottotesto" style="cursor:pointer">Dati inviati all'AI</summary>
            <pre style="white-space:pre-wrap;font-size:0.8rem;max-height:30rem;overflow:auto;background:#f6f8fb;padding:0.6rem"></pre></details>
        <details data-ca="archivio" style="margin-top:0.8rem"><summary style="cursor:pointer">Commenti precedenti</summary>
            <div data-ca="elenco" class="sottotesto">Caricamento…</div></details>`;
    const q = n => contenitore.querySelector(`[data-ca="${n}"]`);

    function mostra(c) {
        q('testo').style.display = '';
        q('testo').innerHTML = markdown(c.testo);
        q('piede').innerHTML = `Commento del ${esc(dataOra(c.data))}${c.utente ? ', ' + esc(c.utente) : ''} — filtri: ${esc(c.descrizione)}`
            + ` — costo stimato ${Number(c.costo || 0).toLocaleString('it-IT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 3 })}`
            + ` — <a href="/api/direzione/commenti-ai/${c.id}/testo">scarica come testo</a>`;
        q('dati').style.display = '';
        q('dati').querySelector('pre').textContent = c.datiInviati || '';
    }

    async function archivio() {
        try {
            const r = await fetch('/api/direzione/commenti-ai?pagina=' + encodeURIComponent(pagina));
            const elenco = await r.json();
            q('elenco').innerHTML = elenco.length
                ? '<ul style="margin:0.4rem 0 0 1rem">' + elenco.map(c => `<li style="margin-bottom:0.4rem"><a href="#" data-id="${c.id}">${esc(dataOra(c.data))}</a>`
                    + ` — ${esc(c.utente || '')} — <em>${esc(c.descrizione)}</em><br>${esc(c.anteprima)}</li>`).join('') + '</ul>'
                : 'Nessun commento ancora.';
        } catch { q('elenco').textContent = 'Archivio non disponibile.'; }
    }

    q('elenco').addEventListener('click', async e => {
        const a = e.target.closest('a[data-id]');
        if (!a) return;
        e.preventDefault();
        const r = await fetch('/api/direzione/commenti-ai/' + a.dataset.id);
        if (r.ok) { mostra(await r.json()); contenitore.scrollIntoView({ behavior: 'smooth' }); }
    });

    q('genera').addEventListener('click', async e => {
        const b = e.currentTarget; b.disabled = true;
        q('stato').textContent = 'L\'AI sta leggendo i dati…';
        try {
            const r = await fetch(`/api/direzione/commento-ai/${pagina}`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(filtri())
            });
            const j = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(j.messaggio || 'Errore ' + r.status);
            q('stato').textContent = '';
            mostra(j);
            archivio();
        } catch (err) { q('stato').innerHTML = `<span style="color:#b00">${esc(err.message)}</span>`; }
        finally { b.disabled = false; }
    });

    archivio();
}
