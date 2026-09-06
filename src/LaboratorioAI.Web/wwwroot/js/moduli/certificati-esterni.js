// Triage e rinomina batch dei certificati dei laboratori esterni (upload o cartella di rete).
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

let righe = [];        // { file?, percorso?, ant }
let filesCartella = []; // { name, percorso }

async function analizzaJson(url, body) {
    const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    const j = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(j.messaggio || 'Errore');
    return j;
}

function preparaTabella() {
    $('pannello-esito').classList.remove('nascosto');
    $('btn-salva-tutti').classList.remove('nascosto');
}

function aggiungiRiga(sorgente, etichetta) {
    const idx = righe.length;
    righe.push({ ...sorgente, ant: null });
    const tr = document.createElement('tr');
    tr.dataset.idx = idx;
    tr.innerHTML = `<td>${esc(etichetta)}</td><td colspan="7" class="sottotesto">analisi…</td>`;
    $('tabella-cert').tBodies[0].appendChild(tr);
    return { idx, tr };
}

// ── Upload dal browser ──
async function analizzaUpload() {
    const files = [...$('file-cert').files];
    if (!files.length) { $('stato').textContent = 'Seleziona uno o più PDF.'; return; }
    preparaTabella();
    let i = 0;
    for (const f of files) {
        i++; $('stato').textContent = `Analisi ${i}/${files.length}: ${f.name}…`;
        const { idx, tr } = aggiungiRiga({ file: f }, f.name);
        try {
            const fd = new FormData(); fd.append('file', f);
            fd.append('rianalizza', $('rianalizza')?.checked ? 'true' : 'false');
            const r = await fetch('/api/certificati-esterni/analizza', { method: 'POST', body: fd });
            const j = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(j.messaggio || 'Errore');
            righe[idx].ant = j; renderRiga(tr, idx, f.name, j);
        } catch (e) { erroreRiga(tr, f.name, e.message); }
    }
    $('stato').textContent = '';
    aggiornaConteggio();
}

// ── Da cartella di rete ──
async function elencaCartella() {
    const percorso = $('cartella').value.trim();
    const q = percorso ? '?percorso=' + encodeURIComponent(percorso) : '';
    const r = await fetch('/api/certificati-esterni/cartella' + q);
    const j = await r.json().catch(() => ({}));
    $('cartella').value = j.cartella || '';
    const tb = $('tabella-file').tBodies[0];
    if (!j.accessibile) { tb.innerHTML = `<tr><td colspan="3">Cartella non accessibile${j.errore ? ': ' + esc(j.errore) : ''}.</td></tr>`; $('btn-analizza-tutti').classList.add('nascosto'); return; }
    filesCartella = j.files || [];
    tb.innerHTML = filesCartella.map(f => `<tr>
        <td>${esc(f.name)}</td><td class="sottotesto">${esc(f.percorso)}</td>
        <td><button class="pulsante pulsante-tenue" data-perc="${esc(f.percorso)}" data-name="${esc(f.name)}">Analizza</button></td></tr>`).join('')
        || '<tr><td colspan="3">Nessun PDF nella cartella.</td></tr>';
    tb.querySelectorAll('[data-perc]').forEach(b => b.addEventListener('click', () => analizzaPercorso(b.dataset.perc, b.dataset.name)));
    $('btn-analizza-tutti').classList.toggle('nascosto', filesCartella.length === 0);
}

async function analizzaPercorso(percorso, name) {
    preparaTabella();
    const { idx, tr } = aggiungiRiga({ percorso }, name || percorso);
    try {
        const j = await analizzaJson('/api/certificati-esterni/analizza-percorso',
            { percorso, rianalizza: $('rianalizza')?.checked || false });
        righe[idx].ant = j; renderRiga(tr, idx, name || percorso, j);
    } catch (e) { erroreRiga(tr, name || percorso, e.message); }
    aggiornaConteggio();
}

async function analizzaTuttiCartella() {
    for (const f of filesCartella) await analizzaPercorso(f.percorso, f.name);
}

// ── Rendering riga risultato ──
function erroreRiga(tr, etichetta, msg) {
    tr.innerHTML = `<td>${esc(etichetta)}</td><td colspan="7" style="color:#b3261e">${esc(msg)}</td>`;
}

function renderRiga(tr, idx, etichetta, a) {
    const e = a.estrazione || {};
    const c = a.campione;
    const crit = a.criticita || [];
    const avvertenze = a.avvertenze || [];
    const avv = !!a.avvisoSuggerito;
    const nome = (avv ? a.nomeConAvviso : a.nomeStandard) || '';

    const critCell = crit.length
        ? `<span style="color:#b3261e;font-weight:600" title="${esc(crit.join(' · '))}">▲ ${crit.length}</span>`
        : `<span style="color:#1a7f37">ok</span>`;
    const avvert = avvertenze.length ? `<div class="sottotesto" style="color:#b26a00">${esc(avvertenze.join(' · '))}</div>` : '';
    const cache = a.daCache ? ' <span class="etichetta" title="estrazione riusata dalla cache">cache</span>' : '';

    tr.innerHTML = `
        <td>${esc(etichetta)}${cache}${avvert}</td>
        <td>${esc(e.laboratorio || '')}<div class="sottotesto">${esc(a.labAcronimo || '')}</div></td>
        <td>${esc(e.codiceNostro || '—')}</td>
        <td>${esc(c ? c.ragioneSociale : '—')}<div class="sottotesto">${esc(c ? (c.denominazioneProdotto || '') : '')}</div></td>
        <td>${critCell}</td>
        <td>
            <label class="casella"><input type="radio" name="avv-${idx}" value="avv" ${avv ? 'checked' : ''}/> AVV</label>
            <label class="casella"><input type="radio" name="avv-${idx}" value="std" ${avv ? '' : 'checked'}/> STD</label>
        </td>
        <td><input type="text" class="nome" style="width:32rem" value="${esc(nome)}" placeholder="nome (compila se mancante)"/></td>
        <td><button class="pulsante pulsante-tenue salva">Salva</button><div class="esito sottotesto"></div></td>`;

    tr.querySelectorAll(`input[name=avv-${idx}]`).forEach(r => r.addEventListener('change', () => {
        const useAvv = tr.querySelector(`input[name=avv-${idx}][value=avv]`).checked;
        const nuovo = useAvv ? a.nomeConAvviso : a.nomeStandard;
        if (nuovo) tr.querySelector('.nome').value = nuovo;
    }));
    tr.querySelector('.salva').addEventListener('click', () => salvaRiga(tr, idx));
}

async function salvaRiga(tr, idx) {
    const r = righe[idx]; if (!r || !r.ant) return;
    const nome = tr.querySelector('.nome').value.trim();
    const esito = tr.querySelector('.esito');
    if (!nome) { esito.textContent = 'Nome mancante.'; return; }
    const c = r.ant.campione;
    const data = c?.dataRicezione ? new Date(c.dataRicezione).toISOString().slice(0, 10) : '';
    esito.textContent = 'salvataggio…';
    try {
        let j;
        if (r.percorso) {
            j = await analizzaJson('/api/certificati-esterni/conferma-percorso',
                { percorso: r.percorso, nomeFinale: nome, labAcronimo: r.ant.labAcronimo || '', data });
        } else {
            const fd = new FormData();
            fd.append('file', r.file); fd.append('nomeFinale', nome);
            fd.append('labAcronimo', r.ant.labAcronimo || ''); fd.append('data', data);
            const rr = await fetch('/api/certificati-esterni/conferma', { method: 'POST', body: fd });
            j = await rr.json().catch(() => ({}));
            if (!rr.ok) throw new Error(j.messaggio || 'Errore');
        }
        esito.innerHTML = '<span style="color:#1a7f37">salvato</span>' + (j.originaleSpostato ? ' · originale spostato' : '');
        tr.querySelector('.salva').disabled = true;
    } catch (e) { esito.textContent = e.message; }
}

async function salvaTutti() {
    for (const tr of [...$('tabella-cert').tBodies[0].querySelectorAll('tr')]) {
        const btn = tr.querySelector('.salva');
        if (btn && !btn.disabled) await salvaRiga(tr, +tr.dataset.idx);
    }
}

function aggiornaConteggio() { $('conteggio').textContent = righe.length + ' certificati'; }

$('btn-analizza')?.addEventListener('click', analizzaUpload);
$('btn-elenca')?.addEventListener('click', elencaCartella);
$('btn-analizza-tutti')?.addEventListener('click', analizzaTuttiCartella);
$('btn-salva-tutti')?.addEventListener('click', salvaTutti);
