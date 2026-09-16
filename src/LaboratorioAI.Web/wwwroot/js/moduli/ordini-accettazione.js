// Ordini di Accettazione — genera il documento dai lotti ArcLab (sola lettura) + guardia anti-doppione.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const postJson = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });

function msg(testo, errore = false) {
    const el = $('msg');
    el.textContent = testo || '';
    el.style.color = errore ? '#b00' : '';
}

function paramsPeriodo() {
    const p = new URLSearchParams();
    if ($('f-dal').value) p.set('dal', $('f-dal').value);
    if ($('f-al').value) p.set('al', $('f-al').value);
    return p;
}

async function caricaLotti() {
    msg('Caricamento…');
    try {
        const lotti = await api('/api/ordini-accettazione/lotti?' + paramsPeriodo().toString());
        render(lotti);
        msg(`${lotti.length} lotti trovati.`);
    } catch (e) { msg(e.message, true); }
}

let ultimiLotti = [];

function render(lotti) {
    ultimiLotti = lotti;
    const mostraNonPronti = $('filtro-non-pronti')?.checked;
    const visibili = lotti.filter(l => mostraNonPronti || l.pronto);
    const tb = $('tabella-lotti').querySelector('tbody');
    tb.innerHTML = '';
    for (const l of visibili) {
        const tr = document.createElement('tr');
        let stato;
        if (!l.pronto) {
            const mancanti = l.numeroCampioni - l.campioniConParametri;
            stato = `<span class="etichetta" style="background:#f8d7da;color:#842029" title="Campioni senza parametri caricati">Senza parametri (${mancanti}/${l.numeroCampioni})</span>`;
        } else if (l.giaEmesso) {
            stato = '<span class="etichetta" style="background:#e7f5e7;color:#256029">Emesso</span>';
        } else {
            stato = '<span class="etichetta" style="background:#fff3cd;color:#7a5b00">Da emettere</span>';
        }
        const azioni = l.pronto
            ? `<a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/${l.idLotto}/documento?formato=pdf" target="_blank">PDF</a>
               <a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/${l.idLotto}/documento?formato=docx">Word</a>
               <button type="button" class="pulsante pulsante-tenue btn-emetti" data-id="${l.idLotto}" ${l.giaEmesso ? 'disabled' : ''}>Segna emesso</button>`
            : `<span class="sottotesto">non generabile</span>`;
        if (!l.pronto) tr.style.opacity = '0.7';
        tr.innerHTML = `
            <td>${esc(l.codLotto)}</td>
            <td>${esc(l.dataRicezione || '')}</td>
            <td>${esc(l.cliente || '')}</td>
            <td class="numerico">${l.campioniConParametri}/${l.numeroCampioni}</td>
            <td>${stato}</td>
            <td style="white-space:nowrap">${azioni}</td>`;
        tb.appendChild(tr);
    }
    tb.querySelectorAll('.btn-emetti').forEach(b => b.addEventListener('click', () => emetti(b.dataset.id)));
}

async function emetti(idLotto) {
    if (!confirm('Segnare come emesso questo Ordine di Accettazione?\nIl PDF verrà archiviato e messo in coda per l\'invio al portale.')) return;
    try {
        const r = await api(`/api/ordini-accettazione/${idLotto}/emetti?origine=manuale`, { method: 'POST' });
        msg(r.messaggio, r.doppione);
        await caricaLotti();
    } catch (e) { msg(e.message, true); }
}

async function generaSchedulati() {
    if (!confirm('Registrare come emessi tutti i lotti del periodo non ancora emessi? (guardia anti-doppione attiva)')) return;
    msg('Elaborazione…');
    try {
        const r = await api('/api/ordini-accettazione/genera-schedulati?' + paramsPeriodo().toString(), { method: 'POST' });
        msg(`Completato: ${r.nuovi} nuovi, ${r.giaEmessi} già emessi, ${r.nonPronti} senza parametri, ${r.errori} errori (su ${r.totale}).`);
        await caricaLotti();
    } catch (e) { msg(e.message, true); }
}

$('btn-carica').addEventListener('click', caricaLotti);
$('btn-schedulati').addEventListener('click', generaSchedulati);
$('filtro-non-pronti')?.addEventListener('change', () => render(ultimiLotti));

// Default: ultimi 7 giorni
(function init() {
    const oggi = new Date();
    const dal = new Date(oggi); dal.setDate(dal.getDate() - 7);
    $('f-al').value = oggi.toISOString().slice(0, 10);
    $('f-dal').value = dal.toISOString().slice(0, 10);
    caricaLotti();
})();
