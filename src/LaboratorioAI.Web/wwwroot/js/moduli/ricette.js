// Ricette nutrizionali: composizione della ricetta dal catalogo materie prime
// e anteprima della dichiarazione (per 100 g / porzione / %RI) via motore.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const numN = (id) => { const t = $(id).value.trim(); if (t === '') return null; const v = parseFloat(t.replace(',', '.')); return isNaN(v) ? null : v; };
const gg = (x, d = 2) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d }));
const gi = (x) => (x == null ? '—' : Math.round(Number(x)).toLocaleString('it-IT'));
const pct = (x) => (x == null ? '' : (Number(x) * 100).toLocaleString('it-IT', { maximumFractionDigits: 1 }) + ' %');

let elenco = [];
let clienti = [];
let righe = [];   // { idMateriaPrima, denominazione, quantitaKg }

// ── Elenco ricette ─────────────────────────────────────────────
async function carica() {
    const q = $('f-q').value.trim();
    try {
        elenco = await chiamaApi('/api/ricette/elenco' + (q ? '?q=' + encodeURIComponent(q) : ''));
        renderElenco();
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderElenco() {
    const corpo = document.querySelector('#tabella-elenco tbody');
    if (elenco.length === 0) { corpo.innerHTML = '<tr><td colspan="4" class="vuoto">Nessuna ricetta.</td></tr>'; return; }
    corpo.innerHTML = elenco.map(r => `
        <tr class="riga-cliccabile" data-id="${r.idRicetta}">
            <td><strong>${testo(r.denominazione)}</strong></td>
            <td>${testo(r.committente) || '—'}</td>
            <td class="numerico">${r.nRighe}</td>
            <td>${testo(r.dataUltimaModifica)}</td>
        </tr>`).join('');
    corpo.querySelectorAll('.riga-cliccabile').forEach(t =>
        t.addEventListener('click', () => apri(Number(t.dataset.id))));
}

async function caricaClienti() {
    try {
        clienti = await chiamaApi('/api/ricette/clienti');
        $('lista-clienti').innerHTML = clienti.map(c => `<option value="${testo(c.nome)}">`).join('');
    } catch { /* non bloccante */ }
}

// ── Apertura form ──────────────────────────────────────────────
function pulisci() {
    ['r-id','r-denominazione','r-committente','r-lotto','r-pezzi','r-calo','r-peso-cotto','r-redatto'].forEach(id => $(id).value = '');
    $('r-unita').value = '100';
    righe = [];
    renderRighe();
    $('tabella-risultati').querySelector('tbody').innerHTML = '';
    $('massa-info').textContent = '';
    $('pannello-verifica').classList.add('nascosto');
    $('pannello-usa').classList.add('nascosto');
    $('pannello-ca').classList.add('nascosto');
    $('r-zucc-agg').value = '';
    $('risultati-avviso').classList.add('nascosto');
}

function apriNuova() {
    pulisci();
    $('form-titolo').textContent = 'Nuova ricetta';
    $('pannello-form').classList.remove('nascosto');
    $('r-denominazione').focus();
}

async function apri(id) {
    try {
        const r = await chiamaApi('/api/ricette/' + id);
        pulisci();
        $('r-id').value = r.idRicetta;
        $('r-denominazione').value = r.denominazione ?? '';
        $('r-committente').value = r.committente ?? '';
        $('r-lotto').value = r.lotto ?? '';
        $('r-unita').value = r.unitaConsumoG ?? 100;
        $('r-pezzi').value = r.unitaConsumoPezzi ?? '';
        $('r-calo').value = r.caloPesoPercentuale ?? '';
        $('r-peso-cotto').value = r.pesoCottoKg ?? '';
        $('r-redatto').value = r.redattoDa ?? '';
        righe = (r.righe ?? []).map(x => ({ idMateriaPrima: x.idMateriaPrima, denominazione: x.denominazione, quantitaKg: x.quantitaKg }));
        renderRighe();
        $('form-titolo').textContent = 'Modifica ricetta';
        $('pannello-form').classList.remove('nascosto');
        $('pannello-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
        ricalcola();
    } catch (e) { avvisa(e.message, 'errore'); }
}

// ── Ricerca e aggiunta ingredienti ─────────────────────────────
async function cercaIngredienti() {
    const q = $('i-cerca').value.trim();
    if (q.length < 2) { $('i-risultati').classList.add('nascosto'); return; }
    try {
        const res = await chiamaApi('/api/ricette/materie-prime?q=' + encodeURIComponent(q));
        const corpo = $('i-risultati-corpo');
        corpo.innerHTML = res.length === 0
            ? '<tr><td class="vuoto">Nessun risultato.</td></tr>'
            : res.map(m => {
                const rif = [testo(m.provenienza), m.marca ? testo(m.marca) : ''].filter(Boolean).join(' · ');
                return `<tr class="riga-cliccabile" data-id="${m.idMateriaPrima}" data-nome="${testo(m.denominazione)}">
                <td>${testo(m.denominazione)} <span class="sottotesto">— ${rif}</span></td></tr>`;
            }).join('');
        corpo.querySelectorAll('.riga-cliccabile').forEach(t =>
            t.addEventListener('click', () => aggiungi(Number(t.dataset.id), t.dataset.nome)));
        $('i-risultati').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
}

function aggiungi(id, nome) {
    if (!righe.some(r => r.idMateriaPrima === id))
        righe.push({ idMateriaPrima: id, denominazione: nome, quantitaKg: 0 });
    $('i-cerca').value = '';
    $('i-risultati').classList.add('nascosto');
    renderRighe();
}

function renderRighe() {
    const corpo = document.querySelector('#tabella-ingredienti tbody');
    if (righe.length === 0) { corpo.innerHTML = '<tr><td colspan="3" class="vuoto">Nessun ingrediente.</td></tr>'; return; }
    corpo.innerHTML = righe.map((r, i) => `
        <tr>
            <td>${testo(r.denominazione)}</td>
            <td class="numerico"><input type="number" step="0.001" class="q-input" data-i="${i}" value="${r.quantitaKg || ''}" style="width:7rem" /></td>
            <td><button type="button" class="pulsante pulsante-tenue btn-rimuovi" data-i="${i}">Rimuovi</button></td>
        </tr>`).join('');
    corpo.querySelectorAll('.q-input').forEach(inp =>
        inp.addEventListener('input', () => {
            const v = parseFloat(inp.value.replace(',', '.'));
            righe[Number(inp.dataset.i)].quantitaKg = isNaN(v) ? 0 : v;
            ricalcolaDeb();
        }));
    corpo.querySelectorAll('.btn-rimuovi').forEach(b =>
        b.addEventListener('click', () => { righe.splice(Number(b.dataset.i), 1); renderRighe(); ricalcola(); }));
}

// ── Calcolo (anteprima) ────────────────────────────────────────
async function ricalcola() {
    const corpoR = $('tabella-risultati').querySelector('tbody');
    const valide = righe.filter(r => r.quantitaKg > 0);
    if (valide.length === 0) { corpoR.innerHTML = ''; $('massa-info').textContent = ''; return; }

    const corpo = {
        righe: valide.map(r => ({ idMateriaPrima: r.idMateriaPrima, quantitaKg: r.quantitaKg })),
        unitaConsumoG: numN('r-unita') ?? 100,
        caloPesoPercentuale: numN('r-calo'),
        pesoCottoKg: numN('r-peso-cotto')
    };
    try {
        const res = await inviaJson('/api/ricette/calcola', corpo);
        renderRisultati(res);
    } catch (e) { avvisa(e.message, 'errore'); }
}

function renderRisultati(res) {
    // Colonne = valori ESATTI (come la colonna "per 100 g" del foglio).
    const e = res.per100g, p = res.perUnitaConsumo, ri = res.riferimentiPer100g;
    const righeTab = [
        ['Energia (kJ)',  gi(e.energiaKj),  gi(p.energiaKj),  pct(ri.energiaKj)],
        ['Energia (kcal)',gi(e.energiaKcal),gi(p.energiaKcal),pct(ri.energiaKcal)],
        ['Grassi (g)',    gg(e.grassi),     gg(p.grassi),     pct(ri.grassi)],
        ['di cui saturi (g)', gg(e.grassiSaturi), gg(p.grassiSaturi), pct(ri.grassiSaturi)],
        ['Carboidrati (g)', gg(e.carboidrati), gg(p.carboidrati), pct(ri.carboidrati)],
        ['di cui zuccheri (g)', gg(e.zuccheri), gg(p.zuccheri), pct(ri.zuccheri)],
        ['Proteine (g)',  gg(e.proteine),   gg(p.proteine),   pct(ri.proteine)],
        ['Sale (g)',      gg(e.sale, 2),    gg(p.sale, 2),    pct(ri.sale)],
        ['Fibre (g)',     gg(e.fibre),      gg(p.fibre),      ''],
        ['Acqua (g)',     gg(e.acqua),      gg(p.acqua),      '']
    ];
    $('tabella-risultati').querySelector('tbody').innerHTML = righeTab.map(r => `
        <tr><td>${r[0]}</td><td class="numerico">${r[1]}</td><td class="numerico">${r[2]}</td><td class="numerico">${r[3]}</td></tr>`).join('');

    // Riga arrotondata per l'etichetta (Reg. 1169/2011).
    const a = res.per100gArrotondato;
    $('massa-info').innerHTML =
        `Arrotondati per etichetta (100 g): Energia ${gi(a.energiaKcal)} kcal / ${gi(a.energiaKj)} kJ · `
        + `Grassi ${gg(a.grassi)} · di cui saturi ${gg(a.grassiSaturi)} · Carboidrati ${gg(a.carboidrati)} · `
        + `zuccheri ${gg(a.zuccheri)} · Proteine ${gg(a.proteine)} · Sale ${gg(a.sale, 2)} · Fibre ${gg(a.fibre)}`
        + `<br>Massa totale ${gg(res.massaTotaleKg, 3)} kg · massa cotta ${gg(res.massaCottaKg, 3)} kg`;

    const avv = $('risultati-avviso');
    if (res.avvisi && res.avvisi.length) { avv.textContent = res.avvisi.join(' '); avv.classList.remove('nascosto'); }
    else avv.classList.add('nascosto');
}

// ── Salvataggio ────────────────────────────────────────────────
async function salva(pulsante) {
    const denom = $('r-denominazione').value.trim();
    if (!denom) { avvisa('La denominazione è obbligatoria.', 'errore'); return; }

    const nomeComm = $('r-committente').value.trim();
    const cliente = clienti.find(c => c.nome.toLowerCase() === nomeComm.toLowerCase());
    const corpo = {
        id: $('r-id').value ? Number($('r-id').value) : null,
        denominazione: denom,
        idClienteArcLab: cliente ? cliente.id : null,
        committente: nomeComm || null,
        lotto: $('r-lotto').value,
        unitaConsumoG: numN('r-unita') ?? 100,
        unitaConsumoPezzi: numN('r-pezzi'),
        caloPesoPercentuale: numN('r-calo'),
        pesoCottoKg: numN('r-peso-cotto'),
        note: null,
        redattoDa: $('r-redatto').value || null,
        righe: righe.filter(r => r.quantitaKg > 0).map(r => ({ idMateriaPrima: r.idMateriaPrima, quantitaKg: r.quantitaKg }))
    };

    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const esito = await inviaJson('/api/ricette/salva', corpo);
        $('r-id').value = esito.idRicetta;
        $('form-titolo').textContent = 'Modifica ricetta';
        avvisa('Ricetta salvata.', 'successo');
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function generaRapporto(pulsante, formato) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta, poi genera il rapporto.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/ricette/${id}/rapporto`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formato, redattoDa: $('r-redatto').value })
        });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const blob = await risposta.blob();
        const cd = risposta.headers.get('Content-Disposition') || '';
        const m = cd.match(/filename="?([^"]+)"?/);
        const nome = m ? m[1] : (formato === 'word' ? 'Rapporto_Nutrizionale.rtf' : 'Rapporto_Nutrizionale.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = nome; document.body.appendChild(a); a.click();
        a.remove(); URL.revokeObjectURL(url);
        avvisa('Rapporto generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function nutritionFacts(pulsante) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta, poi genera la Nutrition Facts.', 'errore'); return; }
    const libera = occupa(pulsante, 'Calcolo…');
    try {
        const f = await inviaJson(`/api/ricette/${id}/nutrition-facts`, {
            porzioneG: numN('r-unita'),
            zuccheriAggiuntiPer100g: numN('r-zucc-agg')
        });
        const dv = (r) => (r && r.dv != null ? r.dv + '%' : '');
        const q = (r) => (r ? r.quantita : '');
        const righe = [
            ['<strong>Calories</strong>', '<strong>' + f.calorie + '</strong>', ''],
            ['Total Fat', q(f.grassi), dv(f.grassi)],
            ['&nbsp;&nbsp;Saturated Fat', q(f.grassiSaturi), dv(f.grassiSaturi)],
            ['&nbsp;&nbsp;Trans Fat', f.grassiTrans, ''],
            ['Cholesterol', q(f.colesterolo), dv(f.colesterolo)],
            ['Sodium', q(f.sodio), dv(f.sodio)],
            ['Total Carbohydrate', q(f.carboidrati), dv(f.carboidrati)],
            ['&nbsp;&nbsp;Dietary Fiber', q(f.fibre), dv(f.fibre)],
            ['&nbsp;&nbsp;Total Sugars', f.zuccheriTotali, ''],
            ['&nbsp;&nbsp;&nbsp;&nbsp;Includes Added Sugars', q(f.zuccheriAggiunti), dv(f.zuccheriAggiunti)],
            ['Protein', f.proteine, ''],
            ['Vitamin D', q(f.vitaminaD), dv(f.vitaminaD)],
            ['Calcium', q(f.calcio), dv(f.calcio)],
            ['Iron', q(f.ferro), dv(f.ferro)],
            ['Potassium', q(f.potassio), dv(f.potassio)]
        ];
        $('tabella-usa').querySelector('tbody').innerHTML = righe.map(r =>
            `<tr><td>${r[0]}</td><td class="numerico">${testo(r[1])}</td><td class="numerico">${r[2]}</td></tr>`).join('');
        $('usa-porzione').textContent = `Porzione: ${f.porzioneG} g`;
        const avv = $('usa-avviso');
        if (f.avvisi && f.avvisi.length) { avv.textContent = f.avvisi.join(' '); avv.classList.remove('nascosto'); }
        else avv.classList.add('nascosto');
        $('usa-verifica').classList.add('nascosto');
        $('pannello-usa').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function nutritionFactsCa(pulsante) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta.', 'errore'); return; }
    const libera = occupa(pulsante, 'Calcolo…');
    try {
        const f = await inviaJson(`/api/ricette/${id}/nutrition-facts-ca`, { porzioneG: numN('r-unita') });
        const dv = (r) => (r && r.dv != null ? r.dv + '%' : '');
        const q = (r) => (r ? r.quantita : '');
        const righe = [
            ['<strong>Calories</strong>', '<strong>' + f.calorie + '</strong>', ''],
            ['Fat / Lipides', q(f.grassi), dv(f.grassi)],
            ['&nbsp;&nbsp;Saturated / saturés', f.grassiSaturi, f.saturiTransDv != null ? f.saturiTransDv + '%' : ''],
            ['&nbsp;&nbsp;+ Trans / trans', f.grassiTrans, ''],
            ['Cholesterol / Cholestérol', f.colesterolo, ''],
            ['Sodium / Sodium', q(f.sodio), dv(f.sodio)],
            ['Carbohydrate / Glucides', f.carboidrati, ''],
            ['&nbsp;&nbsp;Fibre / Fibres', q(f.fibre), dv(f.fibre)],
            ['&nbsp;&nbsp;Sugars / Sucres', q(f.zuccheri), dv(f.zuccheri)],
            ['Protein / Protéines', f.proteine, ''],
            ['Potassium / Potassium', q(f.potassio), dv(f.potassio)],
            ['Calcium / Calcium', q(f.calcio), dv(f.calcio)],
            ['Iron / Fer', q(f.ferro), dv(f.ferro)]
        ];
        $('tabella-ca').querySelector('tbody').innerHTML = righe.map(r =>
            `<tr><td>${r[0]}</td><td class="numerico">${testo(r[1])}</td><td class="numerico">${r[2]}</td></tr>`).join('');
        $('ca-porzione').textContent = `Porzione / portion: ${f.porzioneG} g`;
        const avv = $('ca-avviso');
        if (f.avvisi && f.avvisi.length) { avv.textContent = f.avvisi.join(' '); avv.classList.remove('nascosto'); }
        else avv.classList.add('nascosto');
        $('ca-verifica').classList.add('nascosto');
        $('pannello-ca').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function scaricaEtichettaCa(pulsante, formato) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/ricette/${id}/etichetta-ca`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formato, porzioneG: numN('r-unita') })
        });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const blob = await risposta.blob();
        const cd = risposta.headers.get('Content-Disposition') || '';
        const m = cd.match(/filename="?([^"]+)"?/);
        const nome = m ? m[1] : (formato === 'word' ? 'Valeur_Nutritive.rtf' : 'Valeur_Nutritive.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = nome; document.body.appendChild(a); a.click();
        a.remove(); URL.revokeObjectURL(url);
        avvisa('Pannello generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function verificaCa(pulsante) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta.', 'errore'); return; }
    const libera = occupa(pulsante, 'Verifico…');
    try {
        const res = await inviaJson(`/api/ricette/${id}/verifica-ca`, { porzioneG: numN('r-unita'), forza: false });
        $('ca-verifica-esito').textContent =
            (res.esito ? res.esito + ' · ' : '') + res.dataVerifica + (res.daArchivio ? ' (da archivio)' : '');
        $('ca-verifica-testo').textContent = res.testo || '';
        $('ca-verifica').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function verificaUsa(pulsante) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta.', 'errore'); return; }
    const libera = occupa(pulsante, 'Verifico…');
    try {
        const res = await inviaJson(`/api/ricette/${id}/verifica-usa`, {
            porzioneG: numN('r-unita'), zuccheriAggiuntiPer100g: numN('r-zucc-agg'), forza: false
        });
        $('usa-verifica-esito').textContent =
            (res.esito ? res.esito + ' · ' : '') + res.dataVerifica + (res.daArchivio ? ' (da archivio)' : '');
        $('usa-verifica-testo').textContent = res.testo || '';
        $('usa-verifica').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function scaricaEtichettaUsa(pulsante, formato) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/ricette/${id}/etichetta-usa`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formato, porzioneG: numN('r-unita'), zuccheriAggiuntiPer100g: numN('r-zucc-agg') })
        });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const blob = await risposta.blob();
        const cd = risposta.headers.get('Content-Disposition') || '';
        const m = cd.match(/filename="?([^"]+)"?/);
        const nome = m ? m[1] : (formato === 'word' ? 'Nutrition_Facts.rtf' : 'Nutrition_Facts.pdf');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = nome; document.body.appendChild(a); a.click();
        a.remove(); URL.revokeObjectURL(url);
        avvisa('Pannello generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function verifica(pulsante) {
    const id = $('r-id').value;
    if (!id) { avvisa('Salva prima la ricetta, poi esegui la verifica.', 'errore'); return; }
    const libera = occupa(pulsante, 'Verifico…');
    try {
        const res = await inviaJson(`/api/ricette/${id}/verifica`, { forza: false });
        $('verifica-esito').textContent =
            (res.esito ? res.esito + ' · ' : '') + res.dataVerifica + (res.daArchivio ? ' (da archivio)' : '');
        $('verifica-testo').textContent = res.testo || '';
        $('pannello-verifica').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

function debounce(fn, ms) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }
const ricalcolaDeb = debounce(ricalcola, 350);

// ── Avvio ──────────────────────────────────────────────────────
$('btn-nuova').addEventListener('click', apriNuova);
$('btn-annulla').addEventListener('click', () => $('pannello-form').classList.add('nascosto'));
$('btn-salva').addEventListener('click', (e) => salva(e.currentTarget));
$('btn-verifica').addEventListener('click', (e) => verifica(e.currentTarget));
$('btn-usa').addEventListener('click', (e) => nutritionFacts(e.currentTarget));
$('btn-ca').addEventListener('click', (e) => nutritionFactsCa(e.currentTarget));
$('btn-ca-verifica').addEventListener('click', (e) => verificaCa(e.currentTarget));
$('btn-ca-pdf').addEventListener('click', (e) => scaricaEtichettaCa(e.currentTarget, 'pdf'));
$('btn-ca-word').addEventListener('click', (e) => scaricaEtichettaCa(e.currentTarget, 'word'));
$('btn-usa-verifica').addEventListener('click', (e) => verificaUsa(e.currentTarget));
$('btn-usa-pdf').addEventListener('click', (e) => scaricaEtichettaUsa(e.currentTarget, 'pdf'));
$('btn-usa-word').addEventListener('click', (e) => scaricaEtichettaUsa(e.currentTarget, 'word'));
$('btn-pdf').addEventListener('click', (e) => generaRapporto(e.currentTarget, 'pdf'));
$('btn-word').addEventListener('click', (e) => generaRapporto(e.currentTarget, 'word'));
$('f-q').addEventListener('input', debounce(carica, 300));
$('i-cerca').addEventListener('input', debounce(cercaIngredienti, 300));
['r-unita', 'r-calo', 'r-peso-cotto'].forEach(id => $(id).addEventListener('input', ricalcolaDeb));

caricaClienti();
carica();
