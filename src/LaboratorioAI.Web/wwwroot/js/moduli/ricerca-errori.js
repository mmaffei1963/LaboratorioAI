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
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.nomeParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td>
            <td style="color:${r.cifreSignificative == null ? '#b21a1a' : 'inherit'};font-weight:${r.cifreSignificative == null ? 600 : 400}">${r.cifreSignificative == null ? '(vuoto)' : esc(r.cifreSignificative)}</td>
            <td style="color:${r.decimaliMax == null ? '#b21a1a' : 'inherit'};font-weight:${r.decimaliMax == null ? 600 : 400}">${r.decimaliMax == null ? '(vuoto)' : esc(r.decimaliMax)}</td>
        </tr>`,
        d => `${d.totale} metodi da completare`, 10);
}

// ── 3. Margine ──────────────────────────────────────────────────
function cercaMargine() {
    esegui($('m-cerca'), $('m-info'), $('m-tab'),
        '/api/ricerca-errori/margine', 'POST', soglia(),
        r => `<tr>
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.nomeParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td>
            <td class="numerico">${num(r.costoAnoi)}</td><td class="numerico">${num(r.costoMetodoEuro)}</td>
            <td class="numerico" style="font-weight:600;color:${(r.marginePct ?? 0) < 0 ? '#b21a1a' : '#b26a00'}">${r.marginePct == null ? '—' : Number(r.marginePct).toLocaleString('it-IT', { minimumFractionDigits: 1, maximumFractionDigits: 2 })}</td>
        </tr>`,
        d => `${d.totale} metodi sotto soglia`, 11);
}

// ── 4. Doppi ────────────────────────────────────────────────────
function cercaDoppi() {
    esegui($('p-cerca'), $('p-info'), $('p-tab'),
        '/api/ricerca-errori/doppi', 'GET', undefined,
        r => `<tr>
            <td>${esc(r.idMetodo)}</td><td>${esc(r.idParametro)}</td><td>${esc(r.nomeParametro)}</td><td>${esc(r.idTipoMetodo)}</td>
            <td>${esc(r.rifMetodo)}</td><td>${esc(r.dettagliMetodo)}</td><td>${esc(r.rifBiblio)}</td>
            <td>${esc(r.idUnita)}</td><td class="numerico" style="font-weight:600">${esc(r.nMetodi)}</td>
        </tr>`,
        d => `${d.totale} metodi in ${d.gruppi} gruppi doppi`, 9);
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

// ── 8. Campioni «chiusi» non pronti alla stampa ─────────────────
const critStampa = () => ({
    da: $('s-da').value || null,
    a: $('s-a').value || null,
    codLotto: $('s-lotto').value || null,
    codCampione: $('s-campione').value || null
});

function cercaStampa() {
    esegui($('s-cerca'), $('s-info'), $('s-tab'), '/api/ricerca-errori/campioni-stampa', 'POST', critStampa(),
        r => {
            const an = [
                !r.verificaValore ? 'valore non verificato' : null,
                !r.dataInizioProva ? 'data inizio mancante' : null,
                !r.dataFineProva ? 'data fine mancante' : null,
                r.arrotonda == null ? 'Arrotonda mancante' : null,
                r.maxDecimali == null ? 'MaxDecimali mancante' : null
            ].filter(Boolean).join(' + ');
            return `<tr>
                <td>${esc(r.idLotto)}</td><td>${esc(r.codLotto)}</td><td>${esc(r.dataRic)}</td>
                <td>${esc(r.idCampione)}</td><td>${esc(r.codCampione)}</td><td>${esc(r.ragioneSociale)}</td>
                <td>${esc(r.idProva)}</td><td>${esc(r.idRigaMetodo)}</td><td>${esc(r.denominazioneRiga)}</td><td>${esc(r.nomeParametro)}</td>
                <td style="font-weight:600;color:${r.verificaValore ? '#1a7f37' : '#b21a1a'}">${r.verificaValore ? 'Sì' : 'NO'}</td>
                <td>${esc(r.dataInizioProva) || '—'}</td><td>${esc(r.dataFineProva) || '—'}</td>
                <td style="color:${r.arrotonda == null ? '#b21a1a' : 'inherit'}">${r.arrotonda == null ? '(vuoto)' : esc(r.arrotonda)}</td>
                <td style="color:${r.maxDecimali == null ? '#b21a1a' : 'inherit'}">${r.maxDecimali == null ? '(vuoto)' : esc(r.maxDecimali)}</td>
                <td style="color:#b21a1a;font-weight:600">${an}</td>
            </tr>`;
        },
        d => `${d.totale} prove da sistemare · ${d.campioni} campioni`, 16);
}

$('s-cerca')?.addEventListener('click', cercaStampa);
$('s-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/campioni-stampa/esporta', 'POST', critStampa(), 'RicercaErrori_CampioniStampa.xlsx'));

// ── Cruscotto controlli ─────────────────────────────────────────
function cruscottoBody() {
    const sel = {};
    document.querySelectorAll('.k-chk').forEach(c => sel[c.dataset.chiave] = c.checked);
    return {
        duplicati: !!sel.duplicati,
        arrotondamento: !!sel.arrotondamento,
        margine: !!sel.margine,
        doppi: !!sel.doppi,
        lottiRicevitore: !!sel.ricevitore,
        lottiVettore: !!sel.vettore,
        coerenza: !!sel.coerenza,
        stampa: !!sel.stampa,
        annoDa: +$('k-da').value || 0,
        annoA: +$('k-a').value || 0,
        sogliaPct: parseFloat($('k-soglia').value) || 15
    };
}

// mappa chiave → pulsante Cerca della sezione + sincronizzazione filtri dal cruscotto
const APRI = {
    duplicati: { btn: 'd-cerca', sync: () => { $('d-da').value = $('k-da').value; $('d-a').value = $('k-a').value; } },
    arrotondamento: { btn: 'a-cerca', sync: () => { } },
    margine: { btn: 'm-cerca', sync: () => { $('m-soglia').value = $('k-soglia').value; } },
    doppi: { btn: 'p-cerca', sync: () => { } },
    ricevitore: { btn: 'r-cerca', sync: () => { $('r-da').value = $('k-da').value; $('r-a').value = $('k-a').value; } },
    vettore: { btn: 'v-cerca', sync: () => { $('v-da').value = $('k-da').value; $('v-a').value = $('k-a').value; } },
    coerenza: { btn: 'c-cerca', sync: () => { $('c-da').value = $('k-da').value; $('c-a').value = $('k-a').value; } },
    stampa: {
        btn: 's-cerca', sync: () => {
            $('s-da').value = `${(+$('k-da').value)}-01-01`; $('s-a').value = `${(+$('k-a').value)}-12-31`;
            $('s-lotto').value = ''; $('s-campione').value = '';
        }
    }
};

function apri(chiave) {
    const m = APRI[chiave]; if (!m) return;
    m.sync();
    const btn = $(m.btn); if (!btn) return;
    const det = btn.closest('details'); if (det) det.open = true;
    det?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    btn.click();
}

async function eseguiCruscotto() {
    const btn = $('k-esegui'); btn.disabled = true; const t = btn.textContent; btn.textContent = 'Eseguo…';
    $('k-info').textContent = 'Esecuzione in corso… su molti anni può richiedere qualche minuto.';
    try {
        const d = await (await chiamata('/api/ricerca-errori/cruscotto', 'POST', cruscottoBody())).json();
        const tot = d.risultati.reduce((s, r) => s + r.totale, 0);
        $('k-info').textContent = `${d.risultati.length} controlli · ${tot} anomalie totali (anni ${d.annoDa}–${d.annoA})`;
        $('k-tab').tBodies[0].innerHTML = d.risultati.map(r => `<tr>
            <td>${esc(r.nome)}</td>
            <td class="numerico" style="font-weight:600;color:${r.totale ? '#b21a1a' : '#1a7f37'}">${r.totale}</td>
            <td style="font-weight:600;color:${r.totale ? '#b21a1a' : '#1a7f37'}">${r.totale ? '● da correggere' : '● ok'}</td>
            <td>${r.totale ? `<button type="button" class="pulsante pulsante-tenue" data-apri="${esc(r.chiave)}">Apri</button>` : ''}</td>
        </tr>`).join('') || '<tr><td colspan="4">Nessun controllo selezionato.</td></tr>';
        $('k-tab').tBodies[0].querySelectorAll('[data-apri]').forEach(b => b.addEventListener('click', () => apri(b.dataset.apri)));
    } catch (e) { $('k-info').innerHTML = `<span style="color:#b21a1a">${esc(e.message)}</span>`; }
    finally { btn.disabled = false; btn.textContent = t; }
}

$('k-esegui')?.addEventListener('click', eseguiCruscotto);
$('k-esporta')?.addEventListener('click', () => scarica('/api/ricerca-errori/esporta-tutto', 'POST', cruscottoBody(), 'RicercaErrori_Cruscotto.xlsx'));
