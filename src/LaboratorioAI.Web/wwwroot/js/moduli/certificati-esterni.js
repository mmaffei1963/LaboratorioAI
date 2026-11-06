// Triage e rinomina batch dei certificati dei laboratori esterni (upload o cartella di rete).
// Ogni salvataggio finisce nel registro di controllo (consegna in Palomar verificata).
import { disegnaRegistro } from './registro-documenti.js';
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

let righe = [];        // { file?, percorso?, ant, tipo: 'SoloEsterni' | 'Misto' }
let filesCartella = []; // { name, percorso }

async function analizzaJson(url, body) {
    const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    const j = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(j.messaggio || 'Errore');
    return j;
}

function preparaTabella() {
    $('pannello-esito').classList.remove('nascosto');
    $('btn-salva-tutti').classList.remove('nascosto');
}

function aggiungiRiga(sorgente, etichetta) {
    const idx = righe.length;
    righe.push({ ...sorgente, ant: null, tipo: 'SoloEsterni' });
    const tr = document.createElement('tr');
    tr.dataset.idx = idx;
    tr.innerHTML = `<td>${esc(etichetta)}</td><td colspan="8" class="sottotesto">analisi…</td>`;
    $('tabella-cert').tBodies[0].prepend(tr); // i più recenti in cima
    return { idx, tr };
}

// ── Upload dal browser ──
async function analizzaUpload() {
    const files = [...$('file-cert').files];
    if (!files.length) { $('stato').textContent = 'Seleziona uno o più PDF.'; return; }
    preparaTabella();
    let i = 0;
    for (const f of files) {
        i++; $('stato').textContent = `Analisi ${i}/${files.length}: ${f.name}…`;
        const { idx, tr } = aggiungiRiga({ file: f }, f.name);
        try {
            const fd = new FormData(); fd.append('file', f);
            fd.append('rianalizza', $('rianalizza')?.checked ? 'true' : 'false');
            const r = await fetch('/api/certificati-esterni/analizza', { method: 'POST', body: fd });
            const j = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(j.messaggio || 'Errore');
            righe[idx].ant = j; renderRiga(tr, idx, f.name, j);
        } catch (e) { erroreRiga(tr, f.name, e.message); }
    }
    completamento('Analisi');
}

// ── Da cartella di rete ──
async function elencaCartella() {
    const percorso = $('cartella').value.trim();
    const q = percorso ? '?percorso=' + encodeURIComponent(percorso) : '';
    const r = await fetch('/api/certificati-esterni/cartella' + q);
    const j = await r.json().catch(() => ({}));
    $('cartella').value = j.cartella || '';
    const tb = $('tabella-file').tBodies[0];
    if (!j.accessibile) { tb.innerHTML = `<tr><td colspan="3">Cartella non accessibile${j.errore ? ': ' + esc(j.errore) : ''}.</td></tr>`; $('btn-analizza-tutti').classList.add('nascosto'); return; }
    filesCartella = j.files || [];
    tb.innerHTML = filesCartella.map(f => `<tr>
        <td>${esc(f.name)}</td><td class="sottotesto">${esc(f.percorso)}</td>
        <td><button class="pulsante pulsante-tenue" data-perc="${esc(f.percorso)}" data-name="${esc(f.name)}">Analizza</button></td></tr>`).join('')
        || '<tr><td colspan="3">Nessun PDF nella cartella.</td></tr>';
    tb.querySelectorAll('[data-perc]').forEach(b => b.addEventListener('click', () => analizzaPercorso(b.dataset.perc, b.dataset.name)));
    $('btn-analizza-tutti').classList.toggle('nascosto', filesCartella.length === 0);
}

async function analizzaPercorso(percorso, name) {
    preparaTabella();
    const { idx, tr } = aggiungiRiga({ percorso }, name || percorso);
    try {
        const j = await analizzaJson('/api/certificati-esterni/analizza-percorso',
            { percorso, rianalizza: $('rianalizza')?.checked || false });
        righe[idx].ant = j; renderRiga(tr, idx, name || percorso, j);
    } catch (e) { erroreRiga(tr, name || percorso, e.message); }
    contatore();
}

async function analizzaTuttiCartella() {
    for (const f of filesCartella) await analizzaPercorso(f.percorso, f.name);
    completamento('Analisi');
}

// ── Rendering riga risultato ──
function erroreRiga(tr, etichetta, msg) {
    tr.innerHTML = `<td>${esc(etichetta)}</td><td colspan="8" style="color:#b3261e">${esc(msg)}</td>`;
}

// Solo esterni / misti (12/10/2026): la riga va nella tabella del suo tipo.
function collocaRiga(tr, idx) {
    const next = tr.nextElementSibling;
    if (next && next.classList.contains('dettaglio-ai')) next.remove();
    const misti = $('tabella-misti').tBodies[0];
    const dest = righe[idx].tipo === 'Misto' ? misti : $('tabella-cert').tBodies[0];
    if (tr.parentElement !== dest) dest.prepend(tr);
    misti.querySelector('tr.vuota')?.remove();
    if (!misti.querySelector('tr[data-idx]'))
        misti.innerHTML = '<tr class="vuota"><td colspan="9" class="sottotesto">Nessun certificato misto.</td></tr>';
}

function cellaTipo(idx, a) {
    const t = righe[idx].tipo;
    const dubbio = !a.tipoProposto && a.campione;
    const int = a.parametriInterni || [], ext = a.parametriEsterni || [];
    const titolo = (int.length ? 'Prove nostre: ' + int.join(', ') + '. ' : '') + (ext.length ? 'Prove esterne: ' + ext.join(', ') + '.' : '');
    return `<select class="tipo" title="${esc(titolo)}">
            <option value="SoloEsterni" ${t === 'SoloEsterni' ? 'selected' : ''}>Solo esterni</option>
            <option value="Misto" ${t === 'Misto' ? 'selected' : ''}>Misto</option>
        </select>`
        + (dubbio ? '<div class="sottotesto" style="color:#b26a00">da scegliere</div>'
            : a.tipoProposto && a.tipoProposto !== t ? '<div class="sottotesto" style="color:#b26a00">cambiato a mano</div>'
            : int.length ? `<div class="sottotesto">nostre: ${esc(int.slice(0, 3).join(', '))}${int.length > 3 ? '…' : ''}</div>` : '');
}

function renderRiga(tr, idx, etichetta, a) {
    const e = a.estrazione || {};
    const c = a.campione;
    const crit = a.criticita || [];
    const avvertenze = a.avvertenze || [];
    const avv = !!a.avvisoSuggerito;
    const nome = (avv ? a.nomeConAvviso : a.nomeStandard) || '';

    const critCell = `<a href="#" class="dett" style="text-decoration:none">`
        + (crit.length ? `<span style="color:#b3261e;font-weight:600">▲ ${crit.length}</span>` : `<span style="color:#1a7f37">ok</span>`)
        + ` <span class="sottotesto">dettagli</span></a>`;
    const avvert = avvertenze.length ? `<div class="sottotesto" style="color:#b26a00">${esc(avvertenze.join(' · '))}</div>` : '';
    const cache = a.daCache ? ' <span class="etichetta" title="estrazione riusata dalla cache">cache</span>' : '';
    if (!tr.dataset.tipoImpostato) { righe[idx].tipo = a.tipoProposto === 'Misto' ? 'Misto' : 'SoloEsterni'; tr.dataset.tipoImpostato = '1'; }

    tr.innerHTML = `
        <td>${esc(etichetta)}${cache}${avvert}</td>
        <td>${esc(e.laboratorio || '')}<div class="sottotesto">${esc(a.labAcronimo || '')}</div></td>
        <td>${esc(e.codiceNostro || '—')}</td>
        <td>${esc(c ? c.ragioneSociale : '—')}<div class="sottotesto">${esc(c ? (c.denominazioneProdotto || '') : '')}</div></td>
        <td class="cella-tipo">${cellaTipo(idx, a)}</td>
        <td>${critCell}</td>
        <td>
            <label class="casella"><input type="radio" name="avv-${idx}" value="avv" ${avv ? 'checked' : ''}/> AVV</label>
            <label class="casella"><input type="radio" name="avv-${idx}" value="std" ${avv ? '' : 'checked'}/> STD</label>
        </td>
        <td><input type="text" class="nome" style="width:32rem" value="${esc(nome)}" placeholder="nome (compila se mancante)"/></td>
        <td><button class="pulsante pulsante-tenue salva">Salva</button><div class="esito sottotesto"></div></td>`;

    tr.querySelectorAll(`input[name=avv-${idx}]`).forEach(r => r.addEventListener('change', () => {
        const useAvv = tr.querySelector(`input[name=avv-${idx}][value=avv]`).checked;
        const nuovo = useAvv ? a.nomeConAvviso : a.nomeStandard;
        if (nuovo) tr.querySelector('.nome').value = nuovo;
    }));
    const legaTipo = () => tr.querySelector('.tipo').addEventListener('change', ev => {
        righe[idx].tipo = ev.target.value;
        tr.querySelector('.cella-tipo').innerHTML = cellaTipo(idx, a);
        legaTipo();
        collocaRiga(tr, idx);
    });
    legaTipo();
    collocaRiga(tr, idx);
    tr.querySelector('.salva').addEventListener('click', () => salvaRiga(tr, idx));
    tr.querySelector('.dett').addEventListener('click', ev => { ev.preventDefault(); toggleDettaglio(tr, a); });
}

function toggleDettaglio(tr, a) {
    const next = tr.nextElementSibling;
    if (next && next.classList.contains('dettaglio-ai')) { next.remove(); return; }
    const e = a.estrazione || {};
    const crit = a.criticita || [];
    const motivo = a.avvisoSuggerito
        ? 'AVVISO suggerito' + (e.nonConforme ? ' — non conformità dichiarata' : (crit.length ? ` — ${crit.length} criticità` : ''))
        : 'Avviso non necessario';
    const tr2 = document.createElement('tr');
    tr2.className = 'dettaglio-ai';
    const int = a.parametriInterni || [], ext = a.parametriEsterni || [];
    tr2.innerHTML = `<td colspan="9" style="background:#f7f7f7">
        ${int.length || ext.length ? `<div><strong>Prove del campione in ArcLab:</strong> esterne ${esc(ext.join(', ') || '—')}; nostre ${esc(int.join(', ') || 'nessuna')}.</div>` : ''}
        <div><strong>Giudizio avviso:</strong> ${esc(motivo)}</div>
        ${e.valutazione ? `<div style="margin-top:.3rem"><strong>Valutazione AI:</strong> ${esc(e.valutazione)}</div>` : ''}
        ${crit.length ? `<div style="margin-top:.3rem"><strong>Criticità:</strong><ul style="margin:.2rem 0">${crit.map(c => `<li>${esc(c)}</li>`).join('')}</ul></div>` : ''}
        ${e.note ? `<div style="margin-top:.3rem" class="sottotesto"><strong>Note:</strong> ${esc(e.note)}</div>` : ''}
    </td>`;
    tr.after(tr2);
}

async function salvaRiga(tr, idx, idEsecuzione = '') {
    const r = righe[idx]; if (!r || !r.ant) return false;
    const nome = tr.querySelector('.nome').value.trim();
    const esito = tr.querySelector('.esito');
    if (!nome) { esito.textContent = 'Nome mancante.'; return false; }
    if (/IDMATRICE|IDLAB/.test(nome)) { esito.textContent = 'Sostituire IDMATRICE/IDLAB con gli id corretti (servono a Palomar).'; return false; }
    const c = r.ant.campione;
    const e = r.ant.estrazione || {};
    // data del nome/smistamento = ricezione del campione (dataNome); in ArcLab va la data di emissione
    const data = r.ant.dataNome || (c?.dataRicezione ? new Date(c.dataRicezione).toISOString().slice(0, 10) : '');
    const dataCertificato = r.ant.dataEmissione || data;
    const palomar = $('cartella-palomar')?.value.trim() || '';
    const palomarMisti = $('cartella-palomar-misti')?.value.trim() || '';
    const tipo = r.tipo || 'SoloEsterni';
    const idLabEsterno = r.ant.idLabEsterno || '';
    const archivio = $('cartella-archivio')?.value.trim() || '';
    // Aggancio import ArcLab (solo se il campione è stato trovato)
    const idCampione = c?.idCampione || '';
    const numeroCertificato = e.rdPEsterno || '';
    const laboratorio = r.ant.labAcronimo || e.laboratorio || '';
    esito.textContent = 'salvataggio…';
    try {
        let j;
        if (r.percorso) {
            j = await analizzaJson('/api/certificati-esterni/conferma-percorso',
                { percorso: r.percorso, nomeFinale: nome, labAcronimo: r.ant.labAcronimo || '', data, cartellaPalomar: palomar, cartellaArchivio: archivio,
                  idCampione: idCampione || null, numeroCertificato, dataCertificato, laboratorio, idEsecuzione,
                  tipo, idLabEsterno: idLabEsterno || null, cartellaPalomarMisti: palomarMisti });
        } else {
            const fd = new FormData();
            fd.append('file', r.file); fd.append('nomeFinale', nome);
            fd.append('labAcronimo', r.ant.labAcronimo || ''); fd.append('data', data);
            fd.append('cartellaPalomar', palomar); fd.append('cartellaArchivio', archivio);
            fd.append('idCampione', idCampione); fd.append('numeroCertificato', numeroCertificato);
            fd.append('dataCertificato', dataCertificato); fd.append('laboratorio', laboratorio);
            fd.append('idEsecuzione', idEsecuzione);
            fd.append('tipo', tipo); fd.append('idLabEsterno', idLabEsterno); fd.append('cartellaPalomarMisti', palomarMisti);
            const rr = await fetch('/api/certificati-esterni/conferma', { method: 'POST', body: fd });
            j = await rr.json().catch(() => ({}));
            if (!rr.ok) throw new Error(j.messaggio || 'Errore');
        }
        esito.innerHTML = '<span style="color:#1a7f37">salvato · in Palomar' + (tipo === 'Misto' ? ' INTERNI_ESTERNI' : '') + '</span>'
            + (tipo === 'Misto' ? ' · <span style="color:#5b2a86">valori da inserire a mano in ArcLab</span>' : '')
            + (j.importProgrammato ? ' · <span style="color:#1741b3">import ArcLab programmato</span>' : '')
            + (j.originaleSpostato ? ' · originale spostato' : '')
            + (j.avviso ? ' · <span style="color:#b26a00">' + esc(j.avviso) + '</span>' : '');
        tr.querySelector('.salva').disabled = true;
        tr.querySelector('.tipo')?.setAttribute('disabled', 'disabled');
        return true;
    } catch (e) {
        esito.innerHTML = '<span style="color:#b00">' + esc(e.message) + '</span>';
        return false;
    } finally {
        if (!idEsecuzione) caricaRegistro();
    }
}

function nuovaEsecuzione() {
    const d = new Date(), p = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}-`
        + Math.random().toString(36).slice(2, 8);
}

async function salvaTutti() {
    let daFare = 0, fatti = 0;
    const idEsecuzione = nuovaEsecuzione();
    $('stato').textContent = 'Salvataggio in corso…';
    const tutte = [...$('tabella-cert').tBodies[0].querySelectorAll('tr[data-idx]'), ...$('tabella-misti').tBodies[0].querySelectorAll('tr[data-idx]')];
    for (const tr of tutte) {
        const btn = tr.querySelector('.salva');
        if (btn && !btn.disabled) { daFare++; if (await salvaRiga(tr, +tr.dataset.idx, idEsecuzione)) fatti++; }
    }
    if (!daFare) { $('stato').textContent = 'Niente da salvare.'; return; }
    let testo = `Salvataggio completato: ${fatti}/${daFare} salvati e verificati in Palomar.`;
    try {
        const r = await analizzaJson('/api/certificati-esterni/fine-esecuzione', { idEsecuzione });
        if (r.parziali || r.errori)
            testo += ` ATTENZIONE: ${r.parziali + r.errori} con problemi` + (r.emailInviata ? ' (inviata mail di riepilogo).' : '.');
    } catch (e) { testo += ' (riepilogo non disponibile: ' + e.message + ')'; }
    if (daFare - fatti > 0 && !testo.includes('ATTENZIONE')) testo += ` ${daFare - fatti} non salvati: vedi la colonna esito.`;
    $('stato').textContent = testo;
    $('stato').style.color = fatti < daFare ? '#b00' : '';
    caricaRegistro();
}

async function caricaRegistro() {
    const tab = $('tabella-registro'); if (!tab) return;
    const p = new URLSearchParams({
        giorni: $('reg-giorni').value, soloProblemi: $('reg-solo-problemi').checked,
        soloDaInserire: $('reg-da-inserire').checked, mostraAnnullati: $('reg-annullati').checked
    });
    const m = $('reg-msg');
    m.textContent = 'Caricamento…'; m.style.color = '';
    try {
        const r = await fetch('/api/certificati-esterni/registro?' + p.toString());
        const j = await r.json();
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        disegnaRegistro(tab, j.righe, { colonnaTipo: true, annulla: annullaRiga });
        const aperti = j.righe.filter(x => !x.annullato && (x.esito === 'Errore' || x.esito === 'Parziale') && x.statoAttuale !== 'Inviato').length;
        let t = `${j.righe.length} righe` + (aperti ? ` — ${aperti} problemi ancora aperti.` : ' — nessun problema aperto.');
        if (j.daInserire) t += ` Misti con valori ancora da inserire in ArcLab: ${j.daInserire}.`;
        if (j.erroreArcLab) t += ' ' + j.erroreArcLab;
        if (j.inArrivo !== null && j.inArrivo !== undefined)
            t += ` PDF ancora nella cartella di arrivo: ${j.inArrivo}.`;
        m.textContent = t;
        m.style.color = aperti || j.erroreArcLab ? '#b00' : '';
    } catch (e) { m.textContent = e.message; m.style.color = '#b00'; }
}

async function annullaRiga(r, motivo, rimettiInArrivo) {
    const j = await analizzaJson(`/api/certificati-esterni/registro/${r.idRegistroDocumento}/annulla`, { motivo, rimettiInArrivo });
    await caricaRegistro();
    const m = $('reg-msg');
    m.textContent = (j.messaggio || 'Annullato.') + ' ' + m.textContent;
}

function contatore() { $('conteggio').textContent = righe.length + ' certificati'; }

function svuota() {
    righe = [];
    $('tabella-cert').tBodies[0].innerHTML = '';
    $('tabella-misti').tBodies[0].innerHTML = '<tr class="vuota"><td colspan="9" class="sottotesto">Nessun certificato misto.</td></tr>';
    $('conteggio').textContent = '';
    $('stato').textContent = '';
    $('pannello-esito').classList.add('nascosto');
    $('btn-salva-tutti').classList.add('nascosto');
}
function completamento(azione) {
    const tot = righe.length, ok = righe.filter(r => r.ant).length, ko = tot - ok;
    $('conteggio').textContent = tot + ' certificati';
    $('stato').textContent = `${azione} completata: ${tot} certificati` + (ko ? ` (${ok} ok, ${ko} con errore)` : ' — tutti analizzati');
}

$('btn-analizza')?.addEventListener('click', analizzaUpload);
$('btn-elenca')?.addEventListener('click', elencaCartella);
$('btn-analizza-tutti')?.addEventListener('click', analizzaTuttiCartella);
$('btn-salva-tutti')?.addEventListener('click', salvaTutti);
$('btn-svuota')?.addEventListener('click', svuota);
$('btn-registro')?.addEventListener('click', caricaRegistro);
$('reg-giorni')?.addEventListener('change', caricaRegistro);
$('reg-solo-problemi')?.addEventListener('change', caricaRegistro);
$('reg-da-inserire')?.addEventListener('change', caricaRegistro);
$('reg-annullati')?.addEventListener('change', caricaRegistro);
caricaRegistro();

// Memoria dei percorsi (localStorage): ricorda l'ultimo valore usato; se assente,
// resta il default proposto da appsettings (già nel value del campo).
// 12/10/2026: chiave Palomar rinnovata (nuove cartelle ESTERNI / INTERNI_ESTERNI), così vale l'impostazione del server.
const CHIAVI = { 'cartella': 'ce_arrivo_2', 'cartella-palomar': 'ce_palomar_esterni', 'cartella-palomar-misti': 'ce_palomar_misti', 'cartella-archivio': 'ce_archivio_2' };
for (const [id, key] of Object.entries(CHIAVI)) {
    const el = $(id); if (!el) continue;
    try { const v = localStorage.getItem(key); if (v) el.value = v; } catch { }
    el.addEventListener('change', () => { try { localStorage.setItem(key, el.value.trim()); } catch { } });
}
