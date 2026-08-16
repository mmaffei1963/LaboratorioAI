// Taratura incubatori/celle: registro, esecuzione (misure + calcolo + anteprima),
// rapporto, duplicazione, analisi AI e validazione del calcolo.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const num = (v) => { const x = parseFloat(String(v).replace(',', '.')); return isNaN(x) ? 0 : x; };
const gg = (x, d = 2) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d }));

let cdrs = [], clienti = [], strumenti = [], misure = [];

// ══ Caricamenti ══
async function caricaRegistro() {
    try {
        const r = await chiamaApi('/api/incubatori/elenco');
        $('conteggio-registro').textContent = `${r.length} tarature`;
        $('tabella-registro').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="6" class="vuoto">Nessuna taratura.</td></tr>'
            : r.map(t => `<tr class="riga-cliccabile" data-id="${t.idTaratura}">
                <td>${testo(t.numeroRapporto)}</td><td>${testo(t.misuratoreIdentificazione)}</td>
                <td>${testo(t.apparecchiaturaIdentificazione)}</td>
                <td class="numerico">${gg(t.valoreNominale)}</td><td>${testo(t.dataTaratura)}</td>
                <td>${t.dataScadenza ? testo(t.dataScadenza) : '—'} ${t.scaduta ? '<strong style="color:#b3261e">scaduta</strong>' : ''}</td></tr>`).join('');
        $('tabella-registro').querySelectorAll('.riga-cliccabile').forEach(x =>
            x.addEventListener('click', () => apri(Number(x.dataset.id))));
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaCdr() {
    try {
        cdrs = await chiamaApi('/api/incubatori/cdr/elenco');
        const sel = $('t-cdr');
        sel.innerHTML = '<option value="">— seleziona —</option>' +
            cdrs.map(c => `<option value="${c.idCampione}">${testo(c.identificazione)}</option>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaAnagrafiche() {
    try { clienti = await chiamaApi('/api/incubatori/clienti'); } catch { clienti = []; }
    try { strumenti = await chiamaApi('/api/incubatori/strumenti'); } catch { strumenti = []; }
    aggiornaDatalist('m');
    aggiornaDatalist('a');
}

// Sorgente del datalist secondo l'origine (interno = strumenti, cliente = clienti).
function aggiornaDatalist(pref) {
    const origine = $(pref + '-origine').value;
    const voci = origine === 'interno' ? strumenti : clienti;
    $('lista-' + pref).innerHTML = voci.map(v => `<option value="${testo(v.nome)}">`).join('');
}

function risolviStrumento(pref) {
    const origine = $(pref + '-origine').value;
    const nome = $(pref + '-identificazione').value.trim();
    const interno = origine === 'interno';
    const lista = interno ? strumenti : clienti;
    const trovato = lista.find(v => (v.nome || '').toLowerCase() === nome.toLowerCase());
    return {
        identificazione: nome,
        codice: $(pref + '-codice').value.trim() || null,
        interno,
        idStrumentoArcLab: interno && trovato ? trovato.id : null,
        idClienteArcLab: !interno && trovato ? trovato.id : null
    };
}

// ══ Form ══
function nuova() {
    ['t-id', 't-numero', 't-scadenza', 'm-identificazione', 'm-codice', 'a-identificazione', 'a-codice', 't-nominale'].forEach(id => $(id).value = '');
    $('t-cdr').value = ''; $('t-istruzione').value = 'IO 04-02';
    $('t-tolleranza').value = '1'; $('t-risoluzione').value = '0.1'; $('t-mappa').value = '0';
    $('m-origine').value = 'interno'; $('a-origine').value = 'interno';
    $('t-responsabile').value = $('t-responsabile').value; // conserva default
    $('t-data').value = new Date().toISOString().substring(0, 10);
    aggiornaDatalist('m'); aggiornaDatalist('a');
    misure = [nuovaMisura('A')];
    renderMisure();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura';
    $('pannello-taratura').classList.remove('nascosto');
    $('m-identificazione').focus();
}

function nuovaMisura(p) { return { posizione: p || 'A', temperaturaRilevata: 0, letturaStrumento: 0 }; }

async function apri(id) {
    try {
        const t = await chiamaApi('/api/incubatori/' + id);
        $('t-id').value = t.idTaratura;
        $('t-cdr').value = t.idCampioneRiferimento;
        $('m-origine').value = t.misuratoreInterno ? 'interno' : 'cliente';
        $('a-origine').value = t.apparecchiaturaInterna ? 'interno' : 'cliente';
        aggiornaDatalist('m'); aggiornaDatalist('a');
        $('m-identificazione').value = t.misuratoreIdentificazione ?? '';
        $('m-codice').value = t.misuratoreCodice ?? '';
        $('a-identificazione').value = t.apparecchiaturaIdentificazione ?? '';
        $('a-codice').value = t.apparecchiaturaCodice ?? '';
        $('t-nominale').value = t.valoreNominale;
        $('t-tolleranza').value = t.tolleranza;
        $('t-risoluzione').value = t.risoluzioneStrumento ?? 0.1;
        $('t-mappa').value = t.dispersioneMappatura ?? 0;
        $('t-istruzione').value = t.riferimentoIstruzione ?? '';
        $('t-responsabile').value = t.responsabile ?? '';
        $('t-data').value = (t.dataTaratura ?? '').substring(0, 10);
        $('t-scadenza').value = (t.dataScadenza ?? '').substring(0, 10);
        $('t-numero').value = t.numeroRapporto ?? '';
        misure = (t.misure ?? []).map(m => ({ posizione: m.posizione, temperaturaRilevata: m.temperaturaRilevata, letturaStrumento: m.letturaStrumento }));
        if (misure.length === 0) misure = [nuovaMisura('A')];
        renderMisure();
        $('taratura-numero').textContent = t.numeroRapporto ? 'N° ' + t.numeroRapporto : '';
        $('pannello-risultati-taratura').classList.add('nascosto');
        $('pannello-analisi-ai').classList.add('nascosto');
        $('taratura-titolo').textContent = 'Modifica taratura';
        $('pannello-taratura').classList.remove('nascosto');
        $('pannello-taratura').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderMisure() {
    $('tabella-misure').querySelector('tbody').innerHTML = misure.map((m, i) =>
        `<tr>
            <td><input type="text" data-i="${i}" data-c="posizione" value="${testo(m.posizione)}" style="width:4rem" /></td>
            <td class="numerico"><input type="number" step="0.001" data-i="${i}" data-c="temperaturaRilevata" value="${m.temperaturaRilevata}" style="width:7rem" /></td>
            <td class="numerico"><input type="number" step="0.001" data-i="${i}" data-c="letturaStrumento" value="${m.letturaStrumento}" style="width:7rem" /></td>
            <td><button type="button" class="pulsante pulsante-tenue mis-rm" data-i="${i}">×</button></td>
        </tr>`).join('');
    $('tabella-misure').querySelectorAll('input').forEach(inp =>
        inp.addEventListener('input', () => {
            const v = inp.dataset.c === 'posizione' ? inp.value : num(inp.value);
            misure[Number(inp.dataset.i)][inp.dataset.c] = v;
        }));
    $('tabella-misure').querySelectorAll('.mis-rm').forEach(b =>
        b.addEventListener('click', () => { misure.splice(Number(b.dataset.i), 1); renderMisure(); }));
}

function corpoCalcolo() {
    return {
        idCampioneRiferimento: $('t-cdr').value ? Number($('t-cdr').value) : 0,
        valoreNominale: num($('t-nominale').value),
        tolleranza: num($('t-tolleranza').value),
        risoluzioneStrumento: num($('t-risoluzione').value),
        dispersioneMappatura: num($('t-mappa').value),
        misure: misure.filter(m => (m.posizione || '').trim() !== '')
    };
}

function renderRisultato(r) {
    const conf = r.conforme
        ? '<strong style="color:#1a7f37">CONFORME</strong>'
        : '<strong style="color:#b3261e">NON CONFORME</strong>';
    $('tabella-risultati-taratura').querySelector('tbody').innerHTML =
        `<tr><td class="numerico">${gg(r.temperaturaMediaRiferimento)}</td>
         <td class="numerico">${gg(r.temperaturaMediaStrumento)}</td>
         <td class="numerico">${gg(r.differenzaMedia, 3)}</td>
         <td class="numerico">${gg(r.incertezzaEstesa, 3)}</td>
         <td class="numerico">${gg(r.fattoreCorrezione, 3)}</td>
         <td>${gg(r.allarmeMinimo)} … ${gg(r.allarmeMassimo)}</td>
         <td>${conf}</td></tr>`;
    $('pannello-risultati-taratura').classList.remove('nascosto');
}

async function calcola(pulsante) {
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const libera = occupa(pulsante, 'Calcolo…');
    try { renderRisultato(await inviaJson('/api/incubatori/calcola', corpoCalcolo())); }
    catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

function corpoSalva() {
    const m = risolviStrumento('m');
    const a = risolviStrumento('a');
    return {
        id: $('t-id').value ? Number($('t-id').value) : null,
        idCampioneRiferimento: Number($('t-cdr').value),
        misuratoreIdentificazione: m.identificazione, misuratoreCodice: m.codice,
        misuratoreInterno: m.interno, misuratoreIdStrumentoArcLab: m.idStrumentoArcLab, misuratoreIdClienteArcLab: m.idClienteArcLab,
        apparecchiaturaIdentificazione: a.identificazione, apparecchiaturaCodice: a.codice,
        apparecchiaturaInterna: a.interno, apparecchiaturaIdStrumentoArcLab: a.idStrumentoArcLab, apparecchiaturaIdClienteArcLab: a.idClienteArcLab,
        valoreNominale: num($('t-nominale').value), tolleranza: num($('t-tolleranza').value),
        risoluzioneStrumento: num($('t-risoluzione').value), dispersioneMappatura: num($('t-mappa').value),
        numeroRapporto: $('t-numero').value || null,
        dataTaratura: $('t-data').value, dataScadenza: $('t-scadenza').value || null,
        riferimentoIstruzione: $('t-istruzione').value,
        responsabile: $('t-responsabile').value || null,
        misure: misure.filter(m => (m.posizione || '').trim() !== '')
    };
}

async function salva(pulsante) {
    if (!$('m-identificazione').value.trim()) { avvisa('Identificazione misuratore obbligatoria.', 'errore'); return; }
    if (!$('a-identificazione').value.trim()) { avvisa('Identificazione apparecchiatura obbligatoria.', 'errore'); return; }
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const e = await inviaJson('/api/incubatori/salva', corpoSalva());
        $('t-id').value = e.idTaratura;
        $('t-numero').value = e.numeroRapporto;
        $('taratura-numero').textContent = 'N° ' + e.numeroRapporto;
        avvisa('Taratura salvata.', 'successo');
        await caricaRegistro();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

function duplica() {
    if (!$('t-cdr').value) { avvisa('Apri o seleziona prima una taratura da duplicare.', 'info'); return; }
    const copia = $('t-copia-letture')?.checked ?? true;
    const stato = {
        cdr: $('t-cdr').value, nominale: $('t-nominale').value, tolleranza: $('t-tolleranza').value,
        risoluzione: $('t-risoluzione').value, mappa: $('t-mappa').value,
        istruzione: $('t-istruzione').value, data: $('t-data').value, scadenza: $('t-scadenza').value,
        responsabile: $('t-responsabile').value,
        aOrig: $('a-origine').value, aId: $('a-identificazione').value, aCod: $('a-codice').value
    };
    const nuoveMisure = misure.map(m => ({ posizione: m.posizione, temperaturaRilevata: m.temperaturaRilevata, letturaStrumento: copia ? m.letturaStrumento : 0 }));

    ['t-id', 't-numero', 'm-identificazione', 'm-codice'].forEach(id => $(id).value = '');
    $('t-cdr').value = stato.cdr; $('t-nominale').value = stato.nominale; $('t-tolleranza').value = stato.tolleranza;
    $('t-risoluzione').value = stato.risoluzione; $('t-mappa').value = stato.mappa;
    $('t-istruzione').value = stato.istruzione; $('t-data').value = stato.data; $('t-scadenza').value = stato.scadenza;
    $('t-responsabile').value = stato.responsabile;
    $('a-origine').value = stato.aOrig; aggiornaDatalist('a');
    $('a-identificazione').value = stato.aId; $('a-codice').value = stato.aCod;
    $('m-origine').value = 'interno'; aggiornaDatalist('m');
    misure = nuoveMisure.length ? nuoveMisure : [nuovaMisura('A')];
    renderMisure();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura (duplicata)';
    $('m-identificazione').focus();
    avvisa('Duplicata: inserisci il nuovo misuratore e ritocca le letture.', 'successo');
}

async function scaricaRapporto(pulsante, formato) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/incubatori/${id}/rapporto`, {
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
        const mm = cd.match(/filename="?([^"]+)"?/);
        const nome = mm ? mm[1] : (formato === 'word' ? 'Rapporto_Incubatore.rtf' : 'Rapporto_Incubatore.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = nome;
        document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
        avvisa('Rapporto generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function analizza(pulsante) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Analisi…');
    try {
        const r = await inviaJson(`/api/incubatori/${id}/analizza`, {});
        $('pannello-analisi-ai').classList.remove('nascosto');
        $('analisi-ai-badge').textContent = (r.daArchivio ? 'da archivio · ' : '') + (r.dataAnalisi || '');
        const an = r.anomalie || [];
        $('analisi-ai-anomalie').innerHTML = an.length === 0
            ? '<p class="sottotesto">Nessuna anomalia dai controlli automatici.</p>'
            : '<ul>' + an.map(a =>
                `<li><strong style="color:${a.livello === 'Errore' ? '#b3261e' : '#9a6700'}">${testo(a.livello)}</strong>` +
                `${a.punto ? ' — pos. ' + testo(a.punto) : ''}: ${testo(a.messaggio)}</li>`).join('') + '</ul>';
        $('analisi-ai-testo').textContent = r.testo || '';
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ══ Validazione ══
async function caricaStatoValidazione() {
    const el = $('validazione-stato'); if (!el) return;
    try {
        const s = await chiamaApi('/api/incubatori/validazione/stato');
        const av = $('validazione-avviso');
        if (!s.eseguitaMai) {
            el.textContent = 'mai eseguita';
            av.textContent = 'La validazione del calcolo non è mai stata eseguita: eseguila per avere l\'evidenza.';
            av.classList.remove('nascosto');
        } else {
            el.textContent = `ultima: ${s.data} — ${s.validato ? 'VALIDATO' : 'NON VALIDATO'}`;
            if (s.scaduta) { av.textContent = `Ultima validazione del ${s.data}: oltre ${s.mesiValidita} mesi fa. Esegui una nuova validazione.`; av.classList.remove('nascosto'); }
            else if (!s.validato) { av.textContent = 'L\'ultima validazione risulta NON VALIDATA.'; av.classList.remove('nascosto'); }
            else av.classList.add('nascosto');
        }
    } catch { /* non bloccante */ }
}

async function caricaRegistroValidazioni() {
    const tb = $('tabella-validazioni'); if (!tb) return;
    try {
        const r = await chiamaApi('/api/incubatori/validazione/elenco');
        tb.querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="7" class="vuoto">Nessuna validazione.</td></tr>'
            : r.map(v => `<tr>
                <td>${testo(v.data)}</td>
                <td>${v.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td>
                <td class="numerico">${v.numeroOk}/${v.numeroKo}</td>
                <td>${testo(v.versioneSet)}</td><td>${testo(v.versioneApplicazione) || '—'}</td><td>${testo(v.operatore) || '—'}</td>
                <td><a href="/api/incubatori/validazione/${v.idEsito}/rapporto" target="_blank" rel="noopener">PDF</a></td></tr>`).join('');
    } catch { /* non bloccante */ }
}

async function valida(pulsante) {
    const libera = occupa(pulsante, 'Validazione…');
    try {
        const e = await inviaJson('/api/incubatori/validazione/esegui', {});
        $('validazione-esito').classList.remove('nascosto');
        $('validazione-badge').innerHTML = e.validato
            ? `<p><strong style="color:#1a7f37">VALIDATO</strong> — ${e.numeroOk} confronti OK (${testo(e.data)})</p>`
            : `<p><strong style="color:#b3261e">NON VALIDATO</strong> — ${e.numeroKo} KO su ${e.numeroOk + e.numeroKo}</p>`;
        $('tabella-validazione-casi').querySelector('tbody').innerHTML = e.casi.map(c =>
            `<tr><td>${testo(c.nome)}</td><td>${testo(c.descrizione)}</td>
             <td class="numerico">${c.ok}/${c.ko}</td>
             <td>${c.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td></tr>`).join('');
        avvisa(e.validato ? 'Validazione superata.' : 'Validazione NON superata.', e.validato ? 'successo' : 'errore');
        await caricaStatoValidazione(); await caricaRegistroValidazioni();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

// ══ Avvio ══
$('m-origine')?.addEventListener('change', () => aggiornaDatalist('m'));
$('a-origine')?.addEventListener('change', () => aggiornaDatalist('a'));
$('btn-nuova-taratura')?.addEventListener('click', nuova);
$('btn-taratura-annulla')?.addEventListener('click', () => $('pannello-taratura').classList.add('nascosto'));
$('btn-misura-riga')?.addEventListener('click', () => { misure.push(nuovaMisura(misure.length ? misure[misure.length - 1].posizione : 'A')); renderMisure(); });
$('btn-calcola')?.addEventListener('click', (e) => calcola(e.currentTarget));
$('btn-taratura-salva')?.addEventListener('click', (e) => salva(e.currentTarget));
$('btn-taratura-duplica')?.addEventListener('click', duplica);
$('btn-taratura-pdf')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'pdf'));
$('btn-taratura-word')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'word'));
$('btn-taratura-analizza')?.addEventListener('click', (e) => analizza(e.currentTarget));
$('btn-valida')?.addEventListener('click', (e) => valida(e.currentTarget));

caricaAnagrafiche();
caricaCdr();
caricaRegistro();
caricaStatoValidazione();
caricaRegistroValidazioni();
