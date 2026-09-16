// Taratura termometri: registro, anagrafica CDR, esecuzione taratura
// (misure + calcolo + anteprima) e generazione del rapporto.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const num = (v) => { const x = parseFloat(String(v).replace(',', '.')); return isNaN(x) ? 0 : x; };
const numN = (id) => { const t = $(id).value.trim(); if (t === '') return null; const x = parseFloat(t.replace(',', '.')); return isNaN(x) ? null : x; };
const gg = (x, d = 2) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d }));

let clienti = [], cdrs = [], certRighe = [], certSecRighe = [], misure = [];

// ══ Registro ══
async function caricaRegistro() {
    try {
        const r = await chiamaApi('/api/taratura/elenco');
        $('conteggio-registro').textContent = `${r.length} tarature`;
        $('tabella-registro').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessuna taratura.</td></tr>'
            : r.map(t => `<tr class="riga-cliccabile" data-id="${t.idTaratura}">
                <td>${testo(t.numeroRapporto)}</td><td>${testo(t.strumentoIdentificazione)}</td>
                <td>${testo(t.committente) || '—'}</td><td>${testo(t.dataTaratura)}</td>
                <td>${t.dataScadenza ? testo(t.dataScadenza) : '—'} ${t.scaduta ? '<strong style="color:#b3261e">scaduta</strong>' : t.inScadenza ? '<strong style="color:#9a6700">in scadenza</strong>' : ''}</td></tr>`).join('');
        $('tabella-registro').querySelectorAll('.riga-cliccabile').forEach(x =>
            x.addEventListener('click', () => apriTaratura(Number(x.dataset.id))));
    } catch (e) { avvisa(e.message, 'errore'); }
}

// ══ CDR: lista + select ══
async function caricaCdr() {
    try {
        cdrs = await chiamaApi('/api/taratura/cdr/elenco');
        $('tabella-cdr').querySelector('tbody').innerHTML = cdrs.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessun CDR.</td></tr>'
            : cdrs.map(c => `<tr class="riga-cliccabile" data-id="${c.idCampione}">
                <td>${testo(c.identificazione)}</td><td>${testo(c.codiceInterno) || '—'}</td>
                <td>${testo(c.centroLat) || '—'}</td><td>${testo(c.numeroCertificato) || '—'}</td>
                <td>${c.dataScadenza ? testo(c.dataScadenza) : '—'} ${c.scaduto ? '<strong style="color:#b3261e">scaduto</strong>' : ''}</td></tr>`).join('');
        $('tabella-cdr').querySelectorAll('.riga-cliccabile').forEach(x =>
            x.addEventListener('click', () => apriCdr(Number(x.dataset.id))));
        const sel = $('t-cdr');
        sel.innerHTML = '<option value="">— seleziona —</option>' +
            cdrs.map(c => `<option value="${c.idCampione}">${testo(c.identificazione)}</option>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaClienti() {
    try {
        clienti = await chiamaApi('/api/taratura/clienti');
        $('lista-clienti-tar').innerHTML = clienti.map(c => `<option value="${testo(c.nome)}">`).join('');
    } catch { /* non bloccante */ }
    try {
        const strumenti = await chiamaApi('/api/taratura/strumenti');
        const dl = $('lista-strumenti-tar');
        if (dl) dl.innerHTML = strumenti.map(s => `<option value="${testo(s.nome)}">`).join('');
    } catch { /* non bloccante */ }
}

// ══ Form CDR ══
function nuovoCdr() {
    ['cdr-id', 'cdr-identificazione', 'cdr-tipo', 'cdr-codice', 'cdr-centrolat', 'cdr-certificato', 'cdr-datatar', 'cdr-scadenza'].forEach(id => $(id).value = '');
    $('cdr-risoluzione').value = '0.01'; $('cdr-k').value = '2'; $('cdr-instabilita').value = '0'; $('cdr-ue-testata').value = '0';
    certRighe = [emptyCert(), emptyCert()];
    certSecRighe = [];
    renderCert(); renderCertSec();
    $('cdr-titolo').textContent = 'Nuovo CDR';
    $('pannello-cdr').classList.remove('nascosto');
    $('pannello-taratura').classList.add('nascosto');
    $('cdr-identificazione').focus();
}

function emptyCert() { return { tIndicata: 0, tRiferimento: 0, differenza: 0, incertezzaEstesa: 0.06, incertezzaTipo: 0.025 }; }

async function apriCdr(id) {
    try {
        const c = await chiamaApi('/api/taratura/cdr/' + id);
        $('cdr-id').value = c.idCampione;
        $('cdr-identificazione').value = c.identificazione ?? '';
        $('cdr-tipo').value = c.tipoTermometro ?? '';
        $('cdr-codice').value = c.codiceInterno ?? '';
        $('cdr-centrolat').value = c.centroLat ?? '';
        $('cdr-certificato').value = c.numeroCertificato ?? '';
        $('cdr-datatar').value = (c.dataTaratura ?? '').substring(0, 10);
        $('cdr-scadenza').value = (c.dataScadenza ?? '').substring(0, 10);
        $('cdr-risoluzione').value = c.risoluzione ?? 0.01;
        $('cdr-k').value = c.fattoreCoperturaK ?? 2;
        $('cdr-instabilita').value = c.incertezzaInstabilita ?? 0;
        $('cdr-ue-testata').value = c.incertezzaEstesaCertificato ?? 0;
        certRighe = (c.punti ?? []).map(p => ({ ...p }));
        if (certRighe.length === 0) certRighe = [emptyCert(), emptyCert()];
        certSecRighe = (c.puntiSecondari ?? []).map(p => ({ ...p }));
        renderCert(); renderCertSec();
        $('cdr-titolo').textContent = 'Modifica CDR';
        $('pannello-cdr').classList.remove('nascosto');
        $('pannello-cdr').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderCert() {
    const campi = ['tIndicata', 'tRiferimento', 'differenza', 'incertezzaEstesa', 'incertezzaTipo'];
    $('tabella-cert').querySelector('tbody').innerHTML = certRighe.map((r, i) =>
        `<tr>${campi.map(c => `<td><input type="number" step="0.001" data-i="${i}" data-c="${c}" value="${r[c]}" style="width:6.5rem" /></td>`).join('')}
        <td><button type="button" class="pulsante pulsante-tenue cert-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-cert').querySelectorAll('input').forEach(inp =>
        inp.addEventListener('input', () => { certRighe[Number(inp.dataset.i)][inp.dataset.c] = num(inp.value); }));
    $('tabella-cert').querySelectorAll('.cert-rm').forEach(b =>
        b.addEventListener('click', () => { certRighe.splice(Number(b.dataset.i), 1); renderCert(); }));
}

function emptyCertSec() { return { tIndicata: 0, tRiferimento: 0, differenza: 0, incertezzaEstesa: 0, incertezzaTipo: 0 }; }

function renderCertSec() {
    const campi = ['tIndicata', 'tRiferimento'];
    $('tabella-cert-sec').querySelector('tbody').innerHTML = certSecRighe.map((r, i) =>
        `<tr>${campi.map(c => `<td><input type="number" step="0.001" data-i="${i}" data-c="${c}" value="${r[c]}" style="width:6.5rem" /></td>`).join('')}
        <td><button type="button" class="pulsante pulsante-tenue certsec-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-cert-sec').querySelectorAll('input').forEach(inp =>
        inp.addEventListener('input', () => { certSecRighe[Number(inp.dataset.i)][inp.dataset.c] = num(inp.value); }));
    $('tabella-cert-sec').querySelectorAll('.certsec-rm').forEach(b =>
        b.addEventListener('click', () => { certSecRighe.splice(Number(b.dataset.i), 1); renderCertSec(); }));
}

async function salvaCdr(pulsante) {
    if (!$('cdr-identificazione').value.trim()) { avvisa('Identificazione obbligatoria.', 'errore'); return; }
    const corpo = {
        id: $('cdr-id').value ? Number($('cdr-id').value) : null,
        identificazione: $('cdr-identificazione').value, tipoTermometro: $('cdr-tipo').value,
        codiceInterno: $('cdr-codice').value, centroLat: $('cdr-centrolat').value,
        numeroCertificato: $('cdr-certificato').value,
        dataTaratura: $('cdr-datatar').value || null, dataScadenza: $('cdr-scadenza').value || null,
        risoluzione: num($('cdr-risoluzione').value), fattoreCoperturaK: num($('cdr-k').value),
        incertezzaInstabilita: num($('cdr-instabilita').value),
        incertezzaEstesaCertificato: num($('cdr-ue-testata').value),
        punti: certRighe,
        puntiSecondari: certSecRighe
    };
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const e = await inviaJson('/api/taratura/cdr/salva', corpo);
        $('cdr-id').value = e.idCampione;
        avvisa('CDR salvato.', 'successo');
        $('pannello-cdr').classList.add('nascosto');
        await caricaCdr();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

// ══ Form taratura ══
function nuovaTaratura() {
    ['t-id', 't-strumento', 't-codice', 't-committente', 't-scadenza', 't-numero', 't-responsabile'].forEach(id => $(id).value = '');
    $('t-cdr').value = ''; $('t-risoluzione').value = '0.1'; $('t-istruzione').value = 'IO 4-01';
    $('t-data').value = new Date().toISOString().substring(0, 10);
    misure = [nuovaMisura('A')];
    renderMisure();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura';
    $('pannello-taratura').classList.remove('nascosto');
    $('pannello-cdr').classList.add('nascosto');
    $('t-strumento').focus();
}

function nuovaMisura(p) { return { punto: p || 'A', tRiferimento: 0, letturaStrumento: 0 }; }

// Duplica la taratura corrente per un altro termometro sullo stesso CDR:
// mantiene intestazione e valori del primario (T riferimento); azzera identità
// e n° rapporto. Le letture dello strumento sono copiate o azzerate a scelta.
function duplica() {
    if (!$('t-cdr').value) { avvisa('Apri o seleziona prima una taratura da duplicare.', 'info'); return; }

    const copiaLetture = $('t-copia-letture')?.checked ?? true;
    const intestazione = {
        cdr: $('t-cdr').value,
        risoluzione: $('t-risoluzione').value,
        committente: $('t-committente').value,
        istruzione: $('t-istruzione').value,
        data: $('t-data').value,
        scadenza: $('t-scadenza').value,
        responsabile: $('t-responsabile').value
    };
    const nuoveMisure = misure.map(m => ({
        punto: m.punto,
        tRiferimento: m.tRiferimento,
        letturaStrumento: copiaLetture ? m.letturaStrumento : 0
    }));

    // Nuova taratura: azzera identità strumento e numero, ripristina intestazione.
    ['t-id', 't-strumento', 't-codice', 't-numero'].forEach(id => $(id).value = '');
    $('t-cdr').value = intestazione.cdr;
    $('t-risoluzione').value = intestazione.risoluzione;
    $('t-committente').value = intestazione.committente;
    $('t-istruzione').value = intestazione.istruzione;
    $('t-data').value = intestazione.data;
    $('t-scadenza').value = intestazione.scadenza;
    $('t-responsabile').value = intestazione.responsabile;

    misure = nuoveMisure.length ? nuoveMisure : [nuovaMisura('A')];
    renderMisure();

    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura (duplicata)';
    $('pannello-taratura').classList.remove('nascosto');
    $('pannello-cdr').classList.add('nascosto');
    $('t-strumento').focus();
    avvisa(copiaLetture
        ? 'Duplicata: inserisci il nuovo strumento e ritocca le letture.'
        : 'Duplicata: inserisci il nuovo strumento e le sue letture.', 'successo');
}

async function apriTaratura(id) {
    try {
        const t = await chiamaApi('/api/taratura/' + id);
        $('t-id').value = t.idTaratura;
        $('t-cdr').value = t.idCampioneRiferimento;
        $('t-strumento').value = t.strumentoIdentificazione ?? '';
        $('t-codice').value = t.strumentoCodice ?? '';
        $('t-risoluzione').value = t.risoluzioneStrumento ?? 0.1;
        $('t-committente').value = t.committente ?? '';
        $('t-istruzione').value = t.riferimentoIstruzione ?? '';
        $('t-responsabile').value = t.responsabile ?? '';
        $('t-data').value = (t.dataTaratura ?? '').substring(0, 10);
        $('t-scadenza').value = (t.dataScadenza ?? '').substring(0, 10);
        $('t-numero').value = t.numeroRapporto ?? '';
        misure = (t.misure ?? []).map(m => ({ punto: m.punto, tRiferimento: m.tRiferimento, letturaStrumento: m.letturaStrumento }));
        if (misure.length === 0) misure = [nuovaMisura('A')];
        renderMisure();
        $('taratura-numero').textContent = t.numeroRapporto ? 'N° ' + t.numeroRapporto : '';
        $('pannello-risultati-taratura').classList.add('nascosto');
        $('pannello-analisi-ai').classList.add('nascosto');
        $('taratura-titolo').textContent = 'Modifica taratura';
        $('pannello-taratura').classList.remove('nascosto');
        $('pannello-cdr').classList.add('nascosto');
        $('pannello-taratura').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderMisure() {
    // Numerazione della lettura all'interno del punto: riparte a ogni cambio di lettera.
    // Solo di supporto all'inserimento: non finisce nei rapporti.
    let prevPunto = null, contatore = 0;
    const numeri = misure.map(m => {
        const p = (m.punto || '').trim().toUpperCase();
        if (p !== prevPunto) { contatore = 1; prevPunto = p; } else { contatore++; }
        return contatore;
    });
    $('tabella-misure').querySelector('tbody').innerHTML = misure.map((m, i) =>
        `<tr>
            <td class="numerico" style="color:#888">${numeri[i]}</td>
            <td><input type="text" data-i="${i}" data-c="punto" value="${testo(m.punto)}" style="width:4rem" /></td>
            <td class="numerico"><input type="number" step="0.001" data-i="${i}" data-c="tRiferimento" value="${m.tRiferimento}" style="width:7rem" /></td>
            <td class="numerico"><input type="number" step="0.001" data-i="${i}" data-c="letturaStrumento" value="${m.letturaStrumento}" style="width:7rem" /></td>
            <td><button type="button" class="pulsante pulsante-tenue mis-rm" data-i="${i}">×</button></td>
        </tr>`).join('');
    $('tabella-misure').querySelectorAll('input').forEach(inp =>
        inp.addEventListener('input', () => {
            const v = inp.dataset.c === 'punto' ? inp.value : num(inp.value);
            misure[Number(inp.dataset.i)][inp.dataset.c] = v;
        }));
    // Alla fine della modifica di un "Punto" ricalcola la numerazione (riparte a ogni lettera).
    $('tabella-misure').querySelectorAll('input[data-c="punto"]').forEach(inp =>
        inp.addEventListener('change', () => renderMisure()));
    $('tabella-misure').querySelectorAll('.mis-rm').forEach(b =>
        b.addEventListener('click', () => { misure.splice(Number(b.dataset.i), 1); renderMisure(); }));
}

function corpoTaratura() {
    return {
        idCampioneRiferimento: $('t-cdr').value ? Number($('t-cdr').value) : 0,
        risoluzioneStrumento: num($('t-risoluzione').value),
        fattoreCopertura: 2,
        misure: misure.filter(m => (m.punto || '').trim() !== '')
    };
}

async function calcola(pulsante) {
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const libera = occupa(pulsante, 'Calcolo…');
    try {
        const r = await inviaJson('/api/taratura/calcola', corpoTaratura());
        $('tabella-risultati-taratura').querySelector('tbody').innerHTML = r.punti.map(p =>
            `<tr><td>${testo(p.etichetta)}</td>
             <td class="numerico">${gg(p.temperaturaMediaRiferimento)}</td>
             <td class="numerico">${gg(p.temperaturaMediaStrumento)}</td>
             <td class="numerico">${gg(p.differenzaMedia, 3)}</td>
             <td class="numerico">${gg(p.incertezzaEstesa, 3)}</td>
             <td class="numerico">${gg(p.fattoreCorrezione, 3)}</td></tr>`).join('');
        $('pannello-risultati-taratura').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function salvaTaratura(pulsante) {
    if (!$('t-strumento').value.trim()) { avvisa('Identificazione strumento obbligatoria.', 'errore'); return; }
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const nomeComm = $('t-committente').value.trim();
    const cliente = clienti.find(c => c.nome.toLowerCase() === nomeComm.toLowerCase());
    const corpo = {
        id: $('t-id').value ? Number($('t-id').value) : null,
        idCampioneRiferimento: Number($('t-cdr').value),
        strumentoIdentificazione: $('t-strumento').value, strumentoCodice: $('t-codice').value,
        risoluzioneStrumento: num($('t-risoluzione').value), fattoreCopertura: 2,
        idClienteArcLab: cliente ? cliente.id : null, committente: nomeComm || null,
        numeroRapporto: $('t-numero').value || null,
        dataTaratura: $('t-data').value, dataScadenza: $('t-scadenza').value || null,
        riferimentoIstruzione: $('t-istruzione').value,
        responsabile: $('t-responsabile').value || null,
        misure: misure.filter(m => (m.punto || '').trim() !== '')
    };
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const e = await inviaJson('/api/taratura/salva', corpo);
        $('t-id').value = e.idTaratura;
        $('t-numero').value = e.numeroRapporto;
        $('taratura-numero').textContent = 'N° ' + e.numeroRapporto;
        avvisa('Taratura salvata.', 'successo');
        await caricaRegistro();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

async function scaricaRapporto(pulsante, formato) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/taratura/${id}/rapporto`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formato, responsabile: $('t-responsabile').value })
        });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const blob = await risposta.blob();
        const cd = risposta.headers.get('Content-Disposition') || '';
        const m = cd.match(/filename="?([^"]+)"?/);
        const nome = m ? m[1] : (formato === 'word' ? 'Rapporto_Taratura.rtf' : 'Rapporto_Taratura.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = nome;
        document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
        avvisa('Rapporto generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ══ Validazione del calcolo ══
async function caricaStatoValidazione() {
    const el = $('validazione-stato'); if (!el) return;
    try {
        const s = await chiamaApi('/api/taratura/validazione/stato');
        const av = $('validazione-avviso');
        if (!s.eseguitaMai) {
            el.textContent = 'mai eseguita';
            av.textContent = 'La validazione del calcolo non è mai stata eseguita: eseguila per avere l\'evidenza.';
            av.classList.remove('nascosto');
        } else {
            el.textContent = `ultima: ${s.data} — ${s.validato ? 'VALIDATO' : 'NON VALIDATO'}`;
            if (s.scaduta) {
                av.textContent = `Ultima validazione del ${s.data}: oltre ${s.mesiValidita} mesi fa. Esegui una nuova validazione.`;
                av.classList.remove('nascosto');
            } else if (!s.validato) {
                av.textContent = 'L\'ultima validazione risulta NON VALIDATA: verifica il calcolo prima di emettere rapporti.';
                av.classList.remove('nascosto');
            } else {
                av.classList.add('nascosto');
            }
        }
    } catch { /* non bloccante */ }
}

async function caricaRegistroValidazioni() {
    const tb = $('tabella-validazioni'); if (!tb) return;
    try {
        const r = await chiamaApi('/api/taratura/validazione/elenco');
        tb.querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="7" class="vuoto">Nessuna validazione.</td></tr>'
            : r.map(v => `<tr>
                <td>${testo(v.data)}</td>
                <td>${v.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td>
                <td class="numerico">${v.numeroOk}/${v.numeroKo}</td>
                <td>${testo(v.versioneSet)}</td>
                <td>${testo(v.versioneApplicazione) || '—'}</td>
                <td>${testo(v.operatore) || '—'}</td>
                <td><a href="/api/taratura/validazione/${v.idEsito}/rapporto" target="_blank" rel="noopener">PDF</a></td>
            </tr>`).join('');
    } catch { /* non bloccante */ }
}

async function valida(pulsante) {
    const libera = occupa(pulsante, 'Validazione…');
    try {
        const e = await inviaJson('/api/taratura/validazione/esegui', {});
        $('validazione-esito').classList.remove('nascosto');
        $('validazione-badge').innerHTML = e.validato
            ? `<p><strong style="color:#1a7f37">VALIDATO</strong> — ${e.numeroOk} confronti OK (${testo(e.data)})</p>`
            : `<p><strong style="color:#b3261e">NON VALIDATO</strong> — ${e.numeroKo} KO su ${e.numeroOk + e.numeroKo}</p>`;
        $('tabella-validazione-casi').querySelector('tbody').innerHTML = e.casi.map(c =>
            `<tr><td>${testo(c.nome)}</td><td>${testo(c.descrizione)}</td>
             <td class="numerico">${c.ok}/${c.ko}</td>
             <td>${c.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td></tr>`).join('');
        avvisa(e.validato ? 'Validazione superata.' : 'Validazione NON superata.', e.validato ? 'successo' : 'errore');
        await caricaStatoValidazione();
        await caricaRegistroValidazioni();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

// ══ Analisi AI anti-errore ══
async function analizza(pulsante) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Analisi…');
    try {
        const r = await inviaJson(`/api/taratura/${id}/analizza`, {});
        $('pannello-analisi-ai').classList.remove('nascosto');
        $('analisi-ai-badge').textContent = (r.daArchivio ? 'da archivio · ' : '') + (r.dataAnalisi || '');
        const an = r.anomalie || [];
        $('analisi-ai-anomalie').innerHTML = an.length === 0
            ? '<p class="sottotesto">Nessuna anomalia dai controlli automatici.</p>'
            : '<ul>' + an.map(a =>
                `<li><strong style="color:${a.livello === 'Errore' ? '#b3261e' : '#9a6700'}">${testo(a.livello)}</strong>` +
                `${a.punto ? ' — punto ' + testo(a.punto) : ''}: ${testo(a.messaggio)}</li>`).join('') + '</ul>';
        $('analisi-ai-testo').textContent = r.testo || '';
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ══ Avvio ══
// NB: uso l'optional chaining (?.) su ogni aggancio: se una vista non fosse
// aggiornata e mancasse un pulsante, il modulo non si interrompe e le tabelle
// vengono comunque caricate.
$('btn-nuovo-cdr')?.addEventListener('click', nuovoCdr);
$('btn-cdr-annulla')?.addEventListener('click', () => $('pannello-cdr').classList.add('nascosto'));
$('btn-cdr-salva')?.addEventListener('click', (e) => salvaCdr(e.currentTarget));
$('btn-cert-riga')?.addEventListener('click', () => { certRighe.push(emptyCert()); renderCert(); });
$('btn-cert-sec-riga')?.addEventListener('click', () => { certSecRighe.push(emptyCertSec()); renderCertSec(); });

$('btn-nuova-taratura')?.addEventListener('click', nuovaTaratura);
$('btn-taratura-annulla')?.addEventListener('click', () => $('pannello-taratura').classList.add('nascosto'));
$('btn-misura-riga')?.addEventListener('click', () => { misure.push(nuovaMisura(misure.length ? misure[misure.length - 1].punto : 'A')); renderMisure(); });
$('btn-calcola')?.addEventListener('click', (e) => calcola(e.currentTarget));
$('btn-taratura-salva')?.addEventListener('click', (e) => salvaTaratura(e.currentTarget));
$('btn-taratura-duplica')?.addEventListener('click', duplica);
$('btn-taratura-pdf')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'pdf'));
$('btn-taratura-word')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'word'));
$('btn-taratura-etichetta')?.addEventListener('click', () => {
    const id = $('t-id').value;
    if (!id) { alert('Salva prima la taratura per generare l\'etichetta.'); return; }
    window.open(`/api/taratura/${id}/etichetta`, '_blank');
});
$('btn-taratura-analizza')?.addEventListener('click', (e) => analizza(e.currentTarget));
$('btn-valida')?.addEventListener('click', (e) => valida(e.currentTarget));

caricaClienti();
caricaCdr();
caricaRegistro();
caricaStatoValidazione();
caricaRegistroValidazioni();
