// Limiti normativi (17/10/2026, fase 1a): schede limite, fonti, estrazione con l'AI, validazione.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const data = d => d ? new Date(d).toLocaleDateString('it-IT') : '—';
const dataOra = d => d ? new Date(d).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';
const puoValidare = $('dati-pagina').dataset.valida === '1';

let opzioni = null, schede = [], fonti = [], attesaFonti = null;

async function api(url, opz = {}) {
    const r = await fetch(url, opz);
    const j = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(j.messaggio || `Errore ${r.status}`);
    return j;
}
const post = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo ?? {}) });
const riempi = (sel, voci, vuota) => {
    sel.innerHTML = (vuota ? `<option value="">${esc(vuota)}</option>` : '') + voci.map(v => `<option value="${v.valore}">${esc(v.nome)}</option>`).join('');
};
const messaggio = (el, testo, errore) => { el.textContent = testo; el.style.color = errore ? '#b00' : '#1a7f37'; };

// ── Schede ──────────────────────────────────────────────────────

async function caricaSchede() {
    $('stato-schede').textContent = 'caricamento…';
    const f = $('f-famiglia').value;
    schede = (await api(`/api/limiti/schede?storico=${$('f-storico').checked}` + (f ? `&famiglia=${f}` : '')))
        .map(x => ({ ...x.riga, fonte: x.fonte, validabile: x.validabile }));
    // 18/10/2026: le spunte restano anche quando la tabella si ricarica (apertura di una scheda, aggiornamento
    // automatico durante un'estrazione, cambio filtro); si tolgono solo quelle di schede non più selezionabili.
    const ancora = new Set(schede.filter(selezionabile).map(s => s.id));
    for (const id of [...selezionate]) if (!ancora.has(id)) selezionate.delete(id);
    $('stato-schede').textContent = '';
    disegnaSchede();
}

// Chiave del parametro per raggruppare: minuscolo, senza accenti, punteggiatura e note tra parentesi.
const chiaveParametro = p => (p || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
    .replace(/\(.*?\)/g, '').replace(/[^a-z0-9]/g, '');
const selezionate = new Set();
let visibili = new Set();      // schede mostrate con i filtri attuali: le azioni in blocco toccano solo queste
const selezionabile = s => puoValidare && (s.stato === 1 || s.stato === 3);

function disegnaSchede() {
    const tb = $('tabella-schede').tBodies[0];
    const stato = $('f-stato').value, testo = $('f-testo').value.trim().toLowerCase();
    const perParametro = $('f-ordine').value === 'parametro';
    const rapida = $('f-rapida').checked;
    const elenco = schede.filter(s => (!stato || String(s.stato) === stato)
        && (!testo || [s.codice, s.parametro, s.matrice, s.riferimento, s.fonte].some(x => (x || '').toLowerCase().includes(testo))));
    const conta = {};
    for (const s of elenco) conta[chiaveParametro(s.parametro)] = (conta[chiaveParametro(s.parametro)] || 0) + 1;
    visibili = new Set(elenco.map(s => s.id));
    if (perParametro) elenco.sort((a, b) => chiaveParametro(a.parametro).localeCompare(chiaveParametro(b.parametro)) || a.codice.localeCompare(b.codice));
    aggiornaSelezione();
    if (!elenco.length) { tb.innerHTML = `<tr><td colspan="10" class="sottotesto">Nessuna scheda.${puoValidare ? ' Parti da «Fonti»: scarica una fonte e usa «Estrai con AI».' : ''}</td></tr>`; return; }
    let gruppo = null, pari = false;
    tb.innerHTML = elenco.map(s => {
        const k = chiaveParametro(s.parametro);
        if (perParametro && k !== gruppo) { gruppo = k; pari = !pari; }
        const sfondo = perParametro && pari ? 'background:#F8FAFC' : '';
        const doppio = conta[k] > 1 ? ` <span class="lim-stato" style="background:#FDE7D7;color:#A6460A" title="Stesso parametro in più schede: controlla se sono doppioni">×${conta[k]}</span>` : '';
        return `<tr class="cliccabile" data-id="${s.id}" style="${sfondo}">
        <td>${selezionabile(s) ? `<input type="checkbox" class="sel" data-id="${s.id}" ${selezionate.has(s.id) ? 'checked' : ''} aria-label="Seleziona ${esc(s.codice)}" />` : ''}</td>
        <td><strong>${esc(s.codice)}</strong>${s.versione > 1 ? ` <span class="sottotesto">v${s.versione}</span>` : ''}</td>
        <td>${esc(s.parametro)}${doppio}</td><td>${esc(s.matrice)}</td>
        <td style="white-space:nowrap">${esc(s.limite)}</td><td>${esc(s.tipoNome)}</td>
        <td><span class="lim-stato lim-stato-${s.stato}">${esc(s.statoNome)}</span></td>
        <td class="numerico" style="${s.abbinamenti ? '' : 'color:#b00'}">${s.abbinamenti}</td>
        <td>${esc(s.fonte)}</td>
        <td>${esc(s.propostaDa)}<br><span class="sottotesto">${data(s.dataProposta)}</span></td></tr>`
        + (rapida ? rigaRapida(s, sfondo) : '');
    }).join('');
    tb.querySelectorAll('tr[data-id]').forEach(tr => tr.addEventListener('click', e => {
        if (e.target.closest('input.sel')) return;
        apriScheda(+tr.dataset.id);
    }));
    tb.querySelectorAll('input.sel').forEach(c => c.addEventListener('change', () => {
        c.checked ? selezionate.add(+c.dataset.id) : selezionate.delete(+c.dataset.id);
        aggiornaSelezione();
    }));
}

// Validazione rapida (18/10/2026): sotto ogni scheda citazione, condizioni e parametri ArcLab, senza aprire il dettaglio.
function rigaRapida(s, sfondo) {
    const arclab = s.abbinamentiNomi?.length
        ? s.abbinamentiNomi.map(esc).join(', ')
        : '<strong style="color:#b00">nessun parametro ArcLab: non potrà segnalare nulla</strong>';
    const nonMia = s.stato === 1 || s.stato === 3 ? (s.validabile ? '' : ' · <em style="color:#7a5b00">la deve validare un altro validatore</em>') : '';
    return `<tr class="cliccabile riga-rapida" data-id="${s.id}" style="${sfondo}"><td></td><td colspan="9" style="padding-top:0;font-size:.8rem;border-top:none">
        <div class="lim-citazione" style="padding:0.3rem 0.6rem">${esc(s.citazione) || '<em style="color:#b00">nessuna citazione</em>'}</div>
        ${s.condizioni ? `<div class="sottotesto">Condizioni: ${esc(s.condizioni)}</div>` : ''}
        <div class="sottotesto">ArcLab: ${arclab}${nonMia}</div></td></tr>`;
}

function aggiornaSelezione() {
    const barra = $('barra-selezione');
    if (!barra) return;
    barra.style.display = selezionate.size === 0 ? 'none' : 'flex';
    const nascoste = [...selezionate].filter(id => !visibili.has(id)).length;
    $('sel-conta').textContent = `${selezionate.size - nascoste} schede selezionate`
        + (nascoste ? ` (+${nascoste} nascoste dal filtro: restano spuntate ma non vengono toccate)` : '');
}

async function validaSelezionate() {
    const scelte = schede.filter(s => selezionate.has(s.id) && visibili.has(s.id));
    const valide = scelte.filter(s => s.validabile);
    if (!valide.length) { messaggio($('sel-msg'), 'Tra le selezionate non ce n\'è nessuna che puoi validare tu (proposte o corrette da te).', true); return; }
    const senzaArcLab = valide.filter(s => !s.abbinamenti).length;
    const saltate = scelte.length - valide.length;
    let testo = `Validare ${valide.length} schede?\n\nHai controllato per ognuna valore, unità, tipo di limite e citazione?`;
    if (saltate) testo += `\n\n${saltate} schede selezionate le deve validare un altro validatore: verranno saltate.`;
    if (senzaArcLab) testo += `\n\nAttenzione: ${senzaArcLab} schede non hanno parametri ArcLab abbinati e non potranno segnalare nulla.`;
    if (!confirm(testo)) return;
    $('sel-valida').disabled = true;
    try {
        const j = await post('/api/limiti/schede/valida', { ids: valide.map(s => s.id), testo: $('sel-motivo').value.trim() || null });
        $('sel-motivo').value = '';
        await caricaSchede();
        messaggio($('stato-schede'), j.messaggio);
    } catch (e) { messaggio($('sel-msg'), e.message, true); }
    $('sel-valida').disabled = false;
}

async function respingiSelezionate() {
    const motivo = $('sel-motivo').value.trim();
    if (!motivo) { messaggio($('sel-msg'), 'Scrivi il motivo.', true); return; }
    $('sel-respingi').disabled = true;
    try {
        const ids = [...selezionate].filter(id => visibili.has(id));
        if (!ids.length) { messaggio($('sel-msg'), 'Nessuna scheda selezionata tra quelle visibili.', true); $('sel-respingi').disabled = false; return; }
        if (!confirm(`Respingere ${ids.length} schede con il motivo «${motivo}»?`)) { $('sel-respingi').disabled = false; return; }
        const j = await post('/api/limiti/schede/respingi', { ids, testo: motivo });
        $('sel-motivo').value = '';
        await caricaSchede();
        messaggio($('stato-schede'), j.messaggio);
    } catch (e) { messaggio($('sel-msg'), e.message, true); }
    $('sel-respingi').disabled = false;
}

function selezionaVisibili(valore) {
    $('tabella-schede').querySelectorAll('input.sel').forEach(c => { c.checked = valore; valore ? selezionate.add(+c.dataset.id) : selezionate.delete(+c.dataset.id); });
    aggiornaSelezione();
}

async function apriScheda(id) {
    const dlg = $('dlg-scheda');
    $('ds-corpo').innerHTML = '<p class="sottotesto">Caricamento…</p>';
    $('ds-azioni').innerHTML = ''; $('ds-msg').textContent = '';
    if (!dlg.open) dlg.showModal();
    const d = await api(`/api/limiti/schede/${id}`);
    const r = d.riga, s = d.dati;
    $('ds-titolo').innerHTML = `${esc(r.codice)} v${r.versione} · ${esc(r.parametro)} <span class="lim-stato lim-stato-${r.stato}">${esc(r.statoNome)}</span>`;
    const abb = (s.abbinamenti || []).map(a => `<span>${esc(a.nomeParametro || a.idParametroArcLab)} <small>(${a.idParametroArcLab})</small>${a.fattoreUnita ? ` ×${a.fattoreUnita}` : ''}</span>`).join('')
        || '<em style="color:#b00">nessun parametro ArcLab abbinato: la scheda non potrà segnalare nulla</em>';
    const f = d.fonte;
    $('ds-corpo').innerHTML = `
        <table class="tabella" style="font-size:.85rem"><tbody>
        <tr><th style="width:12rem">Famiglia</th><td>${esc(r.famigliaNome)}</td></tr>
        <tr><th>Matrice / categoria</th><td>${esc(r.matrice) || '—'}</td></tr>
        <tr><th>Limite</th><td><strong>${esc(r.limite)}</strong> · ${esc(r.tipoNome)}</td></tr>
        <tr><th>Condizioni</th><td>${esc(d.condizioni) || '—'}</td></tr>
        <tr><th>Riferimento</th><td>${esc(r.riferimento) || '—'}</td></tr>
        <tr><th>Validità</th><td>${s.validoDal ? 'dal ' + data(s.validoDal) : ''} ${s.validoAl ? 'al ' + data(s.validoAl) : ''}${!s.validoDal && !s.validoAl ? '—' : ''}</td></tr>
        <tr><th>Testo della segnalazione</th><td>${esc(d.testoSegnalazione) || '—'}</td></tr>
        <tr><th>Parametri ArcLab</th><td><div class="lim-abb">${abb}</div></td></tr>
        <tr><th>Fonte</th><td>${f ? `${esc(f.titolo)}<br><span class="sottotesto">versione consultata il ${data(f.dataConsultazione)}${f.dataAggiornamentoDichiarata ? ', aggiornata al ' + data(f.dataAggiornamentoDichiarata) : ''} (${esc(f.origine)})</span>
            · <a href="/api/limiti/versioni/${f.idVersione}/file" target="_blank">copia salvata</a> · <a href="/api/limiti/versioni/${f.idVersione}/testo" target="_blank">testo letto</a>${f.url ? ` · <a href="${esc(f.url)}" target="_blank" rel="noopener">sito ufficiale</a>` : ''}` : '—'}</td></tr>
        <tr><th>Proposta</th><td>${esc(r.propostaDa)} il ${dataOra(r.dataProposta)}${d.modificataDa ? ` · corretta da ${esc(d.modificataDa)} il ${dataOra(d.dataModifica)}` : ''}</td></tr>
        ${r.validataDa ? `<tr><th>${r.stato === 4 ? 'Respinta' : 'Validata'}</th><td>${esc(r.validataDa)} il ${dataOra(r.dataValidazione)}${d.noteValidazione ? ' — ' + esc(d.noteValidazione) : ''}</td></tr>` : ''}
        ${d.note ? `<tr><th>Note</th><td>${esc(d.note)}</td></tr>` : ''}
        </tbody></table>
        <h4 style="margin:0.8rem 0 0.3rem">Citazione della norma</h4>
        <div class="lim-citazione">${esc(d.citazione) || '<em>nessuna citazione</em>'}</div>
        ${d.versioni.length > 1 ? `<h4 style="margin:0.8rem 0 0.3rem">Versioni di ${esc(r.codice)}</h4>
        <table class="tabella lim-versioni"><thead><tr><th>Ver.</th><th>Limite</th><th>Stato</th><th>Proposta</th><th>Validazione</th></tr></thead><tbody>
        ${d.versioni.map(v => `<tr${v.id === r.id ? ' style="background:#FFFBEB"' : ' class="cliccabile" data-id="' + v.id + '"'}><td>v${v.versione}</td><td>${esc(v.limite)}</td><td>${esc(v.statoNome)}</td><td>${esc(v.propostaDa)} ${data(v.dataProposta)}</td><td>${esc(v.validataDa) || ''} ${v.dataValidazione ? data(v.dataValidazione) : ''}</td></tr>`).join('')}
        </tbody></table>` : ''}`;
    $('ds-corpo').querySelectorAll('tr[data-id]').forEach(tr => tr.addEventListener('click', () => apriScheda(+tr.dataset.id)));

    if (!puoValidare) return;
    const az = $('ds-azioni');
    if (r.stato === 1 || r.stato === 3) {
        az.innerHTML = `<label style="flex:1;min-width:16rem;display:flex;flex-direction:column;font-size:.82rem">Nota (obbligatoria per respingere)
            <input id="ds-nota" maxlength="1000" /></label>
            <button class="pulsante" type="button" id="ds-valida" ${d.puoValidare ? '' : 'disabled'} title="${esc(d.motivoNonValidabile || '')}">Valida</button>
            <button class="pulsante pulsante-tenue" type="button" id="ds-respingi">Respingi</button>
            <button class="pulsante pulsante-tenue" type="button" id="ds-correggi">Correggi</button>`;
        if (!d.puoValidare && d.motivoNonValidabile) messaggio($('ds-msg'), d.motivoNonValidabile, true);
        $('ds-valida').addEventListener('click', () => azione(`/api/limiti/schede/${id}/valida`, id));
        $('ds-respingi').addEventListener('click', () => azione(`/api/limiti/schede/${id}/respingi`, id));
    } else if (r.stato === 2) {
        // 20/10/2026: ritirare una scheda validata (doppia o non più voluta): resta nello storico come respinta.
        az.innerHTML = `<label style="flex:1;min-width:16rem;display:flex;flex-direction:column;font-size:.82rem">Motivo (obbligatorio per ritirare)
            <input id="ds-nota" maxlength="1000" placeholder="es. doppia di MOL-003" /></label>
            <button class="pulsante pulsante-tenue" type="button" id="ds-correggi">Correggi (nuova versione)</button>
            <button class="pulsante pulsante-tenue" type="button" id="ds-ritira">Ritira</button>`;
        $('ds-ritira').addEventListener('click', () => {
            if (!confirm(`Ritirare ${r.codice} v${r.versione}? Non sarà più usata nei controlli di «Da Validare» (resta nello storico).`)) return;
            azione(`/api/limiti/schede/${id}/ritira`, id);
        });
    }
    $('ds-correggi')?.addEventListener('click', () => { dlg.close(); apriModifica(d); });
}

async function azione(url, id) {
    try {
        const j = await post(url, { testo: $('ds-nota')?.value || null });
        messaggio($('ds-msg'), j.messaggio);
        await caricaSchede();
        await apriScheda(id);
        messaggio($('ds-msg'), j.messaggio);
    } catch (e) { messaggio($('ds-msg'), e.message, true); }
}

// ── Nuova scheda / correzione ───────────────────────────────────

let modifica = { id: null, abbinamenti: [], parametri: [] };
// «1.000» = mille, «0,5» e «0.5» = mezzo
const num = v => {
    let t = (v ?? '').toString().trim().replace(/\s/g, '');
    if (t === '') return null;
    if (t.includes(',')) t = t.replace(/\./g, '').replace(',', '.');
    else if (/^\d{1,3}(\.\d{3})+$/.test(t)) t = t.replace(/\./g, '');
    return Number(t);
};
const numTesto = v => v === null || v === undefined ? '' : String(v).replace('.', ',');
const giorno = d => d ? d.substring(0, 10) : '';

async function caricaParametri() {
    const f = $('dm-famiglia').value;
    $('dm-elenco-par').innerHTML = '';
    try {
        modifica.parametri = await api(`/api/limiti/parametri-arclab?famiglia=${f}`);
        $('dm-elenco-par').innerHTML = modifica.parametri.map(p => `<option value="${esc(p.nome)} [${p.id}]">${esc(p.unita)} · ${p.prove} prove</option>`).join('');
    } catch (e) { messaggio($('dm-msg'), 'Parametri ArcLab non disponibili: ' + e.message, true); }
}

function disegnaAbbinamenti() {
    const box = $('dm-abbinamenti');
    box.innerHTML = modifica.abbinamenti.map((a, i) => `<span>${esc(a.nomeParametro || a.idParametroArcLab)} <small>(${a.idParametroArcLab})</small><button type="button" data-i="${i}" title="Togli">×</button></span>`).join('')
        || '<span style="background:#FEF3C7;color:#7a5b00">nessuno</span>';
    box.querySelectorAll('button[data-i]').forEach(b => b.addEventListener('click', () => { modifica.abbinamenti.splice(+b.dataset.i, 1); disegnaAbbinamenti(); }));
}

function aggiungiParametro() {
    const v = $('dm-cerca-par').value;
    const m = v.match(/\[(\d+)\]\s*$/);
    const p = m ? modifica.parametri.find(x => x.id === +m[1]) : modifica.parametri.find(x => x.nome.toLowerCase() === v.trim().toLowerCase());
    if (!p) { messaggio($('dm-msg'), 'Scegli un parametro dall\'elenco.', true); return; }
    if (!modifica.abbinamenti.some(a => a.idParametroArcLab === p.id)) modifica.abbinamenti.push({ idParametroArcLab: p.id, nomeParametro: p.nome });
    $('dm-cerca-par').value = ''; $('dm-msg').textContent = '';
    disegnaAbbinamenti();
}

function aggiornaCampi() {
    const op = +$('dm-operatore').value, piano = +$('dm-regola').value === 2;
    $('dm-valoremax').disabled = op !== 4;
    $('dm-valore').disabled = op === 5;
    for (const id of ['dm-n', 'dm-c', 'dm-m', 'dm-mm']) $(id).disabled = !piano;
}

async function apriModifica(d) {
    const s = d?.dati;
    modifica = { id: d ? d.riga.id : null, abbinamenti: s ? [...s.abbinamenti] : [], parametri: [] };
    $('dm-titolo').textContent = d ? `Correggi ${d.riga.codice} v${d.riga.versione}` + (d.riga.stato === 2 ? ' (nascerà una nuova versione)' : '') : 'Nuova scheda';
    $('dm-famiglia').value = s ? s.famiglia : ($('f-famiglia').value || opzioni.famiglieFase1[0]);
    $('dm-famiglia').disabled = !!d;
    $('dm-parametro').value = s?.parametro ?? '';
    $('dm-tipo').value = s?.tipoLimite ?? 1;
    $('dm-operatore').value = s?.operatore ?? 1;
    $('dm-valore').value = numTesto(s?.valore);
    $('dm-valoremax').value = numTesto(s?.valoreMax);
    $('dm-unita').value = s?.unita ?? '';
    $('dm-regola').value = s?.regolaValutazione ?? 1;
    $('dm-n').value = s?.n ?? ''; $('dm-c').value = s?.c ?? '';
    $('dm-m').value = numTesto(s?.m); $('dm-mm').value = numTesto(s?.mMaiuscola);
    $('dm-dal').value = giorno(s?.validoDal); $('dm-al').value = giorno(s?.validoAl);
    $('dm-matrice').value = s?.matriceCategoria ?? '';
    $('dm-condizioni').value = s?.condizioni ?? '';
    $('dm-riferimento').value = s?.riferimento ?? '';
    $('dm-citazione').value = s?.citazione ?? '';
    $('dm-segnalazione').value = s?.testoSegnalazione ?? '';
    $('dm-note').value = s?.note ?? '';
    $('dm-msg').textContent = '';
    aggiornaCampi(); disegnaAbbinamenti();
    $('dlg-modifica').showModal();
    await caricaParametri();
}

async function salvaModifica() {
    const intero = v => v.trim() === '' ? null : parseInt(v, 10);
    const corpo = {
        famiglia: +$('dm-famiglia').value, idVersioneFonte: null,
        parametro: $('dm-parametro').value, matriceCategoria: $('dm-matrice').value,
        operatore: +$('dm-operatore').value, valore: num($('dm-valore').value), valoreMax: num($('dm-valoremax').value),
        unita: $('dm-unita').value, n: intero($('dm-n').value), c: intero($('dm-c').value), m: num($('dm-m').value), mMaiuscola: num($('dm-mm').value),
        tipoLimite: +$('dm-tipo').value, regolaValutazione: +$('dm-regola').value,
        condizioni: $('dm-condizioni').value, riferimento: $('dm-riferimento').value, citazione: $('dm-citazione').value,
        testoSegnalazione: $('dm-segnalazione').value, note: $('dm-note').value,
        validoDal: $('dm-dal').value || null, validoAl: $('dm-al').value || null,
        abbinamenti: modifica.abbinamenti
    };
    if ([corpo.valore, corpo.valoreMax, corpo.m, corpo.mMaiuscola].some(v => v !== null && Number.isNaN(v))) { messaggio($('dm-msg'), 'Controlla i numeri (es. 1000 oppure 0,5).', true); return; }
    if (modifica.id) {
        // la correzione conserva la versione della fonte da cui la scheda è stata tratta
        const d = await api(`/api/limiti/schede/${modifica.id}`);
        corpo.idVersioneFonte = d.dati.idVersioneFonte;
    }
    try {
        const j = await post(modifica.id ? `/api/limiti/schede/${modifica.id}/correggi` : '/api/limiti/schede', corpo);
        $('dlg-modifica').close();
        await caricaSchede();
        await apriScheda(j.id);
        messaggio($('ds-msg'), j.messaggio);
    } catch (e) { messaggio($('dm-msg'), e.message, true); }
}

// ── Fonti ───────────────────────────────────────────────────────

const statiEstrazione = { 0: '', 1: 'in corso', 2: 'completata', 3: 'errore' };

async function caricaFonti() {
    const j = await api('/api/limiti/fonti');
    fonti = j.fonti;
    disegnaFonti(j.caratteriMassimi);
    clearTimeout(attesaFonti);
    if (fonti.some(f => f.versioni.some(v => v.statoEstrazione === 1))) attesaFonti = setTimeout(async () => { await caricaFonti(); await caricaSchede(); }, 6000);
}

function disegnaFonti(massimo) {
    const tb = $('tabella-fonti').tBodies[0];
    if (!fonti.length) { tb.innerHTML = `<tr><td colspan="5" class="sottotesto">Nessuna fonte.${puoValidare ? ' Premi «Aggiungi le fonti della fase 1».' : ''}</td></tr>`; return; }
    tb.innerHTML = fonti.map(f => {
        const vers = f.versioni.map((v, i) => `<div style="margin-bottom:0.4rem${i ? ';opacity:.75' : ''}">
            <strong>${data(v.dataConsultazione)}</strong> ${esc(v.origine.toLowerCase())}${v.dataAggiornamentoDichiarata ? `, aggiornata al ${data(v.dataAggiornamentoDichiarata)}` : ''}
            · ${v.caratteri.toLocaleString('it-IT')} caratteri${v.caratteri > massimo ? ' <span style="color:#b00">(troppo lungo per l\'AI)</span>' : ''}
${v.nonValida ? ' <span style="color:#b00">(non è la norma: pagina di controllo del sito, da ignorare)</span>' : ''}
            · <a href="/api/limiti/versioni/${v.idVersioneFonte}/file" target="_blank">copia</a> · <a href="/api/limiti/versioni/${v.idVersioneFonte}/testo" target="_blank">testo</a>
            ${v.statoEstrazione ? `<br><span class="sottotesto" style="${v.statoEstrazione === 3 ? 'color:#b00' : ''}">AI ${statiEstrazione[v.statoEstrazione]}${v.esitoEstrazione ? ': ' + esc(v.esitoEstrazione) : ''}</span>` : ''}
            ${puoValidare && !v.nonValida && v.statoEstrazione !== 1 && v.caratteri <= massimo ? `<br><button class="pulsante pulsante-tenue estrai" type="button" data-v="${v.idVersioneFonte}">${v.statoEstrazione ? 'Estrai di nuovo con AI' : 'Estrai con AI'}</button>` : ''}
            </div>`).join('') || '<span class="sottotesto">nessuna</span>';
        return `<tr style="${f.attiva ? '' : 'opacity:.55'}" data-id="${f.id}">
            <td>${esc(f.famigliaNome)}</td>
            <td><strong>${esc(f.titolo)}</strong>${f.attiva ? '' : ' <span class="sottotesto">(disattivata)</span>'}<br>
                <span class="sottotesto">${esc(f.ente)}</span>${f.url ? ` · <a href="${esc(f.url)}" target="_blank" rel="noopener">sito</a>` : ''}
                <br><span class="sottotesto" style="color:${f.canale ? '#1a7f37' : '#7a5b00'}">${f.canale ? `aggiornata dall'agente fonti (${esc(f.canale)})` : 'aggiornamento a mano («Carica file»)'}</span>
                ${f.note ? `<br><span class="sottotesto">${esc(f.note)}</span>` : ''}</td>
            <td>${dataOra(f.dataUltimaVerifica)}<br><span class="sottotesto">${esc(f.esitoUltimaVerifica)}</span></td>
            <td style="min-width:18rem">${vers}</td>
            <td style="white-space:nowrap">${puoValidare ? `${f.canale ? '' : `<button class="pulsante pulsante-tenue scarica" type="button" ${f.url ? '' : 'disabled'}>Scarica</button><br>`}
                <button class="pulsante pulsante-tenue carica" type="button" style="margin-top:0.3rem">Carica file</button><br>
                <button class="pulsante pulsante-tenue modifica" type="button" style="margin-top:0.3rem">Modifica</button>` : ''}</td></tr>`;
    }).join('');
    tb.querySelectorAll('tr[data-id]').forEach(tr => {
        const f = fonti.find(x => x.id === +tr.dataset.id);
        tr.querySelector('.scarica')?.addEventListener('click', e => scarica(f, e.target));
        tr.querySelector('.carica')?.addEventListener('click', () => apriCarica(f));
        tr.querySelector('.modifica')?.addEventListener('click', () => apriFonte(f));
        tr.querySelectorAll('.estrai').forEach(b => b.addEventListener('click', () => apriEstrai(f, +b.dataset.v)));
    });
}

async function scarica(f, btn) {
    btn.disabled = true; $('stato-fonti').textContent = `Scarico «${f.titolo}»…`; $('stato-fonti').style.color = '';
    try { messaggio($('stato-fonti'), (await post(`/api/limiti/fonti/${f.id}/scarica`)).messaggio); }
    catch (e) { messaggio($('stato-fonti'), e.message, true); }
    btn.disabled = false;
    await caricaFonti();
}

// 18/10/2026: scelta delle famiglie da estrarre (l'AI assegna la famiglia a ogni scheda).
let estrazione = null;
function apriEstrai(f, idVersione) {
    estrazione = { idVersione };
    $('de-fonte').textContent = f.titolo;
    $('de-famiglie').innerHTML = opzioni.famiglie.map(x => `<label class="casella"><input type="checkbox" value="${x.valore}" checked /> ${esc(x.nome)}</label>`).join('');
    $('de-solo').textContent = `solo «${f.famigliaNome}»`;
    $('de-solo').onclick = () => $('de-famiglie').querySelectorAll('input').forEach(c => c.checked = +c.value === f.famiglia);
    $('de-msg').textContent = '';
    $('dlg-estrai').showModal();
}
async function avviaEstrazione() {
    const scelte = [...$('de-famiglie').querySelectorAll('input:checked')].map(c => +c.value);
    if (!scelte.length) { messaggio($('de-msg'), 'Scegli almeno una famiglia.', true); return; }
    const tutte = scelte.length === opzioni.famiglie.length;
    try {
        const j = await post(`/api/limiti/versioni/${estrazione.idVersione}/estrai`, { famiglie: tutte ? null : scelte });
        $('dlg-estrai').close();
        messaggio($('stato-fonti'), j.messaggio);
    } catch (e) { messaggio($('de-msg'), e.message, true); }
    await caricaFonti();
}

let fonteInCarica = null;
function apriCarica(f) {
    fonteInCarica = f;
    $('dc-fonte').textContent = f.titolo; $('dc-file').value = ''; $('dc-data').value = ''; $('dc-msg').textContent = '';
    $('dlg-carica').showModal();
}
async function inviaCarica() {
    const file = $('dc-file').files[0];
    if (!file) { messaggio($('dc-msg'), 'Scegli il file.', true); return; }
    const fd = new FormData(); fd.append('file', file);
    if ($('dc-data').value) fd.append('data', $('dc-data').value);
    $('dc-invia').disabled = true; $('dc-msg').textContent = 'Caricamento e lettura del testo…'; $('dc-msg').style.color = '';
    try {
        const j = await api(`/api/limiti/fonti/${fonteInCarica.id}/carica`, { method: 'POST', body: fd });
        $('dlg-carica').close(); messaggio($('stato-fonti'), j.messaggio); await caricaFonti();
    } catch (e) { messaggio($('dc-msg'), e.message, true); }
    $('dc-invia').disabled = false;
}

let fonteInModifica = null;
function apriFonte(f) {
    fonteInModifica = f || null;
    $('df-titolo').textContent = f ? 'Modifica fonte' : 'Nuova fonte';
    $('df-famiglia').value = f?.famiglia ?? opzioni.famiglieFase1[0];
    $('df-titolo-in').value = f?.titolo ?? ''; $('df-ente').value = f?.ente ?? '';
    $('df-tipo').value = f?.tipoFonte ?? 1; $('df-url').value = f?.url ?? ''; $('df-note').value = f?.note ?? '';
    $('df-attiva').checked = f ? f.attiva : true; $('df-msg').textContent = '';
    $('dlg-fonte').showModal();
}
async function salvaFonte() {
    try {
        const j = await post('/api/limiti/fonti', {
            id: fonteInModifica?.id ?? null, famiglia: +$('df-famiglia').value, titolo: $('df-titolo-in').value, ente: $('df-ente').value,
            tipoFonte: +$('df-tipo').value, url: $('df-url').value, note: $('df-note').value, attiva: $('df-attiva').checked
        });
        $('dlg-fonte').close(); messaggio($('stato-fonti'), j.messaggio); await caricaFonti();
    } catch (e) { messaggio($('df-msg'), e.message, true); }
}

// ── Verifica delle schede validate (18/10/2026) ─────────────────

let verifica = null, attesaVerifica = null;
const rilieviSel = new Set();
const coloriGravita = { 3: 'background:#FEE2E2;color:#b00', 2: 'background:#FDE7D7;color:#A6460A', 1: 'background:#FEF3C7;color:#7a5b00', 0: 'background:#EBF7EF;color:#1a7f37' };
const statiGiro = { 1: 'in coda', 2: 'in corso', 3: 'completato', 4: 'errore' };

async function caricaVerifica() {
    const giro = $('ver-giro').value;
    verifica = await api('/api/limiti/verifica?' + (giro ? `giro=${giro}` : `chiusi=${$('ver-chiusi').checked}`));
    disegnaVerifica();
    clearTimeout(attesaVerifica);
    if (verifica.giri.some(g => g.stato === 1 || g.stato === 2)) attesaVerifica = setTimeout(caricaVerifica, 8000);
}

function disegnaVerifica() {
    const v = verifica;
    // fonti
    const tf = $('tabella-ver-fonti').tBodies[0];
    tf.innerHTML = v.fonti.length ? v.fonti.map(f => `<tr>
        <td>${esc(f.titolo)}</td><td class="numerico">${f.schede}</td>
        <td>${f.ultima ? `${dataOra(f.ultima.dataRichiesta)} · ${statiGiro[f.ultima.stato]}<br><span class="sottotesto">${esc(f.ultima.esito)}</span>` : '<span class="sottotesto">mai</span>'}</td>
        <td>${puoValidare ? `<button class="pulsante pulsante-tenue rev" type="button" data-f="${f.id}" ${f.ultima && (f.ultima.stato === 1 || f.ultima.stato === 2) ? 'disabled' : ''}>Revisione con AI</button>` : ''}</td></tr>`).join('')
        : '<tr><td colspan="4" class="sottotesto">Nessuna scheda validata.</td></tr>';
    tf.querySelectorAll('.rev').forEach(b => b.addEventListener('click', () => avviaRevisione(+b.dataset.f, b)));
    // giri
    const sel = $('ver-giro'), attuale = sel.value;
    sel.innerHTML = '<option value="">tutti i rilievi aperti</option>' + v.giri.map(g => `<option value="${g.id}">n. ${g.id} · ${esc(g.tipo)} · ${esc(g.fonte)} · ${data(g.dataRichiesta)}</option>`).join('');
    sel.value = attuale;
    $('ver-excel').hidden = !attuale;
    if (attuale) $('ver-excel').href = `/api/limiti/verifica/${attuale}/excel`;
    $('tabella-giri').tBodies[0].innerHTML = v.giri.map(g => `<tr>
        <td>${g.id}</td><td>${esc(g.tipo)}</td><td>${esc(g.fonte)}</td><td>${dataOra(g.dataRichiesta)}<br><span class="sottotesto">${esc(g.richiestoDa)}</span></td>
        <td><strong>${statiGiro[g.stato]}</strong> ${esc(g.esito)}</td>
        <td><a href="/api/limiti/verifica/${g.id}/excel">Excel</a></td></tr>`).join('');
    // rilievi
    for (const id of [...rilieviSel]) if (!v.rilievi.some(r => r.id === id && !r.chiuso)) rilieviSel.delete(id);
    const tr = $('tabella-rilievi').tBodies[0];
    tr.innerHTML = v.rilievi.length ? v.rilievi.map(r => `<tr class="cliccabile" data-scheda="${r.idScheda}" style="${r.chiuso ? 'opacity:.6' : ''}">
        <td>${puoValidare && !r.chiuso ? `<input type="checkbox" class="rsel" data-id="${r.id}" ${rilieviSel.has(r.id) ? 'checked' : ''} />` : ''}</td>
        <td><span class="lim-stato" style="${coloriGravita[r.gravita]}">${esc(r.gravitaNome)}</span></td>
        <td>${esc(r.regola)}</td>
        <td><strong>${esc(r.codice)}</strong> <span class="sottotesto">v${r.versione}</span><br><span class="sottotesto">${esc(r.statoScheda)}</span></td>
        <td>${esc(r.parametro)}<br><span class="sottotesto">${esc(r.matrice)}</span></td>
        <td style="white-space:nowrap">${esc(r.limite)}</td>
        <td>${esc(r.motivo)}${r.citazione ? `<div class="lim-citazione" style="margin-top:0.3rem;padding:0.2rem 0.5rem;font-size:.78rem">${esc(r.citazione)}${r.citazioneTrovata === false ? ' <strong style="color:#b00">(non trovata nel testo)</strong>' : ''}</div>` : ''}</td>
        <td>${r.chiuso ? `chiuso${r.chiusoDa ? ' da ' + esc(r.chiusoDa) : ''}${r.notaChiusura ? '<br><span class="sottotesto">' + esc(r.notaChiusura) + '</span>' : ''}` : '<strong>aperto</strong>'}</td></tr>`).join('')
        : '<tr><td colspan="8" class="sottotesto">Nessun rilievo.</td></tr>';
    tr.querySelectorAll('tr[data-scheda]').forEach(r => r.addEventListener('click', e => { if (!e.target.closest('input')) apriScheda(+r.dataset.scheda); }));
    tr.querySelectorAll('input.rsel').forEach(c => c.addEventListener('change', () => { c.checked ? rilieviSel.add(+c.dataset.id) : rilieviSel.delete(+c.dataset.id); barraRilievi(); }));
    barraRilievi();
}

function barraRilievi() {
    const b = $('ver-barra'); if (!b) return;
    b.style.display = rilieviSel.size ? 'flex' : 'none';
    $('ver-conta').textContent = `${rilieviSel.size} rilievi selezionati`;
}

async function avviaRevisione(idFonte, btn) {
    btn.disabled = true;
    try { messaggio($('stato-verifica'), (await post(`/api/limiti/verifica/revisione/${idFonte}`)).messaggio); }
    catch (e) { messaggio($('stato-verifica'), e.message, true); btn.disabled = false; }
    await caricaVerifica();
}

$('ver-automatica')?.addEventListener('click', async e => {
    e.target.disabled = true; $('stato-verifica').textContent = 'Controlli in corso…'; $('stato-verifica').style.color = '';
    try {
        const j = await post('/api/limiti/verifica/automatica');
        messaggio($('stato-verifica'), j.messaggio);
        await caricaVerifica();
        $('ver-giro').value = String(j.giro); await caricaVerifica();
    } catch (err) { messaggio($('stato-verifica'), err.message, true); }
    e.target.disabled = false;
});
$('ver-chiudi')?.addEventListener('click', async () => {
    const nota = $('ver-nota').value.trim();
    if (!nota) { messaggio($('ver-msg'), 'Scrivi la nota.', true); return; }
    try {
        const j = await post('/api/limiti/verifica/chiudi', { ids: [...rilieviSel], testo: nota });
        rilieviSel.clear(); $('ver-nota').value = '';
        messaggio($('stato-verifica'), j.messaggio);
        await caricaVerifica();
    } catch (e) { messaggio($('ver-msg'), e.message, true); }
});
$('ver-giro').addEventListener('change', caricaVerifica);
$('ver-chiusi').addEventListener('change', caricaVerifica);

// ── Categorie e matrici (fase 1b, 18/10/2026) ───────────────────

let categorie = null, attesaCategorie = null;
const matriciSel = new Set();

async function caricaCategorie() {
    categorie = await api('/api/limiti/categorie');
    disegnaCategorie();
    clearTimeout(attesaCategorie);
    if (categorie.giro && (categorie.giro.stato === 1 || categorie.giro.stato === 2)) attesaCategorie = setTimeout(caricaCategorie, 8000);
}

function disegnaCategorie() {
    const c = categorie;
    if ($('stato-categorie')) {
        $('stato-categorie').textContent = c.avviso || (c.giro ? `Ultima proposta AI: ${dataOra(c.giro.dataRichiesta)} · ${statiGiro[c.giro.stato]} · ${c.giro.esito || ''}` : '');
        $('stato-categorie').style.color = c.avviso ? '#b00' : '';
    }
    $('cat-conta').textContent = `(${c.categorie.length})`;
    $('tabella-categorie').tBodies[0].innerHTML = c.categorie.map(k => `<tr><td>${esc(k.nome)}</td><td>${esc(k.famigliaNome)}</td>
        <td class="sottotesto">${esc(k.parametri.join(', '))}</td><td class="numerico">${k.validate}</td><td class="numerico">${k.matrici}</td></tr>`).join('')
        || '<tr><td colspan="5" class="sottotesto">Nessuna categoria: prima estrai le schede degli Alimenti (Fonti → Estrai con AI).</td></tr>';
    const filtro = $('cat-filtro').value, cerca = $('cat-cerca').value.trim().toLowerCase();
    const elenco = c.matrici.filter(m => {
        const prop = m.abbinamenti.some(a => !a.confermato), conf = m.abbinamenti.some(a => a.confermato);
        if (filtro === 'proposte' && !prop) return false;
        if (filtro === 'senza' && m.abbinamenti.length) return false;
        if (filtro === 'dafare' && conf && !prop) return false;
        return !cerca || m.nome.toLowerCase().includes(cerca) || m.abbinamenti.some(a => a.categoria.toLowerCase().includes(cerca));
    });
    for (const id of [...matriciSel]) if (!elenco.some(m => m.id === id)) matriciSel.delete(id);
    const opzCat = '<option value="">aggiungi categoria…</option><option value="0">— nessuna categoria —</option>'
        + c.categorie.map(k => `<option value="${k.id}">${esc(k.nome)}</option>`).join('');
    const tb = $('tabella-matrici').tBodies[0];
    tb.innerHTML = elenco.length ? elenco.map(m => `<tr data-m="${m.id}">
        <td>${puoValidare ? `<input type="checkbox" class="msel" ${matriciSel.has(m.id) ? 'checked' : ''} />` : ''}</td>
        <td><strong>${esc(m.nome)}</strong></td><td class="numerico">${m.prove.toLocaleString('it-IT')}</td>
        <td>${m.abbinamenti.map(a => `<span class="lim-stato" style="${a.confermato ? 'background:#EBF7EF;color:#1a7f37' : 'background:#FEF3C7;color:#7a5b00'};margin:1px 2px;display:inline-block"
            title="${esc((a.confermato ? 'confermato da ' + (a.confermatoDa || '') : 'proposto da ' + a.propostoDa) + (a.nota ? ' — ' + a.nota : ''))}">${esc(a.categoria)}
            ${puoValidare && !a.confermato ? `<button type="button" class="ab-ok" data-a="${a.id}" title="Conferma" style="border:none;background:none;cursor:pointer;color:#1a7f37">✓</button>` : ''}
            ${puoValidare ? `<button type="button" class="ab-no" data-a="${a.id}" title="Togli" style="border:none;background:none;cursor:pointer;color:#b00">✕</button>` : ''}</span>`).join('')
            || '<span class="sottotesto">nessun abbinamento</span>'}</td>
        <td>${puoValidare ? `<select class="ab-add" style="max-width:16rem">${opzCat}</select>` : ''}</td></tr>`).join('')
        : '<tr><td colspan="5" class="sottotesto">Nessuna matrice con questo filtro.</td></tr>';
    tb.querySelectorAll('tr[data-m]').forEach(tr => {
        const m = c.matrici.find(x => x.id === +tr.dataset.m);
        tr.querySelector('.msel')?.addEventListener('change', e => { e.target.checked ? matriciSel.add(m.id) : matriciSel.delete(m.id); });
        tr.querySelectorAll('.ab-ok').forEach(b => b.addEventListener('click', () => azioneCategorie('/api/limiti/categorie/conferma', { ids: [+b.dataset.a] })));
        tr.querySelectorAll('.ab-no').forEach(b => b.addEventListener('click', () => azioneCategorie('/api/limiti/categorie/respingi', { ids: [+b.dataset.a] })));
        tr.querySelector('.ab-add')?.addEventListener('change', e => {
            if (e.target.value === '') return;
            azioneCategorie('/api/limiti/categorie/abbina', { idMatrice: m.id, nomeMatrice: m.nome, idCategoria: e.target.value === '0' ? null : +e.target.value });
        });
    });
}

async function azioneCategorie(url, corpo) {
    try { messaggio($('cat-msg'), (await post(url, corpo)).messaggio); }
    catch (e) { messaggio($('cat-msg'), e.message, true); }
    await caricaCategorie();
}

$('cat-filtro').addEventListener('change', disegnaCategorie);
$('cat-cerca').addEventListener('input', disegnaCategorie);
$('cat-sel-tutte')?.addEventListener('click', () => {
    $('tabella-matrici').querySelectorAll('tr[data-m]').forEach(tr => { matriciSel.add(+tr.dataset.m); const c = tr.querySelector('.msel'); if (c) c.checked = true; });
});
$('cat-conferma')?.addEventListener('click', async () => {
    const ids = categorie.matrici.filter(m => matriciSel.has(m.id)).flatMap(m => m.abbinamenti.filter(a => !a.confermato).map(a => a.id));
    if (!ids.length) { messaggio($('cat-msg'), 'Nessuna proposta da confermare tra le matrici selezionate.', true); return; }
    if (!confirm(`Confermare ${ids.length} abbinamenti proposti per ${matriciSel.size} matrici?`)) return;
    matriciSel.clear();
    await azioneCategorie('/api/limiti/categorie/conferma', { ids });
});
$('cat-proponi')?.addEventListener('click', async e => {
    e.target.disabled = true;
    try { messaggio($('stato-categorie'), (await post('/api/limiti/categorie/proponi')).messaggio); } catch (err) { messaggio($('stato-categorie'), err.message, true); }
    e.target.disabled = false;
    await caricaCategorie();
});

// ── Fase 1b: tempi massimi ricezione → inizio prova (18/10/2026) ─────

let tempi = [], tempoInModifica = null, parametriTempo = [];

async function caricaTempi() {
    tempi = await api('/api/limiti/tempi');
    $('tabella-tempi').tBodies[0].innerHTML = tempi.map(t => `<tr${t.attivo ? '' : ' style="opacity:.55"'}>
        <td>${esc(t.famigliaNome)}</td>
        <td>${t.idParametro ? `${esc(t.nomeParametro)} <small class="sottotesto">(${t.idParametro})</small>` : '<em>tutti i parametri</em>'}</td>
        <td class="numerico"><strong>${t.ore}</strong> h</td>
        <td class="sottotesto">${esc(t.nota)}</td>
        <td>${t.attivo ? '<span class="lim-stato lim-stato-2">attiva</span>' : '<span class="lim-stato lim-stato-5">disattivata</span>'}</td>
        <td class="sottotesto">${esc(t.modificatoDa)}<br>${dataOra(t.dataModifica)}</td>
        <td>${puoValidare ? `<button class="pulsante pulsante-tenue" type="button" data-tm="${t.id}">Modifica</button>` : ''}</td>
    </tr>`).join('') || '<tr><td colspan="7" class="sottotesto">Nessuna riga.</td></tr>';
    $('tabella-tempi').querySelectorAll('[data-tm]').forEach(b => b.addEventListener('click', () => apriTempo(tempi.find(t => t.id === +b.dataset.tm))));
}

async function caricaParametriTempo() {
    $('dt-elenco-par').innerHTML = '';
    try {
        parametriTempo = await api(`/api/limiti/parametri-arclab?famiglia=${$('dt-famiglia').value}`);
        $('dt-elenco-par').innerHTML = parametriTempo.map(p => `<option value="${esc(p.nome)} [${p.id}]">${esc(p.unita)} · ${p.prove} prove</option>`).join('');
    } catch (e) { messaggio($('dt-msg'), 'Parametri ArcLab non disponibili: ' + e.message, true); }
}

function apriTempo(t) {
    tempoInModifica = t;
    $('dt-titolo').textContent = t ? 'Modifica tempo massimo' : 'Nuovo tempo massimo';
    riempi($('dt-famiglia'), opzioni.famiglie);
    $('dt-famiglia').value = t ? t.famiglia : opzioni.famiglie[0]?.valore;
    $('dt-parametro').value = t?.idParametro ? `${t.nomeParametro} [${t.idParametro}]` : '';
    $('dt-ore').value = t ? t.ore : 24;
    $('dt-nota').value = t?.nota || '';
    $('dt-attivo').checked = t ? t.attivo : true;
    $('dt-msg').textContent = '';
    caricaParametriTempo();
    $('dlg-tempo').showModal();
}

async function salvaTempo() {
    const v = $('dt-parametro').value.trim();
    let idParametro = null, nomeParametro = null;
    if (v) {
        const m = v.match(/^(.*)\[(\d+)\]\s*$/);
        const p = m ? { id: +m[2], nome: m[1].trim() } : parametriTempo.find(x => x.nome.toLowerCase() === v.toLowerCase());
        if (!p) { messaggio($('dt-msg'), 'Scegli un parametro dall\'elenco, oppure lascia vuoto per tutta la famiglia.', true); return; }
        idParametro = p.id; nomeParametro = p.nome;
    }
    try {
        const r = await post('/api/limiti/tempi', {
            id: tempoInModifica?.id ?? null, famiglia: +$('dt-famiglia').value, idParametro, nomeParametro,
            ore: +$('dt-ore').value, attivo: $('dt-attivo').checked, nota: $('dt-nota').value
        });
        $('dlg-tempo').close();
        messaggio($('tm-msg'), r.messaggio);
        await caricaTempi();
    } catch (e) { messaggio($('dt-msg'), e.message, true); }
}

$('tm-nuovo')?.addEventListener('click', () => apriTempo(null));
$('dt-famiglia').addEventListener('change', () => { $('dt-parametro').value = ''; caricaParametriTempo(); });
$('dt-salva').addEventListener('click', salvaTempo);

// ── Avvio ───────────────────────────────────────────────────────

function mostra(nome) {
    document.querySelectorAll('[data-scheda]').forEach(b => b.setAttribute('aria-selected', String(b.dataset.scheda === nome)));
    $('pannello-schede').hidden = nome !== 'schede';
    $('pannello-fonti').hidden = nome !== 'fonti';
    $('pannello-verifica').hidden = nome !== 'verifica';
    $('pannello-categorie').hidden = nome !== 'categorie';
    $('pannello-tempi').hidden = nome !== 'tempi';
    if (nome === 'tempi') caricaTempi().catch(e => messaggio($('tm-msg'), e.message, true));
    if (nome === 'categorie') caricaCategorie().catch(e => messaggio($('cat-msg'), e.message, true));
    if (nome === 'verifica') caricaVerifica().catch(e => messaggio($('stato-verifica') || $('stato-schede'), e.message, true));
    try { sessionStorage.setItem('limiti.scheda', nome); } catch { /* facoltativo */ }
}

document.querySelectorAll('[data-scheda]').forEach(b => b.addEventListener('click', () => mostra(b.dataset.scheda)));
document.querySelectorAll('dialog [data-chiudi]').forEach(b => b.addEventListener('click', () => b.closest('dialog').close()));
$('f-famiglia').addEventListener('change', caricaSchede);
$('f-storico').addEventListener('change', caricaSchede);
$('f-stato').addEventListener('change', disegnaSchede);
$('f-testo').addEventListener('input', disegnaSchede);
$('f-ordine').addEventListener('change', disegnaSchede);
$('sel-respingi')?.addEventListener('click', respingiSelezionate);
$('sel-valida')?.addEventListener('click', validaSelezionate);
$('f-rapida').addEventListener('change', disegnaSchede);
$('sel-tutte')?.addEventListener('click', () => selezionaVisibili(true));
$('sel-nessuna')?.addEventListener('click', () => selezionaVisibili(false));
$('btn-nuova')?.addEventListener('click', () => apriModifica(null));
$('dm-famiglia').addEventListener('change', caricaParametri);
$('dm-operatore').addEventListener('change', aggiornaCampi);
$('dm-regola').addEventListener('change', aggiornaCampi);
$('dm-aggiungi-par').addEventListener('click', aggiungiParametro);
$('dm-cerca-par').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); aggiungiParametro(); } });
$('dm-salva').addEventListener('click', salvaModifica);
$('btn-fase1')?.addEventListener('click', async () => {
    try { messaggio($('stato-fonti'), (await post('/api/limiti/fonti/fase1')).messaggio); } catch (e) { messaggio($('stato-fonti'), e.message, true); }
    await caricaFonti();
});
$('btn-nuova-fonte')?.addEventListener('click', () => apriFonte(null));
$('dc-invia').addEventListener('click', inviaCarica);
$('de-avvia').addEventListener('click', avviaEstrazione);
$('de-tutte').addEventListener('click', () => $('de-famiglie').querySelectorAll('input').forEach(c => c.checked = true));
$('df-salva').addEventListener('click', salvaFonte);

(async () => {
    opzioni = await api('/api/limiti/opzioni');
    riempi($('f-famiglia'), opzioni.famiglie, 'tutte');
    riempi($('f-stato'), opzioni.stati, 'tutti (tranne respinte e superate)');
    riempi($('dm-famiglia'), opzioni.famiglie);
    riempi($('df-famiglia'), opzioni.famiglie);
    riempi($('df-tipo'), opzioni.tipiFonte);
    riempi($('dm-tipo'), opzioni.tipiLimite);
    riempi($('dm-operatore'), opzioni.operatori);
    riempi($('dm-regola'), opzioni.regole);
    let iniziale = 'schede';
    try { iniziale = sessionStorage.getItem('limiti.scheda') || 'schede'; } catch { /* facoltativo */ }
    mostra(iniziale);
    await Promise.all([caricaSchede(), caricaFonti()]);
})().catch(e => { $('stato-schede').textContent = e.message; $('stato-schede').style.color = '#b00'; });
