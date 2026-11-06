// Amministrazione — Voci preventivi (voci accessorie e metodi di pagamento).
// Righe modificabili inline; "Attivo" è sempre una spunta, mai una cancellazione fisica.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const postJson = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });

function msg(id, t, err = false) { const el = $(id); el.textContent = t || ''; el.style.color = err ? '#b00' : ''; }

// ── Voci accessorie ──────────────────────────────────────────────────

function rigaAccessorio(v) {
    const tr = document.createElement('tr');
    tr.dataset.id = v.idVoceAccessorio;
    tr.innerHTML = `
        <td><input type="text" class="a-codice" value="${esc(v.codice)}" style="width:100%" /></td>
        <td><input type="text" class="a-descrizione" value="${esc(v.descrizione)}" style="width:100%" /></td>
        <td class="numerico"><input type="number" class="a-prezzo" value="${v.prezzo}" min="0" step="0.01" style="width:5.5rem;text-align:right" /></td>
        <td><input type="text" class="a-unita" value="${esc(v.unitaMisura)}" style="width:5.5rem" /></td>
        <td style="text-align:center"><input type="checkbox" class="a-attivo" ${v.attivo ? 'checked' : ''} /></td>
        <td><button type="button" class="pulsante pulsante-tenue a-salva">Salva</button></td>`;
    tr.querySelector('.a-salva').addEventListener('click', () => salvaAccessorio(tr));
    return tr;
}

async function salvaAccessorio(tr) {
    const dto = {
        idVoceAccessorio: Number(tr.dataset.id) || 0,
        codice: tr.querySelector('.a-codice').value.trim(),
        descrizione: tr.querySelector('.a-descrizione').value.trim(),
        prezzo: Number(tr.querySelector('.a-prezzo').value) || 0,
        unitaMisura: tr.querySelector('.a-unita').value.trim(),
        attivo: tr.querySelector('.a-attivo').checked
    };
    if (!dto.codice || !dto.descrizione) { msg('msg-accessori', 'Codice e descrizione sono obbligatori.', true); return; }
    try {
        const r = await postJson('/api/voci-preventivo/accessori', dto);
        tr.dataset.id = r.id;
        msg('msg-accessori', 'Voce salvata.');
    } catch (e) { msg('msg-accessori', e.message, true); }
}

async function caricaAccessori() {
    const voci = await api('/api/voci-preventivo/accessori');
    const corpo = $('corpo-accessori');
    corpo.innerHTML = '';
    voci.forEach(v => corpo.appendChild(rigaAccessorio(v)));
}

$('btn-nuovo-accessorio').addEventListener('click', () => {
    $('corpo-accessori').appendChild(rigaAccessorio({ idVoceAccessorio: 0, codice: '', descrizione: '', prezzo: 0, unitaMisura: 'cad', attivo: true }));
});

// ── Metodi di pagamento ──────────────────────────────────────────────

function rigaPagamento(v) {
    const tr = document.createElement('tr');
    tr.dataset.id = v.idMetodoPagamento;
    tr.innerHTML = `
        <td><input type="text" class="p-descrizione" value="${esc(v.descrizione)}" style="width:100%" /></td>
        <td class="numerico"><input type="number" class="p-ordine" value="${v.ordine}" min="0" step="1" style="width:4rem;text-align:right" /></td>
        <td style="text-align:center"><input type="checkbox" class="p-attivo" ${v.attivo ? 'checked' : ''} /></td>
        <td><button type="button" class="pulsante pulsante-tenue p-salva">Salva</button></td>`;
    tr.querySelector('.p-salva').addEventListener('click', () => salvaPagamento(tr));
    return tr;
}

async function salvaPagamento(tr) {
    const dto = {
        idMetodoPagamento: Number(tr.dataset.id) || 0,
        descrizione: tr.querySelector('.p-descrizione').value.trim(),
        ordine: Number(tr.querySelector('.p-ordine').value) || 0,
        attivo: tr.querySelector('.p-attivo').checked
    };
    if (!dto.descrizione) { msg('msg-pagamento', 'La descrizione è obbligatoria.', true); return; }
    try {
        const r = await postJson('/api/voci-preventivo/pagamento', dto);
        tr.dataset.id = r.id;
        msg('msg-pagamento', 'Voce salvata.');
    } catch (e) { msg('msg-pagamento', e.message, true); }
}

async function caricaPagamento() {
    const voci = await api('/api/voci-preventivo/pagamento');
    const corpo = $('corpo-pagamento');
    corpo.innerHTML = '';
    voci.forEach(v => corpo.appendChild(rigaPagamento(v)));
}

$('btn-nuovo-pagamento').addEventListener('click', () => {
    $('corpo-pagamento').appendChild(rigaPagamento({ idMetodoPagamento: 0, descrizione: '', ordine: 0, attivo: true }));
});

caricaAccessori().catch(e => msg('msg-accessori', e.message, true));
caricaPagamento().catch(e => msg('msg-pagamento', e.message, true));
