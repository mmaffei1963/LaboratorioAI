// Preventivi — composizione offerte da controlli/metodi ArcLab (sola lettura) + subappalto a campo libero.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const eur = n => (n == null ? '' : Number(n).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const postJson = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });

// ── Enum (allineati al server) ──
const TIPO = { CONTROLLO: 0, PROPOSTA: 1, ACCESSORIO: 2, LIBERO: 3 };
const ORIGINE = { COMMESSA: 0, LISTINO: 1, MANUALE: 2 };
const STATO = { BOZZA: 0, DA_APPROVARE: 1, APPROVATO: 2, INVIATO: 3, ACCETTATO: 4, RIFIUTATO: 5 };
const tipoLabel = t => ({ 0: 'Controllo', 1: 'Proposta', 2: 'Accessorio', 3: 'Subappalto' }[t] ?? '—');
const origineLabel = o => ({ 0: 'commessa', 1: 'listino', 2: 'manuale' }[o] ?? '');

// ── Stato in memoria ──
let stato = nuovoStato();
let mapControlli = new Map();   // nome → {id, nome}
let mapClienti = new Map();     // nome → {id, nome}
let accessori = [];             // [{idVoceAccessorio, descrizione, prezzo, unitaMisura}]
let labEsterni = [];            // [{id, nome, frase}]
let libMetodi = [];             // metodi del controllo libero in composizione

function nuovoStato() {
    return {
        id: null, numero: null, anno: null, statoPrev: STATO.BOZZA,
        idClienteArcLab: null, righe: []
    };
}

// ═══════════════════════════════ Caricamenti ═══════════════════════════════

async function caricaRegistro() {
    const righe = await api('/api/preventivi/elenco');
    $('conteggio').textContent = righe.length ? `(${righe.length})` : '';
    const tb = $('tabella-registro').querySelector('tbody');
    tb.innerHTML = '';
    for (const p of righe) {
        const tr = document.createElement('tr');
        const num = p.numero ? `${p.anno}/${String(p.numero).padStart(3, '0')}` : '—';
        const data = p.dataOfferta ? new Date(p.dataOfferta).toLocaleDateString('it-IT') : '';
        tr.innerHTML = `<td>${num}</td><td>${esc(p.stato)}${p.richiedeApprovazione ? ' ⚑' : ''}</td>`
            + `<td>${esc(p.clienteRagioneSociale)}</td><td>${esc(p.oggetto)}</td><td>${data}</td>`
            + `<td class="numerico">${eur(p.totaleImponibile)}</td>`
            + `<td><button type="button" class="pulsante pulsante-tenue" data-apri="${p.idPreventivo}">Apri</button></td>`;
        tb.appendChild(tr);
    }
    tb.querySelectorAll('[data-apri]').forEach(b => b.addEventListener('click', () => apri(+b.dataset.apri)));
}

async function caricaControlli() {
    const c = await api('/api/preventivi/controlli');
    mapControlli = new Map();
    const dl = $('lista-controlli'); dl.innerHTML = '';
    for (const x of c) {
        mapControlli.set(x.controllo, { id: x.idControllo, nome: x.controllo });
        const o = document.createElement('option'); o.value = x.controllo; dl.appendChild(o);
    }
}

async function caricaAccessori() {
    accessori = await api('/api/preventivi/accessori');
    const sel = $('acc-input'); sel.innerHTML = '<option value="">—</option>';
    for (const a of accessori) {
        const o = document.createElement('option');
        o.value = a.idVoceAccessorio;
        o.textContent = `${a.descrizione} — ${eur(a.prezzo)} €${a.unitaMisura ? '/' + a.unitaMisura : ''}`;
        sel.appendChild(o);
    }
}

async function caricaLabEsterni() {
    labEsterni = await api('/api/preventivi/lab-esterni');
    const sel = $('lib-m-lab'); sel.innerHTML = '<option value="">— (interno / nessuno)</option>';
    for (const l of labEsterni) {
        const o = document.createElement('option'); o.value = l.id; o.textContent = l.nome; sel.appendChild(o);
    }
}

let timerClienti = null;
function cercaClienti() {
    clearTimeout(timerClienti);
    const q = $('c-cerca').value.trim();
    if (q.length < 2) return;
    timerClienti = setTimeout(async () => {
        const c = await api('/api/preventivi/clienti?filtro=' + encodeURIComponent(q));
        mapClienti = new Map();
        const dl = $('lista-clienti'); dl.innerHTML = '';
        for (const x of c) {
            mapClienti.set(x.nome, { id: x.id, nome: x.nome });
            const o = document.createElement('option'); o.value = x.nome; dl.appendChild(o);
        }
    }, 250);
}

function scegliCliente() {
    const v = $('c-cerca').value.trim();
    const cli = mapClienti.get(v);
    if (cli) {
        stato.idClienteArcLab = cli.id;
        $('p-id-cliente').value = cli.id;
        $('c-ragione').value = cli.nome;
    } else {
        // Cliente non in archivio: si compila a mano, nessun id ArcLab.
        stato.idClienteArcLab = null;
        $('p-id-cliente').value = '';
        if (!$('c-ragione').value) $('c-ragione').value = v;
    }
}

// ═══════════════════════════════ Righe ═══════════════════════════════

async function aggiungiControllo() {
    const v = $('ctrl-input').value.trim();
    const c = mapControlli.get(v);
    if (!c) return;
    $('ctrl-input').value = '';
    try {
        const riga = await postJson('/api/preventivi/riga-controllo', {
            idControllo: c.id, nomeControllo: c.nome,
            idClienteArcLab: stato.idClienteArcLab, scontoPercentuale: 0
        });
        stato.righe.push(riga);
        renderRighe();
    } catch (e) { alert(e.message); }
}

function aggiungiAccessorio() {
    const id = +$('acc-input').value;
    if (!id) return;
    const a = accessori.find(x => x.idVoceAccessorio === id);
    if (!a) return;
    $('acc-input').value = '';
    stato.righe.push({
        progressivo: stato.righe.length + 1, tipo: TIPO.ACCESSORIO,
        idVoceAccessorio: a.idVoceAccessorio, descrizione: a.descrizione,
        originePrezzo: ORIGINE.LISTINO, prezzoBase: a.prezzo, scontoPercentuale: 0, quantita: 1,
        metodi: []
    });
    renderRighe();
}

function renderRighe() {
    const tb = $('tabella-righe').querySelector('tbody');
    tb.innerHTML = '';
    const tpl = $('tpl-riga');
    stato.righe.forEach((r, i) => {
        r.progressivo = i + 1;
        const tr = tpl.content.firstElementChild.cloneNode(true);
        tr.querySelector('.r-descr').textContent = r.descrizione || '';
        const nMet = (r.metodi || []).length;
        tr.querySelector('.r-tipo').textContent = tipoLabel(r.tipo) + (nMet ? ` · ${nMet} metodi` : '');
        tr.querySelector('.r-origine').textContent = origineLabel(r.originePrezzo);
        tr.querySelector('.r-base').textContent = eur(r.prezzoBase);
        const inSconto = tr.querySelector('.r-sconto'); inSconto.value = r.scontoPercentuale ?? 0;
        const inQta = tr.querySelector('.r-qta'); inQta.value = r.quantita ?? 1;
        tr.querySelector('.r-tot').textContent = eur(totaleRiga(r));
        inSconto.addEventListener('input', () => { r.scontoPercentuale = +inSconto.value || 0; tr.querySelector('.r-tot').textContent = eur(totaleRiga(r)); ricalcola(); });
        inQta.addEventListener('input', () => { r.quantita = Math.max(1, +inQta.value || 1); tr.querySelector('.r-tot').textContent = eur(totaleRiga(r)); ricalcola(); });
        tr.querySelector('.r-rimuovi').addEventListener('click', () => { stato.righe.splice(i, 1); renderRighe(); });
        tb.appendChild(tr);
    });
    ricalcola();
}

const round2 = n => Math.round((n + Number.EPSILON) * 100) / 100;
function totaleRiga(r) {
    const q = Math.max(1, r.quantita || 1);
    return round2((r.prezzoBase || 0) * q * (1 - (r.scontoPercentuale || 0) / 100));
}

function ricalcola() {
    const imponibile = round2(stato.righe.reduce((s, r) => s + totaleRiga(r), 0));
    const iva = round2(imponibile * (+$('p-iva').value || 0) / 100);
    $('tot-imponibile').textContent = eur(imponibile);
    $('tot-iva').textContent = eur(iva);
    $('tot-totale').textContent = eur(round2(imponibile + iva));
    const serveApprovazione = stato.righe.some(r => (r.scontoPercentuale || 0) > 0);
    $('avviso-approvazione').textContent = serveApprovazione
        ? '⚑ Il preventivo applica uno sconto: prima dell\'invio serve l\'approvazione.' : '';
    $('btn-approvazione').classList.toggle('nascosto', !serveApprovazione);
}

// ═══════════════════════════════ Controllo libero (subappalto) ═══════════════════════════════

function apriLibero() {
    libMetodi = [];
    $('lib-nome').value = ''; $('lib-prezzo').value = ''; $('lib-sconto').value = 0;
    $('lib-verifica').value = ''; $('lib-verifica-esito').textContent = '';
    pulisciCampiMetodoLibero();
    renderLibMetodi();
    $('pannello-libero').classList.remove('nascosto');
    $('pannello-libero').scrollIntoView({ behavior: 'smooth' });
}
function chiudiLibero() { $('pannello-libero').classList.add('nascosto'); }

function pulisciCampiMetodoLibero() {
    for (const id of ['lib-m-descr', 'lib-m-denom', 'lib-m-rif', 'lib-m-unita']) $(id).value = '';
    $('lib-m-lab').value = '';
}

let timerVerifica = null;
function verificaMetodo() {
    clearTimeout(timerVerifica);
    const q = $('lib-verifica').value.trim();
    if (q.length < 2) { $('lib-verifica-esito').textContent = ''; return; }
    timerVerifica = setTimeout(async () => {
        try {
            const m = await api('/api/preventivi/metodi?filtro=' + encodeURIComponent(q));
            $('lib-verifica-esito').textContent = m.length
                ? `⚠ Attenzione: ${m.length} metodo/i già presenti tra i nostri (es. ${esc(m[0].descrizione || '')}). Valuta se usarlo invece di crearne uno libero.`
                : '✓ Nessun metodo nostro corrisponde: puoi inserirlo come libero.';
        } catch { }
    }, 300);
}

function aggiungiMetodoLibero() {
    const descr = $('lib-m-descr').value.trim();
    if (!descr) { alert('Indica almeno il parametro/metodo.'); return; }
    const idLab = +$('lib-m-lab').value || null;
    const lab = idLab ? labEsterni.find(l => l.id === idLab) : null;
    libMetodi.push({
        idMetodoArcLab: null,
        descrizione: descr,
        denominazioneRiga: $('lib-m-denom').value.trim() || null,
        riferimentoNormativo: $('lib-m-rif').value.trim() || null,
        unitaMisura: $('lib-m-unita').value.trim() || null,
        accreditato: false,
        idLabEsternoArcLab: idLab,
        labEsterno: lab ? lab.nome : null,
        labEsternoFrase: lab ? lab.frase : null
    });
    pulisciCampiMetodoLibero();
    renderLibMetodi();
}

function renderLibMetodi() {
    const tb = $('lib-tabella-metodi').querySelector('tbody');
    tb.innerHTML = '';
    const tpl = $('tpl-lib-metodo');
    libMetodi.forEach((m, i) => {
        const tr = tpl.content.firstElementChild.cloneNode(true);
        tr.querySelector('.lm-descr').textContent = m.descrizione || '';
        tr.querySelector('.lm-denom').textContent = m.denominazioneRiga || '';
        tr.querySelector('.lm-rif').textContent = m.riferimentoNormativo || '';
        tr.querySelector('.lm-unita').textContent = m.unitaMisura || '';
        tr.querySelector('.lm-lab').textContent = m.labEsterno || '';
        tr.querySelector('.lm-rimuovi').addEventListener('click', () => { libMetodi.splice(i, 1); renderLibMetodi(); });
        tb.appendChild(tr);
    });
}

function confermaLibero() {
    const nome = $('lib-nome').value.trim();
    if (!nome) { alert('Indica il nome del controllo.'); return; }
    const prezzo = +$('lib-prezzo').value || 0;
    if (libMetodi.length === 0) { alert('Aggiungi almeno un metodo.'); return; }
    stato.righe.push({
        progressivo: stato.righe.length + 1, tipo: TIPO.LIBERO,
        descrizione: nome, originePrezzo: ORIGINE.MANUALE,
        prezzoBase: prezzo, scontoPercentuale: +$('lib-sconto').value || 0, quantita: 1,
        metodi: libMetodi.map((m, i) => ({ ...m, progressivo: i + 1 }))
    });
    chiudiLibero();
    renderRighe();
}

// ═══════════════════════════════ Form ═══════════════════════════════

function nuovo() {
    stato = nuovoStato();
    $('form-titolo').textContent = 'Nuovo preventivo';
    $('form-stato').textContent = '';
    $('p-id').value = ''; $('p-id-cliente').value = '';
    for (const id of ['c-cerca', 'c-ragione', 'c-piva', 'c-indirizzo', 'c-cap', 'c-citta', 'c-prov', 'c-email', 'p-oggetto', 'p-pagamento', 'p-note']) $(id).value = '';
    $('p-data').value = new Date().toISOString().slice(0, 10);
    $('p-validita').value = 60; $('p-iva').value = 22;
    $('tabella-righe').querySelector('tbody').innerHTML = '';
    renderRighe();
    aggiornaBottoniStato();
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth' });
}

async function apri(id) {
    const p = await api('/api/preventivi/' + id);
    stato = {
        id: p.idPreventivo, numero: p.numero, anno: p.anno, statoPrev: p.stato,
        idClienteArcLab: p.idClienteArcLab, righe: (p.righe || []).map(r => ({ ...r }))
    };
    $('p-id').value = p.idPreventivo;
    $('p-id-cliente').value = p.idClienteArcLab ?? '';
    $('c-cerca').value = '';
    $('c-ragione').value = p.clienteRagioneSociale ?? '';
    $('c-piva').value = p.clientePartitaIva ?? '';
    $('c-indirizzo').value = p.clienteIndirizzo ?? '';
    $('c-cap').value = p.clienteCap ?? '';
    $('c-citta').value = p.clienteCitta ?? '';
    $('c-prov').value = p.clienteProvincia ?? '';
    $('c-email').value = p.clienteEmail ?? '';
    $('p-oggetto').value = p.oggetto ?? '';
    $('p-data').value = p.dataOfferta ? p.dataOfferta.slice(0, 10) : new Date().toISOString().slice(0, 10);
    $('p-validita').value = p.validitaGiorni ?? 60;
    $('p-iva').value = p.aliquotaIva ?? 22;
    $('p-pagamento').value = p.condizioniPagamento ?? '';
    $('p-note').value = p.note ?? '';
    const num = p.numero ? `${p.anno}/${String(p.numero).padStart(3, '0')}` : 'bozza';
    $('form-titolo').textContent = 'Preventivo ' + num;
    $('form-stato').textContent = p.statoTesto;
    renderRighe();
    aggiornaBottoniStato();
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth' });
}

function aggiornaBottoniStato() {
    const btnApprova = $('btn-approva');
    if (btnApprova) btnApprova.classList.toggle('nascosto', !(stato.id && stato.statoPrev === STATO.DA_APPROVARE));
    $('btn-elimina').classList.toggle('nascosto', !stato.id);
}

function corpo(statoRichiesto) {
    return {
        idPreventivo: stato.id,
        stato: statoRichiesto,
        idClienteArcLab: stato.idClienteArcLab,
        clienteRagioneSociale: $('c-ragione').value.trim(),
        clientePartitaIva: $('c-piva').value.trim(),
        clienteIndirizzo: $('c-indirizzo').value.trim(),
        clienteCap: $('c-cap').value.trim(),
        clienteCitta: $('c-citta').value.trim(),
        clienteProvincia: $('c-prov').value.trim(),
        clienteEmail: $('c-email').value.trim(),
        oggetto: $('p-oggetto').value.trim(),
        dataOfferta: $('p-data').value || null,
        validitaGiorni: +$('p-validita').value || 60,
        aliquotaIva: +$('p-iva').value || 22,
        condizioniPagamento: $('p-pagamento').value.trim(),
        note: $('p-note').value.trim(),
        righe: stato.righe.map((r, i) => ({
            progressivo: i + 1, tipo: r.tipo,
            idControlloArcLab: r.idControlloArcLab ?? null,
            idPropostaControllo: r.idPropostaControllo ?? null,
            idVoceAccessorio: r.idVoceAccessorio ?? null,
            descrizione: r.descrizione, originePrezzo: r.originePrezzo,
            prezzoBase: r.prezzoBase, scontoPercentuale: r.scontoPercentuale || 0,
            quantita: r.quantita || 1, note: r.note ?? null,
            metodi: (r.metodi || []).map((m, j) => ({ ...m, progressivo: j + 1 }))
        }))
    };
}

async function salva(statoRichiesto) {
    if (!$('c-ragione').value.trim()) { alert('Indica il cliente (ragione sociale).'); return; }
    if (stato.righe.length === 0) { alert('Aggiungi almeno una riga.'); return; }
    try {
        const r = await postJson('/api/preventivi/salva', corpo(statoRichiesto));
        stato.id = r.idPreventivo; stato.numero = r.numero; stato.anno = r.anno; stato.statoPrev = r.stato;
        $('p-id').value = r.idPreventivo;
        $('form-stato').textContent = r.statoTesto;
        $('form-titolo').textContent = 'Preventivo ' + (r.numero ? `${r.anno}/${String(r.numero).padStart(3, '0')}` : 'bozza');
        aggiornaBottoniStato();
        await caricaRegistro();
    } catch (e) { alert(e.message); }
}

async function approva() {
    if (!stato.id) return;
    try {
        const r = await postJson(`/api/preventivi/${stato.id}/approva`, {});
        stato.statoPrev = r.stato; $('form-stato').textContent = r.statoTesto;
        aggiornaBottoniStato(); await caricaRegistro();
    } catch (e) { alert(e.message); }
}

async function elimina() {
    if (!stato.id || !confirm('Eliminare questo preventivo?')) return;
    try {
        await api(`/api/preventivi/${stato.id}/elimina`, { method: 'POST' });
        $('pannello-form').classList.add('nascosto');
        await caricaRegistro();
    } catch (e) { alert(e.message); }
}

// ═══════════════════════════════ Avvio ═══════════════════════════════

function avvia() {
    $('btn-nuovo').addEventListener('click', nuovo);
    $('c-cerca').addEventListener('input', cercaClienti);
    $('c-cerca').addEventListener('change', scegliCliente);
    $('ctrl-input').addEventListener('change', aggiungiControllo);
    $('acc-input').addEventListener('change', aggiungiAccessorio);
    $('p-iva').addEventListener('input', ricalcola);
    $('btn-libero').addEventListener('click', apriLibero);
    $('lib-chiudi').addEventListener('click', chiudiLibero);
    $('lib-annulla').addEventListener('click', chiudiLibero);
    $('lib-verifica').addEventListener('input', verificaMetodo);
    $('lib-m-aggiungi').addEventListener('click', aggiungiMetodoLibero);
    $('lib-conferma').addEventListener('click', confermaLibero);
    $('btn-annulla').addEventListener('click', () => $('pannello-form').classList.add('nascosto'));
    $('btn-salva').addEventListener('click', () => salva(STATO.BOZZA));
    $('btn-approvazione').addEventListener('click', () => salva(STATO.DA_APPROVARE));
    const ba = $('btn-approva'); if (ba) ba.addEventListener('click', approva);
    $('btn-elimina').addEventListener('click', elimina);

    Promise.all([caricaRegistro(), caricaControlli(), caricaAccessori(), caricaLabEsterni()])
        .catch(e => console.error(e));
}

document.addEventListener('DOMContentLoaded', avvia);
