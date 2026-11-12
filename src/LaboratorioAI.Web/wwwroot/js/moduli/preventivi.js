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
const TIPO = { CONTROLLO: 0, PROPOSTA: 1, ACCESSORIO: 2, LIBERO: 3, GRUPPO: 4 };
const ORIGINE = { COMMESSA: 0, LISTINO: 1, MANUALE: 2 };
const STATO = { BOZZA: 0, DA_APPROVARE: 1, APPROVATO: 2, INVIATO: 3, ACCETTATO: 4, RIFIUTATO: 5 };
const tipoLabel = t => ({ 0: 'Controllo', 1: 'Proposta', 2: 'Accessorio', 3: 'Subappalto', 4: 'Gruppo' }[t] ?? '—');
const origineLabel = o => ({ 0: 'commessa', 1: 'listino', 2: 'manuale' }[o] ?? '');

// ── Stato in memoria ──
let stato = nuovoStato();
let listaControlli = [];        // [{id, nome, testo}] — testo = "Nome (ID 123)", per i suggerimenti
let mapClienti = new Map();     // nome → {id, nome}
let accessori = [];             // [{idVoceAccessorio, descrizione, prezzo, unitaMisura}]
let labEsterni = [];            // [{id, nome, frase}]
let metodiPagamento = [];       // [{idMetodoPagamento, descrizione}] — condizioni di pagamento predefinite
let libMetodi = [];             // metodi del controllo libero in composizione
let libLabSelezionato = null;   // {id, nome, frase} scelto nel campo "Laboratorio esterno" del metodo in composizione
let metodoCatalogoSelezionato = null; // VoceMetodo scelto dalla verifica, per restare collegato all'ID ArcLab anche se i campi vengono corretti
let revisioneDefault = '';      // revisione Condizioni Generali corrente (predefinita)
let indiceLiberoInModifica = null; // indice in stato.righe della riga ControlloLibero in modifica (null = nuova riga)

function nuovoStato() {
    return {
        id: null, numero: null, anno: null, statoPrev: STATO.BOZZA,
        idClienteArcLab: null, righe: [], idOrigine: null, numeroOrigine: null
    };
}

// ═══════════════════════════════ Caricamenti ═══════════════════════════════

// 21/10/2026: ricerca nel registro per numero / cliente e intervallo di date (filtri lato server).
function parametriRegistro() {
    const p = new URLSearchParams();
    const t = $('r-testo').value.trim(), dal = $('r-dal').value, al = $('r-al').value;
    if (t) p.set('testo', t);
    if (dal) p.set('dal', dal);
    if (al) p.set('al', al);
    return p;
}
let timerRegistro = null;
function caricaRegistroRitardato() {
    clearTimeout(timerRegistro);
    timerRegistro = setTimeout(() => caricaRegistro().catch(e => console.error(e)), 300);
}

async function caricaRegistro() {
    const par = parametriRegistro();
    const righe = await api('/api/preventivi/elenco' + (par.toString() ? '?' + par : ''));
    const filtrato = par.toString() !== '';
    $('conteggio').textContent = righe.length
        ? `(${righe.length}${filtrato ? ' trovati' : ''})`
        : (filtrato ? '(nessun preventivo trovato)' : '');
    const tb = $('tabella-registro').querySelector('tbody');
    tb.innerHTML = '';
    for (const p of righe) {
        const tr = document.createElement('tr');
        const num = p.numero ? `${p.anno}/${String(p.numero).padStart(3, '0')}` : '—';
        const data = p.dataOfferta ? new Date(p.dataOfferta).toLocaleDateString('it-IT') : '';
        const ultimaModifica = p.ultimaModificaIl
            ? `${esc((p.ultimaModificaDi || '—').split('@')[0])} · ${new Date(p.ultimaModificaIl).toLocaleDateString('it-IT')}`
            : '';
        tr.innerHTML = `<td>${num}</td><td>${esc(p.stato)}${p.richiedeApprovazione ? ' ⚑' : ''}</td>`
            + `<td>${esc(p.clienteRagioneSociale)}${p.clienteNome ? ` <span class="sottotesto">– ${esc(p.clienteNome)}</span>` : ''}</td><td>${esc(p.oggetto)}</td><td>${data}</td>`
            + `<td class="numerico">${eur(p.totaleImponibile)}</td>`
            + `<td>${esc(p.redattore || '—')}</td>`
            + `<td class="sottotesto" title="${esc(p.ultimaModificaDi || '')}">${ultimaModifica}</td>`
            + `<td style="white-space:nowrap"><button type="button" class="pulsante pulsante-tenue" data-apri="${p.idPreventivo}">Apri</button>`
            + ` <button type="button" class="pulsante pulsante-tenue" data-duplica="${p.idPreventivo}" title="Nuovo preventivo con le stesse righe, numero e data di oggi">Duplica</button></td>`;
        tb.appendChild(tr);
    }
    tb.querySelectorAll('[data-apri]').forEach(b => b.addEventListener('click', () => apri(+b.dataset.apri)));
    tb.querySelectorAll('[data-duplica]').forEach(b => b.addEventListener('click', () => duplica(+b.dataset.duplica).catch(e => alert(e.message))));
}

async function caricaControlli() {
    const c = await api('/api/preventivi/controlli');
    // L'ID controllo è riportato in coda al testo: è univoco ed è spesso quello che il
    // commerciale conosce meglio, quindi la ricerca lo trova anche digitando solo l'ID.
    listaControlli = c.map(x => ({ id: x.idControllo, nome: x.controllo, testo: `${x.controllo} (ID ${x.idControllo})` }));
}

// 27/10/2026: prezzo minimo e massimo di ogni controllo nelle commesse di laboratorio attive (tutti i clienti).
let prezziCommesse = {};
async function caricaPrezziCommesse() {
    try { prezziCommesse = await api('/api/preventivi/prezzi-commesse'); } catch (e) { console.error(e); prezziCommesse = {}; }
}
function fasciaCommesse(idControllo) {
    const f = prezziCommesse[idControllo];
    if (!f) return '';
    // 12/11/2026: con il medio pesato per cliente.
    const prezzo = f.minimo === f.massimo ? `${eur(f.minimo)} €` : `${eur(f.minimo)}–${eur(f.massimo)} €, medio ${eur(f.medio)} €`;
    return `commesse attive: ${prezzo} (${f.commesse})`;
}

async function caricaAccessori() {
    accessori = await api('/api/preventivi/accessori');
}

async function caricaImpostazioni() {
    try {
        const i = await api('/api/preventivi/impostazioni');
        revisioneDefault = i.revisioneCondizioni || '';
    } catch { }
}

async function caricaLabEsterni() {
    labEsterni = await api('/api/preventivi/lab-esterni');
}

async function caricaMetodiPagamento() {
    metodiPagamento = await api('/api/preventivi/metodi-pagamento');
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
            // In ArcLab alcune ragioni sociali hanno spazi superflui in coda: si normalizza qui,
            // altrimenti il confronto con il valore (trimmato) del campo non troverebbe mai corrispondenza.
            const nome = (x.nome || '').trim();
            mapClienti.set(nome, { id: x.id, nome });
            const o = document.createElement('option'); o.value = nome; dl.appendChild(o);
        }
    }, 250);
}

async function scegliCliente() {
    const v = $('c-cerca').value.trim();
    const cli = mapClienti.get(v);
    if (cli) {
        const cambiato = stato.idClienteArcLab !== cli.id;
        stato.idClienteArcLab = cli.id;
        $('p-id-cliente').value = cli.id;
        $('c-ragione').value = cli.nome;
        await precompilaDatiCliente(cli.id);
        // 05/11/2026: insediamenti del cliente per le righe; cambiando cliente quelli scelti non valgono più
        if (cambiato) stato.righe.forEach(r => { r.idInsediamentoArcLab = null; r.insediamento = null; });
        await caricaInsediamenti(cli.id);
        renderRighe();
        if (stato.idOrigine && !stato.id) segnalaPrezzi().catch(e => console.error(e));
    } else {
        // Cliente non in archivio: si compila a mano, nessun id ArcLab.
        stato.idClienteArcLab = null;
        stato.insediamenti = [];
        stato.righe.forEach(r => { r.idInsediamentoArcLab = null; r.insediamento = null; });
        renderRighe();
        $('p-id-cliente').value = '';
        if (!$('c-ragione').value) $('c-ragione').value = v;
    }
    aggiornaPagato();
}

// 05/11/2026 (richiesta di Zatterin): le commesse ArcLab sono per insediamento, con prezzi propri. Ogni riga di
// controllo può indicare l'insediamento: il prezzo di commessa si prende da quella sede (se non c'è, dal cliente).
async function caricaInsediamenti(idCliente) {
    stato.insediamenti = [];
    if (!idCliente) return;
    try { stato.insediamenti = await api(`/api/preventivi/clienti/${idCliente}/insediamenti`); } catch (e) { console.error(e); }
}

async function cambiaInsediamento(r, valore, testoScelto) {
    r.idInsediamentoArcLab = valore ? Number(valore) : null;
    r.insediamento = valore ? testoScelto : null;
    if (r.idControlloArcLab && r.originePrezzo !== ORIGINE.MANUALE && stato.idClienteArcLab) {
        try {
            const p = await postJson('/api/preventivi/prezzo-riga', {
                idControllo: r.idControlloArcLab, idClienteArcLab: stato.idClienteArcLab, idInsediamentoArcLab: r.idInsediamentoArcLab
            });
            r.prezzoBase = p.prezzoBase; r.originePrezzo = p.originePrezzo;
        } catch (e) { alert(e.message); }
    }
    renderRighe();
}

// Precompila i dati del cliente dall'archivio ArcLab quando se ne sceglie uno già censito:
// Partita IVA e indirizzo di fatturazione (sede legale), entrambi letti dalla tabella Clienti —
// non dall'insediamento, che è un indirizzo operativo e non quello del cliente.
async function precompilaDatiCliente(idCliente) {
    try {
        const d = await api(`/api/preventivi/clienti/${idCliente}/dettaglio`);
        $('c-piva').value = d.partitaIva ?? '';
        $('c-indirizzo').value = d.indirizzo ?? '';
        $('c-cap').value = d.cap ?? '';
        $('c-citta').value = d.citta ?? '';
        $('c-prov').value = d.provincia ?? '';
        $('c-email').value = d.email ?? '';
        $('c-telefono').value = d.telefono ?? '';   // 29/10/2026: dall'anagrafica AZW
    } catch (e) { console.error(e); }
}

// ═══════════════════════════════ Righe ═══════════════════════════════

// ── Motore generico dei campi "cerca e scegli" (menu personalizzato, largo quanto il campo:
// né il <select> né il <datalist> nativi vanno bene quando le opzioni hanno testo lungo o sono
// numerose). Nato per "Aggiungi controllo esistente", riusato per accessori e laboratorio esterno. ──
//
// Parametri:
//   input, box      — l'<input type="text"> e il <div> dei suggerimenti (elenco-suggerimenti)
//   elenco()        — funzione che restituisce l'array corrente su cui cercare
//   testoFn(d)      — testo mostrato/cercato per ogni elemento
//   filtroFn(d, q)  — opzionale: filtro personalizzato (default: testoFn include q)
//   risolviEsatto(q)— opzionale: per Invio, prova a risolvere una corrispondenza esatta (es. per ID)
//   svuotaDopoScelta— default true: svuota il campo dopo la scelta (azione immediata, es. aggiunge
//                     subito una riga). false: lascia il testo scelto nel campo (selezione che serve
//                     più avanti in un form, es. laboratorio esterno del metodo in composizione).
//   onScegli(d)     — chiamata con l'elemento scelto
function creaComboRicerca({ input, box, elenco, testoFn, filtroFn, ordinaFn, risolviEsatto, svuotaDopoScelta = true, onScegli }) {
    function filtra(q) {
        q = q.trim().toLowerCase();
        if (!q) return [];
        const fn = filtroFn || ((d, qq) => testoFn(d).toLowerCase().includes(qq));
        const trovati = elenco().filter(d => fn(d, q));
        if (ordinaFn) trovati.sort((a, b) => ordinaFn(a, b, q));   // 27/10/2026: es. l'ID esatto per primo
        return trovati.slice(0, 30);
    }
    function mostra() {
        const risultati = filtra(input.value);
        if (risultati.length === 0) { box.classList.add('nascosto'); box.innerHTML = ''; return; }
        box.innerHTML = risultati.map((d, i) =>
            `<div class="voce-suggerimento" data-indice="${i}">${esc(testoFn(d))}</div>`).join('');
        box.querySelectorAll('.voce-suggerimento').forEach((el, i) => {
            // mousedown (non click): scatta prima del blur dell'input, altrimenti il menu si chiuderebbe prima.
            el.addEventListener('mousedown', ev => { ev.preventDefault(); scegli(risultati[i]); });
        });
        box.classList.remove('nascosto');
    }
    function nascondi() { box.classList.add('nascosto'); }
    function scegli(d) {
        input.value = svuotaDopoScelta ? '' : testoFn(d);
        nascondi();
        onScegli(d);
    }
    function scegliDaTastiera() {
        // Invio senza aver cliccato un suggerimento: prova prima una corrispondenza esatta
        // (es. l'ID digitato per intero), altrimenti usa il risultato se è l'unico.
        const v = input.value.trim();
        if (!v) return;
        const risultati = filtra(v);
        const esatto = risolviEsatto ? risolviEsatto(v) : null;
        const scelto = esatto ?? (risultati.length === 1 ? risultati[0] : null);
        if (scelto) scegli(scelto);
    }
    input.addEventListener('input', mostra);
    input.addEventListener('focus', mostra);
    input.addEventListener('keydown', ev => { if (ev.key === 'Enter') { ev.preventDefault(); scegliDaTastiera(); } });
    input.addEventListener('blur', () => setTimeout(nascondi, 150));
    return { scegli, nascondi };
}

async function aggiungiControllo(c) {
    if (!c) return;
    try {
        // Con un solo insediamento lo si propone già sulla riga
        const unico = (stato.insediamenti || []).length === 1 ? stato.insediamenti[0] : null;
        const riga = await postJson('/api/preventivi/riga-controllo', {
            idControllo: c.id, nomeControllo: c.nome,
            idClienteArcLab: stato.idClienteArcLab, scontoPercentuale: 0,
            idInsediamentoArcLab: unico ? unico.id : null
        });
        if (unico) riga.insediamento = unico.nome;
        stato.righe.push(riga);
        renderRighe();
    } catch (e) { alert(e.message); }
}

function aggiungiAccessorio(a) {
    if (!a) return;
    stato.righe.push({
        progressivo: stato.righe.length + 1, tipo: TIPO.ACCESSORIO,
        idVoceAccessorio: a.idVoceAccessorio, descrizione: a.descrizione,
        originePrezzo: ORIGINE.LISTINO, prezzoBase: a.prezzo, scontoPercentuale: 0, quantita: 1,
        metodi: []
    });
    renderRighe();
}

function aggiungiAccessorioLibero() {
    const descr = $('acc-descr').value.trim();
    if (!descr) { alert('Indica la descrizione della voce accessoria.'); return; }
    const prezzo = +$('acc-prezzo').value || 0;
    const qta = Math.max(1, +$('acc-qta').value || 1);
    stato.righe.push({
        progressivo: stato.righe.length + 1, tipo: TIPO.ACCESSORIO,
        idVoceAccessorio: null, descrizione: descr, note: $('acc-testo').value.trim() || null,
        originePrezzo: ORIGINE.MANUALE, prezzoBase: prezzo, scontoPercentuale: 0, quantita: qta,
        metodi: []
    });
    $('acc-descr').value = ''; $('acc-prezzo').value = ''; $('acc-qta').value = 1; $('acc-testo').value = '';
    renderRighe();
}

function renderRighe() {
    const tb = $('tabella-righe').querySelector('tbody');
    tb.innerHTML = '';
    const tpl = $('tpl-riga');
    stato.righe.forEach((r, i) => {
        r.progressivo = i + 1;
        const tr = tpl.content.firstElementChild.cloneNode(true);
        tr.dataset.indice = i;

        const tdDescr = tr.querySelector('.r-descr');
        if (r.tipo === TIPO.ACCESSORIO) {
            // Voci accessorie (12/10/2026): titolo modificabile e testo descrittivo facoltativo, stampato in offerta
            // sotto il titolo su tutta la larghezza (il prezzo va sotto, a destra).
            tdDescr.innerHTML = `<input type="text" class="r-descr-input" maxlength="500" value="${esc(r.descrizione || '')}" style="width:100%" />`
                + `<textarea class="r-note-input" rows="2" maxlength="4000" placeholder="testo descrittivo (facoltativo)" style="width:100%;font:inherit;margin-top:3px">${esc(r.note || '')}</textarea>`;
            tdDescr.querySelector('.r-descr-input').addEventListener('input', e => { r.descrizione = e.target.value; });
            tdDescr.querySelector('.r-note-input').addEventListener('input', e => { r.note = e.target.value; });
        } else {
            // Descrizione sempre modificabile: il nome lo sceglie il commerciale, ma l'ID del/dei
            // controllo/i d'origine resta sempre visibile (e non modificabile) accanto, per non
            // perdere il riferimento univoco al catalogo ArcLab.
            const suffisso = r.tipo === TIPO.GRUPPO
                ? (r.idControlliOrigine ? `(ID ${esc(r.idControlliOrigine)})` : '')
                : r.idControlloArcLab != null ? `(ID ${r.idControlloArcLab})`
                : r.rifLocale ? `(rif. ${esc(r.rifLocale)})` : '';
            tdDescr.innerHTML = `<input type="text" class="r-descr-input" value="${esc(r.descrizione || '')}" style="width:100%" />`
                + (suffisso ? `<div class="sottotesto">${suffisso}</div>` : '');
            tdDescr.querySelector('.r-descr-input').addEventListener('input', e => { r.descrizione = e.target.value; });
            // 05/11/2026: insediamento facoltativo della riga (solo controlli, con un cliente ArcLab)
            const ins = stato.insediamenti || [];
            if (r.tipo === TIPO.CONTROLLO && (ins.length > 0 || r.idInsediamentoArcLab)) {
                const voci = ins.slice();
                if (r.idInsediamentoArcLab && !voci.some(v => v.id === r.idInsediamentoArcLab))
                    voci.unshift({ id: r.idInsediamentoArcLab, nome: r.insediamento || ('insediamento ' + r.idInsediamentoArcLab) });
                const d = document.createElement('div');
                d.className = 'sottotesto';
                d.innerHTML = `Insediamento: <select class="r-insediamento" style="max-width:100%">
                    <option value="">— tutti (prezzo del cliente)</option>
                    ${voci.map(v => `<option value="${v.id}" ${v.id === r.idInsediamentoArcLab ? 'selected' : ''}>${esc(v.nome)}</option>`).join('')}
                </select>`;
                const sel = d.querySelector('select');
                sel.addEventListener('change', () => cambiaInsediamento(r, sel.value, sel.value ? sel.options[sel.selectedIndex].text : null));
                tdDescr.appendChild(d);
            }
        }

        const nMet = (r.metodi || []).length;
        tr.querySelector('.r-tipo').textContent = tipoLabel(r.tipo) + (nMet ? ` · ${nMet} metodi` : '');
        tr.querySelector('.r-origine').textContent = origineLabel(r.originePrezzo);
        if (r.tipo === TIPO.CONTROLLO && r.idControlloArcLab && fasciaCommesse(r.idControlloArcLab)) {
            const f = document.createElement('div');
            f.className = 'sottotesto'; f.textContent = fasciaCommesse(r.idControlloArcLab);
            tr.querySelector('.r-origine').appendChild(f);
        }
        const inBase = tr.querySelector('.r-base'); inBase.value = r.prezzoBase ?? 0;
        // 26/10/2026 («Duplica»): prezzo di oggi diverso da quello copiato.
        if (r.prezzoAttuale != null && Math.abs(r.prezzoAttuale - (r.prezzoBase ?? 0)) >= 0.005) {
            const d = document.createElement('div');
            d.className = 'sottotesto avviso-prezzo';
            d.style.color = '#B45309';
            d.innerHTML = `oggi: ${eur(r.prezzoAttuale)} € <button type="button" class="pulsante pulsante-tenue" style="padding:0 .4rem">usa</button>`;
            d.querySelector('button').addEventListener('click', () => {
                r.prezzoBase = r.prezzoAttuale; r.originePrezzo = r.originePrezzoAttuale ?? r.originePrezzo; delete r.prezzoAttuale;
                renderRighe(); ricalcola();
            });
            inBase.parentElement.appendChild(d);
        }
        const inSconto = tr.querySelector('.r-sconto'); inSconto.value = r.scontoPercentuale ?? 0;
        const inQta = tr.querySelector('.r-qta'); inQta.value = r.quantita ?? 1;
        tr.querySelector('.r-tot').textContent = eur(totaleRiga(r));
        // Il prezzo base si può correggere direttamente (es. prezzo concordato diverso dal listino),
        // indipendentemente dallo sconto: non serve passare per la percentuale per cambiare l'importo.
        inBase.addEventListener('input', () => { r.prezzoBase = +inBase.value || 0; r.originePrezzo = ORIGINE.MANUALE; tr.querySelector('.r-tot').textContent = eur(totaleRiga(r)); tr.querySelector('.r-origine').textContent = origineLabel(r.originePrezzo); ricalcola(); });
        inSconto.addEventListener('input', () => { r.scontoPercentuale = +inSconto.value || 0; tr.querySelector('.r-tot').textContent = eur(totaleRiga(r)); ricalcola(); });
        inQta.addEventListener('input', () => { r.quantita = Math.max(1, +inQta.value || 1); tr.querySelector('.r-tot').textContent = eur(totaleRiga(r)); ricalcola(); });
        tr.querySelector('.r-rimuovi').addEventListener('click', () => { stato.righe.splice(i, 1); renderRighe(); });

        // Le voci accessorie non si raggruppano con i controlli: niente spunta di selezione.
        if (r.tipo === TIPO.ACCESSORIO) tr.querySelector('.r-sel-cell').innerHTML = '';

        // Solo i controlli in subappalto a campo libero si possono riaprire per modificare i metodi
        // (per gli altri tipi i metodi arrivano dal catalogo ArcLab e non sono modificabili a mano).
        const btnModifica = tr.querySelector('.r-modifica-libero');
        if (r.tipo === TIPO.LIBERO) {
            btnModifica.classList.remove('nascosto');
            btnModifica.addEventListener('click', () => modificaLibero(i));
        } else {
            btnModifica.remove();
        }

        tb.appendChild(tr);
    });
    ricalcola();
}

// Raggruppa 2+ righe selezionate (controlli/proposte/liberi/altri gruppi, non accessori) in
// un'unica riga con un prezzo unico: la descrizione è modificabile, gli ID dei controlli
// d'origine restano sempre in vista tra parentesi, i metodi sono l'unione di quelli raggruppati.
function raggruppaSelezionate() {
    const righeSel = Array.from($('tabella-righe').querySelectorAll('tbody tr'))
        .filter(tr => tr.querySelector('.r-sel')?.checked)
        .map(tr => +tr.dataset.indice);
    if (righeSel.length < 2) { alert('Seleziona almeno due righe da raggruppare (controlli, non voci accessorie).'); return; }

    const indici = righeSel.slice().sort((a, b) => a - b);
    const selezionate = indici.map(i => stato.righe[i]);
    if (selezionate.some(r => r.tipo === TIPO.ACCESSORIO)) {
        alert('Le voci accessorie non si possono raggruppare con i controlli.'); return;
    }

    const descrizione = selezionate.map(r => r.descrizione).filter(Boolean).join(' + ');
    // ID dei controlli ArcLab d'origine: per una riga già raggruppata in precedenza l'ID del
    // controllo ArcLab è sempre null (un gruppo non è un controllo di catalogo), quindi si riparte
    // dal suo idControlliOrigine già calcolato, per non perdere gli ID quando si raggruppano più
    // volte di seguito (es. si aggiunge un controllo a un gruppo già esistente). I controlli in
    // subappalto a campo libero restano senza ID, non essendo presenti nel catalogo ArcLab.
    const idControlliOrigine = selezionate
        .map(r => r.idControlliOrigine || (r.idControlloArcLab != null ? String(r.idControlloArcLab) : null) || r.rifLocale || null)
        .filter(Boolean)
        .join('-') || null;
    const prezzoBase = round2(selezionate.reduce((s, r) => s + (r.prezzoBase || 0), 0));
    const metodi = [];
    selezionate.forEach(r => (r.metodi || []).forEach(m => metodi.push({ ...m })));
    metodi.forEach((m, j) => m.progressivo = j + 1);

    const nuovaRiga = {
        tipo: TIPO.GRUPPO, descrizione, idControlliOrigine,
        idControlloArcLab: null, idPropostaControllo: null, idVoceAccessorio: null,
        originePrezzo: ORIGINE.MANUALE, prezzoBase, scontoPercentuale: 0, quantita: 1,
        metodi
    };
    // Sostituisce le righe raggruppate con la nuova, nella posizione della prima.
    const primaIndice = indici[0];
    stato.righe = stato.righe.filter((_, i) => !indici.includes(i));
    stato.righe.splice(Math.min(primaIndice, stato.righe.length), 0, nuovaRiga);
    renderRighe();
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
    indiceLiberoInModifica = null;
    libMetodi = [];
    $('lib-nome').value = ''; $('lib-prezzo').value = ''; $('lib-sconto').value = 0;
    $('lib-verifica').value = ''; $('lib-verifica-esito').innerHTML = '';
    pulisciCampiMetodoLibero();
    renderLibMetodi();
    $('pannello-libero').querySelector('h3').textContent = 'Controllo in subappalto (campo libero)';
    $('pannello-libero').classList.remove('nascosto');
    $('pannello-libero').scrollIntoView({ behavior: 'smooth' });
}

// Riapre il pannello del controllo libero su una riga già confermata, per aggiungere/togliere
// metodi senza dover cancellare e rifare l'intera riga da capo.
function modificaLibero(i) {
    const r = stato.righe[i];
    indiceLiberoInModifica = i;
    libMetodi = (r.metodi || []).map(m => ({ ...m }));
    $('lib-nome').value = r.descrizione || '';
    $('lib-prezzo').value = r.prezzoBase ?? '';
    $('lib-sconto').value = r.scontoPercentuale || 0;
    $('lib-verifica').value = ''; $('lib-verifica-esito').innerHTML = '';
    pulisciCampiMetodoLibero();
    renderLibMetodi();
    $('pannello-libero').querySelector('h3').textContent = 'Modifica controllo in subappalto (campo libero)';
    $('pannello-libero').classList.remove('nascosto');
    $('pannello-libero').scrollIntoView({ behavior: 'smooth' });
}

function chiudiLibero() { indiceLiberoInModifica = null; $('pannello-libero').classList.add('nascosto'); }

function pulisciCampiMetodoLibero() {
    for (const id of ['lib-m-descr', 'lib-m-denom', 'lib-m-rif', 'lib-m-unita']) $(id).value = '';
    $('lib-m-lab-input').value = '';
    libLabSelezionato = null;
    metodoCatalogoSelezionato = null;
}

let timerVerifica = null;
let risultatiVerifica = [];
function verificaMetodo() {
    clearTimeout(timerVerifica);
    const q = $('lib-verifica').value.trim();
    const box = $('lib-verifica-esito');
    if (q.length < 2) { box.innerHTML = ''; risultatiVerifica = []; return; }
    timerVerifica = setTimeout(async () => {
        try {
            const m = await api('/api/preventivi/metodi?filtro=' + encodeURIComponent(q));
            risultatiVerifica = m;
            if (m.length === 0) {
                box.innerHTML = '<p class="sottotesto">✓ Nessun metodo nostro corrisponde: puoi inserirlo come libero qui sotto.</p>';
                return;
            }
            const intro = `<p class="sottotesto">⚠ ${m.length} metodo/i già presenti tra i nostri: seleziona quello giusto per precompilare i campi sotto (restano modificabili prima di aggiungerlo), oppure ignora e compilali a mano per inserirlo comunque come libero.</p>`;
            // Laboratorio esterno mostrato accanto al risultato (stesso badge della tendina di
            // Metodi e Controlli): il commerciale deve poter distinguere a colpo d'occhio un metodo
            // interno da uno in subappalto presso un laboratorio esterno, prima ancora di selezionarlo.
            //
            // La denominazione di riga (rigametodo.DenominazioneRiga) è mostrata SEMPRE a parte,
            // anche quando c'è già un "dettagliMetodo": in ArcLab è lì che spesso compaiono matrice e
            // quantità (es. "Acqua potabile - 250 mL"), e prima veniva scartata dal fallback "||" se
            // dettagliMetodo era già valorizzato — invisibile proprio quando serviva di più.
            const voci = m.map((x, i) => {
                const principale = x.dettagliMetodo || x.descrizione || x.denominazioneRiga || '—';
                const denomRiga = x.denominazioneRiga && x.denominazioneRiga !== principale
                    ? `<div class="sottotesto">${esc(x.denominazioneRiga)}</div>` : '';
                const rif = x.rifBiblio ? ` — ${esc(x.rifBiblio)}` : '';
                const acc = x.accreditato ? ' <span class="sottotesto">(accreditato)</span>' : '';
                const lab = x.labEsterno ? ` <span class="etichetta etichetta-outsourcing">${esc(x.labEsterno)}</span>` : '';
                return `<div class="voce-suggerimento" data-indice="${i}">${esc(principale)}${rif}${acc}${lab}${denomRiga}</div>`;
            }).join('');
            box.innerHTML = intro + `<div class="elenco-inline">${voci}</div>`;
            box.querySelectorAll('.voce-suggerimento').forEach((el, i) => {
                el.addEventListener('mousedown', ev => { ev.preventDefault(); selezionaMetodoCatalogo(risultatiVerifica[i]); });
            });
        } catch { }
    }, 300);
}

// Precompila i campi del metodo con un risultato trovato tra i nostri (stessa mappatura usata lato
// server per i controlli di catalogo — vedi ServizioPreventivi.ComponiRigaControlloAsync), così il
// commerciale può ancora correggere/adattare (es. denominazione riga, unità) prima di aggiungerlo:
// non lo aggiunge subito alla tabella, si preme comunque "+ Aggiungi metodo" per confermarlo.
function selezionaMetodoCatalogo(m) {
    metodoCatalogoSelezionato = m;
    $('lib-m-descr').value = m.descrizione || m.dettagliMetodo || '';
    $('lib-m-denom').value = m.denominazioneRiga || '';
    $('lib-m-rif').value = m.rifBiblio || '';
    $('lib-m-unita').value = m.unitaMisura || '';
    if (m.idLabEsterno) {
        libLabSelezionato = { id: m.idLabEsterno, nome: m.labEsterno, frase: m.labEsternoFrase };
        $('lib-m-lab-input').value = m.labEsterno || '';
    } else {
        libLabSelezionato = null;
        $('lib-m-lab-input').value = '';
    }
    $('lib-verifica').value = '';
    $('lib-verifica-esito').innerHTML = '';
    $('lib-m-descr').focus();
}

function aggiungiMetodoLibero() {
    const descr = $('lib-m-descr').value.trim();
    if (!descr) { alert('Indica almeno il parametro/metodo.'); return; }
    const lab = libLabSelezionato;
    // Se il metodo era stato precompilato da un risultato di catalogo, resta collegato all'ID ArcLab
    // (e all'accreditamento) anche se il commerciale ha corretto descrizione/riferimento/unità: solo
    // la scelta di un altro laboratorio esterno, o il suo azzeramento, invalidano quel legame.
    const cat = metodoCatalogoSelezionato;
    libMetodi.push({
        idMetodoArcLab: cat ? cat.id : null,
        descrizione: descr,
        denominazioneRiga: $('lib-m-denom').value.trim() || null,
        denominazione: cat ? (cat.dettagliMetodo || null) : null,
        riferimentoNormativo: $('lib-m-rif').value.trim() || null,
        unitaMisura: $('lib-m-unita').value.trim() || null,
        accreditato: cat ? cat.accreditato : false,
        idLabEsternoArcLab: lab ? lab.id : null,
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

// Riferimento locale ("L00001", "L00002", ...) per i controlli in subappalto a campo libero: non
// essendo di catalogo ArcLab non hanno un ID, ma serve comunque poterli identificare sempre quando
// vengono raggruppati con altri controlli. Vale solo all'interno di questo preventivo, non è un ID
// ArcLab. Calcolato al volo dalle righe correnti, così non serve un contatore separato da mantenere
// allineato tra nuovo()/apri().
function prossimoRifLocale() {
    let max = 0;
    for (const r of stato.righe) {
        // Sia le righe libere non ancora raggruppate (rifLocale), sia quelle già raggruppate in una
        // sessione precedente e poi ricaricate (il riferimento resta nel testo di idControlliOrigine,
        // salvato sul server): va controllato anche lì, altrimenti dopo un salvataggio e riapertura si
        // potrebbe riassegnare un riferimento già usato in un gruppo esistente.
        for (const m of `${r.rifLocale || ''} ${r.idControlliOrigine || ''}`.matchAll(/L(\d{5,})/g))
            max = Math.max(max, +m[1]);
    }
    return 'L' + String(max + 1).padStart(5, '0');
}

function confermaLibero() {
    const nome = $('lib-nome').value.trim();
    if (!nome) { alert('Indica il nome del controllo.'); return; }
    const prezzo = +$('lib-prezzo').value || 0;
    if (libMetodi.length === 0) { alert('Aggiungi almeno un metodo.'); return; }
    const metodi = libMetodi.map((m, i) => ({ ...m, progressivo: i + 1 }));

    if (indiceLiberoInModifica != null) {
        // Modifica di una riga già confermata: si aggiornano i campi conservando quantità e il
        // riferimento locale (rifLocale) già assegnato, per non perdere il legame con eventuali
        // raggruppamenti già fatti in precedenza.
        const r = stato.righe[indiceLiberoInModifica];
        r.descrizione = nome;
        r.prezzoBase = prezzo;
        r.scontoPercentuale = +$('lib-sconto').value || 0;
        r.metodi = metodi;
    } else {
        stato.righe.push({
            progressivo: stato.righe.length + 1, tipo: TIPO.LIBERO, rifLocale: prossimoRifLocale(),
            descrizione: nome, originePrezzo: ORIGINE.MANUALE,
            prezzoBase: prezzo, scontoPercentuale: +$('lib-sconto').value || 0, quantita: 1,
            metodi
        });
    }
    chiudiLibero();
    renderRighe();
}

// ═══════════════════════════════ Form ═══════════════════════════════

function nuovo() {
    stato = nuovoStato();
    $('form-titolo').textContent = 'Nuovo preventivo';
    $('form-stato').textContent = '';
    $('p-id').value = ''; $('p-id-cliente').value = '';
    for (const id of ['c-cerca', 'c-ragione', 'c-piva', 'c-indirizzo', 'c-cap', 'c-citta', 'c-prov', 'c-email', 'c-telefono', 'c-nome', 'p-oggetto', 'p-pagamento-cerca', 'p-pagamento', 'p-note']) $(id).value = '';
    $('p-rinnovo').checked = true;
    $('c-pagato').checked = false;
    aggiornaPagato();
    $('p-data').value = new Date().toISOString().slice(0, 10);
    $('p-validita').value = 365; $('p-iva').value = 22;
    $('p-revisione').value = revisioneDefault;
    $('p-promo').checked = false;
    $('p-mostra-insediamenti').checked = false;
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
        idClienteArcLab: p.idClienteArcLab, righe: (p.righe || []).map(r => ({ ...r })),
        idOrigine: p.idPreventivoOrigine ?? null, numeroOrigine: p.numeroOrigine ?? null, insediamenti: []
    };
    await caricaInsediamenti(p.idClienteArcLab);
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
    $('c-telefono').value = p.clienteTelefono ?? '';
    $('c-nome').value = p.clienteNome ?? '';
    $('c-pagato').checked = !!p.giaPagato;
    aggiornaPagato();
    $('p-oggetto').value = p.oggetto ?? '';
    $('p-data').value = p.dataOfferta ? p.dataOfferta.slice(0, 10) : new Date().toISOString().slice(0, 10);
    $('p-validita').value = p.validitaGiorni ?? 365;
    $('p-rinnovo').checked = p.rinnovoTacito !== false;
    $('p-iva').value = p.aliquotaIva ?? 22;
    $('p-pagamento-cerca').value = '';
    $('p-pagamento').value = p.condizioniPagamento ?? '';
    avvisoPagamento();
    $('p-revisione').value = p.revisioneCondizioni ?? revisioneDefault;
    $('p-note').value = p.note ?? '';
    $('p-promo').checked = !!p.riepilogoPromozionale;
    $('p-mostra-insediamenti').checked = !!p.mostraInsediamenti;
    const num = p.numero ? `${p.anno}/${String(p.numero).padStart(3, '0')}` : 'bozza';
    $('form-titolo').textContent = 'Preventivo ' + num;
    $('form-stato').textContent = p.statoTesto + (p.numeroOrigine ? ` · copia del n. ${p.numeroOrigine}` : '');
    renderRighe();
    aggiornaBottoniStato();
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth' });
}

// 26/10/2026: «Duplica» — nuovo preventivo in bozza con le stesse righe e gli stessi prezzi, numero e data di oggi.
// Il numero si assegna al salvataggio. Le righe il cui prezzo di oggi è diverso sono segnalate («oggi: … € usa»).
async function duplica(id) {
    if (id && id !== stato.id) await apri(id);
    if (!stato.id) return;
    const numero = stato.numero ? `${stato.anno}/${String(stato.numero).padStart(3, '0')}` : 'bozza';
    stato.idOrigine = stato.id; stato.numeroOrigine = numero;
    stato.id = null; stato.numero = null; stato.anno = null; stato.statoPrev = STATO.BOZZA;
    stato.righe.forEach(r => { delete r.idRigaPreventivo; delete r.prezzoAttuale; });
    $('p-id').value = '';
    $('p-data').value = new Date().toISOString().slice(0, 10);
    $('p-revisione').value = revisioneDefault;   // le Condizioni Generali in vigore oggi
    $('form-titolo').textContent = 'Nuovo preventivo';
    $('form-stato').textContent = `copia del n. ${numero} · non ancora salvato (puoi cambiare il cliente)`;
    renderRighe();
    aggiornaBottoniStato();
    await segnalaPrezzi();
}

async function segnalaPrezzi() {
    const conControllo = stato.righe.filter(r => r.tipo === TIPO.CONTROLLO && r.idControlloArcLab);
    const ids = [...new Set(conControllo.filter(r => !r.idInsediamentoArcLab).map(r => r.idControlloArcLab))];
    const conSede = conControllo.filter(r => r.idInsediamentoArcLab).map(r => ({ idControllo: r.idControlloArcLab, idInsediamentoArcLab: r.idInsediamentoArcLab }));
    const chiave = r => r.idInsediamentoArcLab ? `${r.idControlloArcLab}-${r.idInsediamentoArcLab}` : String(r.idControlloArcLab);
    const attuali = conControllo.length ? await postJson('/api/preventivi/prezzi-attuali', { idClienteArcLab: stato.idClienteArcLab, idControlli: ids, righe: conSede }) : {};
    for (const r of stato.righe) {
        delete r.prezzoAttuale; delete r.originePrezzoAttuale;
        if (r.tipo === TIPO.CONTROLLO && r.idControlloArcLab && attuali[chiave(r)] != null) r.prezzoAttuale = attuali[chiave(r)];
        if (r.tipo === TIPO.ACCESSORIO && r.idVoceAccessorio) {
            const a = accessori.find(x => x.idVoceAccessorio === r.idVoceAccessorio);
            if (a) { r.prezzoAttuale = a.prezzo; r.originePrezzoAttuale = ORIGINE.LISTINO; }
        }
    }
    renderRighe();
    const n = stato.righe.filter(r => r.prezzoAttuale != null && Math.abs(r.prezzoAttuale - (r.prezzoBase ?? 0)) >= 0.005).length;
    if (n) $('form-stato').textContent += ` · ${n} ${n === 1 ? 'riga ha' : 'righe hanno'} oggi un prezzo diverso`;
}

function aggiornaBottoniStato() {
    const btnDup = $('btn-duplica');
    if (btnDup) btnDup.classList.toggle('nascosto', !stato.id);
    const btnApprova = $('btn-approva');
    if (btnApprova) btnApprova.classList.toggle('nascosto', !(stato.id && stato.statoPrev === STATO.DA_APPROVARE));
    $('btn-elimina').classList.toggle('nascosto', !stato.id);
    $('btn-offerta-word').classList.toggle('nascosto', !stato.id);
    $('btn-offerta-pdf').classList.toggle('nascosto', !stato.id);
}

function scaricaOfferta(formato) {
    if (!stato.id) return;
    window.open(`/api/preventivi/${stato.id}/offerta?formato=${formato}`, '_blank');
}

function corpo(statoRichiesto) {
    return {
        idPreventivo: stato.id,
        idPreventivoOrigine: stato.id ? null : stato.idOrigine,
        stato: statoRichiesto,
        idClienteArcLab: stato.idClienteArcLab,
        clienteRagioneSociale: $('c-ragione').value.trim(),
        clientePartitaIva: $('c-piva').value.trim(),
        clienteIndirizzo: $('c-indirizzo').value.trim(),
        clienteCap: $('c-cap').value.trim(),
        clienteCitta: $('c-citta').value.trim(),
        clienteProvincia: $('c-prov').value.trim(),
        clienteEmail: $('c-email').value.trim(),
        clienteTelefono: $('c-telefono').value.trim(),
        giaPagato: clienteGenerico() && $('c-pagato').checked,
        clienteNome: clienteGenerico() ? $('c-nome').value.trim() : '',
        rinnovoTacito: $('p-rinnovo').checked,
        oggetto: $('p-oggetto').value.trim(),
        dataOfferta: $('p-data').value || null,
        validitaGiorni: +$('p-validita').value || 365,
        aliquotaIva: +$('p-iva').value || 22,
        condizioniPagamento: $('p-pagamento').value.trim(),
        revisioneCondizioni: $('p-revisione').value.trim(),
        note: $('p-note').value.trim(),
        riepilogoPromozionale: $('p-promo').checked,
        mostraInsediamenti: $('p-mostra-insediamenti').checked,
        righe: stato.righe.map((r, i) => ({
            progressivo: i + 1, tipo: r.tipo,
            idControlloArcLab: r.idControlloArcLab ?? null,
            idPropostaControllo: r.idPropostaControllo ?? null,
            idVoceAccessorio: r.idVoceAccessorio ?? null,
            descrizione: r.descrizione, idControlliOrigine: r.idControlliOrigine ?? null,
            rifLocale: r.rifLocale ?? null,
            idInsediamentoArcLab: r.idInsediamentoArcLab ?? null, insediamento: r.insediamento ?? null,
            originePrezzo: r.originePrezzo,
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
    creaComboRicerca({
        input: $('ctrl-input'), box: $('ctrl-suggerimenti'),
        elenco: () => listaControlli,
        testoFn: c => { const f = fasciaCommesse(c.id); return c.testo + (f ? ` · ${f}` : ''); },
        // L'ID controllo è riportato in coda al testo ed è spesso quello che il commerciale conosce
        // meglio: la ricerca lo trova anche digitando solo l'ID, e l'Invio lo risolve per intero.
        // 27/10/2026: con «per ID» spuntato si cercano solo gli ID che iniziano con le cifre digitate.
        filtroFn: (c, q) => $('ctrl-per-id').checked
            ? String(c.id).startsWith(q.replace(/\D/g, '')) && q.replace(/\D/g, '') !== ''
            : c.testo.toLowerCase().includes(q) || String(c.id) === q,
        ordinaFn: (a, b, q) => {
            const qa = String(a.id) === q ? 0 : 1, qb = String(b.id) === q ? 0 : 1;
            if (qa !== qb) return qa - qb;
            return $('ctrl-per-id').checked ? a.id - b.id : 0;
        },
        risolviEsatto: v => listaControlli.find(c => String(c.id) === v),
        onScegli: aggiungiControllo
    });
    creaComboRicerca({
        input: $('acc-input'), box: $('acc-suggerimenti'),
        elenco: () => accessori,
        testoFn: a => `${a.descrizione} — ${eur(a.prezzo)} €${a.unitaMisura ? '/' + a.unitaMisura : ''}`,
        onScegli: aggiungiAccessorio
    });
    creaComboRicerca({
        input: $('lib-m-lab-input'), box: $('lib-m-lab-suggerimenti'),
        elenco: () => labEsterni, testoFn: l => l.nome,
        svuotaDopoScelta: false,
        onScegli: l => { libLabSelezionato = l; }
    });
    creaComboRicerca({
        input: $('p-pagamento-cerca'), box: $('p-pagamento-suggerimenti'),
        elenco: () => metodiPagamento, testoFn: m => m.descrizione,
        // Precompila il testo (poi resta modificabile per questa singola offerta): non sostituisce
        // il campo libero, gli offre solo un punto di partenza già pronto tra le condizioni note.
        // Più modalità di pagamento (12/10/2026): ogni scelta si aggiunge come nuova riga (senza doppioni);
        // con due o più righe nell'offerta compaiono le caselle da spuntare.
        onScegli: m => {
            // Più modalità (12/10/2026): ogni scelta si aggiunge come blocco separato da una riga vuota (senza doppioni).
            const blocchi = bloccoPagamento($('p-pagamento').value);
            const nuovo = m.descrizione.trim();
            if (!blocchi.some(b => b.toLowerCase() === nuovo.toLowerCase())) blocchi.push(nuovo);
            $('p-pagamento').value = blocchi.join('\n\n');
            avvisoPagamento();
        }
    });
    $('p-pagamento').addEventListener('input', avvisoPagamento);
    // Selezione invalidata se l'utente ritocca il testo senza scegliere di nuovo dai suggerimenti.
    $('lib-m-lab-input').addEventListener('input', () => { libLabSelezionato = null; });
    $('btn-acc-libero').addEventListener('click', aggiungiAccessorioLibero);
    $('btn-raggruppa').addEventListener('click', raggruppaSelezionate);
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
    $('btn-duplica').addEventListener('click', () => duplica(stato.id).catch(e => alert(e.message)));
    $('btn-offerta-word').addEventListener('click', () => scaricaOfferta('word'));
    $('btn-offerta-pdf').addEventListener('click', () => scaricaOfferta('pdf'));

    $('r-testo').addEventListener('input', caricaRegistroRitardato);
    $('r-dal').addEventListener('change', caricaRegistroRitardato);
    $('r-al').addEventListener('change', caricaRegistroRitardato);
    $('r-pulisci').addEventListener('click', () => {
        $('r-testo').value = ''; $('r-dal').value = ''; $('r-al').value = '';
        caricaRegistro().catch(e => console.error(e));
    });

    $('ctrl-per-id').addEventListener('change', () => { $('ctrl-input').focus(); $('ctrl-input').dispatchEvent(new Event('input')); });
    caricaPrezziCommesse().then(() => { if (stato.righe.length) renderRighe(); });
    Promise.all([caricaRegistro(), caricaControlli(), caricaAccessori(), caricaLabEsterni(), caricaMetodiPagamento(), caricaImpostazioni()])
        .catch(e => console.error(e));
}

document.addEventListener('DOMContentLoaded', avvia);

// Più modalità di pagamento (12/10/2026): modalità separate da una riga vuota; dice cosa comparirà nell'offerta.
function bloccoPagamento(testo) {
    return testo.replace(/\r/g, '').split(/\n\s*\n/).map(b => b.split('\n').map(s => s.trim()).filter(s => s).join('\n')).filter(b => b);
}
function avvisoPagamento() {
    const n = bloccoPagamento($('p-pagamento').value).length;
    $('p-pagamento-avviso').textContent = n > 1
        ? `${n} modalità: nell'offerta compaiono con la casella da spuntare e la frase «Condizioni di pagamento – indicare con una X la modalità scelta».`
        : "Per proporre più modalità sceglile dall'elenco: si aggiungono separate da una riga vuota (ognuna può avere più righe, es. banca e IBAN).";
}

// 21/10/2026: «Già pagato» solo per il cliente generico («Spett.le cliente», ragione sociale che inizia con «Spett»).
function clienteGenerico() {
    const r = $('c-ragione').value.trim();
    return r === '' || /^spett/i.test(r);
}
function aggiornaPagato() {
    $('c-pagato-riga').classList.toggle('nascosto', !clienteGenerico());
    $('c-nome-riga').classList.toggle('nascosto', !clienteGenerico());
}
$('c-ragione').addEventListener('input', aggiornaPagato);
