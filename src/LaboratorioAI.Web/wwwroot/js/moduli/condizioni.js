// Condizioni e firme (09/10/2026, Specifica v0.4 — fase 1): revisioni delle Condizioni Generali con PDF e
// clausole, Firmatari dei clienti, richieste di approvazione, registro delle evidenze.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';
import { collegaSuggerimenti } from './suggerimenti.js';

const $ = (id) => document.getElementById(id);
const GESTIONE = $('condizioni-pagina')?.dataset.gestione === 'true';

const ETICHETTE_STATO = {
    Bozza: 'bozza', Pubblicata: 'pubblicata',
    DaInviare: 'da inviare', Inviata: 'inviata', Aperta: 'aperta', InVerifica: 'in verifica',
    Approvata: 'approvata', Rifiutata: 'rifiutata', Annullata: 'annullata'
};
const MODALITA = { Caselle: 'caselle', CaselleOtp: 'caselle + OTP', FirmaDigitale: 'firma digitale', Autografa: 'autografa', FirmaGrafica: 'firma grafica' };
const COLORE_STATO = { Approvata: '#1a7f37', Rifiutata: '#b3261e', Annullata: '#777', InVerifica: '#9a6700', Pubblicata: '#1a7f37' };
const statoHtml = (s, modalita) =>
    `<span style="color:${COLORE_STATO[s] || 'inherit'}">${testo(ETICHETTE_STATO[s] || s)}${modalita ? ' – ' + testo(MODALITA[modalita] || modalita) : ''}</span>`;
const kb = (n) => n == null ? '' : `${Math.round(n / 1024).toLocaleString('it-IT')} KB`;

// ══ Revisioni ══
let revisioneCorrente = null, clausole = [];

async function caricaRevisioni() {
    if (!GESTIONE) return;
    try {
        const r = await chiamaApi('/api/condizioni/revisioni');
        $('tabella-revisioni').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="6" class="vuoto">Nessuna revisione caricata. Inizia con «Nuova revisione».</td></tr>'
            : r.map(x => `<tr class="riga-cliccabile" data-id="${x.idRevisioneCondizioni}">
                <td>${testo(x.codice)}</td><td>${testo(x.dataEntrataVigore) || '—'}</td><td>${statoHtml(x.stato)}</td>
                <td class="numerico">${x.clausole}</td><td>${x.nomeFilePdf ? testo(x.nomeFilePdf) + ' · ' + kb(x.dimensionePdf) : '<span style="color:#b3261e">manca</span>'}</td>
                <td>${x.dataPubblicazione ? testo(x.dataPubblicazione) + ' · ' + testo(x.nomeUtentePubblicazione) : '—'}</td></tr>`).join('');
        $('tabella-revisioni').querySelectorAll('.riga-cliccabile').forEach(t =>
            t.addEventListener('click', () => apriRevisione(Number(t.dataset.id))));
    } catch (e) { avvisa(e.message, 'errore'); }
}

// Riferimento stampato: composto da numero e data con il prefisso di Impostazioni documenti (10/10/2026).
let prefisso = 'Mod. Com. 05', riferimentoImpostazioni = null;
async function caricaProposta() {
    try {
        const p = await chiamaApi('/api/condizioni/revisioni/proposta');
        prefisso = p.prefisso || prefisso; riferimentoImpostazioni = p.riferimentoImpostazioni;
        return p;
    } catch { return null; }
}
function aggiornaRiferimento() {
    if ((revisioneCorrente?.stato ?? 'Bozza') !== 'Bozza') return;
    const n = $('r-numero').value, d = $('r-data').value;
    const data = d ? `${d.substring(8, 10)}/${d.substring(5, 7)}/${d.substring(2, 4)}` : '';
    $('r-codice').value = n ? `${prefisso} Rev. ${n}${data ? ' del ' + data : ''}` : '';
}
function mostraImpostazioni(codice) {
    $('r-impostazioni').innerHTML = riferimentoImpostazioni
        ? `In Impostazioni documenti (usata da Offerte e Ordini di Accettazione): «${testo(riferimentoImpostazioni)}»`
          + (codice && codice !== riferimentoImpostazioni ? ' — <span style="color:#9a6700">diversa da questa revisione: va cambiata lì alla data di entrata in vigore</span>' : '')
        : 'In Impostazioni documenti la revisione delle Condizioni non è impostata.';
}

async function nuovaRevisione() {
    revisioneCorrente = null;
    ['r-id', 'r-codice', 'r-numero', 'r-data', 'r-vigore', 'r-cosa-cambia', 'r-note'].forEach(id => $(id).value = '');
    $('r-nuova-approvazione').checked = true;
    clausole = [];
    const p = await caricaProposta();
    if (p) { $('r-numero').value = p.numero ?? ''; $('r-data').value = p.data ?? ''; }
    aggiornaRiferimento();
    mostraImpostazioni(null);
    mostraRevisione({ stato: 'Bozza' });
    $('revisione-titolo').textContent = 'Nuova revisione';
    $('r-numero').focus();
}

async function apriRevisione(id) {
    try {
        const r = await chiamaApi('/api/condizioni/revisioni/' + id);
        revisioneCorrente = r;
        $('r-id').value = r.idRevisioneCondizioni;
        $('r-codice').value = r.codice ?? '';
        $('r-numero').value = r.numeroRevisione ?? '';
        $('r-data').value = r.dataRevisione ?? '';
        $('r-vigore').value = r.dataEntrataVigore ?? '';
        $('r-nuova-approvazione').checked = !!r.richiedeNuovaApprovazione;
        $('r-cosa-cambia').value = r.testoCosaCambia ?? '';
        $('r-note').value = r.note ?? '';
        clausole = (r.clausole ?? []).map(c => ({ id: c.idClausolaCondizioni, articolo: c.articolo, contenutoBreve: c.contenutoBreve, testoIntegrale: c.testoIntegrale ?? '' }));
        $('revisione-titolo').textContent = r.codice;
        await caricaProposta();
        mostraImpostazioni(r.codice);
        mostraRevisione(r);
        $('pannello-revisione').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

/** Mostra il pannello; se la revisione è pubblicata tutto diventa di sola lettura. */
function mostraRevisione(r) {
    const bozza = r.stato === 'Bozza';
    const salvata = !!$('r-id').value;
    $('pannello-revisione').classList.remove('nascosto');
    $('revisione-stato').innerHTML = bozza ? 'bozza: modificabile' : `pubblicata · impronta del PDF <code>${testo(r.improntaPdf)}</code>`;
    $('pannello-revisione').querySelectorAll('.griglia-filtri input, textarea, #r-note').forEach(el => el.disabled = !bozza);
    $('r-codice').disabled = false;
    $('r-pdf-info').innerHTML = r.nomeFilePdf
        ? `${testo(r.nomeFilePdf)} · ${kb(r.dimensionePdf)} · impronta <code>${testo(r.improntaPdf)}</code>`
        : 'Nessun PDF caricato.';
    $('btn-pdf-scarica').classList.toggle('nascosto', !r.nomeFilePdf);
    if (r.nomeFilePdf) $('btn-pdf-scarica').href = `/api/condizioni/revisioni/${$('r-id').value}/pdf`;
    $('r-pdf-file').classList.toggle('nascosto', !bozza || !salvata);
    $('btn-pdf-carica').classList.toggle('nascosto', !bozza || !salvata);
    if (bozza && !salvata) $('r-pdf-info').textContent = 'Salva la bozza per poter caricare il PDF.';
    $('r-clausole-azioni').classList.toggle('nascosto', !bozza);
    $('btn-revisione-salva').classList.toggle('nascosto', !bozza);
    $('btn-revisione-pubblica').classList.toggle('nascosto', !bozza || !salvata);
    $('btn-revisione-elimina').classList.toggle('nascosto', !bozza || !salvata);
    $('btn-revisione-copia').classList.toggle('nascosto', !salvata);
    $('btn-revisione-ritira').classList.toggle('nascosto', bozza || !salvata);
    renderClausole(bozza);
}

function renderClausole(modificabile = (revisioneCorrente?.stato ?? 'Bozza') === 'Bozza') {
    const tb = $('tabella-clausole').querySelector('tbody');
    if (clausole.length === 0) { tb.innerHTML = '<tr><td colspan="5" class="vuoto">Nessuna clausola.</td></tr>'; return; }
    tb.innerHTML = clausole.map((c, i) => modificabile
        ? `<tr><td class="numerico" style="color:#888">${i + 1}</td>
            <td><input type="text" data-i="${i}" data-c="articolo" value="${testo(c.articolo)}" style="width:4.5rem" /></td>
            <td><input type="text" data-i="${i}" data-c="contenutoBreve" value="${testo(c.contenutoBreve)}" style="width:100%;min-width:22rem" /></td>
            <td><textarea data-i="${i}" data-c="testoIntegrale" rows="1" style="width:100%;min-width:14rem">${testo(c.testoIntegrale)}</textarea></td>
            <td><button type="button" class="pulsante pulsante-tenue cl-rm" data-i="${i}">×</button></td></tr>`
        : `<tr><td class="numerico" style="color:#888">${i + 1}</td><td>Art. ${testo(c.articolo)}</td><td>${testo(c.contenutoBreve)}</td>
            <td class="sottotesto">${c.testoIntegrale ? 'sì' : '—'}</td><td></td></tr>`).join('');
    tb.querySelectorAll('input, textarea').forEach(el => el.addEventListener('input', () => clausole[Number(el.dataset.i)][el.dataset.c] = el.value));
    tb.querySelectorAll('.cl-rm').forEach(b => b.addEventListener('click', () => { clausole.splice(Number(b.dataset.i), 1); renderClausole(); }));
}

async function caricaClausoleRev19() {
    if (clausole.length && !confirm('Sostituire l\'elenco attuale con le 14 clausole della Rev. 19?')) return;
    try {
        const r = await chiamaApi('/api/condizioni/clausole-predefinite');
        // Le righe esistenti si riusano in ordine (così restano le stesse in database), le altre si aggiungono.
        const vecchie = clausole;
        clausole = r.map((c, i) => ({ id: vecchie[i]?.id, articolo: c.articolo, contenutoBreve: c.contenutoBreve, testoIntegrale: vecchie[i]?.testoIntegrale ?? '' }));
        renderClausole();
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function salvaRevisione(pulsante) {
    const corpo = {
        id: $('r-id').value ? Number($('r-id').value) : null,
        numeroRevisione: Number($('r-numero').value || 0),
        dataRevisione: $('r-data').value || null,
        dataEntrataVigore: $('r-vigore').value || null,
        richiedeNuovaApprovazione: $('r-nuova-approvazione').checked,
        testoCosaCambia: $('r-cosa-cambia').value,
        note: $('r-note').value,
        clausole: clausole.map(c => ({ id: c.id ?? null, articolo: c.articolo, contenutoBreve: c.contenutoBreve, testoIntegrale: c.testoIntegrale }))
    };
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const r = await inviaJson('/api/condizioni/revisioni/salva', corpo);
        avvisa(r.messaggio, 'successo');
        await apriRevisione(r.idRevisioneCondizioni);
        await caricaRevisioni();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function caricaPdf(pulsante) {
    const id = $('r-id').value; const file = $('r-pdf-file').files[0];
    if (!id) { avvisa('Salva prima la bozza.', 'info'); return; }
    if (!file) { avvisa('Scegli il PDF.', 'info'); return; }
    const fd = new FormData(); fd.append('file', file);
    const libera = occupa(pulsante, 'Caricamento…');
    try {
        // fetch diretto: chiamaApi imposterebbe Content-Type JSON anche per un modulo con file.
        const risposta = await fetch(`/api/condizioni/revisioni/${id}/pdf`, { method: 'POST', body: fd, headers: { 'Accept': 'application/json' } });
        const r = await risposta.json().catch(() => ({}));
        if (!risposta.ok) throw new Error(r.messaggio || `Errore ${risposta.status}`);
        avvisa(r.messaggio, 'successo');
        $('r-pdf-file').value = '';
        await apriRevisione(Number(id));
        await caricaRevisioni();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function pubblicaRevisione(pulsante) {
    const id = $('r-id').value; if (!id) return;
    if (!confirm('Pubblicare la revisione?\n\nDopo la pubblicazione PDF e clausole non si possono più modificare: per cambiarle servirà una revisione nuova.\nSalva prima eventuali modifiche non salvate.')) return;
    const libera = occupa(pulsante, 'Pubblicazione…');
    try {
        const r = await inviaJson(`/api/condizioni/revisioni/${id}/pubblica`, {});
        avvisa(r.messaggio, 'successo');
        await apriRevisione(Number(id));
        await Promise.all([caricaRevisioni(), caricaRegistro()]);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function copiaRevisione() {
    const id = $('r-id').value; if (!id) return;
    try {
        const r = await inviaJson(`/api/condizioni/revisioni/${id}/copia`, {});
        avvisa(r.messaggio, 'successo');
        await apriRevisione(r.idRevisioneCondizioni);
        await caricaRevisioni();
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function ritiraRevisione() {
    const id = $('r-id').value; if (!id) return;
    const motivo = prompt('Ritirare la revisione pubblicata?\nSi può solo se non ha richieste di approvazione. Resta nel registro delle evidenze.\nScrivi il motivo (obbligatorio):');
    if (motivo === null) return;
    if (!motivo.trim()) { avvisa('Operazione annullata: il motivo è obbligatorio.', 'info'); return; }
    try {
        const r = await inviaJson(`/api/condizioni/revisioni/${id}/ritira`, { motivo: motivo.trim() });
        avvisa(r.messaggio, 'successo');
        $('pannello-revisione').classList.add('nascosto');
        await Promise.all([caricaRevisioni(), caricaRegistro()]);
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function eliminaRevisione() {
    const id = $('r-id').value; if (!id) return;
    if (!confirm('Eliminare questa bozza? (resta nel database, non compare più nell\'elenco)')) return;
    try {
        const r = await inviaJson(`/api/condizioni/revisioni/${id}/elimina`, {});
        avvisa(r.messaggio, 'successo');
        $('pannello-revisione').classList.add('nascosto');
        await caricaRevisioni();
    } catch (e) { avvisa(e.message, 'errore'); }
}

// ══ Cliente ══
let clienteCorrente = null;

async function apriCliente(id) {
    try {
        const c = await chiamaApi('/api/condizioni/clienti/' + id);
        clienteCorrente = c;
        $('scheda-cliente').classList.remove('nascosto');
        $('c-nome').textContent = c.nome ?? `Cliente ${id}`;
        $('c-dati').textContent = `ID ArcLab ${c.idCliente} · P.IVA ${c.partitaIva || '—'} · e-mail in anagrafica ${c.email || '—'}`;
        const attivo = (c.firmatari ?? []).find(f => f.attivo);
        $('c-firmatario').innerHTML = attivo
            ? `Attivo: <strong>${testo(attivo.nome)} ${testo(attivo.cognome)}</strong>${attivo.qualifica ? ', ' + testo(attivo.qualifica) : ''} · ${testo(attivo.email)} · designato il ${testo(attivo.dataDesignazione)} da ${testo(attivo.nomeUtenteDesignazione)}. Compila sotto per sostituirlo.`
            : '<span style="color:#9a6700">Nessun Firmatario designato.</span>';
        ['f-nome', 'f-cognome', 'f-email', 'f-cf', 'f-note'].forEach(x => $(x).value = '');
        $('tabella-firmatari-storico').querySelector('tbody').innerHTML = (c.firmatari ?? []).map(f => `<tr>
            <td>${testo(f.nome)} ${testo(f.cognome)}</td><td>${testo(f.qualifica) || '—'}</td><td>${testo(f.email)}</td>
            <td>${testo(f.dataDesignazione)}</td><td>${testo(f.nomeUtenteDesignazione)}</td><td>${f.attivo ? 'attivo' : 'sostituito'}</td></tr>`).join('')
            || '<tr><td colspan="6" class="vuoto">Nessuno.</td></tr>';
        $('q-offerta').innerHTML = '<option value="">— nessuna —</option>' + (c.offerte ?? []).map(o =>
            `<option value="${o.idPreventivo}">Offerta ${o.anno}/${String(o.numero).padStart(3, '0')} del ${testo(o.dataOfferta) || '—'} · ${testo(o.stato)}${o.oggetto ? ' · ' + testo(o.oggetto) : ''}</option>`).join('');
        renderRichiesteCliente(c.richieste ?? []);
        $('c-invito').classList.toggle('nascosto', !!attivo);
        $('i-email').value = c.email ?? '';
        $('c-inviti').innerHTML = (c.inviti ?? []).map(i => `<li>Invito ${testo(STATI_INVITO[i.stato] ?? i.stato)}${i.inCoda ? ' (in coda)' : ''}
            · scritto a ${testo(i.emailDestinatari)}${i.dataInvio ? ' il ' + testo(i.dataInvio) : ''}${i.numeroPromemoria ? ` · ${i.numeroPromemoria} promemoria` : ''}
            ${i.indicato ? ' · indicato: <strong>' + testo(i.indicato) + '</strong>' : ''}${i.errore ? ' · <span style="color:#b3261e">' + testo(i.errore) + '</span>' : ''}</li>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderRichiesteCliente(righe) {
    $('tabella-richieste-cliente').querySelector('tbody').innerHTML = righe.length === 0
        ? '<tr><td colspan="7" class="vuoto">Nessuna richiesta.</td></tr>'
        : righe.map(r => `<tr><td>${testo(r.revisione)}</td><td>${testo(r.firmatario) || '—'}</td><td>${statoHtml(r.stato, r.modalita)}</td>
            <td>${testo(r.dataCreazione)}</td><td>${testo(r.dataInvio) || '—'}</td><td>${testo(r.dataPerfezionamento) || '—'}</td>
            <td>${pulsanteAnnulla(r)}</td></tr>`).join('');
    collegaAnnulla($('tabella-richieste-cliente'));
}

async function salvaFirmatario(pulsante, conferma = false) {
    if (!clienteCorrente) return;
    const corpo = {
        idCliente: clienteCorrente.idCliente, nome: $('f-nome').value, cognome: $('f-cognome').value,
        qualifica: $('f-qualifica').value, email: $('f-email').value, codiceFiscale: $('f-cf').value, note: $('f-note').value,
        confermaEmailGenerica: conferma
    };
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const r = await inviaJson('/api/condizioni/firmatari/salva', corpo);
        if (r.richiedeConferma) {
            libera();
            if (confirm(r.messaggio)) await salvaFirmatario(pulsante, true);
            return;
        }
        avvisa(r.messaggio, 'successo');
        await Promise.all([apriCliente(clienteCorrente.idCliente), caricaRichieste(), caricaRegistro()]);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function creaRichiesta(pulsante) {
    if (!clienteCorrente) return;
    const libera = occupa(pulsante, 'Creazione…');
    try {
        const r = await inviaJson('/api/condizioni/richieste/crea', {
            idCliente: clienteCorrente.idCliente,
            idPreventivo: $('q-offerta').value ? Number($('q-offerta').value) : null,
            note: $('q-note').value
        });
        avvisa(r.messaggio, 'successo');
        $('q-note').value = '';
        await Promise.all([apriCliente(clienteCorrente.idCliente), caricaRichieste(), caricaRegistro()]);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ══ Richieste ══
// Azioni sulla richiesta (fase 2): invia/reinvia il link, scarica la Ricevuta, annulla.
function pulsanteAnnulla(r) {
    const b = [];
    if (r.stato === 'DaInviare') b.push(`<button type="button" class="pulsante pulsante-secondario btn-invia-link" data-id="${r.idRichiestaApprovazione}">Invia link</button>`);
    if (['Inviata', 'Aperta'].includes(r.stato)) b.push(`<button type="button" class="pulsante pulsante-tenue btn-invia-link" data-id="${r.idRichiestaApprovazione}" data-reinvio="1" title="Nuovo link: quello precedente non vale più">Reinvia</button>`);
    if (r.haRicevuta) b.push(`<a class="pulsante pulsante-tenue" href="/api/condizioni/richieste/${r.idRichiestaApprovazione}/ricevuta" target="_blank" title="${testo(r.dataInvioRicevuta ? 'inviata il ' + r.dataInvioRicevuta : 'non ancora inviata')}">Ricevuta ${testo(r.numeroRicevuta)}</a>`);
    if (r.stato === 'Approvata' && !r.haRicevuta) b.push('<span class="sottotesto">ricevuta in preparazione</span>');
    if (r.erroreInvioRicevuta) b.push(`<span style="color:#b3261e" title="${testo(r.erroreInvioRicevuta)}">invio ricevuta non riuscito (si ritenta)</span>`);
    if (!['Approvata', 'Annullata'].includes(r.stato)) b.push(`<button type="button" class="pulsante pulsante-tenue btn-annulla-richiesta" data-id="${r.idRichiestaApprovazione}" style="color:var(--rosso)">Annulla</button>`);
    return b.join(' ');
}

function collegaAnnulla(tabella) {
    tabella.querySelectorAll('.btn-invia-link').forEach(b => b.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        if (b.dataset.reinvio && !confirm('Reinviare il link? Il link inviato prima non funzionerà più.')) return;
        const libera = occupa(b, 'Invio…');
        try {
            const r = await inviaJson(`/api/condizioni/richieste/${b.dataset.id}/invia`, {});
            avvisa(r.messaggio, 'successo');
            await Promise.all([caricaRichieste(), caricaRegistro(), clienteCorrente ? apriCliente(clienteCorrente.idCliente) : null]);
        } catch (e) { avvisa(e.message, 'errore'); }
        finally { libera(); }
    }));
    tabella.querySelectorAll('a.pulsante').forEach(a => a.addEventListener('click', (ev) => ev.stopPropagation()));
    tabella.querySelectorAll('.btn-annulla-richiesta').forEach(b => b.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        const motivo = prompt('Annullare la richiesta di approvazione?\nScrivi il motivo (obbligatorio):');
        if (motivo === null) return;
        if (!motivo.trim()) { avvisa('Annullamento non eseguito: il motivo è obbligatorio.', 'info'); return; }
        try {
            const r = await inviaJson(`/api/condizioni/richieste/${b.dataset.id}/annulla`, { motivo: motivo.trim() });
            avvisa(r.messaggio, 'successo');
            await Promise.all([caricaRichieste(), caricaRegistro(), clienteCorrente ? apriCliente(clienteCorrente.idCliente) : null]);
        } catch (e) { avvisa(e.message, 'errore'); }
    }));
}

let timerFiltro = null;
async function caricaRichieste() {
    try {
        const q = new URLSearchParams();
        if ($('q-stato').value) q.set('stato', $('q-stato').value);
        if ($('q-filtro').value.trim()) q.set('filtro', $('q-filtro').value.trim());
        const r = await chiamaApi('/api/condizioni/richieste?' + q.toString());
        $('conteggio-richieste').textContent = `${r.length} richieste`;
        $('tabella-richieste').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="9" class="vuoto">Nessuna richiesta.</td></tr>'
            : r.map(x => `<tr class="riga-cliccabile" data-cliente="${x.idClienteArcLab}">
                <td>${testo(x.clienteRagioneSociale)}</td><td>${testo(x.revisione)}</td>
                <td>${testo(x.firmatario) || '—'}<br><span class="sottotesto">${testo(x.firmatarioEmail)}</span></td>
                <td>${statoHtml(x.stato, x.modalita)}</td><td>${testo(x.dataCreazione)}</td><td>${testo(x.dataInvio) || '—'}</td>
                <td class="numerico">${x.giorniAttesa ?? ''}</td><td>${testo(x.dataPerfezionamento) || (x.dataApertura ? '<span class="sottotesto">aperta il ' + testo(x.dataApertura) + '</span>' : '—')}${x.dichiarante ? '<br><span class="sottotesto">' + testo(x.dichiarante) + '</span>' : ''}</td><td>${pulsanteAnnulla(x)}</td></tr>`).join('');
        $('tabella-richieste').querySelectorAll('.riga-cliccabile').forEach(t => t.addEventListener('click', () => {
            apriCliente(Number(t.dataset.cliente));
            $('scheda-cliente').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }));
        collegaAnnulla($('tabella-richieste'));
    } catch (e) { avvisa(e.message, 'errore'); }
}

// ══ Registro ══
const NOMI_EVENTO = {
    'revisione.pubblicata': 'Revisione pubblicata', 'revisione.ritirata': 'Revisione ritirata', 'firmatario.designato': 'Firmatario designato',
    'richiesta.creata': 'Richiesta creata', 'richiesta.annullata': 'Richiesta annullata',
    'richiesta.inviata': 'Link inviato', 'richiesta.reinviata': 'Link reinviato', 'pagina.aperta': 'Pagina aperta dal Firmatario',
    'documento.aperto': 'Documento aperto', 'casella1.spuntata': 'Casella 1 spuntata', 'casella1.tolta': 'Casella 1 tolta',
    'casella2.spuntata': 'Casella 2 spuntata', 'casella2.tolta': 'Casella 2 tolta', 'casella3.spuntata': 'Casella 3 spuntata',
    'casella3.tolta': 'Casella 3 tolta', 'conferma.richiesta': 'Prima conferma', 'conferma.annullata': 'Conferma annullata',
    'firma.aperta': 'Area firma aperta', 'approvazione.perfezionata': 'APPROVAZIONE PERFEZIONATA',
    'ricevuta.generata': 'Ricevuta generata', 'ricevuta.inviata': 'Ricevuta inviata',
    'invito.inviato': 'Invito inviato al cliente', 'invito.promemoria': 'Promemoria dell\'invito', 'invito.aperto': 'Invito aperto dal cliente',
    'firmatario.indicato': 'Firmatario indicato dal cliente', 'richiesta.promemoria': 'Promemoria del link'
};

async function caricaRegistro() {
    try {
        const r = await chiamaApi('/api/condizioni/registro');
        $('tabella-registro').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="7" class="vuoto">Registro vuoto.</td></tr>'
            : r.map(x => `<tr><td class="numerico">${x.idEventoApprovazione}</td><td>${testo(x.data)}</td>
                <td>${testo(NOMI_EVENTO[x.tipo] || x.tipo)}</td><td>${x.idClienteArcLab ?? '—'}</td><td>${testo(x.nomeUtente) || '—'}</td>
                <td><details><summary class="sottotesto">vedi</summary><pre style="white-space:pre-wrap;max-width:40rem;font-size:.72rem">${testo(formattaJson(x.dati))}</pre></details></td>
                <td><code title="${testo(x.impronta)}">${testo((x.impronta || '').substring(0, 12))}…</code></td></tr>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}
function formattaJson(s) { try { return JSON.stringify(JSON.parse(s), null, 2); } catch { return s ?? ''; } }

async function verificaRegistro(pulsante) {
    const libera = occupa(pulsante, 'Verifica…');
    try {
        const r = await chiamaApi('/api/condizioni/registro/verifica');
        $('registro-esito').innerHTML = `<strong style="color:${r.integra ? '#1a7f37' : '#b3261e'}">${testo(r.messaggio)}</strong>`;
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ══ Invio massivo ══
let anteprima = null;

async function anteprimaMassivo(pulsante) {
    const file = $('m-file').files[0];
    if (!file) { avvisa('Scegli il file Excel compilato.', 'info'); return; }
    const fd = new FormData(); fd.append('file', file);
    const libera = occupa(pulsante, 'Controllo…');
    try {
        const risposta = await fetch('/api/condizioni/massivo/anteprima', { method: 'POST', body: fd, headers: { 'Accept': 'application/json' } });
        const r = await risposta.json().catch(() => ({}));
        if (!risposta.ok) throw new Error(r.messaggio || `Errore ${risposta.status}`);
        anteprima = r;
        renderAnteprima();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

const COLORE_ESITO = { errore: '#b3261e', salta: '#777', nuovo: '#1a7f37', cambia: '#9a6700', invariato: '#1a7f37' };
const righeDaPreparare = () => (anteprima?.righe ?? []).filter(r => (r.designa || r.crea) && (r.inclusa || ($('m-generiche').checked && r.emailGenerica)));

function renderAnteprima() {
    const r = anteprima;
    $('m-anteprima').classList.remove('nascosto');
    $('m-n-generiche').textContent = r.generiche;
    $('m-riepilogo').innerHTML = `Revisione: <strong>${testo(r.revisione)}</strong> · ${r.totale} righe · <strong>${righeDaPreparare().length} da preparare</strong> · ${r.saltate} saltate · <span style="color:#b3261e">${r.errori} errori</span>`;
    $('tabella-massivo').querySelector('tbody').innerHTML = r.righe.map(x => `<tr${(x.designa || x.crea) ? '' : ' style="opacity:.6"'}>
        <td class="numerico">${x.riga}</td><td>${testo(x.ragioneSociale) || x.idCliente}</td>
        <td>${testo(x.nome)} ${testo(x.cognome)}${x.qualifica ? '<br><span class="sottotesto">' + testo(x.qualifica) + '</span>' : ''}</td>
        <td>${testo(x.email)}</td><td style="color:${COLORE_ESITO[x.esito] || 'inherit'}">${testo(x.messaggio)}</td></tr>`).join('');
}

async function preparaMassivo(pulsante) {
    const righe = righeDaPreparare();
    if (righe.length === 0) { avvisa('Nessuna riga da preparare.', 'info'); return; }
    if (!confirm(`Preparare ${righe.length} clienti?\nVengono designati i Firmatari e create le richieste. Non parte ancora nessuna e-mail.`)) return;
    const libera = occupa(pulsante, 'Preparazione…');
    try {
        const r = await inviaJson('/api/condizioni/massivo/prepara', {
            righe: righe.map(x => ({ idCliente: x.idCliente, nome: x.nome, cognome: x.cognome, qualifica: x.qualifica, email: x.email }))
        });
        avvisa(r.messaggio, r.errori?.length ? 'info' : 'successo');
        $('m-errori').innerHTML = (r.errori ?? []).map(e => `<li>${testo(e)}</li>`).join('');
        anteprima = null; $('m-anteprima').classList.add('nascosto'); $('m-file').value = '';
        await Promise.all([caricaRichieste(), caricaRegistro(), statoMassivo()]);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function accodaMassivo(pulsante) {
    if (!confirm('Mettere in coda l\'invio del link a tutte le richieste «da inviare»?\nLe e-mail partono da sole, una alla volta.')) return;
    const libera = occupa(pulsante, 'Accodamento…');
    try { const r = await inviaJson('/api/condizioni/massivo/accoda', {}); avvisa(r.messaggio, 'successo'); await statoMassivo(); }
    catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function fermaMassivo() {
    if (!confirm('Fermare l\'invio? Le richieste non ancora inviate restano «da inviare».')) return;
    try { const r = await inviaJson('/api/condizioni/massivo/ferma', {}); avvisa(r.messaggio, 'successo'); await statoMassivo(); }
    catch (e) { avvisa(e.message, 'errore'); }
}

let timerStato = null;
async function statoMassivo() {
    clearTimeout(timerStato);
    try {
        const s = await chiamaApi('/api/condizioni/massivo/stato');
        $('m-stato').textContent = s.inCoda > 0
            ? `in coda ${s.inCoda} · inviati oggi ${s.inviatiOggi} su ${s.limite}`
            : `nessun invio in coda · inviati oggi ${s.inviatiOggi} su ${s.limite}`;
        if (s.errori?.length) $('m-errori').innerHTML = s.errori.map(e => `<li style="color:#b3261e">${testo(e.clienteRagioneSociale)}: ${testo(e.erroreInvioLink)}</li>`).join('');
        if (s.inCoda > 0) timerStato = setTimeout(() => { statoMassivo(); caricaRichieste(); }, 20000);
    } catch { /* non bloccante */ }
}

// ══ Avvio ══
$('btn-nuova-revisione')?.addEventListener('click', nuovaRevisione);
$('btn-revisione-chiudi')?.addEventListener('click', () => $('pannello-revisione').classList.add('nascosto'));
$('btn-revisione-salva')?.addEventListener('click', (e) => salvaRevisione(e.currentTarget));
$('btn-revisione-pubblica')?.addEventListener('click', (e) => pubblicaRevisione(e.currentTarget));
$('btn-revisione-copia')?.addEventListener('click', copiaRevisione);
$('btn-revisione-elimina')?.addEventListener('click', eliminaRevisione);
$('btn-revisione-ritira')?.addEventListener('click', ritiraRevisione);
$('r-numero')?.addEventListener('input', aggiornaRiferimento);
$('r-data')?.addEventListener('input', aggiornaRiferimento);
$('btn-pdf-carica')?.addEventListener('click', (e) => caricaPdf(e.currentTarget));
$('btn-clausola-riga')?.addEventListener('click', () => { clausole.push({ articolo: '', contenutoBreve: '', testoIntegrale: '' }); renderClausole(); });
$('btn-clausole-rev19')?.addEventListener('click', caricaClausoleRev19);
$('btn-registro-verifica')?.addEventListener('click', (e) => verificaRegistro(e.currentTarget));

// Ricerca cliente con elenco proprio (15/10/2026): il <datalist> del browser nascondeva i risultati della ricerca tollerante.
if ($('c-cerca')) collegaSuggerimenti($('c-cerca'), {
    cerca: async t => (await chiamaApi('/api/condizioni/clienti?filtro=' + encodeURIComponent(t))).map(c => ({ testo: c.nome, sotto: `n. ${c.id}`, valore: c.id })),
    scegli: v => apriCliente(v.valore)
});
$('btn-firmatario-salva')?.addEventListener('click', (e) => salvaFirmatario(e.currentTarget));
$('btn-richiesta-crea')?.addEventListener('click', (e) => creaRichiesta(e.currentTarget));
$('q-stato')?.addEventListener('change', caricaRichieste);
$('q-filtro')?.addEventListener('input', () => { clearTimeout(timerFiltro); timerFiltro = setTimeout(caricaRichieste, 300); });

$('btn-m-anteprima')?.addEventListener('click', (e) => anteprimaMassivo(e.currentTarget));
$('btn-m-prepara')?.addEventListener('click', (e) => preparaMassivo(e.currentTarget));
$('btn-m-accoda')?.addEventListener('click', (e) => accodaMassivo(e.currentTarget));
$('btn-m-ferma')?.addEventListener('click', fermaMassivo);
$('m-generiche')?.addEventListener('change', () => anteprima && renderAnteprima());

caricaRevisioni();
caricaRichieste();
caricaRegistro();
statoMassivo();


// ══ Richiesta del Firmatario ai clienti (11/10/2026) ══
const STATI_INVITO = { DaInviare: 'da inviare', Inviato: 'inviato', Aperto: 'aperto dal cliente', Indicato: 'risposto', Elaborato: 'risposto, link inviato', Annullato: 'annullato' };

async function invitaCliente(pulsante) {
    if (!clienteCorrente) return;
    const email = $('i-email').value.trim();
    if (!email) { avvisa("Indica l'e-mail a cui scrivere.", 'errore'); return; }
    if (!confirm(`Inviare a ${email} la richiesta di indicare il Firmatario?`)) return;
    const libera = occupa(pulsante, 'Invio…');
    try {
        const r = await inviaJson('/api/condizioni/inviti/cliente', { idCliente: clienteCorrente.idCliente, email });
        avvisa(r.messaggio, 'successo');
        await apriCliente(clienteCorrente.idCliente);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function situazioneInviti(pulsante) {
    const libera = pulsante ? occupa(pulsante, 'Calcolo…') : () => {};
    try {
        const r = await chiamaApi('/api/condizioni/inviti/situazione');
        $('i-revisione').textContent = r.revisione ? `Revisione in vigore: ${r.revisione} · ${r.totale} clienti con campioni negli ultimi 12 mesi` : 'Nessuna revisione pubblicata';
        $('tabella-i-situazione').querySelector('tbody').innerHTML = r.conteggi.map(c =>
            `<tr><td>${testo(c.situazione)}</td><td class="numerico">${c.numero}</td></tr>`).join('') || '<tr><td colspan="2" class="vuoto">Nessun cliente.</td></tr>';
        $('tabella-i-ultimi').querySelector('tbody').innerHTML = r.ultimi.map(i => `<tr>
            <td>${testo(i.clienteRagioneSociale)}</td><td>${testo(i.emailDestinatari)}</td>
            <td>${testo(STATI_INVITO[i.stato] ?? i.stato)}${i.dataIndicazione ? ' il ' + testo(i.dataIndicazione) : ''}</td>
            <td>${testo(i.indicato) || '—'}</td><td style="color:#b3261e">${testo(i.errore) || ''}</td></tr>`).join('')
            || '<tr><td colspan="5" class="vuoto">Ancora nessuna risposta.</td></tr>';
        $('i-situazione').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function preparaInviti(pulsante) {
    if (!confirm('Creare gli inviti per tutti i clienti «da invitare»? Non parte nessuna e-mail finché non vengono messi in coda.')) return;
    const libera = occupa(pulsante, 'Preparazione…');
    try { const r = await inviaJson('/api/condizioni/inviti/prepara', {}); avvisa(r.messaggio, 'successo'); await situazioneInviti(); }
    catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function accodaInviti(pulsante) {
    if (!confirm("Mettere in coda tutti gli inviti preparati?\nLe e-mail partono da sole ai clienti, una alla volta.")) return;
    const libera = occupa(pulsante, 'Accodamento…');
    try { const r = await inviaJson('/api/condizioni/inviti/accoda', {}); avvisa(r.messaggio, 'successo'); await Promise.all([situazioneInviti(), statoMassivo()]); }
    catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

$('btn-invito-cliente')?.addEventListener('click', (e) => invitaCliente(e.currentTarget));
$('btn-i-situazione')?.addEventListener('click', (e) => situazioneInviti(e.currentTarget));
$('btn-i-prepara')?.addEventListener('click', (e) => preparaInviti(e.currentTarget));
$('btn-i-accoda')?.addEventListener('click', (e) => accodaInviti(e.currentTarget));
