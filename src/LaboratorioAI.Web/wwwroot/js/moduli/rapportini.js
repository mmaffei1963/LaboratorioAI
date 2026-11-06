// Rapportini di consulenza: compilazione al sopralluogo, invio al cliente (Palomar), gestione e conferma.
import { disegnaRegistro } from './registro-documenti.js';

const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const GESTORE = $('pannello-elenco')?.dataset.gestore === '1';
const euro = v => (v ?? 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dataIt = s => s ? s.split('-').reverse().join('/') : '';
const oggi = () => { const d = new Date(); d.setMinutes(d.getMinutes() - d.getTimezoneOffset()); return d.toISOString().slice(0, 10); };

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const invia = (url, metodo, corpo) => api(url, { method: metodo, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo ?? {}) });

function msg(el, testo, errore = false) { const e = $(el); e.textContent = testo || ''; e.style.color = errore ? '#b00' : ''; }

const COLORI_STATO = {
    Bozza: ['#eee', '#333'], Inviato: ['#e8eef7', '#1f3b63'], Contestato: ['#f8d7da', '#842029'],
    Confermato: ['#e7f5e7', '#256029'], Rendicontato: ['#eef6ee', '#3d6b3d'], InPrefatturato: ['#fff3cd', '#7a5b00'],
    Fatturato: ['#dfe9f5', '#1f3b63'], Annullato: ['#f3f3f3', '#888']
};
const NOMI_STATO = { InPrefatturato: 'In prefatturato' };
const etichettaStato = s => { const [b, c] = COLORI_STATO[s] || ['#eee', '#333']; return `<span class="etichetta" style="background:${b};color:${c}">${esc(NOMI_STATO[s] || s)}</span>`; };

// ═══════════════════════════════ Elenco ═══════════════════════════════
let righe = [];
const selezionati = new Set();
const confermabile = r => r.stato === 'Inviato' || r.stato === 'Contestato';

async function carica() {
    msg('msg', 'Caricamento…');
    const p = new URLSearchParams();
    if ($('f-dal').value) p.set('dal', $('f-dal').value);
    if ($('f-al').value) p.set('al', $('f-al').value);
    if ($('f-stato').value) p.set('stato', $('f-stato').value);
    if ($('f-testo').value.trim()) p.set('testo', $('f-testo').value.trim());
    if ($('f-miei')?.checked) p.set('soloMiei', 'true');
    try {
        const r = await api('/api/rapportini?' + p.toString());
        righe = r.righe;
        disegna();
        const daConfermare = righe.filter(confermabile).length;
        msg('msg', `${righe.length} rapportini` + (GESTORE && daConfermare ? `, ${daConfermare} in attesa di conferma.` : '.'));
    } catch (e) { msg('msg', e.message, true); }
}

function disegna() {
    for (const id of [...selezionati]) if (!righe.some(r => r.idRapportino === id && confermabile(r))) selezionati.delete(id);
    const tb = $('tabella-rapportini').querySelector('tbody');
    tb.innerHTML = '';
    for (const r of righe) {
        const tr = document.createElement('tr');
        const importo = r.soloRendicontato ? '<span class="sottotesto">rendicontato</span>'
            : (r.importoCorretto != null ? `<s class="sottotesto">${euro(r.importo)}</s> ${euro(r.importoCorretto)}` : euro(r.importo));
        tr.innerHTML = `
            ${GESTORE ? `<td>${confermabile(r) ? `<input type="checkbox" class="sel" ${selezionati.has(r.idRapportino) ? 'checked' : ''} />` : ''}</td>` : ''}
            <td>${r.idRapportino}</td>
            <td style="white-space:nowrap">${dataIt(r.dataIntervento)}</td>
            <td>${esc(r.ragioneSociale)}<div class="sottotesto">${esc(r.nomeInsediamento || '')}</div></td>
            <td>${esc(r.consulente)}</td>
            <td style="max-width:22rem">${esc(r.descrizioneFattura)}</td>
            <td class="numerico">${(r.ore ?? 0).toLocaleString('it-IT')}</td>
            <td class="numerico">${importo}</td>
            <td>${etichettaStato(r.stato)}</td>
            <td style="white-space:nowrap">
                <button type="button" class="pulsante pulsante-tenue btn-apri">Apri</button>
                <a class="pulsante pulsante-tenue" href="/api/rapportini/${r.idRapportino}/pdf" target="_blank">PDF</a>
            </td>`;
        tr.querySelector('.sel')?.addEventListener('change', e => {
            e.target.checked ? selezionati.add(r.idRapportino) : selezionati.delete(r.idRapportino);
            aggiornaSelezione();
        });
        tr.querySelector('.btn-apri').addEventListener('click', () => apri(r.idRapportino));
        tb.appendChild(tr);
    }
    aggiornaSelezione();
}

function aggiornaSelezione() {
    const b = $('btn-conferma'); if (!b) return;
    b.textContent = `Conferma selezionati (${selezionati.size})`;
    b.disabled = selezionati.size === 0;
    const tutti = righe.filter(confermabile);
    if ($('sel-tutti')) $('sel-tutti').checked = tutti.length > 0 && selezionati.size === tutti.length;
}

async function confermaSelezionati(ids) {
    if (!ids.length) return false;
    if (!confirm(`Confermare ${ids.length} rapportini?\nQuelli in convenzione o prepagati diventano "Rendicontato" e non andranno in fattura.`)) return false;
    try {
        const r = await invia('/api/rapportini/conferma', 'POST', { ids });
        selezionati.clear();
        msg('msg', r.messaggio);
        await carica();
        return true;
    } catch (e) { msg('msg', e.message, true); return false; }
}

// ═══════════════════════════════ Cataloghi ═══════════════════════════════
let consulenti = [], voci = [], commesse = [], insediamenti = [], frasi = [], tipiDocumento = [];
let docRighe = [];   // { idDocumentoTipo, nome, revisione, dataDocumento, azione, consegna }
let clienteScelto = null;   // { id, nome }

async function caricaCataloghi() {
    const [c, v, t] = await Promise.all([api('/api/rapportini/consulenti'), api('/api/rapportini/voci'), api('/api/rapportini/documenti-tipo')]);
    tipiDocumento = t;
    consulenti = c.consulenti;
    voci = v;
    const sel = $('r-consulente');
    sel.innerHTML = '<option value="">— scegli —</option>' + consulenti.map(x => `<option value="${x.idOrganico}">${esc(x.nome)}</option>`).join('');
    sel.dataset.predefinito = c.predefinito ?? '';
    $('r-voce').innerHTML = '<option value="">—</option>' + voci.map(x => `<option>${esc(x)}</option>`).join('');
}

// Ricerca cliente con menu di suggerimenti
let timerCliente = null, risultatiCliente = [];
function cercaCliente() {
    clearTimeout(timerCliente);
    const q = $('r-cliente').value.trim();
    clienteScelto = null;
    if (q.length < 2) { $('r-cliente-sugg').classList.add('nascosto'); return; }
    timerCliente = setTimeout(async () => {
        try { risultatiCliente = await api('/api/rapportini/clienti?filtro=' + encodeURIComponent(q)); } catch { risultatiCliente = []; }
        const box = $('r-cliente-sugg');
        if (!risultatiCliente.length) { box.classList.add('nascosto'); return; }
        box.innerHTML = risultatiCliente.map((c, i) => `<div class="voce-suggerimento" data-i="${i}">${esc(c.nome)} <span class="sottotesto">(${c.id})</span></div>`).join('');
        box.querySelectorAll('.voce-suggerimento').forEach(el =>
            el.addEventListener('mousedown', ev => { ev.preventDefault(); scegliCliente(risultatiCliente[+el.dataset.i]); }));
        box.classList.remove('nascosto');
    }, 250);
}

async function scegliCliente(c, idInsediamento = null, idCommessa = null) {
    clienteScelto = c;
    $('r-cliente').value = c.nome;
    $('r-cliente-sugg').classList.add('nascosto');
    $('r-insediamento').innerHTML = '<option value="">caricamento…</option>';
    $('r-commessa').innerHTML = '<option value="">caricamento…</option>';
    try {
        [insediamenti, commesse] = await Promise.all([
            api(`/api/rapportini/clienti/${c.id}/insediamenti`),
            api(`/api/rapportini/clienti/${c.id}/commesse`)]);
    } catch (e) { msg('form-msg', e.message, true); insediamenti = []; commesse = []; }
    $('r-insediamento').innerHTML = (insediamenti.length === 1 ? '' : '<option value="">— scegli —</option>') +
        insediamenti.map(i => `<option value="${i.idInsediamento}">${esc(i.nome)}${i.email ? '' : ' (senza email)'}</option>`).join('');
    if (idInsediamento) $('r-insediamento').value = idInsediamento;
    mostraEmail();
    $('r-commessa').innerHTML = '<option value="">— nessuna commessa —</option>' + commesse.map(m => {
        const stato = m.attiva ? 'attiva' : 'non attiva';
        const prezzo = m.prezzoControllo != null ? ` · € ${euro(m.prezzoControllo)}` : '';
        return `<option value="${m.idCommessa}">[${stato}] ${esc(m.descrIncarico || '(senza descrizione)')} — ${esc(m.nomeInsediamento || '')}${prezzo}</option>`;
    }).join('');
    if (idCommessa) $('r-commessa').value = idCommessa;
    await Promise.all([caricaFrasi(), caricaDelega()]);
}

async function caricaDelega() {
    const p = $('r-delega');
    $('pannello-delega').classList.add('nascosto');
    if (!clienteScelto) { p.textContent = ''; return; }
    const idOrg = $('r-consulente').value;
    if (!idOrg) { p.textContent = 'Scegli il consulente per vedere la sua delega operativa per questo cliente.'; return; }
    try {
        const ins = $('r-insediamento').value ? `&idInsediamento=${$('r-insediamento').value}` : '';
        const d = await api(`/api/rapportini/clienti/${clienteScelto.id}/delega?idOrganico=${idOrg}${ins}&data=${encodeURIComponent($('r-data').value || oggi())}`);
        if (d.presente) {
            p.innerHTML = `<span class="etichetta" style="background:#e7f5e7;color:#256029">Delega operativa firmata: SÌ</span> <strong>${esc(d.riferimento)}</strong> (riportata nel rapporto).`;
            return;
        }
        const pr = await api(`/api/deleghe/proposta?idCliente=${clienteScelto.id}&idOrganico=${idOrg}`);
        if (pr.esistente?.stato === 'DaFirmare') {
            const id = pr.esistente.idDelegaCliente;
            p.innerHTML = `<span class="etichetta" style="background:#fdecea;color:#a12622">Delega operativa firmata: NO</span> <span style="color:#7a5b00">${esc(pr.esistente.estremi)} generata e <strong>in attesa di firma</strong>: non vale ancora.</span>
                <a href="/api/deleghe/${id}/word">Word</a> · <a href="/api/deleghe/${id}/pdf" target="_blank">PDF</a>`;
            return;
        }
        if (!pr.contrattualizzato) {
            p.innerHTML = '<span class="etichetta" style="background:#fdecea;color:#a12622">Delega operativa firmata: NO</span> <span style="color:#8a4500">il cliente non ha commesse di consulenza attive, quindi non si può generare.</span>';
            return;
        }
        p.innerHTML = `<span class="etichetta" style="background:#fdecea;color:#a12622">Delega operativa firmata: NO</span> <span style="color:#8a4500">Nessuna delega in essere per questo consulente (nel rapporto risulterà «nessuna delega operativa in essere»): se intervieni in autonomia sulla documentazione del cliente (es. revisione diretta del manuale), segnalalo e richiedila prima di interventi analoghi.</span>
            <button type="button" class="pulsante pulsante-tenue" id="btn-apri-delega">Genera delega</button>`;
        $('btn-apri-delega').addEventListener('click', () => {
            // Sedi: quelle del cliente, con la sede del rapportino già spuntata.
            const sel = +($('r-insediamento').value || 0);
            $('dg-tutte').checked = false;
            $('dg-sedi').innerHTML = insediamenti.length
                ? insediamenti.map(i => `<label class="casella" style="font-weight:normal"><input type="checkbox" value="${i.idInsediamento}" ${i.idInsediamento === sel || insediamenti.length === 1 ? 'checked' : ''} /> ${esc(i.nome)}</label>`).join('')
                : 'Nessun insediamento in ArcLab.';
            $('dg-tutte').onchange = () => $('dg-sedi').querySelectorAll('input').forEach(x => x.disabled = $('dg-tutte').checked);
            $('dg-lr-nome').value = pr.legaleRappNome || '';
            $('dg-lr-cognome').value = pr.legaleRappCognome || '';
            msg('dg-msg', pr.qualifica ? '' : 'Attenzione: manca la qualifica del consulente in Amministrazione → Consulenze: documenti e deleghe.', !pr.qualifica);
            $('pannello-delega').classList.remove('nascosto');
        });
    } catch { p.textContent = ''; }
}

async function generaDelega() {
    if (!clienteScelto) return;
    try {
        const r = await invia('/api/deleghe/genera', 'POST', {
            idCliente: clienteScelto.id,
            idInsediamento: 0,
            tuttiInsediamenti: $('dg-tutte').checked,
            idInsediamenti: [...$('dg-sedi').querySelectorAll('input:checked')].map(x => +x.value),
            idOrganico: +($('r-consulente').value || 0),
            legaleRappNome: $('dg-lr-nome').value.trim(),
            legaleRappCognome: $('dg-lr-cognome').value.trim(),
            idRapportino: corrente?.idRapportino ?? null
        });
        $('dg-msg').innerHTML = `Generata: <strong>${esc(r.estremi)}</strong> — <a href="/api/deleghe/${r.idDelegaCliente}/word">Word</a> · <a href="/api/deleghe/${r.idDelegaCliente}/pdf" target="_blank">PDF</a>. Falla firmare al cliente: vale dalla registrazione della firma (Amministrazione → Consulenze: documenti e deleghe).`;
        $('dg-msg').style.color = '';
    } catch (e) { msg('dg-msg', e.message, true); }
}

function mostraEmail() {
    const ins = insediamenti.find(i => String(i.idInsediamento) === $('r-insediamento').value);
    const p = $('r-email');
    if (!ins) { p.textContent = ''; return; }
    p.innerHTML = ins.email
        ? `Il rapportino arriverà a: <strong>${esc(ins.email)}</strong>${ins.indirizzo ? ' · ' + esc(ins.indirizzo) : ''}`
        : '<span style="color:#b00">Questo insediamento non ha email in ArcLab: il rapportino non potrà essere inviato.</span>';
}

function scegliCommessa() {
    const m = commesse.find(x => String(x.idCommessa) === $('r-commessa').value);
    if (m) {
        if (m.voceProposta) $('r-voce').value = m.voceProposta;
        if (m.riferimentoProposto && !$('r-rif-offerta').value.trim()) $('r-rif-offerta').value = m.riferimentoProposto;
        // Se l'insediamento non è ancora scelto, proponi quello della commessa.
        if (!$('r-insediamento').value && m.idInsediamento) { $('r-insediamento').value = m.idInsediamento; mostraEmail(); }
    }
    aggiornaVisibilita();
}

async function caricaFrasi() {
    const p = new URLSearchParams();
    if (clienteScelto) p.set('idCliente', clienteScelto.id);
    if ($('r-consulente').value) p.set('idOrganico', $('r-consulente').value);
    try { frasi = await api('/api/rapportini/frasi?' + p.toString()); } catch { frasi = []; }
}

function suggerisciFrasi() {
    const box = $('r-descr-sugg');
    const q = $('r-descrizione').value.trim().toLowerCase();
    const elenco = (q ? frasi.filter(f => f.toLowerCase().includes(q)) : frasi).filter(f => f.toLowerCase() !== q).slice(0, 12);
    if (!elenco.length) { box.classList.add('nascosto'); return; }
    box.innerHTML = elenco.map((f, i) => `<div class="voce-suggerimento" data-i="${i}">${esc(f)}</div>`).join('');
    box.querySelectorAll('.voce-suggerimento').forEach(el => el.addEventListener('mousedown', ev => {
        ev.preventDefault();
        $('r-descrizione').value = elenco[+el.dataset.i];
        contaDescrizione();
        box.classList.add('nascosto');
    }));
    box.classList.remove('nascosto');
}
function contaDescrizione() { $('r-descr-conta').textContent = `${$('r-descrizione').value.length}/250`; }

// ═══════════════════════════════ Documentazione di riferimento ═══════════════════════════════
const AZIONI = [[1, 'esaminato'], [2, 'modificato'], [3, 'emesso']];
const tipoDi = r => tipiDocumento.find(t => t.idDocumentoTipo === r.idDocumentoTipo);

function disegnaDocumenti(modificabile = true) {
    const tb = $('tabella-doc').querySelector('tbody');
    tb.innerHTML = docRighe.length ? '' : '<tr><td colspan="6" class="sottotesto">Nessun documento indicato.</td></tr>';
    docRighe.forEach((r, i) => {
        const tr = document.createElement('tr');
        const opzioni = '<option value="">— altro (scrivi il nome) —</option>' +
            tipiDocumento.map(t => `<option value="${t.idDocumentoTipo}" ${t.idDocumentoTipo === r.idDocumentoTipo ? 'selected' : ''}>${esc(t.nome)}</option>`).join('');
        tr.innerHTML = `
            <td style="min-width:16rem"><select class="d-tipo" style="width:100%">${opzioni}</select>
                <input type="text" class="d-nome ${r.idDocumentoTipo ? 'nascosto' : ''}" maxlength="200" value="${esc(r.idDocumentoTipo ? '' : r.nome)}" placeholder="nome del documento" style="width:100%;margin-top:0.2rem" /></td>
            <td><input type="text" class="d-rev" maxlength="30" value="${esc(r.revisione || '')}" style="width:4.5rem" /></td>
            <td><input type="date" class="d-data" value="${r.dataDocumento || ''}" /></td>
            <td><select class="d-azione">${AZIONI.map(([v, t]) => `<option value="${v}" ${v === r.azione ? 'selected' : ''}>${t}</option>`).join('')}</select></td>
            <td><select class="d-consegna" ${r.azione === 1 ? 'disabled' : ''}>
                <option value="">non ancora</option>
                <option value="1" ${r.consegna === 1 ? 'selected' : ''}>in bozza</option>
                <option value="2" ${r.consegna === 2 ? 'selected' : ''}>definitiva</option></select></td>
            <td><button type="button" class="pulsante pulsante-tenue d-togli" title="Togli la riga">✕</button></td>`;
        const leggi = () => {
            const idt = tr.querySelector('.d-tipo').value;
            r.idDocumentoTipo = idt ? +idt : null;
            r.nome = r.idDocumentoTipo ? (tipoDi(r)?.nome || '') : tr.querySelector('.d-nome').value.trim();
            r.revisione = tr.querySelector('.d-rev').value.trim();
            r.dataDocumento = tr.querySelector('.d-data').value || null;
            r.azione = +tr.querySelector('.d-azione').value;
            r.consegna = r.azione === 1 ? null : (tr.querySelector('.d-consegna').value ? +tr.querySelector('.d-consegna').value : null);
        };
        tr.querySelector('.d-tipo').addEventListener('change', () => { leggi(); disegnaDocumenti(); });
        tr.querySelector('.d-azione').addEventListener('change', () => { leggi(); disegnaDocumenti(); });
        tr.querySelectorAll('.d-nome, .d-rev, .d-data, .d-consegna').forEach(el => el.addEventListener('change', leggi));
        tr.querySelector('.d-togli').addEventListener('click', () => { docRighe.splice(i, 1); disegnaDocumenti(); });
        tr.querySelectorAll('input, select, button').forEach(el => el.disabled = !modificabile || (el.classList.contains('d-consegna') && r.azione === 1));
        tb.appendChild(tr);
    });
    aggiornaDocumentazione();
}

function aggiornaDocumentazione() {
    const cambiati = docRighe.filter(r => r.azione !== 1);
    $('lbl-piano').classList.toggle('nascosto', !cambiati.some(r => tipoDi(r)?.pianoAnalitico));
    $('lbl-verifica').classList.toggle('nascosto', !cambiati.some(r => tipoDi(r)?.richiamaLaboratorio));
}

function contaAttivita() {
    const n = $('r-attivita').value.trim().length;
    $('r-attivita-conta').textContent = `${n} caratteri` + (n < 80 ? ' (minimo 80 per l\'invio)' : '');
    $('r-attivita-conta').style.color = n < 80 ? '#8a4500' : '';
}

// ═══════════════════════════════ Form ═══════════════════════════════
const radio = nome => +(document.querySelector(`input[name="${nome}"]:checked`)?.value || 0);
const impostaRadio = (nome, v) => { const el = document.querySelector(`input[name="${nome}"][value="${v}"]`); if (el) el.checked = true; };
const num = id => { const v = $(id).value; return v === '' ? null : Number(v); };

function aggiornaVisibilita() {
    const convenzione = radio('r-modfatt') === 3;
    const prepagato = $('r-prepagato').checked;
    $('blocco-importo').classList.toggle('nascosto', convenzione || prepagato);
    const orario = radio('r-modimp') === 2;
    $('lbl-tariffa').classList.toggle('nascosto', !orario);
    $('r-importo').readOnly = orario;
    if (orario) {
        const t = num('r-tariffa'), o = num('r-ore');
        $('r-importo').value = t != null && o != null ? (Math.round(t * o * 100) / 100).toFixed(2) : '';
    }
    document.querySelectorAll('.blocco-prepagato').forEach(el => el.classList.toggle('nascosto', !prepagato));
    const prima = num('r-ore-residue'), ore = num('r-ore');
    $('r-ore-dopo').textContent = prepagato && prima != null && ore != null ? `Ore residue dopo questa prestazione: ${(prima - ore).toLocaleString('it-IT')}` : '';
}

let corrente = null;   // rapportino aperto (dettaglio dal server) o null se nuovo

function nuovo() {
    corrente = null;
    $('r-id').value = '';
    $('form-titolo').textContent = 'Nuovo rapportino';
    $('form-stato').textContent = '';
    $('avviso-stato').textContent = '';
    clienteScelto = null; insediamenti = []; commesse = []; frasi = [];
    for (const id of ['r-cliente', 'r-ore', 'r-tariffa', 'r-importo', 'r-descrizione', 'r-ultimo-scalo', 'r-ore-residue', 'r-note',
        'r-rif-offerta', 'r-attivita', 'r-fonte', 'r-piano', 'r-fu-azione', 'r-fu-termine']) $(id).value = '';
    $('r-fu-resp').value = '';
    $('r-verifica').checked = false;
    $('r-delega').textContent = '';
    docRighe = [];
    $('r-insediamento').innerHTML = '<option value="">— scegli prima il cliente —</option>';
    $('r-commessa').innerHTML = '<option value="">— nessuna commessa —</option>';
    $('r-email').textContent = '';
    $('r-data').value = oggi();
    $('r-consulente').value = $('r-consulente').dataset.predefinito || '';
    $('r-voce').value = '';
    impostaRadio('r-luogo', 1); impostaRadio('r-modfatt', 1); impostaRadio('r-modimp', 1);
    $('r-prepagato').checked = false;
    contaDescrizione(); contaAttivita();
    abilitaForm(true, 'Bozza');
    disegnaDocumenti(true);
    aggiornaVisibilita();
    msg('form-msg', '');
    mostraForm();
    caricaFrasi();
}

async function apri(id) {
    try {
        const r = await api(`/api/rapportini/${id}`);
        corrente = r;
        $('r-id').value = r.idRapportino;
        $('form-titolo').textContent = `Rapportino n. ${r.idRapportino}`;
        $('form-stato').innerHTML = etichettaStato(r.stato);
        await scegliCliente({ id: r.idClienteArcLab, nome: r.ragioneSociale }, r.idInsediamentoArcLab, r.idCommessaArcLab);
        $('r-data').value = r.dataIntervento;
        if (![...$('r-consulente').options].some(o => o.value == r.idOrganicoArcLab))
            $('r-consulente').insertAdjacentHTML('beforeend', `<option value="${r.idOrganicoArcLab}">${esc(r.consulente)} (non in servizio)</option>`);
        $('r-consulente').value = r.idOrganicoArcLab;
        if (r.modificabile) caricaDelega();
        $('r-voce').value = r.voce || '';
        $('r-descrizione').value = r.descrizioneFattura; contaDescrizione();
        $('r-ore').value = r.ore;
        impostaRadio('r-luogo', r.luogo); impostaRadio('r-modfatt', r.modalitaFatturazione); impostaRadio('r-modimp', r.modalitaImporto);
        $('r-tariffa').value = r.tariffaOraria ?? '';
        $('r-importo').value = r.importo ?? '';
        $('r-prepagato').checked = r.prepagato;
        $('r-ultimo-scalo').value = r.dataUltimoScalo || '';
        $('r-ore-residue').value = r.oreResiduePrima ?? '';
        $('r-note').value = r.noteInterne || '';
        $('r-rif-offerta').value = r.riferimentoOfferta || '';
        $('r-attivita').value = r.attivitaSvolta || ''; contaAttivita();
        $('r-fonte').value = r.fonteInformazioni || '';
        $('r-piano').value = r.notaPianoAnalitico || '';
        $('r-fu-azione').value = r.followUpAzione || '';
        $('r-fu-resp').value = r.followUpResponsabile || '';
        $('r-fu-termine').value = r.followUpTermine || '';
        $('r-verifica').checked = !!r.verificaRichiamiLaboratorio;
        docRighe = (r.documenti || []).map(x => ({ ...x }));
        if (r.riferimentoDelega && !r.modificabile) $('r-delega').innerHTML = `Delega operativa riportata nel rapporto: <strong>${esc(r.riferimentoDelega)}</strong>`;
        const avvisi = [];
        if (r.notaContestazione) avvisi.push(`Contestazione: ${r.notaContestazione}`);
        if (r.importoCorretto != null) avvisi.push(`Importo corretto: € ${euro(r.importoCorretto)} (era € ${euro(r.importo)}) — ${r.motivoCorrezione || ''}`);
        if (r.motivoAnnullamento) avvisi.push(`Annullato: ${r.motivoAnnullamento}`);
        if (r.filePalomar) avvisi.push(`Consegnato a Palomar: ${r.filePalomar}`);
        $('avviso-stato').textContent = avvisi.join(' · ');
        abilitaForm(r.modificabile, r.stato);
        disegnaDocumenti(r.modificabile);
        aggiornaVisibilita();
        msg('form-msg', r.modificabile ? '' : 'Rapportino già inviato: i dati si possono cambiare solo dopo una contestazione.');
        mostraForm();
    } catch (e) { msg('msg', e.message, true); }
}

function abilitaForm(modificabile, stato) {
    $('pannello-form').querySelectorAll('input, select, textarea').forEach(el => { if (el.id !== 'r-id') el.disabled = !modificabile; });
    $('btn-salva').classList.toggle('nascosto', !modificabile);
    $('btn-invia').classList.toggle('nascosto', !modificabile);
    $('btn-doc-aggiungi').classList.toggle('nascosto', !modificabile);
    $('btn-invia').textContent = stato === 'Contestato' ? 'Salva e reinvia al cliente' : 'Salva e invia al cliente';
    const esiste = !!corrente;
    $('btn-anteprima').classList.toggle('nascosto', !esiste);
    $('btn-annulla').classList.toggle('nascosto', !esiste || ['InPrefatturato', 'Fatturato', 'Annullato'].includes(stato) || (!GESTORE && stato !== 'Bozza'));
    if (GESTORE) {
        $('btn-contesta').classList.toggle('nascosto', !esiste || !['Inviato', 'Confermato', 'Rendicontato'].includes(stato));
        $('btn-correggi').classList.toggle('nascosto', !esiste || !['Inviato', 'Contestato'].includes(stato));
        $('btn-conferma-uno').classList.toggle('nascosto', !esiste || !['Inviato', 'Contestato'].includes(stato));
    }
}

function mostraForm() {
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
}
function chiudiForm() { $('pannello-form').classList.add('nascosto'); corrente = null; }

function datiForm() {
    return {
        idCliente: clienteScelto?.id ?? 0,
        idInsediamento: +($('r-insediamento').value || 0),
        dataIntervento: $('r-data').value || null,
        idOrganico: +($('r-consulente').value || 0),
        idCommessa: $('r-commessa').value ? +$('r-commessa').value : null,
        voce: $('r-voce').value || null,
        descrizioneFattura: $('r-descrizione').value.trim(),
        ore: num('r-ore') ?? 0,
        luogo: radio('r-luogo'),
        modalitaImporto: radio('r-modimp'),
        tariffaOraria: num('r-tariffa'),
        importo: num('r-importo'),
        modalitaFatturazione: radio('r-modfatt'),
        prepagato: $('r-prepagato').checked,
        dataUltimoScalo: $('r-ultimo-scalo').value || null,
        oreResiduePrima: num('r-ore-residue'),
        noteInterne: $('r-note').value.trim() || null,
        attivitaSvolta: $('r-attivita').value.trim() || null,
        riferimentoOfferta: $('r-rif-offerta').value.trim() || null,
        fonteInformazioni: $('r-fonte').value.trim() || null,
        notaPianoAnalitico: $('r-piano').value.trim() || null,
        followUpAzione: $('r-fu-azione').value.trim() || null,
        followUpResponsabile: $('r-fu-resp').value || null,
        followUpTermine: $('r-fu-termine').value.trim() || null,
        verificaRichiamiLaboratorio: $('r-verifica').checked,
        documenti: docRighe.filter(r => r.idDocumentoTipo || (r.nome || '').trim()).map(r => ({
            idDocumentoTipo: r.idDocumentoTipo || null, nome: r.nome || null, revisione: r.revisione || null,
            dataDocumento: r.dataDocumento || null, azione: r.azione || 1, consegna: r.azione === 1 ? null : (r.consegna || null)
        }))
    };
}

async function salva() {
    const d = datiForm();
    if (!clienteScelto) { msg('form-msg', 'Scegli il cliente dai suggerimenti.', true); return null; }
    if (d.modalitaFatturazione === 3 || d.prepagato) { d.importo = 0; d.modalitaImporto = 1; d.tariffaOraria = null; }
    try {
        const r = corrente
            ? await invia(`/api/rapportini/${corrente.idRapportino}`, 'PUT', d)
            : await invia('/api/rapportini', 'POST', d);
        corrente = r;
        $('r-id').value = r.idRapportino;
        $('form-titolo').textContent = `Rapportino n. ${r.idRapportino}`;
        $('form-stato').innerHTML = etichettaStato(r.stato);
        abilitaForm(r.modificabile, r.stato);
        msg('form-msg', 'Salvato.');
        carica();
        return r;
    } catch (e) { msg('form-msg', e.message, true); return null; }
}

async function salvaEInvia() {
    const r = await salva();
    if (!r) return;
    if (!confirm(`Inviare il rapportino n. ${r.idRapportino} al cliente?\nIl PDF viene copiato nella cartella Palomar e arriva all'email dell'insediamento: ${r.emailInsediamento || '(manca!)'}`)) return;
    try {
        const x = await invia(`/api/rapportini/${r.idRapportino}/invia`, 'POST');
        await apri(r.idRapportino);
        msg('form-msg', x.messaggio);
        carica(); caricaRegistro();
    } catch (e) { msg('form-msg', e.message, true); caricaRegistro(); }
}

async function azioneConNota(url, domanda, okMsg) {
    const nota = prompt(domanda);
    if (nota === null) return;
    try {
        await invia(url, 'POST', { nota });
        await apri(corrente.idRapportino);
        msg('form-msg', okMsg);
        carica();
    } catch (e) { msg('form-msg', e.message, true); }
}

async function correggi() {
    const v = prompt('Nuovo importo da fatturare (senza IVA), es. 120,00:', (corrente.importoDaFatturare ?? 0).toFixed(2).replace('.', ','));
    if (v === null) return;
    const importo = Number(v.trim().replace(/\./g, '').replace(',', '.'));
    if (!isFinite(importo) || importo < 0) { msg('form-msg', 'Importo non valido.', true); return; }
    const motivo = prompt('Motivo della correzione (es. sconto concordato con il cliente):');
    if (motivo === null) return;
    try {
        await invia(`/api/rapportini/${corrente.idRapportino}/correggi`, 'POST', { importo, motivo });
        await apri(corrente.idRapportino);
        msg('form-msg', 'Importo corretto: è quello che andrà in fattura.');
        carica();
    } catch (e) { msg('form-msg', e.message, true); }
}

// ═══════════════════════════════ Registro ═══════════════════════════════
async function caricaRegistro() {
    const p = new URLSearchParams({ giorni: 90, soloProblemi: $('reg-solo-problemi').checked });
    try {
        const r = await api('/api/rapportini/registro?' + p.toString());
        disegnaRegistro($('tabella-registro'), r);
        const problemi = r.filter(x => x.esito === 'Errore' || x.esito === 'Parziale').length;
        msg('reg-msg', `${r.length} invii negli ultimi 90 giorni` + (problemi ? ` — ${problemi} con problemi.` : '.'), problemi > 0);
    } catch (e) { msg('reg-msg', e.message, true); }
}

// ═══════════════════════════════ Eventi ═══════════════════════════════
$('btn-nuovo').addEventListener('click', nuovo);
$('btn-carica').addEventListener('click', carica);
$('f-testo').addEventListener('keydown', e => { if (e.key === 'Enter') carica(); });
$('f-stato').addEventListener('change', carica);
$('f-miei')?.addEventListener('change', carica);
$('btn-conferma')?.addEventListener('click', () => confermaSelezionati([...selezionati]));
$('sel-tutti')?.addEventListener('change', e => {
    for (const r of righe.filter(confermabile)) e.target.checked ? selezionati.add(r.idRapportino) : selezionati.delete(r.idRapportino);
    disegna();
});

$('r-cliente').addEventListener('input', cercaCliente);
$('r-cliente').addEventListener('blur', () => setTimeout(() => $('r-cliente-sugg').classList.add('nascosto'), 150));
$('r-insediamento').addEventListener('change', () => { mostraEmail(); caricaDelega(); });
$('r-commessa').addEventListener('change', scegliCommessa);
$('r-consulente').addEventListener('change', () => { caricaFrasi(); caricaDelega(); });
$('btn-dg-genera').addEventListener('click', generaDelega);
$('btn-dg-chiudi').addEventListener('click', () => $('pannello-delega').classList.add('nascosto'));
$('r-descrizione').addEventListener('input', () => { contaDescrizione(); suggerisciFrasi(); });
$('r-descrizione').addEventListener('focus', suggerisciFrasi);
$('r-descrizione').addEventListener('blur', () => setTimeout(() => $('r-descr-sugg').classList.add('nascosto'), 150));
document.querySelectorAll('input[name="r-modfatt"], input[name="r-modimp"]').forEach(el => el.addEventListener('change', aggiornaVisibilita));
for (const id of ['r-tariffa', 'r-ore', 'r-ore-residue']) $(id).addEventListener('input', aggiornaVisibilita);
$('r-prepagato').addEventListener('change', aggiornaVisibilita);

$('btn-doc-aggiungi').addEventListener('click', () => {
    docRighe.push({ idDocumentoTipo: null, nome: '', revisione: '', dataDocumento: null, azione: 1, consegna: null });
    disegnaDocumenti();
});
$('r-attivita').addEventListener('input', contaAttivita);
$('r-data').addEventListener('change', caricaDelega);

$('btn-salva').addEventListener('click', salva);
$('btn-invia').addEventListener('click', salvaEInvia);
$('btn-anteprima').addEventListener('click', () => { if (corrente) window.open(`/api/rapportini/${corrente.idRapportino}/pdf`, '_blank'); });
$('btn-chiudi').addEventListener('click', chiudiForm);
$('btn-annulla').addEventListener('click', () => azioneConNota(`/api/rapportini/${corrente.idRapportino}/annulla`, 'Motivo dell\'annullamento:', 'Rapportino annullato.'));
$('btn-contesta')?.addEventListener('click', () => azioneConNota(`/api/rapportini/${corrente.idRapportino}/contesta`, 'Cosa ha chiesto il cliente?', 'Segnato come contestato: ora si può correggere e reinviare, oppure confermare.'));
$('btn-correggi')?.addEventListener('click', correggi);
$('btn-conferma-uno')?.addEventListener('click', async () => { if (await confermaSelezionati([corrente.idRapportino])) await apri(corrente.idRapportino); });
$('btn-registro').addEventListener('click', caricaRegistro);
$('reg-solo-problemi').addEventListener('change', caricaRegistro);
$('dett-registro').addEventListener('toggle', e => { if (e.target.open) caricaRegistro(); });

(async function init() {
    const d = new Date(); d.setDate(d.getDate() - 30);
    $('f-dal').value = d.toISOString().slice(0, 10);
    try { await caricaCataloghi(); } catch (e) { msg('msg', 'Cataloghi non disponibili: ' + e.message, true); }
    await carica();
})();
