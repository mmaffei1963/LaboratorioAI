// Ricerca errori — verifiche diagnostiche in sola lettura su ArcLab.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const num = v => v == null ? '—' : Number(v).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

async function chiamata(url, metodo, corpo) {
    const opt = { method: metodo, headers: {} };
    if (corpo !== undefined) { opt.headers['Content-Type'] = 'application/json'; opt.body = JSON.stringify(corpo); }
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return r;
}

async function scarica(url, metodo, corpo, nomeDefault) {
    try {
        const resp = await chiamata(url, metodo, corpo);
        const blob = await resp.blob();
        const cd = resp.headers.get('content-disposition') || '';
        const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || nomeDefault;
        const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
        URL.revokeObjectURL(a.href);
    } catch (e) { alert(e.message); }
}

// Esegue una ricerca: aggiorna info, riempie la tabella (mapper), gestisce lo stato del pulsante.
async function esegui(btn, info, tabella, url, metodo, corpo, mappa, vuoto, colonne) {
    btn.disabled = true; const t = btn.textContent; btn.textContent = 'Ricerca…';
    try {
        const d = await (await chiamata(url, metodo, corpo)).json();
        info.textContent = vuoto(d);
        tabella.tBodies[0].innerHTML = d.righe.map(mappa).join('')
            || `<tr><td colspan="${colonne}">Nessun risultato.</td></tr>`;
    } catch (e) { info.innerHTML = `<span style="color:#b21a1a">${esc(e.message)}</span>`; }
    finally { btn.disabled = false; btn.textContent = t; }
}

const anni = () => ({ annoDa: +$('d-da').value || 0, annoA: +$('d-a').value || 0 });
const soglia = () => ({ sogliaPct: parseFloat($('m-soglia').value) || 15 });

// ── 1. Duplicati ────────────────────────────────────────────────
function cercaDuplicati() {
    esegui($('d-cerca'), $('d-info'), $('d-tab'),
        '/api/ricerca-errori/duplicati', 'POST', anni(),
        r => `<tr>
            <td>${esc(r.idCertificato)}</td><td>${esc(r.nCertificato)}</td><td>${esc(r.dataCertificato)}</td>
            <td>${esc(r.idCampione)}</td><td>${esc(r.codCampione)}</td>
            <td>${esc(r.idParametro)}</td><td>${esc(r.nomeParametro)}</td>
            <td>${esc(r.idRigaMetodo)}</td><td>${esc(r.denominazioneRiga)}</td>
            <td>${esc(r.idProva)}</td><td class="numerico">${r.valoreCalcolato ?? '—'}</td>
            <td class="numerico" style="font-weight:600">${esc(r.occorrenze)}</td>
        </tr>`,
        d => `${d.totale} righe · ${d.campioni} campioni coinvolti`, 12);
}

// ── 2. Arrotondamento ───────────────────────────────────────────
function cercaArrotondamento() {
    esegui($('a-cerca'), $('a-info'), $('a-tab'),
        '/api/ricerca-errori/arrotondamento', 'GET', undefined,
        r => `<tr>
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td>
            <td style="color:${r.cifreSignificative == null ? '#b21a1a' : 'inherit'};font-weight:${r.cifreSignificative == null ? 600 : 400}">${r.cifreSignificative == null ? '(vuoto)' : esc(r.cifreSignificative)}</td>
            <td style="color:${r.decimaliMax == null ? '#b21a1a' : 'inherit'};font-weight:${r.decimaliMax == null ? 600 : 400}">${r.decimaliMax == null ? '(vuoto)' : esc(r.decimaliMax)}</td>
        </tr>`,
        d => `${d.totale} metodi da completare`, 9);
}

// ── 3. Margine ──────────────────────────────────────────────────
function cercaMargine() {
    esegui($('m-cerca'), $('m-info'), $('m-tab'),
        '/api/ricerca-errori/margine', 'POST', soglia(),
        r => `<tr>
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td>
            <td class="numerico">${num(r.costoAnoi)}</td><td class="numerico">${num(r.costoMetodoEuro)}</td>
            <td class="numerico" style="font-weight:600;color:${(r.marginePct ?? 0) < 0 ? '#b21a1a' : '#b26a00'}">${r.marginePct == null ? '—' : Number(r.marginePct).toLocaleString('it-IT', { minimumFractionDigits: 1, maximumFractionDigits: 2 })}</td>
        </tr>`,
        d => `${d.totale} metodi sotto soglia`, 10);
}

// ── 4. Doppi ────────────────────────────────────────────────────
function cercaDoppi() {
    esegui($('p-cerca'), $('p-info'), $('p-tab'),
        '/api/ricerca-errori/doppi', 'GET', undefined,
        r => `<tr>
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td><td class="numerico" style="font-weight:600">${esc(r.nMetodi)}</td>
        </tr>`,
        d => `${d.totale} metodi in ${d.gruppi} gruppi doppi`, 8);
}

$('d-cerca')?.addEventListener('click', cercaDuplicati);
$('a-cerca')?.addEventListener('click', cercaArrotondamento);
$('m-cerca')?.addEventListener('click', cercaMargine);
$('p-cerca')?.addEventListener('click', cercaDoppi);

$('d-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/duplicati/esporta', 'POST', anni(), 'RicercaErrori_Duplicati.xlsx'));
$('a-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/arrotondamento/esporta', 'POST', {}, 'RicercaErrori_Arrotondamento.xlsx'));
$('m-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/margine/esporta', 'POST', soglia(), 'RicercaErrori_Margine.xlsx'));
$('p-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/doppi/esporta', 'POST', {}, 'RicercaErrori_MetodiDoppi.xlsx'));
