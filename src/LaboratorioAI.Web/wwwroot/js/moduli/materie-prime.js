// Archivio materie prime nutrizionali: elenco, ricerca, inserimento/modifica
// con validazione della somma dei componenti (~100 g/100 g).

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const num = (id) => { const v = parseFloat($(id).value.replace(',', '.')); return isNaN(v) ? 0 : v; };
const numN = (id) => { const t = $(id).value.trim(); if (t === '') return null; const v = parseFloat(t.replace(',', '.')); return isNaN(v) ? null : v; };
const g = (x) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { maximumFractionDigits: 2 }));

let elenco = [];

// ── Elenco ─────────────────────────────────────────────────────
async function carica() {
    const q = $('f-q').value.trim();
    const tipo = $('f-tipo').value;
    const fonte = $('f-fonte').value;
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    if (tipo) params.set('tipo', tipo);
    if (fonte) params.set('fonte', fonte);
    try {
        elenco = await chiamaApi('/api/materie-prime/elenco?' + params.toString());
        render();
    } catch (e) { avvisa(e.message, 'errore'); }
}

function render() {
    $('conteggio').textContent = `${elenco.length} materie prime`;
    const corpo = document.querySelector('#tabella-elenco tbody');
    if (elenco.length === 0) {
        corpo.innerHTML = '<tr><td colspan="9" class="vuoto">Nessuna materia prima.</td></tr>';
        return;
    }
    corpo.innerHTML = elenco.map(m => {
        const c = m.corrente;
        const marca = [m.marca, m.fornitore].filter(Boolean).map(testo).join(' / ');
        return `<tr class="riga-cliccabile" data-id="${m.idMateriaPrima}">
            <td><strong>${testo(m.denominazione)}</strong></td>
            <td>${testo(m.tipoTesto)}</td>
            <td>${marca || '—'}</td>
            <td class="numerico">${c ? g(c.grassi) : '—'}</td>
            <td class="numerico">${c ? g(c.carboidrati) : '—'}</td>
            <td class="numerico">${c ? g(c.proteine) : '—'}</td>
            <td class="numerico">${c ? g(c.sale) : '—'}</td>
            <td>${c && c.fonte ? testo(c.fonte) : '—'}</td>
            <td><button type="button" class="pulsante pulsante-tenue btn-elimina"
                data-id="${m.idMateriaPrima}" data-nome="${testo(m.denominazione)}">Elimina</button></td>
        </tr>`;
    }).join('');
    corpo.querySelectorAll('.riga-cliccabile').forEach(r =>
        r.addEventListener('click', () => apri(Number(r.dataset.id))));
    corpo.querySelectorAll('.btn-elimina').forEach(b =>
        b.addEventListener('click', (e) => { e.stopPropagation(); elimina(b); }));
}

// ── Fonti (tendina) ────────────────────────────────────────────
async function popolaFonti(idSelezionato) {
    try {
        const fonti = await chiamaApi('/api/materie-prime/fonti');
        const sel = $('m-fonte-id');
        sel.innerHTML = '<option value="">— nuova fonte —</option>';
        for (const f of fonti) {
            const o = document.createElement('option');
            o.value = f.idFonte;
            o.textContent = f.descrizione;
            sel.appendChild(o);
        }
        if (idSelezionato) sel.value = String(idSelezionato);
    } catch { /* non bloccante */ }
}

// ── Form ───────────────────────────────────────────────────────
function pulisciForm() {
    ['m-id','m-denominazione','m-marca','m-fornitore','m-categoria','m-codice',
     'v-grassi','v-saturi','v-carbo','v-zuccheri','v-proteine','v-sale','v-fibre','v-acqua',
     'v-kj','v-kcal','v-sodio','v-trans','v-mono','v-poli','v-colesterolo','v-vitd',
     'v-calcio','v-ferro','v-potassio','m-fonte-descr','m-fonte-rif','m-rif-doc'].forEach(id => $(id).value = '');
    $('m-tipo').value = '1';
    $('m-fonte-tipo').value = '1';
    $('m-fonte-id').value = '';
    $('m-priorita').value = '1';
    $('form-avviso').classList.add('nascosto');
}

async function apriNuova() {
    pulisciForm();
    await popolaFonti();
    $('form-titolo').textContent = 'Nuova materia prima';
    $('pannello-form').classList.remove('nascosto');
    aggiornaSomma();
    $('m-denominazione').focus();
}

async function apri(id) {
    try {
        const m = await chiamaApi('/api/materie-prime/' + id);
        pulisciForm();
        $('m-id').value = m.idMateriaPrima;
        $('m-denominazione').value = m.denominazione ?? '';
        $('m-tipo').value = String(m.tipo ?? 1);
        $('m-marca').value = m.marca ?? '';
        $('m-fornitore').value = m.fornitore ?? '';
        $('m-categoria').value = m.categoria ?? '';
        $('m-codice').value = m.codiceInterno ?? '';
        const v = m.valori;
        if (v) {
            $('v-grassi').value = v.grassi; $('v-saturi').value = v.grassiSaturi;
            $('v-carbo').value = v.carboidrati; $('v-zuccheri').value = v.zuccheri;
            $('v-proteine').value = v.proteine; $('v-sale').value = v.sale;
            $('v-fibre').value = v.fibre; $('v-acqua').value = v.acqua;
            $('v-kj').value = v.energiaKj ?? ''; $('v-kcal').value = v.energiaKcal ?? '';
            $('v-sodio').value = v.sodio ?? ''; $('v-trans').value = v.grassiTrans ?? '';
            $('v-mono').value = v.grassiMonoinsaturi ?? ''; $('v-poli').value = v.grassiPolinsaturi ?? '';
            $('v-colesterolo').value = v.colesterolo ?? ''; $('v-vitd').value = v.vitaminaD ?? '';
            $('v-calcio').value = v.calcio ?? ''; $('v-ferro').value = v.ferro ?? ''; $('v-potassio').value = v.potassio ?? '';
            $('m-priorita').value = v.priorita ?? 1;
            $('m-rif-doc').value = v.riferimentoDocumento ?? '';
        }
        await popolaFonti(v && v.fonte ? v.fonte.idFonte : null);
        if (v && v.fonte) $('m-fonte-tipo').value = String(v.fonte.tipo ?? 1);
        $('form-titolo').textContent = 'Modifica materia prima';
        $('pannello-form').classList.remove('nascosto');
        aggiornaSomma();
        $('pannello-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function importaCrea(ev) {
    const input = ev.currentTarget;
    const f = input.files && input.files[0];
    if (!f) return;
    const btn = $('btn-importa-crea');
    const libera = occupa(btn, 'Import in corso…');
    try {
        const fd = new FormData();
        fd.append('file', f);
        const risposta = await fetch('/api/materie-prime/importa-crea', { method: 'POST', body: fd });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const esito = await risposta.json();
        avvisa(`CREA importato: ${esito.lette} righe (${esito.nuove} nuove, ${esito.aggiornate} aggiornate).`, 'successo');
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); input.value = ''; }
}

async function importaUsda(pulsante) {
    const cartella = prompt('Percorso della cartella con i CSV USDA (contiene food.csv e food_nutrient.csv):');
    if (!cartella) return;
    const libera = occupa(pulsante, 'Import USDA… (può richiedere un minuto)');
    try {
        const esito = await inviaJson('/api/materie-prime/importa-usda', { cartella });
        avvisa(`USDA importato: ${esito.alimenti} alimenti (${esito.nuove} nuovi, ${esito.aggiornate} aggiornati).`, 'successo');
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function importaCnf(pulsante) {
    const cartella = prompt('Percorso della cartella con i CSV CNF (contiene Food_Name.csv e Nutrient_Amount.csv):');
    if (!cartella) return;
    const libera = occupa(pulsante, 'Import CNF… (può richiedere un minuto)');
    try {
        const esito = await inviaJson('/api/materie-prime/importa-cnf', { cartella });
        avvisa(`CNF importato: ${esito.alimenti} alimenti (${esito.nuove} nuovi, ${esito.aggiornate} aggiornati).`, 'successo');
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

async function importaScheda(ev) {
    const input = ev.currentTarget;
    const f = input.files && input.files[0];
    if (!f) return;
    const btn = $('btn-scheda-pdf');
    const libera = occupa(btn, 'Leggo la scheda…');
    try {
        const fd = new FormData();
        fd.append('file', f);
        const risposta = await fetch('/api/materie-prime/estrai-scheda', { method: 'POST', body: fd });
        if (!risposta.ok) {
            let msg = `Errore ${risposta.status}`;
            try { const c = await risposta.json(); if (c.messaggio) msg = c.messaggio; } catch {}
            throw new Error(msg);
        }
        const e = await risposta.json();
        await apriNuova();
        const v = (x) => (x == null ? '' : x);
        $('m-denominazione').value = v(e.denominazione);
        $('m-tipo').value = '1'; // scheda tecnica → specifica
        $('v-grassi').value = v(e.grassi);
        $('v-saturi').value = v(e.grassiSaturi);
        $('v-carbo').value = v(e.carboidrati);
        $('v-zuccheri').value = v(e.zuccheri);
        $('v-proteine').value = v(e.proteine);
        $('v-sale').value = v(e.sale);
        $('v-fibre').value = v(e.fibre);
        $('v-acqua').value = v(e.acqua);
        $('v-kj').value = v(e.energiaKj);
        $('v-kcal').value = v(e.energiaKcal);
        $('m-fonte-tipo').value = '1'; // Scheda tecnica
        $('m-fonte-descr').value = 'Scheda tecnica' + (e.denominazione ? ' — ' + e.denominazione : '');
        $('m-rif-doc').value = f.name;
        aggiornaSomma();

        const avvisi = [];
        if (e.base && e.base !== '100g')
            avvisi.push(`Attenzione: i valori sembrano riferiti a «${e.base}», non a 100 g. Verifica e converti.`);
        if (e.note) avvisi.push(e.note);
        avvisi.push('Valori proposti dall\'AI dalla scheda PDF: controllali prima di salvare.');
        const avv = $('form-avviso');
        avv.textContent = avvisi.join(' ');
        avv.classList.remove('nascosto');
        avvisa('Scheda letta: rivedi i valori.', 'successo');
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { libera(); input.value = ''; }
}

async function elimina(pulsante) {
    const id = Number(pulsante.dataset.id);
    const nome = pulsante.dataset.nome || 'questa materia prima';
    if (!confirm(`Eliminare «${nome}»? Resta in archivio ma non comparirà più nell'elenco.`)) return;
    const libera = occupa(pulsante, 'Elimino…');
    try {
        await inviaJson(`/api/materie-prime/${id}/elimina`, {});
        avvisa('Materia prima eliminata.', 'successo');
        if (Number($('m-id').value) === id) $('pannello-form').classList.add('nascosto');
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); libera(); }
}

function aggiornaSomma() {
    const somma = num('v-grassi') + num('v-carbo') + num('v-proteine') + num('v-sale') + num('v-fibre') + num('v-acqua');
    const el = $('form-somma');
    if (somma === 0) { el.textContent = ''; return; }
    const scostamento = Math.abs(somma - 100);
    el.textContent = `Somma componenti: ${somma.toLocaleString('it-IT', { maximumFractionDigits: 1 })} g/100 g`;
    el.style.color = scostamento > 5 ? '#b3261e' : '#1a7f37';
}

async function salva(pulsante) {
    const denom = $('m-denominazione').value.trim();
    if (!denom) { avvisa('La denominazione è obbligatoria.', 'errore'); return; }

    const idFonte = $('m-fonte-id').value ? Number($('m-fonte-id').value) : null;
    const corpo = {
        id: $('m-id').value ? Number($('m-id').value) : null,
        denominazione: denom,
        tipo: Number($('m-tipo').value),
        marca: $('m-marca').value, fornitore: $('m-fornitore').value,
        categoria: $('m-categoria').value, codiceInterno: $('m-codice').value,
        note: null,
        priorita: Number($('m-priorita').value) || 1,
        riferimentoDocumento: $('m-rif-doc').value,
        valori: {
            grassi: num('v-grassi'), grassiSaturi: num('v-saturi'),
            carboidrati: num('v-carbo'), zuccheri: num('v-zuccheri'),
            proteine: num('v-proteine'), sale: num('v-sale'),
            fibre: num('v-fibre'), acqua: num('v-acqua'),
            energiaKj: numN('v-kj'), energiaKcal: numN('v-kcal'),
            sodio: numN('v-sodio'), grassiTrans: numN('v-trans'),
            grassiMonoinsaturi: numN('v-mono'), grassiPolinsaturi: numN('v-poli'),
            colesterolo: numN('v-colesterolo'), vitaminaD: numN('v-vitd'),
            calcio: numN('v-calcio'), ferro: numN('v-ferro'), potassio: numN('v-potassio')
        },
        fonte: {
            idFonte: idFonte,
            tipo: Number($('m-fonte-tipo').value),
            descrizione: $('m-fonte-descr').value,
            riferimento: $('m-fonte-rif').value,
            url: null
        }
    };

    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const esito = await inviaJson('/api/materie-prime/salva', corpo);
        // Da qui in poi si aggiorna sempre lo stesso record (niente duplicati).
        $('m-id').value = esito.idMateriaPrima;
        $('form-titolo').textContent = 'Modifica materia prima';
        const avv = $('form-avviso');
        if (esito.avvisi && esito.avvisi.length) {
            avv.textContent = esito.avvisi.join(' ');
            avv.classList.remove('nascosto');
            avvisa('Salvato con avvisi.', 'info');
        } else {
            avv.classList.add('nascosto');
            avvisa('Materia prima salvata.', 'successo');
            $('pannello-form').classList.add('nascosto');
        }
        await carica();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}

// ── Avvio ──────────────────────────────────────────────────────
$('btn-importa-crea').addEventListener('click', () => $('file-crea').click());
$('file-crea').addEventListener('change', importaCrea);
$('btn-importa-usda').addEventListener('click', (e) => importaUsda(e.currentTarget));
$('btn-importa-cnf').addEventListener('click', (e) => importaCnf(e.currentTarget));
$('btn-scheda-pdf').addEventListener('click', () => $('file-scheda').click());
$('file-scheda').addEventListener('change', importaScheda);
$('btn-nuova').addEventListener('click', apriNuova);
$('btn-annulla').addEventListener('click', () => $('pannello-form').classList.add('nascosto'));
$('btn-salva').addEventListener('click', (e) => salva(e.currentTarget));
$('f-q').addEventListener('input', debounce(carica, 300));
$('f-tipo').addEventListener('change', carica);
$('f-fonte').addEventListener('change', carica);
document.querySelectorAll('.v-macro').forEach(i => i.addEventListener('input', aggiornaSomma));

$('btn-acqua-diff').addEventListener('click', () => {
    const resto = num('v-grassi') + num('v-carbo') + num('v-proteine') + num('v-sale') + num('v-fibre');
    const acqua = Math.max(0, Math.round((100 - resto) * 100) / 100);
    $('v-acqua').value = acqua;
    aggiornaSomma();
});

function debounce(fn, ms) {
    let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
}

carica();
