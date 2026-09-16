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

function render(lotti) {
    const tb = $('tabella-lotti').querySelector('tbody');
    tb.innerHTML = '';
    for (const l of lotti) {
        const tr = document.createElement('tr');
        const stato = l.giaEmesso
            ? '<span class="etichetta" style="background:#e7f5e7;color:#256029">Emesso</span>'
            : '<span class="etichetta" style="background:#fff3cd;color:#7a5b00">Da emettere</span>';
        tr.innerHTML = `
            <td>${esc(l.codLotto)}</td>
            <td>${esc(l.dataRicezione || '')}</td>
            <td>${esc(l.cliente || '')}</td>
            <td class="numerico">${l.numeroCampioni}</td>
            <td>${stato}</td>
            <td style="white-space:nowrap">
                <a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/${l.idLotto}/documento?formato=pdf" target="_blank">PDF</a>
                <a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/${l.idLotto}/documento?formato=docx">Word</a>
                <button type="button" class="pulsante pulsante-tenue btn-emetti" data-id="${l.idLotto}" ${l.giaEmesso ? 'disabled' : ''}>Segna emesso</button>
            </td>`;
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
        msg(`Completato: ${r.nuovi} nuovi, ${r.giaEmessi} già emessi, ${r.errori} errori (su ${r.totale}).`);
        await caricaLotti();
    } catch (e) { msg(e.message, true); }
}

async function caricaImpostazioni() {
    try {
        const s = await api('/api/ordini-accettazione/impostazioni');
        $('s-revisione').value = s.revisioneCondizioni || '';
        $('s-modulo').value = s.moduloRevisione || '';
    } catch { /* silenzioso */ }
}

async function salvaImpostazioni() {
    const rev = $('s-revisione').value.trim();
    const mod = $('s-modulo').value.trim();
    if (!confirm('Confermi la modifica delle impostazioni?\n\nLa revisione delle Condizioni Generali è condivisa con le offerte: verrà applicata a tutti i nuovi documenti.')) return;
    try {
        await postJson('/api/ordini-accettazione/impostazioni', { revisioneCondizioni: rev, moduloRevisione: mod });
        msg('Impostazioni salvate.');
    } catch (e) { msg(e.message, true); }
}

$('btn-carica').addEventListener('click', caricaLotti);
$('btn-schedulati').addEventListener('click', generaSchedulati);
$('btn-salva-impostazioni').addEventListener('click', salvaImpostazioni);

// Default: ultimi 7 giorni
(function init() {
    const oggi = new Date();
    const dal = new Date(oggi); dal.setDate(dal.getDate() - 7);
    $('f-al').value = oggi.toISOString().slice(0, 10);
    $('f-dal').value = dal.toISOString().slice(0, 10);
    caricaImpostazioni();
    caricaLotti();
})();
