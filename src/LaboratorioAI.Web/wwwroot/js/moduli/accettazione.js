// Accettazione campioni fuori sede — maschera + staging + tracciato XML.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const num = v => { if (v === '' || v == null) return null; const n = Number(String(v).replace(',', '.')); return isNaN(n) ? null : n; };
const dataIt = d => d ? new Date(d).toLocaleDateString('it-IT') : '—';

let termNeg = '', termPos = ''; // nomi termometri da ArcLab (contengono 463 / 446)

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
function opt(sel, voci, vuoto) {
    sel.innerHTML = `<option value="">${vuoto || '— seleziona —'}</option>` +
        voci.map(v => `<option value="${v.id}">${esc(v.nome)}</option>`).join('');
}

let clienti = [], matrici = [], controlli = [], prelevatori = [], ricevitori = [], labEsterni = [];
let commesse = [], commessaControllo = null, statoCorrente = 'Bozza';
let matriciTecnica = {};   // idMatrice → tecnica di prelievo (procedura automatica)
const righeCache = {};
const snapDup = new WeakMap();   // campioni duplicati → snapshot per rilevare "nessuna variazione"

async function righeControllo(idControllo) {
    if (!idControllo) return [];
    if (righeCache[idControllo]) return righeCache[idControllo];
    try { const r = await api('/api/accettazione/righe-controllo?idControllo=' + idControllo); righeCache[idControllo] = r; return r; }
    catch { return []; }
}
function caricaSuCampione(el, righe) {
    const tbody = el.querySelector('.c-prove tbody');
    const presenti = new Set([...tbody.querySelectorAll('.prova')].map(tr => tr.dataset.idrigametodo).filter(Boolean));
    righe.forEach(r => {
        if (presenti.has(String(r.idRigametodo))) return;   // no duplicati per campione
        addProva(tbody, {
            parametro: r.parametro, metodo: r.denominazioneRiga, laboratorioEsterno: r.laboratorioEsterno,
            idRigaMetodo: r.idRigametodo, idMetodo: r.idMetodo, idControllo: r.idControllo
        });
        presenti.add(String(r.idRigametodo));
    });
}
// rimuove dal campione le sole righe caricate in automatico da un controllo
// (hanno dataset.idrigametodo); le prove aggiunte a mano restano.
function svuotaProveAuto(el) {
    el.querySelectorAll('.c-prove tbody .prova').forEach(tr => { if (tr.dataset.idrigametodo) tr.remove(); });
}
async function onCommessa() {
    const c = commesse.find(x => String(x.id) === $('a-commessa').value);
    commessaControllo = c ? c.idControllo : null;
    const righe = commessaControllo ? await righeControllo(commessaControllo) : [];
    $('campioni').querySelectorAll('.campione').forEach(el => {
        svuotaProveAuto(el);                 // via le righe del controllo precedente
        if (righe.length) caricaSuCampione(el, righe);   // dentro quelle del nuovo
        aggiornaProveEst(el);                // ricalcola il laboratorio esterno automatico
    });
}

async function initRif() {
    [clienti, matrici, controlli] = await Promise.all([
        api('/api/accettazione/clienti'), api('/api/accettazione/matrici'), api('/api/accettazione/controlli')
    ]);
    $('lista-clienti').innerHTML = clienti.map(c => `<option value="${esc(c.nome)}">`).join('');
    $('lista-matrici').innerHTML = matrici.map(m => `<option value="${esc(m.nome)}">`).join('');
    $('lista-controlli').innerHTML = controlli.map(c => `<option value="${esc(c.nome)}">`).join('');
    try { const imb = await api('/api/accettazione/imballaggi'); $('lista-imballaggi').innerHTML = imb.map(i => `<option value="${esc(i.nome)}">`).join(''); } catch { }
    try { matriciTecnica = {}; (await api('/api/accettazione/matrici-tecnica')).forEach(x => matriciTecnica[x.id] = x.nome); } catch { matriciTecnica = {}; }
    try { labEsterni = await api('/api/accettazione/laboratori-esterni'); } catch { labEsterni = []; }
    const ops = await api('/api/accettazione/operatori');
    prelevatori = ops.filter(o => o.attivita === 1);
    ricevitori = ops.filter(o => o.attivita === 2);
    try {
        const term = await api('/api/accettazione/termometri');
        termNeg = (term.find(t => (t.nome || '').includes('463')) || {}).nome || '';
        termPos = (term.find(t => (t.nome || '').includes('446')) || {}).nome || '';
    } catch { }
    // prelevatore: interni + Cliente + Terza parte
    $('a-prelevatore').innerHTML = `<option value="">— seleziona —</option>` +
        prelevatori.map(o => `<option value="${o.id}">${esc(o.nome)}</option>`).join('') +
        `<option value="cliente">Cliente</option><option value="terzaparte">Terza parte</option>`;
    opt($('a-ricevitore'), ricevitori, '— nessuno —');
}

const trovaId = (lista, nome) => { const n = (nome || '').trim().toLowerCase(); const v = lista.find(x => (x.nome || '').trim().toLowerCase() === n); return v ? v.id : null; };
const prelInterno = () => { const v = $('a-prelevatore').value; return v !== '' && v !== 'cliente' && v !== 'terzaparte'; };
const prelTesto = () => { const s = $('a-prelevatore'); return s.value ? s.options[s.selectedIndex].text : null; };

function aggiornaPrelevatore() {
    const interno = prelInterno();
    const cliente = $('a-prelevatore').value === 'cliente' || $('a-prelevatore').value === 'terzaparte';
    $('a-vettore').value = interno ? prelTesto() : (cliente ? 'Cliente' : '');
    const verb = $('a-verbale');
    if (cliente) { verb.value = ''; verb.disabled = true; verb.placeholder = 'non richiesto (prelevatore cliente)'; }
    else { verb.disabled = false; verb.placeholder = interno ? 'automatico (numero interno), modificabile' : ''; }
    // il termometro dipende dal prelevatore: ricalcolo tutti i campioni
    $('campioni').querySelectorAll('.campione').forEach(aggiornaAuto);
}

// ── temp. ricezione, termometro, condizioni ──
function numTemp(tr) { const t = (tr || '').replace('+', '').replace(',', '.'); const n = Number(t); return t !== '' && !isNaN(n) ? n : null; }
function condizioni(tr) { const n = numTemp(tr); if (n === null) return 'Temp. ambiente'; return n < 0 ? 'Surgelato' : 'Refrigerato'; }
function termometro(interno, tr) { const n = numTemp(tr); if (n === null) return ''; if (!interno) return termNeg; return n < 0 ? termNeg : termPos; }
function aggiornaAuto(el) {
    const tr = el.querySelector('.c-tricez').value;
    el.querySelector('.c-condizioni').value = condizioni(tr);
    el.querySelector('.c-termometro').value = termometro(prelInterno(), tr);
    const dt = el.querySelector('.c-dataora').value;
    el.querySelector('.c-ora').value = dt ? dt.substring(11, 16) : '';
    // inizio analisi INDICATIVO = oggi + ritardo (la ricezione definitiva la assegna il sync)
    const rit = parseInt(el.querySelector('.c-ritardo').value) || 0;
    const ini = el.querySelector('.c-inizio');
    if (ini) { const d = new Date(); d.setDate(d.getDate() + rit); ini.value = d.toLocaleDateString('it-IT'); }
}
function opzioniTempRicezione(sel, val) {
    let o = '<option value="Ambiente">Ambiente</option>';
    for (let t = -20; t <= 10; t++) o += `<option value="${t}">${t > 0 ? '+' + t : t}</option>`;
    sel.innerHTML = o;
    sel.value = val || '';
}

// ── cliente → insediamenti ──
async function onCliente() {
    opt($('a-commessa'), [], '— scegli prima l\'insediamento —'); commesse = []; commessaControllo = null;
    const id = trovaId(clienti, $('a-cliente').value);
    if (!id) { opt($('a-insediamento'), [], '— nessuno —'); return; }
    try { opt($('a-insediamento'), await api('/api/accettazione/insediamenti?idCliente=' + id), '— nessuno —'); }
    catch (e) { opt($('a-insediamento'), [], '— errore —'); console.error('insediamenti', e); }
}

// ── insediamento → commesse (solo di quell'insediamento) ──
async function onInsediamento() {
    commessaControllo = null;
    const idIns = $('a-insediamento').value;
    if (!idIns) { opt($('a-commessa'), [], '— nessuna —'); commesse = []; return; }
    try { commesse = await api('/api/accettazione/commesse?idInsediamento=' + idIns); opt($('a-commessa'), commesse, '— nessuna —'); }
    catch (e) { commesse = []; opt($('a-commessa'), [], '— errore —'); console.error('commesse', e); }
}

// procedura di prelievo automatica dalla matrice (Matrice.Id_IOCampionamento → TecnicaPrelievo)
function aggiornaProcedura(el) {
    const idm = trovaId(matrici, el.querySelector('.c-matrice').value);
    el.querySelector('.c-procedura').value = (idm && matriciTecnica[idm]) || '';
}
// il laboratorio esterno è automatico: è quello riportato dai parametri affidati all'esterno.
// Mostra solo il nome (readonly); l'id sta nel campo nascosto .c-labest.
function aggiornaLabEst(el) {
    const righe = [...el.querySelectorAll('.c-prove tbody .prova')].filter(tr => (tr.querySelector('.p-lab').value || '').trim() !== '');
    const nomeLab = righe.length ? (righe[0].querySelector('.p-lab').value || '').trim() : '';
    const hid = el.querySelector('.c-labest'), nome = el.querySelector('.c-labest-nome');
    if (!nomeLab) { hid.value = ''; nome.textContent = '—'; return; }
    const key = nomeLab.toLowerCase();
    const voce = labEsterni.find(l => (l.nome || '').toLowerCase().startsWith(key) || key.startsWith((l.nome || '').toLowerCase().split(' ')[0]));
    hid.value = voce ? voce.id : '';
    nome.textContent = voce ? voce.nome : nomeLab;
}
// compat: il cambio prove aggiorna solo il lab esterno automatico
const aggiornaProveEst = el => aggiornaLabEst(el);
// firma dei campi del campione (per rilevare "nessuna variazione" su un duplicato)
function firmaCampione(el) {
    const auto = ['c-ora', 'c-inizio', 'c-termometro', 'c-condizioni', 'c-procedura', 'c-labest', 'c-labest-nome'];
    return [...el.querySelectorAll('input, select, textarea')]
        .filter(x => !auto.some(c => x.classList.contains(c)))
        .map(x => x.type === 'checkbox' ? (x.checked ? '1' : '0') : x.value).join('¦');
}

// ── Campioni / prove dinamici ──
function addProva(tbody, p) {
    const tr = $('tpl-prova').content.firstElementChild.cloneNode(true);
    if (p) {
        tr.querySelector('.p-parametro').value = p.parametro || '';
        tr.querySelector('.p-metodo').value = p.metodo || '';
        tr.querySelector('.p-lab').value = p.laboratorioEsterno || '';
        tr.querySelector('.p-note').value = p.note || '';
        const idrm = p.idRigaMetodoArcLab || p.idRigaMetodo; if (idrm) tr.dataset.idrigametodo = idrm;
        const idm = p.idMetodoArcLab || p.idMetodo; if (idm) tr.dataset.idmetodo = idm;
        const idc = p.idControlloArcLab || p.idControllo; if (idc) tr.dataset.idcontrollo = idc;
        if (p.controllo) tr.dataset.controllo = p.controllo;
    }
    tr.querySelector('.p-rimuovi').addEventListener('click', () => tr.remove());
    tbody.appendChild(tr);
}
function addCampione(c, opts) {
    const el = $('tpl-campione').content.firstElementChild.cloneNode(true);
    const tbody = el.querySelector('.c-prove tbody');
    opt(el.querySelector('.c-labest'), labEsterni, '— nessuno —');
    el.querySelector('.c-rimuovi').addEventListener('click', () => { el.remove(); rinumera(); });
    el.querySelector('.c-duplica').addEventListener('click', () => duplicaCampione(el));
    el.querySelector('.c-prove tbody').addEventListener('input', e => { if (e.target.classList.contains('p-lab')) aggiornaProveEst(el); });
    el.querySelector('.c-pdf-richiesta').addEventListener('click', () => pdfCampione('richiesta', el));
    el.querySelector('.c-pdf-catena').addEventListener('click', () => pdfCampione('catena', el));
    el.querySelector('.c-pdf-etichetta').addEventListener('click', () => pdfCampione('etichetta', el));
    el.querySelector('.c-pdf-scheda').addEventListener('click', () => pdfCampione('scheda', el));
    opzioniTempRicezione(el.querySelector('.c-tricez'), c ? c.tempRicezione : '');
    el.querySelector('.c-tricez').addEventListener('change', () => aggiornaAuto(el));
    el.querySelector('.c-dataora').addEventListener('change', () => aggiornaAuto(el));
    el.querySelector('.c-ritardo').addEventListener('input', () => aggiornaAuto(el));
    el.querySelector('.c-matrice').addEventListener('change', () => aggiornaProcedura(el));
    el.querySelector('.c-matrice').addEventListener('input', () => aggiornaProcedura(el));
    // flag shelf life / inizio posticipato: mostra i relativi campi solo se attivo
    const shelf = el.querySelector('.c-shelf-flag'), shelfBox = el.querySelector('.c-shelf-box');
    shelf.addEventListener('change', () => shelfBox.classList.toggle('nascosto', !shelf.checked));
    if (c) {
        el.querySelector('.c-descrizione').value = c.descrizione || '';
        el.querySelector('.c-matrice').value = c.matrice || '';
        el.querySelector('.c-etichetta').value = c.etichettaCliente || '';
        el.querySelector('.c-sigla').value = c.siglaCliente || '';
        el.querySelector('.c-identificativo').value = c.identificativo || '';
        el.querySelector('.c-imballaggio').value = c.imballaggio || '';
        el.querySelector('.c-legio').value = c.cameraLegionella || '';
        el.querySelector('.c-consegna').value = c.dataPrevistaConsegna ? c.dataPrevistaConsegna.substring(0, 10) : '';
        el.querySelector('.c-ritardo').value = c.ritardoAnalisiGiorni ?? '';
        el.querySelector('.c-dataora').value = c.dataPrelievo ? c.dataPrelievo.substring(0, 16) : '';
        el.querySelector('.c-luogo').value = c.luogoPrelievo || '';
        el.querySelector('.c-punto').value = c.puntoPrelievo || '';
        el.querySelector('.c-temp').value = c.temperatura ?? '';
        el.querySelector('.c-note').value = c.note || '';
        el.querySelector('.c-datainvio').value = c.dataInvioLab ? c.dataInvioLab.substring(0, 10) : '';
        el.querySelector('.c-consegnalab').value = c.consegnaPrevistaLabEstGiorni ?? '';
        el.querySelector('.c-notelab').value = c.noteLabEsterno || '';
        if (c.ritardoAnalisiGiorni != null && c.ritardoAnalisiGiorni !== '') { shelf.checked = true; shelfBox.classList.remove('nascosto'); }
        (c.prove || []).forEach(p => addProva(tbody, p));
    } else addProva(tbody);
    $('campioni').appendChild(el);
    aggiornaProcedura(el);
    aggiornaAuto(el);
    aggiornaProveEst(el);
    rinumera();
    // campione nuovo: carica in automatico le righe metodo della commessa (senza duplicati)
    if (!c && !(opts && opts.noAuto) && commessaControllo) righeControllo(commessaControllo).then(r => { caricaSuCampione(el, r); aggiornaProveEst(el); });
    return el;
}

// duplica un campione copiandone tutti i valori; se poi non viene modificato, al salvataggio avvisa
function duplicaCampione(src) {
    const dati = leggiCampione(src);
    const el = addCampione(dati, { noAuto: true });     // stessa struttura, niente ricarica auto
    snapDup.set(el, firmaCampione(el));                 // firma iniziale = identica al sorgente
    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
}
// legge i valori di un campione dal DOM in un oggetto (come quello del server)
function leggiCampione(el) {
    return {
        descrizione: el.querySelector('.c-descrizione').value,
        matrice: el.querySelector('.c-matrice').value,
        etichettaCliente: el.querySelector('.c-etichetta').value,
        siglaCliente: el.querySelector('.c-sigla').value,
        identificativo: el.querySelector('.c-identificativo').value,
        imballaggio: el.querySelector('.c-imballaggio').value,
        proceduraPrelievo: el.querySelector('.c-procedura').value,
        cameraLegionella: el.querySelector('.c-legio').value,
        dataPrevistaConsegna: el.querySelector('.c-consegna').value,
        ritardoAnalisiGiorni: el.querySelector('.c-ritardo').value,
        dataPrelievo: el.querySelector('.c-dataora').value,
        luogoPrelievo: el.querySelector('.c-luogo').value,
        puntoPrelievo: el.querySelector('.c-punto').value,
        temperatura: el.querySelector('.c-temp').value,
        tempRicezione: el.querySelector('.c-tricez').value,
        note: el.querySelector('.c-note').value,
        idLabEsternoArcLab: el.querySelector('.c-labest').value,
        dataInvioLab: el.querySelector('.c-datainvio').value,
        consegnaPrevistaLabEstGiorni: el.querySelector('.c-consegnalab').value,
        noteLabEsterno: el.querySelector('.c-notelab').value,
        prove: [...el.querySelectorAll('.c-prove tbody .prova')].map(tr => ({
            parametro: tr.querySelector('.p-parametro').value, metodo: tr.querySelector('.p-metodo').value,
            laboratorioEsterno: tr.querySelector('.p-lab').value, note: tr.querySelector('.p-note').value,
            idRigaMetodoArcLab: tr.dataset.idrigametodo, idMetodoArcLab: tr.dataset.idmetodo,
            idControlloArcLab: tr.dataset.idcontrollo, controllo: tr.dataset.controllo
        }))
    };
}
function rinumera() {
    [...$('campioni').querySelectorAll('.campione')].forEach((el, i) => el.querySelector('.campione-titolo').textContent = 'Campione n°' + (i + 1));
}

function raccogli() {
    const idIns = $('a-insediamento').value, idCom = $('a-commessa').value, idRic = $('a-ricevitore').value;
    const testo = sel => sel.value ? sel.options[sel.selectedIndex].text : null;
    const interno = prelInterno();
    return {
        idAccettazioneLotto: +$('a-id').value || null,
        codiceLocale: $('a-codice').value || null,
        idClienteArcLab: trovaId(clienti, $('a-cliente').value),
        cliente: $('a-cliente').value || null,
        idInsediamentoArcLab: idIns ? +idIns : null, insediamento: testo($('a-insediamento')),
        idCommessaArcLab: idCom ? +idCom : null, commessa: testo($('a-commessa')),
        dataOraRicezione: null,   // assegnata alla sincronizzazione da ArcLab
        idPrelevatore: interno ? +$('a-prelevatore').value : null,
        prelevatore: prelTesto(),
        verbalePrelievo: $('a-verbale').value || null,
        idRicevitore: idRic ? +idRic : null, ricevitore: testo($('a-ricevitore')),
        note: $('a-note').value || null,
        origine: 'consulente',
        campioni: [...$('campioni').querySelectorAll('.campione')].map((el, i) => ({
            progressivo: i + 1,
            idMatriceArcLab: trovaId(matrici, el.querySelector('.c-matrice').value),
            matrice: el.querySelector('.c-matrice').value || null,
            descrizione: el.querySelector('.c-descrizione').value || null,
            etichettaCliente: el.querySelector('.c-etichetta').value || null,
            siglaCliente: el.querySelector('.c-sigla').value || null,
            identificativo: el.querySelector('.c-identificativo').value || null,
            imballaggio: el.querySelector('.c-imballaggio').value || null,
            proceduraPrelievo: el.querySelector('.c-procedura').value || null,
            cameraLegionella: el.querySelector('.c-legio').value || null,
            dataPrevistaConsegna: el.querySelector('.c-consegna').value || null,
            ritardoAnalisiGiorni: num(el.querySelector('.c-ritardo').value),
            dataPrelievo: el.querySelector('.c-dataora').value || null,
            luogoPrelievo: el.querySelector('.c-luogo').value || null,
            puntoPrelievo: el.querySelector('.c-punto').value || null,
            temperatura: num(el.querySelector('.c-temp').value),
            tempRicezione: el.querySelector('.c-tricez').value || null,
            note: el.querySelector('.c-note').value || null,
            idLabEsternoArcLab: el.querySelector('.c-labest').value ? +el.querySelector('.c-labest').value : null,
            labEsterno: (el.querySelector('.c-labest-nome').textContent || '').replace('—', '').trim() || null,
            dataInvioLab: el.querySelector('.c-datainvio').value || null,
            noteLabEsterno: el.querySelector('.c-notelab').value || null,
            consegnaPrevistaLabEstGiorni: num(el.querySelector('.c-consegnalab').value),
            prove: [...el.querySelectorAll('.c-prove tbody .prova')].map(tr => ({
                idControlloArcLab: tr.dataset.idcontrollo ? +tr.dataset.idcontrollo : null,
                controllo: tr.dataset.controllo || null,
                idMetodoArcLab: tr.dataset.idmetodo ? +tr.dataset.idmetodo : null,
                idRigaMetodoArcLab: tr.dataset.idrigametodo ? +tr.dataset.idrigametodo : null,
                metodo: tr.querySelector('.p-metodo').value || null,
                parametro: tr.querySelector('.p-parametro').value || null,
                laboratorioEsterno: tr.querySelector('.p-lab').value || null,
                note: tr.querySelector('.p-note').value || null
            })).filter(p => p.parametro || p.metodo)
        }))
    };
}

function validaClient(d) {
    if (!d.cliente) return 'Seleziona il cliente.';
    if (!d.prelevatore) return 'Indicare il prelevatore.';
    if (!d.campioni.length) return 'Aggiungi almeno un campione.';
    for (let i = 0; i < d.campioni.length; i++) {
        const c = d.campioni[i], n = i + 1;
        if (!c.matrice) return `Campione n°${n}: la matrice è obbligatoria.`;
        if (!c.descrizione) return `Campione n°${n}: la denominazione è obbligatoria.`;
        if (!c.imballaggio) return `Campione n°${n}: l'imballaggio è obbligatorio.`;
        if (c.temperatura === null) return `Campione n°${n}: la temperatura di prelievo è obbligatoria.`;
        if (!c.dataPrelievo) return `Campione n°${n}: la data/ora di prelievo è obbligatoria.`;
    }
    return null;
}

// se un campione duplicato non è stato modificato, chiedi conferma prima di procedere
function confermaDuplicatiNonModificati() {
    const invariati = [...$('campioni').querySelectorAll('.campione')]
        .map((el, i) => ({ el, i }))
        .filter(({ el }) => snapDup.has(el) && firmaCampione(el) === snapDup.get(el));
    if (!invariati.length) return true;
    const nums = invariati.map(({ i }) => 'n°' + (i + 1)).join(', ');
    return confirm(`I campioni duplicati ${nums} non presentano alcuna variazione rispetto all'originale.\n\nVuoi continuare comunque senza modifiche?`);
}

async function salva() {
    if (!confermaDuplicatiNonModificati()) return null;
    const corpo = raccogli();
    const err = validaClient(corpo);
    if (err) { alert(err); return null; }
    try {
        const r = await api('/api/accettazione/salva', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
        $('a-id').value = r.idAccettazioneLotto; $('a-codice').value = r.codiceLocale;
        if (r.verbalePrelievo) $('a-verbale').value = r.verbalePrelievo;
        statoCorrente = r.stato;
        $('form-stato').textContent = 'Stato: ' + r.stato + (r.verbalePrelievo ? '  —  Verbale: ' + r.verbalePrelievo : '');
        caricaRegistro();
        return r;
    } catch (e) { alert(e.message); return null; }
}

async function generaTracciato() {
    const r = await salva();
    if (!r) return;
    const resp = await fetch(`/api/accettazione/${r.idAccettazioneLotto}/tracciato`, { method: 'POST' });
    if (!resp.ok) { let m = 'Errore'; try { m = (await resp.json()).messaggio; } catch { } alert(m); return; }
    const blob = await resp.blob();
    const cd = resp.headers.get('content-disposition') || '';
    const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || 'Accettazione.xml';
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
    URL.revokeObjectURL(a.href);
    $('pannello-form').classList.add('nascosto');
    caricaRegistro();
}

// PDF (richiesta lab esterno / catena di custodia / etichette): generatori in arrivo.
// Il campione va prima salvato: qui salviamo, poi scaricheremo il PDF dall'endpoint dedicato.
const PDF_PRONTI = true;
async function scaricaPdf(url, nomeDefault) {
    const resp = await fetch(url, { method: 'POST' });
    if (!resp.ok) { let m = 'Errore'; try { m = (await resp.json()).messaggio; } catch { } alert(m); return; }
    const blob = await resp.blob();
    const cd = resp.headers.get('content-disposition') || '';
    const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || nomeDefault;
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
    URL.revokeObjectURL(a.href);
}
// restituisce l'id del lotto da stampare: se già trasmesso/importato stampa dai dati
// salvati (niente risalvataggio, che sarebbe vietato); altrimenti salva le modifiche.
const trasmesso = () => statoCorrente === 'Trasmesso' || statoCorrente === 'Importato';
async function assicuraId() {
    if (trasmesso()) { const id = +$('a-id').value; if (!id) { alert('Accettazione non salvata.'); return null; } return id; }
    const r = await salva(); return r ? r.idAccettazioneLotto : null;
}
async function pdfCampione(tipo, el) {
    if (!PDF_PRONTI) { alert('La generazione PDF (' + tipo + ') sarà disponibile a breve.'); return; }
    // etichetta e scheda: chiedi se stampare tutto il lotto o solo questo campione
    if (tipo === 'etichetta' || tipo === 'scheda') {
        const tutti = confirm('Stampare per TUTTI i campioni del lotto?\n\nOK = tutto il lotto   —   Annulla = solo questo campione');
        if (tutti) { await pdfLotto(tipo === 'etichetta' ? 'etichette' : 'schede', tipo === 'etichetta' ? 'Etichette_lotto.pdf' : 'Schede_ricezione_lotto.pdf'); return; }
    }
    const prog = [...$('campioni').querySelectorAll('.campione')].indexOf(el) + 1;
    const id = await assicuraId(); if (!id) return;
    await scaricaPdf(`/api/accettazione/${id}/campione/${prog}/pdf/${tipo}`, `Campione_${prog}_${tipo}.pdf`);
}
async function pdfLotto(tipo, nomeDefault) {
    if (!PDF_PRONTI) { alert('La stampa di lotto (' + tipo + ') sarà disponibile a breve.'); return; }
    const id = await assicuraId(); if (!id) return;
    await scaricaPdf(`/api/accettazione/${id}/pdf/${tipo}`, nomeDefault);
}
const pdfEtichetteLotto = () => pdfLotto('etichette', 'Etichette_lotto.pdf');
const pdfSchedeLotto = () => pdfLotto('schede', 'Schede_ricezione_lotto.pdf');

// verifica che i riferimenti ArcLab dell'accettazione esistano ancora (import pulito)
async function verificaImportabilita() {
    const id = await assicuraId(); if (!id) return;
    const box = $('esito-verifica');
    box.innerHTML = '<span class="sottotesto">Verifica in corso…</span>';
    try {
        const r = await api(`/api/accettazione/${id}/verifica`, { method: 'POST' });
        if (r.ok) {
            box.innerHTML = '<div style="color:#1a7f37;font-weight:600">✓ Tutti i riferimenti ArcLab sono validi: il tracciato è importabile.</div>';
        } else {
            box.innerHTML = '<div style="color:#b21a1a;font-weight:600">Riferimenti da sistemare prima dell\'import:</div><ul>'
                + r.problemi.map(p => `<li>${esc(p)}</li>`).join('') + '</ul>';
        }
    } catch (e) { box.innerHTML = `<div style="color:#b21a1a">${esc(e.message)}</div>`; }
}

function nuova() {
    $('a-id').value = ''; $('a-codice').value = '';
    ['a-cliente', 'a-note', 'a-verbale', 'a-vettore'].forEach(id => $(id).value = '');
    opt($('a-insediamento'), [], '— nessuno —'); opt($('a-commessa'), [], '— nessuna —');
    $('a-prelevatore').value = ''; $('a-ricevitore').value = '';
    aggiornaPrelevatore();
    $('campioni').innerHTML = '';
    statoCorrente = 'Bozza';
    addCampione();
    $('form-titolo').textContent = 'Nuova accettazione'; $('form-stato').textContent = '';
    $('pannello-form').classList.remove('nascosto');
}

async function apri(id) {
    const l = await api('/api/accettazione/' + id);
    $('a-id').value = l.idAccettazioneLotto; $('a-codice').value = l.codiceLocale;
    $('a-cliente').value = l.cliente || '';
    $('a-note').value = l.note || '';
    await onCliente();
    if (l.idInsediamentoArcLab) { $('a-insediamento').value = l.idInsediamentoArcLab; await onInsediamento(); }
    if (l.idCommessaArcLab) { $('a-commessa').value = l.idCommessaArcLab; commessaControllo = (commesse.find(x => String(x.id) === String(l.idCommessaArcLab)) || {}).idControllo || null; }
    if (l.idPrelevatore) $('a-prelevatore').value = l.idPrelevatore;
    else if ((l.prelevatore || '').toLowerCase().includes('terza')) $('a-prelevatore').value = 'terzaparte';
    else if ((l.prelevatore || '').toLowerCase() === 'cliente') $('a-prelevatore').value = 'cliente';
    if (l.idRicevitore) $('a-ricevitore').value = l.idRicevitore;
    $('a-verbale').value = l.verbalePrelievo || '';
    aggiornaPrelevatore();
    $('campioni').innerHTML = '';
    (l.campioni || []).sort((a, b) => a.progressivo - b.progressivo).forEach(c => addCampione(c));
    if (!l.campioni || !l.campioni.length) addCampione();
    statoCorrente = l.stato;
    $('form-titolo').textContent = 'Accettazione ' + (l.verbalePrelievo || l.commessa || '');
    $('form-stato').textContent = 'Stato: ' + l.stato + (l.verbalePrelievo ? '  —  Verbale: ' + l.verbalePrelievo : '');
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
}

const badge = s => ({ Bozza: '#666', DaTrasmettere: '#b26a00', Trasmesso: '#1a7f37', Importato: '#1741b3' }[s] || '#666');

async function caricaRegistro() {
    const righe = await api('/api/accettazione/elenco');
    $('conteggio').textContent = righe.length + ' accettazioni';
    $('tabella-registro').tBodies[0].innerHTML = righe.map(l => `<tr>
        <td style="color:${badge(l.stato)};font-weight:600">${esc(l.stato)}</td>
        <td>${esc(l.verbalePrelievo)}</td>
        <td>${esc(l.cliente)}</td><td>${esc(l.commessa)}</td><td>${dataIt(l.dataConferimento)}</td>
        <td class="numerico">${l.numeroCampioni}</td><td>${esc(l.origine)}</td>
        <td>
            <button class="pulsante pulsante-tenue" data-apri="${l.idAccettazioneLotto}">Apri</button>
            ${l.stato === 'Bozza' || l.stato === 'DaTrasmettere' ? `<button class="pulsante pulsante-tenue" data-del="${l.idAccettazioneLotto}">Elimina</button>` : ''}
        </td></tr>`).join('') || '<tr><td colspan="8">Nessuna accettazione. Premi «Nuova accettazione».</td></tr>';
    document.querySelectorAll('#tabella-registro [data-apri]').forEach(b => b.addEventListener('click', () => apri(+b.dataset.apri)));
    document.querySelectorAll('#tabella-registro [data-del]').forEach(b => b.addEventListener('click', async () => {
        if (!confirm('Eliminare l\'accettazione?')) return;
        await api(`/api/accettazione/${b.dataset.del}/elimina`, { method: 'POST' }); caricaRegistro();
    }));
}

// ── Esploso del controllo della commessa ──
async function mostraEsploso() {
    if (!commessaControllo) { alert('Seleziona prima una commessa: il controllo è quello associato.'); return; }
    const righe = await righeControllo(commessaControllo);
    const c = commesse.find(x => String(x.id) === $('a-commessa').value);
    $('dlg-esploso-titolo').textContent = 'Esploso controllo — ' + (c ? c.nome : commessaControllo);
    $('dlg-esploso-corpo').innerHTML = righe.length
        ? righe.map(r => `<tr><td>${esc(r.parametro)}</td><td>${esc(r.denominazioneRiga)}</td><td>${esc(r.laboratorioEsterno)}</td></tr>`).join('')
        : '<tr><td colspan="3">Nessuna riga metodo per questo controllo.</td></tr>';
    const dlg = $('dlg-esploso');
    if (typeof dlg.showModal === 'function') dlg.showModal(); else dlg.setAttribute('open', '');
}

// ── Avvio ──
$('btn-esploso')?.addEventListener('click', mostraEsploso);
$('dlg-esploso-chiudi')?.addEventListener('click', () => $('dlg-esploso').close());
$('btn-nuova')?.addEventListener('click', nuova);
$('btn-annulla')?.addEventListener('click', () => $('pannello-form').classList.add('nascosto'));
$('btn-campione')?.addEventListener('click', () => addCampione());
$('btn-etichette-lotto')?.addEventListener('click', pdfEtichetteLotto);
$('btn-schede-lotto')?.addEventListener('click', pdfSchedeLotto);
$('btn-salva')?.addEventListener('click', salva);
$('btn-verifica')?.addEventListener('click', verificaImportabilita);
$('btn-tracciato')?.addEventListener('click', generaTracciato);
$('a-cliente')?.addEventListener('change', onCliente);
$('a-insediamento')?.addEventListener('change', onInsediamento);
$('a-commessa')?.addEventListener('change', onCommessa);
$('a-prelevatore')?.addEventListener('change', aggiornaPrelevatore);
initRif().then(caricaRegistro);
