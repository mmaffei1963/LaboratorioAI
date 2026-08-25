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

// ── 5/6. Lotti senza ricevitore / vettore ──────────────────────
const anniR = () => ({ annoDa: +$('r-da').value || 0, annoA: +$('r-a').value || 0 });
const anniV = () => ({ annoDa: +$('v-da').value || 0, annoA: +$('v-a').value || 0 });
const anniC = () => ({ annoDa: +$('c-da').value || 0, annoA: +$('c-a').value || 0 });

const mappaLotto = r => `<tr>
    <td>${esc(r.idLotto)}</td><td>${esc(r.codLotto)}</td><td>${esc(r.dataRic)}</td>
    <td>${esc(r.idCommessa)}</td><td>${esc(r.ragioneSociale)}</td>
</tr>`;

function cercaRicevitore() {
    esegui($('r-cerca'), $('r-info'), $('r-tab'), '/api/ricerca-errori/lotti-senza-ricevitore', 'POST', anniR(),
        mappaLotto, d => `${d.totale} lotti senza ricevitore`, 5);
}
function cercaVettore() {
    esegui($('v-cerca'), $('v-info'), $('v-tab'), '/api/ricerca-errori/lotti-senza-vettore', 'POST', anniV(),
        mappaLotto, d => `${d.totale} lotti senza vettore`, 5);
}

// ── 7. Coerenza prelevatore / vettore / verbale ─────────────────
function cercaCoerenza() {
    esegui($('c-cerca'), $('c-info'), $('c-tab'), '/api/ricerca-errori/coerenza-prelevatore', 'POST', anniC(),
        r => `<tr>
            <td>${esc(r.idLotto)}</td><td>${esc(r.codLotto)}</td><td>${esc(r.dataRic)}</td>
            <td>${esc(r.idCampione)}</td><td>${esc(r.codCampione)}</td>
            <td>${esc(r.idPrelevatore)}</td><td>${esc(r.prelevatore)}</td>
            <td>${esc(r.idVettore)}</td><td>${esc(r.vettore)}</td><td>${esc(r.buonoPrelievo)}</td>
            <td style="color:#b21a1a;font-weight:600">${[r.anomaliaVettore ? 'categoria vettore diversa dal prelevatore' : null, r.anomaliaVerbale ? 'verbale non ammesso (prelievo esterno)' : null].filter(Boolean).join(' + ')}</td>
        </tr>`,
        d => `${d.totale} campioni · ${d.vettore} categoria incoerente · ${d.verbale} verbale non ammesso`, 11);
}

$('r-cerca')?.addEventListener('click', cercaRicevitore);
$('v-cerca')?.addEventListener('click', cercaVettore);
$('c-cerca')?.addEventListener('click', cercaCoerenza);
$('r-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/lotti-senza-ricevitore/esporta', 'POST', anniR(), 'RicercaErrori_LottiSenzaRicevitore.xlsx'));
$('v-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/lotti-senza-vettore/esporta', 'POST', anniV(), 'RicercaErrori_LottiSenzaVettore.xlsx'));
$('c-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/coerenza-prelevatore/esporta', 'POST', anniC(), 'RicercaErrori_CoerenzaPrelevatore.xlsx'));
