// Invio campioni ai laboratori esterni (06/11/2026): Excel dei campioni spediti, per e-mail con copia a refertazione@.
import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const data = (d) => d ? new Date(d).toLocaleDateString('it-IT') : '—';
const dataOra = (d) => d ? new Date(d).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';
const iso = (d) => d.toISOString().slice(0, 10);

let labs = [];
let campioni = [];
let copiaFissa = 'refertazione@mare-a.com';

async function avvia() {
    const oggi = new Date();
    const ieri = new Date(oggi); ieri.setDate(oggi.getDate() - 1);
    $('f-dal').value = iso(ieri); $('f-al').value = iso(oggi);
    try {
        const r = await chiamaApi('/api/invii-lab-esterni/laboratori');
        labs = r.laboratori; copiaFissa = r.copiaFissa;
        $('f-lab').innerHTML = labs.map(l => `<option value="${l.id}">${testo(l.nome)}</option>`).join('');
        if (labs.some(l => l.id === r.predefinito)) $('f-lab').value = String(r.predefinito);
        const t = await chiamaApi('/api/invii-lab-esterni/testi');
        $('f-oggetto').value = t.oggetto; $('f-testo').value = t.testo;
    } catch (e) { avvisa(e.message, 'errore'); }
    mostraIndirizzi();
    disegnaContatti();
    $('f-lab').addEventListener('change', () => { mostraIndirizzi(); $('pannello-risultati').classList.add('nascosto'); });
    $('btn-cerca').addEventListener('click', cerca);
    $('btn-tutti').addEventListener('click', () => seleziona(true));
    $('btn-nessuno').addEventListener('click', () => seleziona(false));
    $('btn-excel').addEventListener('click', scaricaExcel);
    $('btn-invia').addEventListener('click', invia);
    $('dett-registro').addEventListener('toggle', () => { if ($('dett-registro').open) caricaRegistro(); });
}

const labCorrente = () => labs.find(l => l.id === Number($('f-lab').value));

function mostraIndirizzi() {
    const l = labCorrente();
    $('f-indirizzi').innerHTML = !l ? '' : l.emailDestinatari
        ? `Destinatari: <strong>${testo(l.emailDestinatari)}</strong> · in copia: ${testo([l.emailCopia, copiaFissa].filter(Boolean).join('; '))}`
        : `<span style="color:#b00">Per questo laboratorio non ci sono indirizzi e-mail: inseriscili in «Indirizzi dei laboratori» qui sotto.</span>`;
}

async function cerca() {
    const fine = occupa($('btn-cerca'), 'Ricerca…');
    try {
        campioni = await chiamaApi(`/api/invii-lab-esterni/anteprima?idLab=${$('f-lab').value}&dal=${$('f-dal').value}&al=${$('f-al').value}`);
        disegnaCampioni();
        $('pannello-risultati').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

function disegnaCampioni() {
    $('conteggio').textContent = `(${campioni.length})`;
    $('tabella-campioni').querySelector('tbody').innerHTML = campioni.map(c => `<tr>
        <td><input type="checkbox" data-id="${c.idCampione}" ${c.tutteInviate ? '' : 'checked'} /></td>
        <td><strong>${testo(c.numeroCampione)}</strong></td>
        <td style="white-space:normal">${testo(c.clienteInsediamento ?? '')}</td>
        <td style="white-space:normal">${testo(c.denominazioneCampione ?? '')}</td>
        <td style="white-space:normal" class="sottotesto">${c.prove.map(p => testo(p.prova ?? '') + (p.rifMetodo ? ' – ' + testo(p.rifMetodo) : '')).join('<br>')}</td>
        <td>${data(c.dataAnalisi)}</td>
        <td>${c.giaInviato ? `<span class="etichetta" title="${c.tutteInviate ? 'Tutte le prove già inviate' : 'Alcune prove già inviate'}">${dataOra(c.giaInviato)}</span>` : '—'}</td></tr>`).join('')
        || '<tr><td colspan="7" class="sottotesto">Nessun campione da inviare nel periodo.</td></tr>';
}

function seleziona(v) { $('tabella-campioni').querySelectorAll('input[type=checkbox]').forEach(x => x.checked = v); }
const scelti = () => [...$('tabella-campioni').querySelectorAll('input[type=checkbox]:checked')].map(x => Number(x.dataset.id));

function scaricaExcel() {
    const ids = scelti();
    if (ids.length === 0) { avvisa('Nessun campione selezionato.', 'errore'); return; }
    window.location.href = `/api/invii-lab-esterni/excel?idLab=${$('f-lab').value}&dal=${$('f-dal').value}&al=${$('f-al').value}&campioni=${ids.join(',')}`;
}

async function invia() {
    const ids = scelti();
    if (ids.length === 0) { avvisa('Nessun campione selezionato.', 'errore'); return; }
    const l = labCorrente();
    if (!l?.emailDestinatari) { avvisa('Inserisci prima gli indirizzi del laboratorio.', 'errore'); return; }
    if (!confirm(`Inviare a ${l.nome} l'Excel di ${ids.length} campioni?\nDestinatari: ${l.emailDestinatari}\nIn copia: ${[l.emailCopia, copiaFissa].filter(Boolean).join('; ')}`)) return;
    const fine = occupa($('btn-invia'), 'Invio…');
    try {
        const r = await inviaJson('/api/invii-lab-esterni/invia', {
            idLab: Number($('f-lab').value), dal: $('f-dal').value, al: $('f-al').value, campioni: ids,
            oggetto: $('f-oggetto').value, testo: $('f-testo').value
        });
        avvisa(`Inviati ${r.numeroCampioni} campioni (${r.numeroProve} prove) a ${r.destinatari}.`);
        await cerca();
        if ($('dett-registro').open) caricaRegistro();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

// ── Indirizzi dei laboratori ──
function disegnaContatti() {
    const corpo = $('tabella-contatti').querySelector('tbody');
    corpo.innerHTML = labs.map(l => `<tr data-id="${l.id}">
        <td style="white-space:normal"><strong>${testo(l.nome)}</strong> <span class="sottotesto">(${l.id})</span></td>
        <td><input type="text" data-campo="dest" value="${testo(l.emailDestinatari ?? '')}" placeholder="es. accettazione@lab.it" style="min-width:16rem" /></td>
        <td><input type="text" data-campo="copia" value="${testo(l.emailCopia ?? '')}" style="min-width:12rem" /></td>
        <td><input type="text" data-campo="note" value="${testo(l.note ?? '')}" /></td>
        <td><button type="button" class="pulsante pulsante-tenue" data-salva>Salva</button></td></tr>`).join('');
    corpo.querySelectorAll('[data-salva]').forEach(b => b.addEventListener('click', async () => {
        const tr = b.closest('tr'); const id = Number(tr.dataset.id); const l = labs.find(x => x.id === id);
        const v = (c) => tr.querySelector(`[data-campo="${c}"]`).value;
        const fine = occupa(b);
        try {
            const r = await inviaJson('/api/invii-lab-esterni/contatti', { idLabEsterno: id, nome: l.nome, emailDestinatari: v('dest'), emailCopia: v('copia'), note: v('note') });
            l.emailDestinatari = r.emailDestinatari; l.emailCopia = r.emailCopia; l.note = v('note');
            avvisa(`Indirizzi di ${l.nome} salvati.`);
            mostraIndirizzi();
        } catch (e) { avvisa(e.message, 'errore'); }
        finally { fine(); }
    }));
}

// ── Registro ──
async function caricaRegistro() {
    let righe = [];
    try { righe = await chiamaApi('/api/invii-lab-esterni/registro'); } catch (e) { avvisa(e.message, 'errore'); }
    $('tabella-registro').querySelector('tbody').innerHTML = righe.map(r => `<tr>
        <td>${dataOra(r.dataInvio)}</td><td>${testo(r.nomeLaboratorio)}</td><td>${data(r.dal)} – ${data(r.al)}</td>
        <td class="numerico">${r.numeroCampioni}</td><td class="numerico">${r.numeroProve}</td>
        <td style="white-space:normal">${testo(r.destinatari)}</td><td>${testo(r.nomeUtente ?? '')}</td>
        <td>${r.riuscito ? 'Inviato' : `<span style="color:#b00" title="${testo(r.errore ?? '')}">Non riuscito</span>`}</td>
        <td><a class="pulsante pulsante-tenue" href="/api/invii-lab-esterni/registro/${r.idInvioLaboratorioEsterno}/excel">Excel</a></td></tr>`).join('')
        || '<tr><td colspan="9" class="sottotesto">Nessun invio.</td></tr>';
}

avvia();
