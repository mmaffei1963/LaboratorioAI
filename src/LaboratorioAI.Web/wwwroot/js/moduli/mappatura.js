// Mappatura apparecchiature termostatiche: registro, esecuzione (letture per
// ripiano + calcolo + anteprima), rapporto, duplicazione, analisi AI, validazione.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const num = (v) => { const x = parseFloat(String(v).replace(',', '.')); return isNaN(x) ? 0 : x; };
const gg = (x, d = 2) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d }));
const RIPIANI = ['A', 'B', 'C', 'D', 'E', 'F'];

let cdrs = [], clienti = [], strumenti = [], misure = [];

async function caricaRegistro() {
    try {
        const r = await chiamaApi('/api/mappatura/elenco');
        $('conteggio-registro').textContent = `${r.length} mappature`;
        $('tabella-registro').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessuna mappatura.</td></tr>'
            : r.map(t => `<tr class="riga-cliccabile" data-id="${t.idMappatura}">
                <td>${testo(t.numeroRapporto)}</td><td>${testo(t.apparecchiaturaIdentificazione)}</td>
                <td class="numerico">${gg(t.valoreNominale)}</td><td>${testo(t.dataMappatura)}</td>
                <td>${t.dataScadenza ? testo(t.dataScadenza) : '—'} ${t.scaduta ? '<strong style="color:#b3261e">scaduta</strong>' : ''}</td></tr>`).join('');
        $('tabella-registro').querySelectorAll('.riga-cliccabile').forEach(x => x.addEventListener('click', () => apri(Number(x.dataset.id))));
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaCdr() {
    try {
        cdrs = await chiamaApi('/api/mappatura/cdr/elenco');
        $('t-cdr').innerHTML = '<option value="">— seleziona —</option>' +
            cdrs.map(c => `<option value="${c.idCampione}">${testo(c.identificazione)}</option>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaAnagrafiche() {
    try { clienti = await chiamaApi('/api/mappatura/clienti'); } catch { clienti = []; }
    try { strumenti = await chiamaApi('/api/mappatura/strumenti'); } catch { strumenti = []; }
    aggiornaDatalist();
}

function aggiornaDatalist() {
    const voci = $('a-origine').value === 'interno' ? strumenti : clienti;
    $('lista-a').innerHTML = voci.map(v => `<option value="${testo(v.nome)}">`).join('');
}

function risolviApparecchiatura() {
    const interno = $('a-origine').value === 'interno';
    const nome = $('a-identificazione').value.trim();
    const lista = interno ? strumenti : clienti;
    const trovato = lista.find(v => (v.nome || '').toLowerCase() === nome.toLowerCase());
    return {
        identificazione: nome, codice: $('a-codice').value.trim() || null, interna: interno,
        idStrumentoArcLab: interno && trovato ? trovato.id : null,
        idClienteArcLab: !interno && trovato ? trovato.id : null
    };
}

function nuova() {
    ['t-id', 't-numero', 't-scadenza', 'a-identificazione', 'a-codice', 't-nominale'].forEach(id => $(id).value = '');
    $('t-cdr').value = ''; $('t-istruzione').value = 'IO 04-02';
    $('t-tolleranza').value = '3'; $('t-risoluzione').value = '0.01'; $('a-origine').value = 'interno';
    $('t-data').value = new Date().toISOString().substring(0, 10);
    aggiornaDatalist();
    misure = ripianoNuovo('A');
    renderMisure();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova mappatura';
    $('pannello-taratura').classList.remove('nascosto');
    $('a-identificazione').focus();
}

function ripianoNuovo(pos) { return Array.from({ length: 5 }, () => ({ posizione: pos, temperaturaRilevata: 0 })); }

async function apri(id) {
    try {
        const t = await chiamaApi('/api/mappatura/' + id);
        $('t-id').value = t.idMappatura;
        $('t-cdr').value = t.idCampioneRiferimento;
        $('a-origine').value = t.apparecchiaturaInterna ? 'interno' : 'cliente';
        aggiornaDatalist();
        $('a-identificazione').value = t.apparecchiaturaIdentificazione ?? '';
        $('a-codice').value = t.apparecchiaturaCodice ?? '';
        $('t-nominale').value = t.valoreNominale;
        $('t-tolleranza').value = t.tolleranza;
        $('t-risoluzione').value = t.risoluzioneCdr ?? 0.01;
        $('t-istruzione').value = t.riferimentoIstruzione ?? '';
        $('t-responsabile').value = t.responsabile ?? '';
        $('t-data').value = (t.dataMappatura ?? '').substring(0, 10);
        $('t-scadenza').value = (t.dataScadenza ?? '').substring(0, 10);
        $('t-numero').value = t.numeroRapporto ?? '';
        misure = (t.misure ?? []).map(m => ({ posizione: m.posizione, temperaturaRilevata: m.temperaturaRilevata }));
        if (misure.length === 0) misure = ripianoNuovo('A');
        renderMisure();
        $('taratura-numero').textContent = t.numeroRapporto ? 'N° ' + t.numeroRapporto : '';
        $('pannello-risultati-taratura').classList.add('nascosto');
        $('pannello-analisi-ai').classList.add('nascosto');
        $('taratura-titolo').textContent = 'Modifica mappatura';
        $('pannello-taratura').classList.remove('nascosto');
        $('pannello-taratura').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderMisure() {
    $('tabella-misure').querySelector('tbody').innerHTML = misure.map((m, i) =>
        `<tr>
            <td><input type="text" data-i="${i}" data-c="posizione" value="${testo(m.posizione)}" style="width:4rem" /></td>
            <td class="numerico"><input type="number" step="0.001" data-i="${i}" data-c="temperaturaRilevata" value="${m.temperaturaRilevata}" style="width:8rem" /></td>
            <td><button type="button" class="pulsante pulsante-tenue mis-rm" data-i="${i}">×</button></td>
        </tr>`).join('');
    $('tabella-misure').querySelectorAll('input').forEach(inp =>
        inp.addEventListener('input', () => {
            misure[Number(inp.dataset.i)][inp.dataset.c] = inp.dataset.c === 'posizione' ? inp.value : num(inp.value);
        }));
    $('tabella-misure').querySelectorAll('.mis-rm').forEach(b =>
        b.addEventListener('click', () => { misure.splice(Number(b.dataset.i), 1); renderMisure(); }));
}

function aggiungiRipiano() {
    const usate = [...new Set(misure.map(m => (m.posizione || '').trim()).filter(Boolean))];
    const prossima = RIPIANI.find(r => !usate.includes(r)) || 'A';
    misure = misure.concat(ripianoNuovo(prossima));
    renderMisure();
}

function corpoCalcolo() {
    return {
        idCampioneRiferimento: $('t-cdr').value ? Number($('t-cdr').value) : 0,
        valoreNominale: num($('t-nominale').value),
        tolleranza: num($('t-tolleranza').value),
        risoluzioneCdr: num($('t-risoluzione').value),
        misure: misure.filter(m => (m.posizione || '').trim() !== '')
    };
}

function renderRisultato(r) {
    $('risultati-sintesi').innerHTML =
        `<p>Temperatura media <strong>${gg(r.temperaturaMedia, 3)} °C</strong> · differenza dal nominale ${gg(r.differenzaNominale, 3)} °C · `
        + `incertezza estesa ${gg(r.incertezzaEstesa, 3)} °C</p>`
        + `<p>Intervallo ammesso: <strong>${gg(r.intervalloMinimo)} … ${gg(r.intervalloMassimo)} °C</strong></p>`;
    $('tabella-ripiani').querySelector('tbody').innerHTML = r.ripiani.map(x =>
        `<tr${x.conforme ? '' : ' style="background:#fdecea"'}>
            <td>${testo(x.posizione)}</td><td class="numerico">${gg(x.media, 3)}</td>
            <td class="numerico">${gg(x.minimo, 3)}</td><td class="numerico">${gg(x.massimo, 3)}</td>
            <td>${x.conforme ? '<strong style="color:#1a7f37">entro</strong>' : '<strong style="color:#b3261e">FUORI</strong>'}</td></tr>`).join('');
    const col = r.differenzeSignificative ? '#b3261e' : '#1a7f37';
    $('risultati-giudizio').innerHTML =
        `<p><strong style="color:${col}">${testo(r.giudizio)}</strong></p>`
        + `<p class="sottotesto">Test F = ${gg(r.fisherF, 3)} (critico ${gg(r.fisherFCritico, 3)}); varianze ${r.varianzeOmogenee ? 'omogenee' : 'non omogenee'}; Kruskal-Wallis p = ${gg(r.kruskalWallisP, 4)}</p>`;
    $('pannello-risultati-taratura').classList.remove('nascosto');
}

async function calcola(pulsante) {
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const libera = occupa(pulsante, 'Calcolo…');
    try { renderRisultato(await inviaJson('/api/mappatura/calcola', corpoCalcolo())); }
    catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

function corpoSalva() {
    const a = risolviApparecchiatura();
    return {
        id: $('t-id').value ? Number($('t-id').value) : null,
        idCampioneRiferimento: Number($('t-cdr').value),
        apparecchiaturaIdentificazione: a.identificazione, apparecchiaturaCodice: a.codice,
        apparecchiaturaInterna: a.interna, apparecchiaturaIdStrumentoArcLab: a.idStrumentoArcLab, apparecchiaturaIdClienteArcLab: a.idClienteArcLab,
        valoreNominale: num($('t-nominale').value), tolleranza: num($('t-tolleranza').value),
        risoluzioneCdr: num($('t-risoluzione').value),
        numeroRapporto: $('t-numero').value || null,
        dataMappatura: $('t-data').value, dataScadenza: $('t-scadenza').value || null,
        riferimentoIstruzione: $('t-istruzione').value, responsabile: $('t-responsabile').value || null,
        misure: misure.filter(m => (m.posizione || '').trim() !== '')
    };
}

async function salva(pulsante) {
    if (!$('a-identificazione').value.trim()) { avvisa('Identificazione apparecchiatura obbligatoria.', 'errore'); return; }
    if (!$('t-cdr').value) { avvisa('Seleziona il CDR.', 'errore'); return; }
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const e = await inviaJson('/api/mappatura/salva', corpoSalva());
        $('t-id').value = e.idMappatura; $('t-numero').value = e.numeroRapporto;
        $('taratura-numero').textContent = 'N° ' + e.numeroRapporto;
        avvisa('Mappatura salvata.', 'successo');
        await caricaRegistro();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

function duplica() {
    if (!$('t-cdr').value) { avvisa('Apri o seleziona prima una mappatura da duplicare.', 'info'); return; }
    const copia = $('t-copia-letture')?.checked ?? true;
    const s = {
        cdr: $('t-cdr').value, nominale: $('t-nominale').value, tolleranza: $('t-tolleranza').value,
        risoluzione: $('t-risoluzione').value, istruzione: $('t-istruzione').value,
        data: $('t-data').value, scadenza: $('t-scadenza').value, responsabile: $('t-responsabile').value
    };
    const nuoveMisure = misure.map(m => ({ posizione: m.posizione, temperaturaRilevata: copia ? m.temperaturaRilevata : 0 }));
    ['t-id', 't-numero', 'a-identificazione', 'a-codice'].forEach(id => $(id).value = '');
    $('t-cdr').value = s.cdr; $('t-nominale').value = s.nominale; $('t-tolleranza').value = s.tolleranza;
    $('t-risoluzione').value = s.risoluzione; $('t-istruzione').value = s.istruzione;
    $('t-data').value = s.data; $('t-scadenza').value = s.scadenza; $('t-responsabile').value = s.responsabile;
    $('a-origine').value = 'interno'; aggiornaDatalist();
    misure = nuoveMisure.length ? nuoveMisure : ripianoNuovo('A');
    renderMisure();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto');
    $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova mappatura (duplicata)';
    $('a-identificazione').focus();
    avvisa('Duplicata: inserisci la nuova apparecchiatura.', 'successo');
}

async function scaricaRapporto(pulsante, formato) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la mappatura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/mappatura/${id}/rapporto`, {
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
        const nome = mm ? mm[1] : (formato === 'word' ? 'Rapporto_Mappatura.rtf' : 'Rapporto_Mappatura.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = nome;
        document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
        avvisa('Rapporto generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function analizza(pulsante) {
    const id = $('t-id').value;
    if (!id) { avvisa('Salva prima la mappatura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Analisi…');
    try {
        const r = await inviaJson(`/api/mappatura/${id}/analizza`, {});
        $('pannello-analisi-ai').classList.remove('nascosto');
        $('analisi-ai-badge').textContent = (r.daArchivio ? 'da archivio · ' : '') + (r.dataAnalisi || '');
        const an = r.anomalie || [];
        $('analisi-ai-anomalie').innerHTML = an.length === 0
            ? '<p class="sottotesto">Nessuna anomalia dai controlli automatici.</p>'
            : '<ul>' + an.map(a => `<li><strong style="color:${a.livello === 'Errore' ? '#b3261e' : '#9a6700'}">${testo(a.livello)}</strong>${a.punto ? ' — pos. ' + testo(a.punto) : ''}: ${testo(a.messaggio)}</li>`).join('') + '</ul>';
        $('analisi-ai-testo').textContent = r.testo || '';
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ── Validazione ──
async function caricaStatoValidazione() {
    const el = $('validazione-stato'); if (!el) return;
    try {
        const s = await chiamaApi('/api/mappatura/validazione/stato');
        const av = $('validazione-avviso');
        if (!s.eseguitaMai) { el.textContent = 'mai eseguita'; av.textContent = 'La validazione del calcolo non è mai stata eseguita.'; av.classList.remove('nascosto'); }
        else {
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
        const r = await chiamaApi('/api/mappatura/validazione/elenco');
        tb.querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="7" class="vuoto">Nessuna validazione.</td></tr>'
            : r.map(v => `<tr>
                <td>${testo(v.data)}</td>
                <td>${v.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td>
                <td class="numerico">${v.numeroOk}/${v.numeroKo}</td>
                <td>${testo(v.versioneSet)}</td><td>${testo(v.versioneApplicazione) || '—'}</td><td>${testo(v.operatore) || '—'}</td>
                <td><a href="/api/mappatura/validazione/${v.idEsito}/rapporto" target="_blank" rel="noopener">PDF</a></td></tr>`).join('');
    } catch { /* non bloccante */ }
}

async function valida(pulsante) {
    const libera = occupa(pulsante, 'Validazione…');
    try {
        const e = await inviaJson('/api/mappatura/validazione/esegui', {});
        $('validazione-esito').classList.remove('nascosto');
        $('validazione-badge').innerHTML = e.validato
            ? `<p><strong style="color:#1a7f37">VALIDATO</strong> — ${e.numeroOk} confronti OK (${testo(e.data)})</p>`
            : `<p><strong style="color:#b3261e">NON VALIDATO</strong> — ${e.numeroKo} KO su ${e.numeroOk + e.numeroKo}</p>`;
        $('tabella-validazione-casi').querySelector('tbody').innerHTML = e.casi.map(c =>
            `<tr><td>${testo(c.nome)}</td><td>${testo(c.descrizione)}</td><td class="numerico">${c.ok}/${c.ko}</td>
             <td>${c.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td></tr>`).join('');
        avvisa(e.validato ? 'Validazione superata.' : 'Validazione NON superata.', e.validato ? 'successo' : 'errore');
        await caricaStatoValidazione(); await caricaRegistroValidazioni();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); }
}

// ── Avvio ──
$('a-origine')?.addEventListener('change', aggiornaDatalist);
$('btn-nuova-taratura')?.addEventListener('click', nuova);
$('btn-taratura-annulla')?.addEventListener('click', () => $('pannello-taratura').classList.add('nascosto'));
$('btn-misura-riga')?.addEventListener('click', () => { misure.push({ posizione: misure.length ? misure[misure.length - 1].posizione : 'A', temperaturaRilevata: 0 }); renderMisure(); });
$('btn-ripiano')?.addEventListener('click', aggiungiRipiano);
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
