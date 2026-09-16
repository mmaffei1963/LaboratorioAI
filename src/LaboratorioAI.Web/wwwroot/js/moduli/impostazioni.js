// Amministrazione — Impostazioni documenti (configurazioni condivise tra i moduli).
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const postJson = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });

let voci = [];

function msg(t, err = false) { const el = $('msg'); el.textContent = t || ''; el.style.color = err ? '#b00' : ''; }

async function carica() {
    voci = await api('/api/impostazioni/documenti');
    $('campi-impostazioni').innerHTML = voci.map(v =>
        `<label style="grid-column:1/-1">${esc(v.etichetta)}
            <input type="text" data-chiave="${esc(v.chiave)}" value="${esc(v.valore)}" />
        </label>`).join('');
}

async function salva() {
    if (!confirm('Confermi il salvataggio delle impostazioni?\n\nLa revisione delle Condizioni Generali è condivisa: verrà applicata a offerte e Ordini di Accettazione.')) return;
    const corpo = {};
    $('campi-impostazioni').querySelectorAll('input[data-chiave]').forEach(inp => corpo[inp.dataset.chiave] = inp.value.trim());
    try {
        await postJson('/api/impostazioni/documenti', corpo);
        msg('Impostazioni salvate.');
    } catch (e) { msg(e.message, true); }
}

$('btn-salva').addEventListener('click', salva);
carica().catch(e => msg(e.message, true));
