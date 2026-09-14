// Monitoraggio TempLog Econorma — caricamento file, evidenziazione escursioni, mappatura canali.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const num = (v, d = 1) => (v === null || v === undefined || v === '') ? '—' : Number(v).toFixed(d);

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    const ct = r.headers.get('content-type') || '';
    return ct.includes('application/json') ? r.json() : r.text();
}

// Stato di una lettura calcolato dai valori (indipendente da come l'enum è serializzato lato server)
function stato(c, l) {
    if (!l || l.valore === null || l.valore === undefined) return 'Vuota';
    if (!c.limitiDisponibili) return 'SenzaLimiti';
    if (l.valore > c.max) return 'SopraMax';
    if (l.valore < c.min) return 'SottoMin';
    return 'InRange';
}

let ultimo = null;
let canaleCorrente = -1;

// ══ Monitoraggio ══
async function monitora() {
    const f = $('file-templog').files[0];
    if (!f) { $('stato-caricamento').textContent = 'Seleziona un file.'; return; }
    $('stato-caricamento').textContent = 'Analisi in corso…';
    const fd = new FormData();
    fd.append('file', f);
    fd.append('salva', $('salva-caricamento').checked);
    try {
        ultimo = await api('/api/templog/monitora', { method: 'POST', body: fd });
        renderRisultato(ultimo);
        $('stato-caricamento').textContent = '';
    } catch (e) { $('stato-caricamento').textContent = e.message; }
}

// ══ Fonte dati: file oppure DB MonitorTemperature ══
function fonteSelezionata() {
    return document.querySelector('input[name="fonte-templog"]:checked')?.value || 'file';
}
function aggiornaFonte() {
    const db = fonteSelezionata() !== 'file';
    $('riga-db')?.classList.toggle('nascosto', !db);
    $('blocco-file')?.classList.toggle('nascosto', db);
    aggiornaFinestra();
}
function aggiornaFinestra() {
    const range = $('db-finestra')?.value === 'range';
    $('db-dal-wrap')?.classList.toggle('nascosto', !range);
    $('db-al-wrap')?.classList.toggle('nascosto', !range);
}

async function monitoraDb() {
    const fonte = fonteSelezionata();
    if (fonte === 'file') return;
    const corpo = { fonte, salva: $('db-salva')?.checked || false };
    const modo = $('db-finestra')?.value || '24';
    if (modo === 'range') {
        if (!$('db-dal').value || !$('db-al').value) { $('stato-db').textContent = 'Indica data/ora inizio e fine.'; return; }
        corpo.dal = $('db-dal').value; corpo.al = $('db-al').value;
    } else {
        corpo.ore = +modo || 24;
    }
    $('stato-db').textContent = 'Lettura dal database…';
    try {
        ultimo = await api('/api/templog/monitora-db', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
        renderRisultato(ultimo);
        $('stato-db').textContent = (!ultimo.canali || ultimo.canali.length === 0)
            ? 'Nessuna misura nel periodo.'
            : (ultimo.numeroCanaliMonitorati === 0
                ? 'Letture caricate, ma nessun canale ha limiti impostati (vedi "Da sistemare").'
                : '');
    } catch (e) { $('stato-db').textContent = e.message; }
}

function renderRisultato(r) {
    canaleCorrente = -1;
    $('dettaglio-canale')?.classList.add('nascosto');
    const periodo = (r.dataDal ? new Date(r.dataDal).toLocaleString('it-IT') : '') +
        (r.dataAl ? ' → ' + new Date(r.dataAl).toLocaleString('it-IT') : '');
    $('sintesi-periodo').textContent = (r.nomeFile || '') + (periodo ? '  ·  ' + periodo : '');
    const conf = r.tuttoConforme;
    $('sintesi-badge').innerHTML =
        `<div class="riquadro-tenue" style="border-left:6px solid ${conf ? '#1a7f37' : '#b3261e'}">
           <strong style="color:${conf ? '#1a7f37' : '#b3261e'}">${conf ? 'TUTTO NEI LIMITI' : 'ESCURSIONI RILEVATE'}</strong>
           — canali monitorati: ${r.numeroCanaliMonitorati}, con escursioni: ${r.numeroCanaliConEscursioni}, escursioni totali: ${r.escursioni.length}
         </div>`;
    $('pannello-sintesi').classList.remove('nascosto');

    // Da sistemare
    if (r.daSistemare.length) {
        $('lista-dasistemare').innerHTML = r.daSistemare.map(c => `<span class="etichetta">${esc(c)}</span>`).join(' ');
        $('pannello-dasistemare').classList.remove('nascosto');
    } else $('pannello-dasistemare').classList.add('nascosto');

    // Escursioni
    $('conteggio-escursioni').textContent = r.escursioni.length + ' escursioni';
    const dataFile = r.dataDal ? new Date(r.dataDal).toLocaleDateString('it-IT') : '—';
    $('tabella-escursioni').tBodies[0].innerHTML = r.escursioni.map(e => {
        const col = e.tipo.includes('massimo') ? '#b3261e' : '#1741b3';
        const dd = e.data ? new Date(e.data).toLocaleDateString('it-IT') : dataFile;
        return `<tr>
            <td>${dd}</td>
            <td>${esc(e.canale)}</td><td>${esc(e.ambiente)}</td>
            <td style="color:${col};font-weight:600">${esc(e.tipo)}</td>
            <td>${esc(e.oraInizio)}</td><td>${esc(e.oraFine)}</td>
            <td class="numerico">${e.numeroLetture}</td>
            <td class="numerico" style="color:${col};font-weight:600">${num(e.valorePeggiore)}</td>
            <td class="numerico">${num(e.limiteSuperato)}</td>
            <td class="numerico">${num(e.scostamentoMax, 2)}</td></tr>`;
    }).join('') || '<tr><td colspan="10">Nessuna escursione.</td></tr>';
    $('pannello-escursioni').classList.remove('nascosto');

    // Canali
    $('tabella-canali').tBodies[0].innerHTML = r.canali.map((c, i) => {
        let esito;
        if (!c.limitiDisponibili) esito = `<span class="etichetta">da sistemare</span>`;
        else if (c.escursioni.length) esito = `<span style="color:#b3261e;font-weight:600">${c.escursioni.length} escursioni</span>`;
        else esito = `<span style="color:#1a7f37">conforme</span>`;
        return `<tr data-i="${i}" style="cursor:pointer">
            <td>${dataFile}</td>
            <td><strong>${esc(c.canale)}</strong></td><td>${esc(c.ambiente || c.sonda || '')}</td>
            <td class="numerico">${num(c.vn)}</td><td class="numerico">${num(c.min)}</td><td class="numerico">${num(c.max)}</td>
            <td class="numerico">${c.numeroLetture}</td>
            <td class="numerico" style="${c.numeroSopra ? 'color:#b3261e;font-weight:600' : ''}">${c.numeroSopra}</td>
            <td class="numerico" style="${c.numeroSotto ? 'color:#1741b3;font-weight:600' : ''}">${c.numeroSotto}</td>
            <td>${esito}</td></tr>`;
    }).join('');
    $('tabella-canali').tBodies[0].querySelectorAll('tr').forEach(tr =>
        tr.addEventListener('click', () => dettaglioCanale(+tr.dataset.i)));
    $('pannello-canali').classList.remove('nascosto');
    renderValori(r);
    renderMonitor(r);
}

function renderValori(r) {
    // asse tempo dal canale con più letture
    const base = r.canali.reduce((a, c) => c.letture.length > a.letture.length ? c : a, r.canali[0] || { letture: [] });
    const ore = base.letture.map(l => l.ora);
    $('tabella-valori').tHead.innerHTML = '<tr><th>Ora</th>' +
        r.canali.map(c => `<th title="${esc(c.ambiente || c.sonda || '')}">${esc(c.canale)}</th>`).join('') + '</tr>';
    const rows = ore.map((ora, i) => {
        const celle = r.canali.map(c => {
            const l = c.letture[i];
            if (!l || l.valore === null || l.valore === undefined) return '<td class="numerico">—</td>';
            const s = stato(c, l);
            let st = '';
            if (s === 'SopraMax') st = 'color:#b3261e;font-weight:600;background:#fdecec';
            else if (s === 'SottoMin') st = 'color:#1741b3;font-weight:600;background:#e9eefb';
            return `<td class="numerico" style="${st}">${num(l.valore)}</td>`;
        }).join('');
        return `<tr><td>${esc(ora)}</td>${celle}</tr>`;
    }).join('');
    $('tabella-valori').tBodies[0].innerHTML = rows;
    $('pannello-valori').classList.remove('nascosto');
}

function renderMonitor(r) {
    const g = $('griglia-monitor');
    g.innerHTML = '';
    r.canali.forEach((c, i) => {
        const box = document.createElement('div');
        box.style.cssText = 'border:1px solid #ddd;border-radius:6px;padding:4px;cursor:pointer';
        box.title = 'clicca per espandere';
        const cap = document.createElement('div');
        cap.style.cssText = 'font-size:0.8rem;font-weight:600;margin-bottom:2px';
        cap.textContent = `${c.canale} — ${c.ambiente || c.sonda || ''}`;
        const cv = document.createElement('canvas');
        cv.width = 320; cv.height = 170; cv.style.width = '100%';
        box.appendChild(cap); box.appendChild(cv); g.appendChild(box);
        box.addEventListener('click', () => dettaglioCanale(i));
        disegnaGrafico(c, cv);
    });
    $('pannello-monitor').classList.remove('nascosto');
}

// ══ Export report ══
async function esporta(formato) {
    const f = $('file-templog').files[0];
    if (!f) { alert('Carica prima un file.'); return; }
    const fd = new FormData(); fd.append('file', f);
    const r = await fetch('/api/templog/report?formato=' + formato, { method: 'POST', body: fd });
    if (!r.ok) { alert('Errore export.'); return; }
    const blob = await r.blob();
    const cd = r.headers.get('content-disposition') || '';
    const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || ('Report_Econorma.' + (formato === 'excel' ? 'xlsx' : 'pdf'));
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
    URL.revokeObjectURL(a.href);
}

// ══ Export PDF mirati (dal risultato già in memoria) ══
async function scaricaPdf(url, body, nomeDefault) {
    if (!ultimo || !ultimo.canali || !ultimo.canali.length) { alert('Analizza prima un file.'); return; }
    const r = await fetch(url, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
    });
    if (!r.ok) { let m = 'Errore export.'; try { m = (await r.json()).messaggio || m; } catch { } alert(m); return; }
    const blob = await r.blob();
    const cd = r.headers.get('content-disposition') || '';
    const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || nomeDefault;
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
    URL.revokeObjectURL(a.href);
}
const pdfGrafici = () => scaricaPdf('/api/templog/pdf/grafici', ultimo, 'Grafici_Econorma.pdf');
const pdfValori = () => scaricaPdf('/api/templog/pdf/valori', ultimo, 'Valori_Econorma.pdf');
const pdfCanale = () => {
    if (canaleCorrente < 0) { alert('Apri prima un canale cliccando una riga della tabella Canali.'); return; }
    scaricaPdf('/api/templog/pdf/canale', { risultato: ultimo, indice: canaleCorrente }, 'Canale_Econorma.pdf');
};

// ══ Caricamento da cartella ══
async function elencaCartella() {
    const percorso = $('cartella').value.trim();
    const q = percorso ? '?percorso=' + encodeURIComponent(percorso) : '';
    const r = await api('/api/templog/cartella' + q);
    $('cartella').value = r.cartella || '';
    const tb = $('tabella-file').tBodies[0];
    if (!r.accessibile) { tb.innerHTML = `<tr><td colspan="3">Cartella non accessibile${r.errore ? ': ' + esc(r.errore) : ''}.</td></tr>`; return; }
    tb.innerHTML = (r.files || []).map(f => `<tr>
        <td>${esc(f.name)}</td><td class="sottotesto">${esc(f.percorso)}</td>
        <td><button class="pulsante pulsante-tenue" data-file="${esc(f.name)}">Analizza</button></td></tr>`).join('')
        || '<tr><td colspan="3">Nessun file.</td></tr>';
    tb.querySelectorAll('[data-file]').forEach(b =>
        b.addEventListener('click', () => caricaDaCartella(r.cartella, b.dataset.file)));
}

async function caricaDaCartella(cartella, nomeFile) {
    $('stato-caricamento').textContent = 'Analisi di ' + nomeFile + '…';
    try {
        const salva = $('salva-caricamento').checked;
        ultimo = await api('/api/templog/carica-cartella?salva=' + salva, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ cartella, nomeFile })
        });
        renderRisultato(ultimo);
        $('stato-caricamento').textContent = 'Analizzato: ' + nomeFile + ' (dalla cartella)';
    } catch (e) { $('stato-caricamento').textContent = e.message; }
}

function dettaglioCanale(i) {
    canaleCorrente = i;
    const c = ultimo.canali[i];
    $('dettaglio-titolo').textContent = `${c.canale} — ${c.ambiente || c.sonda || ''}` +
        (c.limitiDisponibili ? `  (limiti ${num(c.min)} … ${num(c.max)})` : '  (senza limiti)');
    $('tabella-letture').tBodies[0].innerHTML = c.letture.map(l => {
        const s = stato(c, l);
        const col = { SopraMax: '#b3261e', SottoMin: '#1741b3', InRange: '', Vuota: '#999', SenzaLimiti: '#555' }[s] || '';
        const et = { SopraMax: 'sopra max', SottoMin: 'sotto min', InRange: 'ok', Vuota: '—', SenzaLimiti: 'n/d' }[s] || s;
        return `<tr><td>${esc(l.ora)}</td><td class="numerico" style="color:${col};font-weight:${col ? 600 : 400}">${num(l.valore)}</td><td style="color:${col}">${et}</td></tr>`;
    }).join('');
    disegnaGrafico(c, $('grafico-canale'));
    $('dettaglio-canale').classList.remove('nascosto');
    $('dettaglio-canale').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

let tooltipEl = null;
function tooltip() {
    if (!tooltipEl) {
        tooltipEl = document.createElement('div');
        tooltipEl.style.cssText = 'position:fixed;z-index:100;pointer-events:none;display:none;background:#222;color:#fff;padding:4px 8px;border-radius:4px;font-size:.78rem;white-space:nowrap;box-shadow:0 2px 8px rgba(0,0,0,.3)';
        document.body.appendChild(tooltipEl);
    }
    return tooltipEl;
}

function disegnaGrafico(c, cv) {
    const st = calcolaScala(c, cv);
    cv._tl = { c, st };
    renderChart(cv, -1);
    if (!cv._tlHover) {
        cv._tlHover = true;
        cv.addEventListener('mousemove', ev => {
            const s = cv._tl; if (!s) return;
            const rect = cv.getBoundingClientRect();
            const mx = (ev.clientX - rect.left) * (cv.width / rect.width);
            const i = s.st.indiceDa(mx);
            renderChart(cv, i);
            const l = s.c.letture[i];
            const tt = tooltip();
            if (l && l.valore !== null && l.valore !== undefined) {
                const es = stato(s.c, l);
                let extra = '';
                if (es === 'SopraMax') extra = `  ▲ oltre max ${num(s.c.max, 1)} (+${num(l.valore - s.c.max, 2)})`;
                else if (es === 'SottoMin') extra = `  ▼ sotto min ${num(s.c.min, 1)} (−${num(s.c.min - l.valore, 2)})`;
                tt.style.display = 'block';
                tt.innerHTML = `${esc(l.ora)}  ·  ${num(l.valore, 2)}°C<span style="color:${es === 'SopraMax' ? '#ff9a9a' : es === 'SottoMin' ? '#a9c0ff' : '#ccc'}">${esc(extra)}</span>`;
                tt.style.left = (ev.clientX + 12) + 'px';
                tt.style.top = (ev.clientY - 10) + 'px';
            } else tt.style.display = 'none';
        });
        cv.addEventListener('mouseleave', () => { renderChart(cv, -1); tooltip().style.display = 'none'; });
    }
}

function calcolaScala(c, cv) {
    const W = cv.width, H = cv.height, ml = 46, mr = 12, mt = 18, mb = 30;
    const vals = c.letture.map(l => l.valore).filter(v => v !== null && v !== undefined);
    let lo = vals.length ? Math.min(...vals) : 0, hi = vals.length ? Math.max(...vals) : 1;
    if (c.limitiDisponibili) { lo = Math.min(lo, c.min); hi = Math.max(hi, c.max); }
    const pad = (hi - lo) * 0.1 || 1; lo -= pad; hi += pad;
    const n = c.letture.length;
    const px = i => ml + i / Math.max(n - 1, 1) * (W - ml - mr);
    const py = v => mt + (hi - v) / (hi - lo) * (H - mt - mb);
    const indiceDa = mx => Math.max(0, Math.min(n - 1, Math.round((mx - ml) / Math.max(W - ml - mr, 1) * (n - 1))));
    return { W, H, ml, mr, mt, mb, lo, hi, n, px, py, indiceDa };
}

function renderChart(cv, hover) {
    const { c, st } = cv._tl; const ctx = cv.getContext('2d');
    const { W, H, ml, mr, mt, mb, lo, hi, n, px, py } = st;
    ctx.clearRect(0, 0, W, H);
    if (!c.letture.length) return;
    if (c.limitiDisponibili) {
        ctx.fillStyle = 'rgba(26,127,55,0.08)';
        ctx.fillRect(ml, py(c.max), W - ml - mr, py(c.min) - py(c.max));
        ctx.setLineDash([4, 3]);
        ctx.strokeStyle = '#b3261e'; ctx.beginPath(); ctx.moveTo(ml, py(c.max)); ctx.lineTo(W - mr, py(c.max)); ctx.stroke();
        ctx.strokeStyle = '#1741b3'; ctx.beginPath(); ctx.moveTo(ml, py(c.min)); ctx.lineTo(W - mr, py(c.min)); ctx.stroke();
        ctx.setLineDash([]);
    }
    ctx.strokeStyle = '#888'; ctx.beginPath(); ctx.moveTo(ml, mt); ctx.lineTo(ml, H - mb); ctx.lineTo(W - mr, H - mb); ctx.stroke();
    ctx.fillStyle = '#333'; ctx.font = '11px Arial';
    ctx.fillText(hi.toFixed(1), 4, py(hi) + 4); ctx.fillText(lo.toFixed(1), 4, py(lo) + 4);
    // etichette orarie sull'asse x
    const maxLab = Math.max(3, Math.floor((W - ml - mr) / 55));
    const step = Math.max(1, Math.ceil(n / maxLab));
    ctx.fillStyle = '#555'; ctx.font = '10px Arial'; ctx.textAlign = 'center';
    for (let i = 0; i < n; i += step) {
        const ora = c.letture[i].ora || '';
        ctx.fillText(ora, px(i), H - mb + 13);
        ctx.strokeStyle = '#eee'; ctx.beginPath(); ctx.moveTo(px(i), mt); ctx.lineTo(px(i), H - mb); ctx.stroke();
    }
    ctx.textAlign = 'left';
    // linea
    ctx.strokeStyle = '#1741b3'; ctx.lineWidth = 1.6; ctx.beginPath();
    let first = true;
    c.letture.forEach((l, i) => {
        if (l.valore === null || l.valore === undefined) { first = true; return; }
        const x = px(i), y = py(l.valore);
        if (first) { ctx.moveTo(x, y); first = false; } else ctx.lineTo(x, y);
    });
    ctx.stroke();
    // punti fuori limite
    c.letture.forEach((l, i) => {
        const s = stato(c, l);
        if (s === 'SopraMax' || s === 'SottoMin') {
            ctx.fillStyle = s === 'SopraMax' ? '#b3261e' : '#1741b3';
            ctx.beginPath(); ctx.arc(px(i), py(l.valore), 3, 0, 7); ctx.fill();
        }
    });
    // crosshair su hover
    if (hover >= 0 && c.letture[hover] && c.letture[hover].valore != null) {
        const x = px(hover), y = py(c.letture[hover].valore);
        ctx.strokeStyle = '#999'; ctx.setLineDash([3, 3]); ctx.beginPath(); ctx.moveTo(x, mt); ctx.lineTo(x, H - mb); ctx.stroke(); ctx.setLineDash([]);
        ctx.fillStyle = '#111'; ctx.beginPath(); ctx.arc(x, y, 4, 0, 7); ctx.fill();
        ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(x, y, 2, 0, 7); ctx.fill();
    }
}

// ══ Mappatura ══
async function caricaMappatura() {
    const righe = await api('/api/templog/mappatura');
    $('conteggio-mappatura').textContent = righe.length + ' righe';
    $('tabella-mappatura').tBodies[0].innerHTML = righe.map(m => `<tr>
        <td><strong>${esc(m.canale)}</strong></td><td class="numerico">${m.idStrumento}</td>
        <td>${esc(m.sonda)}</td><td>${esc(m.note)}</td>
        <td>${m.attivo ? 'sì' : 'no'}</td>
        <td>${m.attivo ? `<button class="pulsante pulsante-tenue" data-off="${m.idMappatura}">Disattiva</button>` : ''}</td>
    </tr>`).join('') || '<tr><td colspan="6">Nessuna mappatura. Usa “Sincronizza” e “Proponi”.</td></tr>';
    $('tabella-mappatura').tBodies[0].querySelectorAll('[data-off]').forEach(b =>
        b.addEventListener('click', async () => { await api(`/api/templog/mappatura/${b.dataset.off}/disattiva`, { method: 'POST' }); caricaMappatura(); }));
}

async function sincronizza() {
    const sonde = await api('/api/templog/sonde');
    $('tabella-sonde').tBodies[0].innerHTML = sonde.map(s => {
        const stato = !s.limitiOk ? '<span class="etichetta">senza limiti</span>'
            : s.giaMappato ? '<span style="color:#1a7f37">mappato</span>' : '<span style="color:#b26a00">da mappare</span>';
        return `<tr>
            <td>${esc(s.canaleIpotesi)}</td><td class="numerico">${s.idStrumento}</td>
            <td>${esc(s.sonda)}</td><td>${esc(s.ambiente)}</td>
            <td class="numerico">${num(s.vn)}</td><td class="numerico">${num(s.min)}</td><td class="numerico">${num(s.max)}</td>
            <td>${stato}</td>
            <td>${s.giaMappato ? '' : `<button class="pulsante pulsante-tenue" data-add='${esc(JSON.stringify({ canale: s.canaleIpotesi, idStrumento: s.idStrumento, sonda: s.sonda }))}'>Aggiungi</button>`}</td>
        </tr>`;
    }).join('');
    $('tabella-sonde').tBodies[0].querySelectorAll('[data-add]').forEach(b =>
        b.addEventListener('click', async () => {
            const d = JSON.parse(b.dataset.add);
            await api('/api/templog/mappatura', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...d, attivo: true }) });
            await caricaMappatura(); sincronizza();
        }));
}

async function proponi() {
    const r = await api('/api/templog/mappatura/proponi', { method: 'POST' });
    await caricaMappatura();
    await sincronizza();
    alert(`${r.aggiunte} mappature aggiunte.`);
}

// ══ Validazione ══
const badgeEsito = e => {
    if (e === 'Conforme') return '<span style="color:#1a7f37;font-weight:600">Conforme</span>';
    if (e === 'Dubbio') return '<span style="color:#b26a00;font-weight:600">Dubbio</span>';
    if (e === 'NonConforme') return '<span style="color:#b3261e;font-weight:600">Non conforme</span>';
    return '<span class="sottotesto">non validato</span>';
};

async function validaCorrente(esito) {
    const f = $('file-templog').files[0];
    if (!f) { alert('Per validare un file caricato da cartella, spunta “registra il caricamento”, poi validalo dal Registro.'); return; }
    const fd = new FormData();
    fd.append('file', f); fd.append('esito', esito); fd.append('note', $('valida-note').value || '');
    try {
        const r = await api('/api/templog/valida-corrente', { method: 'POST', body: fd });
        $('valida-esito-corrente').innerHTML = `Validato: ${badgeEsito(r.esitoValidazione)} da ${esc(r.validatore)} il ${new Date(r.dataValidazione).toLocaleString('it-IT')}`;
        caricaRegistro();
    } catch (e) { $('valida-esito-corrente').textContent = e.message; }
}

async function caricaRegistro() {
    const righe = await api('/api/templog/registro');
    $('conteggio-registro').textContent = righe.length + ' caricamenti';
    const conCheck = !!$('reg-tutti');
    $('tabella-registro').tBodies[0].innerHTML = righe.map(c => `<tr>
        ${conCheck ? `<td><input type="checkbox" class="reg-sel" value="${c.idCaricamento}" /></td>` : ''}
        <td>${esc(c.nomeFile)}</td>
        <td>${c.dataDal ? new Date(c.dataDal).toLocaleDateString('it-IT') : '—'}</td>
        <td>${new Date(c.dataCreazione).toLocaleString('it-IT')}</td>
        <td class="numerico" style="${c.numeroEscursioni ? 'color:#b3261e;font-weight:600' : ''}">${c.numeroEscursioni}</td>
        <td>${badgeEsito(c.esitoValidazione)}</td>
        <td>${esc(c.validatore)}${c.dataValidazione ? ' · ' + new Date(c.dataValidazione).toLocaleDateString('it-IT') : ''}</td>
        <td>${esc(c.noteValidazione)}</td>
    </tr>`).join('') || `<tr><td colspan="${conCheck ? 8 : 7}">Nessun caricamento registrato.</td></tr>`;
    const tutti = $('reg-tutti');
    if (tutti) tutti.onclick = () => document.querySelectorAll('.reg-sel').forEach(c => c.checked = tutti.checked);
}

async function validaSelezionati(esito) {
    const ids = [...document.querySelectorAll('.reg-sel:checked')].map(c => +c.value);
    if (!ids.length) { alert('Seleziona almeno un caricamento.'); return; }
    const r = await api('/api/templog/valida', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ids, esito, note: $('registro-note').value || '' })
    });
    await caricaRegistro();
    alert(`${r.validati} caricamenti validati come ${esito}.`);
}

// ══ Avvio ══
$('btn-monitora')?.addEventListener('click', monitora);
$('btn-monitora-db')?.addEventListener('click', monitoraDb);
document.querySelectorAll('input[name="fonte-templog"]').forEach(r => r.addEventListener('change', aggiornaFonte));
$('db-finestra')?.addEventListener('change', aggiornaFinestra);
aggiornaFonte();
$('btn-sincronizza')?.addEventListener('click', sincronizza);
$('btn-proponi')?.addEventListener('click', proponi);
$('btn-export-pdf')?.addEventListener('click', () => esporta('pdf'));
$('btn-export-excel')?.addEventListener('click', () => esporta('excel'));
$('btn-pdf-grafici')?.addEventListener('click', pdfGrafici);
$('btn-pdf-valori')?.addEventListener('click', pdfValori);
$('btn-pdf-canale')?.addEventListener('click', pdfCanale);
$('btn-sonde-pdf')?.addEventListener('click', () => window.open('/api/templog/sonde/report?formato=pdf', '_blank'));
$('btn-sonde-excel')?.addEventListener('click', () => window.open('/api/templog/sonde/report?formato=excel', '_blank'));
$('btn-elenca')?.addEventListener('click', elencaCartella);
document.querySelectorAll('#riquadro-validazione [data-esito]').forEach(b =>
    b.addEventListener('click', () => validaCorrente(b.dataset.esito)));
document.querySelectorAll('#pannello-registro [data-bulk]').forEach(b =>
    b.addEventListener('click', () => validaSelezionati(b.dataset.bulk)));
if ($('pannello-mappatura')) caricaMappatura();
if ($('pannello-registro')) caricaRegistro();
elencaCartella();  // propone subito l'ultima cartella usata (o quella predefinita)
