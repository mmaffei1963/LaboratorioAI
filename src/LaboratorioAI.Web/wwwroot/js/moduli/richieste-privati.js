// Richieste di analisi dei clienti privati (05/11/2026): Ordine di Accettazione per i privati (Mod. Com. 05B) da firmare al banco.
// Controlli e prezzi dalle commesse di «Spettabile cliente» (MAF2) in ArcLab, in sola lettura; prezzi IVA inclusa.
// Tempi proposti: 10 giorni lavorativi, 20 se c'è Legionella o una prova esterna (il più lungo tra i controlli),
// modificabili a mano. Il giudizio di conformità chiede la norma, proposta tra quelle con limiti validati per la matrice.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const euro = (v) => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

let catalogo = { matrici: [], contenitori: [], controlli: [], supplementoGiudizio: 50, aliquotaIva: 22 };
let corrente = null;        // id della richiesta aperta (null = nuova)
let righe = [];             // [{ idControllo, idCommessa, controllo, prezzoIvaInclusa, tempi, metodi }]
let tempiToccati = false;   // l'utente ha cambiato i tempi a mano

// ── Avvio ──────────────────────────────────────────────────────
async function avvia() {
    try { catalogo = await chiamaApi('/api/richieste-privati/catalogo'); }
    catch (e) { avvisa('Cataloghi ArcLab non disponibili: ' + e.message, 'errore'); }
    $('lista-matrici').innerHTML = catalogo.matrici.map(m => `<option value="${testo(m.nome)}">`).join('');
    $('f-contenitore').innerHTML = '<option value="">—</option>' +
        catalogo.contenitori.map(c => `<option value="${c.id}">${testo(c.nome)}</option>`).join('');
    if (catalogo.controlli.length === 0)
        avvisa('Nessuna commessa in vigore trovata per il cliente MAF2 (Spettabile cliente).', 'errore');

    $('btn-nuova').addEventListener('click', () => apri(null));
    $('btn-annulla').addEventListener('click', chiudi);
    $('f-controllo').addEventListener('input', () => { clearTimeout(avvia.c); avvia.c = setTimeout(disegnaCatalogo, 200); });
    $('f-controllo').addEventListener('keydown', (e) => { if (e.key === 'Enter') e.preventDefault(); });
    $('f-giudizio').addEventListener('change', aggiornaGiudizio);
    $('f-supplemento').addEventListener('input', aggiornaTotale);
    $('f-tempi').addEventListener('input', () => { tempiToccati = true; aggiornaNotaTempi(); });
    $('f-descrizione').addEventListener('change', caricaNorme);
    $('btn-salva').addEventListener('click', salva);
    $('btn-modulo').addEventListener('click', () => { if (corrente) window.location.href = `/api/richieste-privati/modulo?id=${corrente}`; });
    $('btn-elimina').addEventListener('click', elimina);
    $('r-testo').addEventListener('input', () => { clearTimeout(avvia.t); avvia.t = setTimeout(caricaRegistro, 300); });
    await caricaRegistro();
}

// Metodi già letti per controllo (anche per cercare per nome della prova dopo aver aperto il dettaglio).
const dettagli = new Map();
const aperti = new Set();

async function dettaglioControllo(idControllo) {
    if (!dettagli.has(idControllo)) dettagli.set(idControllo, await chiamaApi(`/api/richieste-privati/controlli/${idControllo}`));
    return dettagli.get(idControllo);
}

const testoMetodo = (m) => testo(m.denominazione) + (m.metodo ? ` <span class="sottotesto">– ${testo(m.metodo)}</span>` : '')
    + (m.idLabEsterno ? ` <span class="sottotesto">(§ ${testo(m.labEsterno ?? 'laboratorio esterno')})</span>`
        : m.accreditato ? '' : ' <span class="sottotesto">(non accreditata)</span>');

function elencoProve(d) {
    return `<div class="sottotesto" style="margin:0.2rem 0 0.4rem">${d.metodi.length} prove · tempi proposti ${d.tempi} giorni lavorativi</div>
        <ul style="margin:0;padding-left:1.2rem;columns:2;column-gap:2rem">${d.metodi.map(m => `<li>${testoMetodo(m)}</li>`).join('')}</ul>`;
}

function disegnaCatalogo() {
    const q = $('f-controllo').value.trim().toLowerCase();
    const voci = catalogo.controlli.filter(c => !q
        || c.controllo.toLowerCase().includes(q) || String(c.idControllo) === q
        || (dettagli.get(c.idControllo)?.metodi ?? []).some(m => (m.denominazione || '').toLowerCase().includes(q)));
    const corpo = $('tabella-catalogo').querySelector('tbody');
    corpo.innerHTML = voci.map(c => {
        const scelto = righe.some(r => r.idCommessa === c.idCommessa);
        const aperto = aperti.has(c.idCommessa);
        return `<tr>
            <td style="white-space:normal"><strong>${testo(c.controllo)}</strong></td>
            <td class="numerico">${c.idControllo}</td><td class="numerico">${euro(c.prezzoIvaInclusa)}</td>
            <td style="white-space:nowrap">
                <button type="button" class="pulsante pulsante-tenue" data-prove="${c.idCommessa}">${aperto ? 'Nascondi prove' : 'Prove'}</button>
                <button type="button" class="pulsante${scelto ? ' pulsante-tenue' : ''}" data-aggiungi="${c.idCommessa}" ${scelto ? 'disabled' : ''}>${scelto ? 'Scelto' : 'Aggiungi'}</button>
            </td></tr>
            ${aperto ? `<tr><td colspan="4" style="white-space:normal;background:var(--sfondo-tenue, #f6f7f9)" data-dettaglio="${c.idCommessa}">Caricamento…</td></tr>` : ''}`;
    }).join('') || '<tr><td colspan="4" class="sottotesto">Nessun controllo trovato.</td></tr>';

    corpo.querySelectorAll('[data-prove]').forEach(b => b.addEventListener('click', () => {
        const id = Number(b.dataset.prove);
        aperti.has(id) ? aperti.delete(id) : aperti.add(id);
        disegnaCatalogo();
    }));
    corpo.querySelectorAll('[data-aggiungi]').forEach(b => b.addEventListener('click', () => aggiungiControllo(Number(b.dataset.aggiungi), b)));
    corpo.querySelectorAll('[data-dettaglio]').forEach(async cella => {
        const voce = catalogo.controlli.find(c => c.idCommessa === Number(cella.dataset.dettaglio));
        try { cella.innerHTML = elencoProve(await dettaglioControllo(voce.idControllo)); }
        catch (e) { cella.textContent = e.message; }
    });
}

// ── Registro ───────────────────────────────────────────────────
async function caricaRegistro() {
    const q = $('r-testo').value.trim();
    let elenco = [];
    try { elenco = await chiamaApi('/api/richieste-privati/elenco' + (q ? `?testo=${encodeURIComponent(q)}` : '')); }
    catch (e) { avvisa(e.message, 'errore'); }
    $('conteggio').textContent = `(${elenco.length})`;
    $('tabella-registro').querySelector('tbody').innerHTML = elenco.map(r => `<tr>
        <td>${testo(r.numeroTesto)}</td><td>${testo(r.nomeCognome)}</td><td>${testo(r.descrizioneCampione ?? '')}</td>
        <td class="numerico">${euro(r.totaleIvaInclusa)}</td><td>${r.giudizioRichiesto ? 'Sì' : '—'}</td>
        <td>${new Date(r.dataCreazione).toLocaleDateString('it-IT')}</td>
        <td><button type="button" class="pulsante pulsante-tenue" data-apri="${r.idRichiestaAnalisiPrivato}">Apri</button>
            <a class="pulsante pulsante-tenue" href="/api/richieste-privati/modulo?id=${r.idRichiestaAnalisiPrivato}">Word</a></td></tr>`).join('')
        || '<tr><td colspan="7" class="sottotesto">Nessuna richiesta.</td></tr>';
    $('tabella-registro').querySelectorAll('[data-apri]').forEach(b =>
        b.addEventListener('click', () => apri(Number(b.dataset.apri))));
}

// ── Modulo ─────────────────────────────────────────────────────
function svuota() {
    for (const id of ['f-nome', 'f-cf', 'f-indirizzo', 'f-telefono', 'f-email', 'f-descrizione', 'f-punto', 'f-dataprelievo',
        'f-temperatura', 'f-controllo', 'f-norma', 'f-note', 'f-tempi']) $(id).value = '';
    $('f-ritiro').value = '0'; $('f-pagamento').value = '1';
    $('f-contenitore').value = ''; $('f-contenitore-da').value = '';
    $('f-giudizio').checked = false;
    $('f-fuorilimite').value = '0'; $('f-consenso').value = '';
    $('f-supplemento').value = catalogo.supplementoGiudizio;
    righe = []; tempiToccati = false;
}

async function apri(id) {
    svuota();
    corrente = id;
    $('form-titolo').textContent = id ? 'Richiesta' : 'Nuova richiesta';
    $('form-stato').textContent = '';
    $('btn-modulo').classList.toggle('nascosto', !id);
    $('btn-elimina').classList.toggle('nascosto', !id);
    if (id) {
        try {
            const r = await chiamaApi(`/api/richieste-privati/${id}`);
            $('form-titolo').textContent = 'Richiesta ' + r.numeroTesto;
            $('f-nome').value = r.nomeCognome ?? ''; $('f-cf').value = r.codiceFiscale ?? '';
            $('f-indirizzo').value = r.indirizzo ?? ''; $('f-telefono').value = r.telefono ?? ''; $('f-email').value = r.email ?? '';
            $('f-ritiro').value = r.ritiroInLaboratorio ? '1' : '0'; $('f-pagamento').value = String(r.pagamento);
            $('f-descrizione').value = r.descrizioneCampione ?? ''; $('f-punto').value = r.puntoPrelievo ?? '';
            $('f-dataprelievo').value = r.dataOraPrelievo ? perInputLocale(r.dataOraPrelievo) : '';
            $('f-contenitore').value = r.idImballaggioArcLab ?? '';
            $('f-contenitore-da').value = r.contenitoreDelLaboratorio === true ? '1' : r.contenitoreDelLaboratorio === false ? '0' : '';
            $('f-temperatura').value = r.temperaturaRicevimento ?? '';
            $('f-giudizio').checked = !!r.giudizioRichiesto;
            $('f-fuorilimite').value = r.comunicazioneFuoriLimite ? '1' : '0';
            $('f-consenso').value = r.consensoComunicazioni === true ? '1' : r.consensoComunicazioni === false ? '0' : ''; $('f-norma').value = r.normaGiudizio ?? '';
            $('f-supplemento').value = r.supplementoGiudizio ?? catalogo.supplementoGiudizio;
            $('f-note').value = r.note ?? '';
            righe = r.righe.map(x => ({ idControllo: x.idControlloArcLab, idCommessa: x.idCommessaArcLab, controllo: x.descrizione,
                prezzoIvaInclusa: x.prezzoIvaInclusa, prezzoImponibile: x.prezzoImponibile, tempi: x.tempiGiorni, metodi: x.metodi }));
            $('f-tempi').value = r.tempiConsegnaGiorni;
            tempiToccati = r.tempiConsegnaGiorni !== tempiProposti();
        } catch (e) { avvisa(e.message, 'errore'); return; }
    }
    $('pannello-form').classList.remove('nascosto');
    aggiornaGiudizio();
    disegnaRighe();
    caricaNorme();
    $('pannello-form').scrollIntoView({ behavior: 'smooth' });
}

function chiudi() { $('pannello-form').classList.add('nascosto'); corrente = null; }

// La data salvata è in UTC: nel campo va l'ora locale.
function perInputLocale(iso) {
    const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z');
    const p = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

// ── Prove ──────────────────────────────────────────────────────
async function aggiungiControllo(idCommessa, pulsante) {
    const voce = catalogo.controlli.find(c => c.idCommessa === idCommessa);
    if (!voce || righe.some(r => r.idCommessa === idCommessa)) return;
    const fine = occupa(pulsante);
    try {
        const d = await dettaglioControllo(voce.idControllo);
        righe.push({ idControllo: voce.idControllo, idCommessa: voce.idCommessa, controllo: voce.controllo,
            prezzoIvaInclusa: voce.prezzoIvaInclusa, prezzoImponibile: voce.prezzo, tempi: d.tempi, metodi: d.metodi, aperto: true });
        disegnaRighe();
    } catch (e) { avvisa(e.message, 'errore'); fine(); }
}

function disegnaRighe() {
    $('tabella-righe').querySelector('tbody').innerHTML = righe.map((r, i) => `<tr>
        <td style="white-space:normal;min-width:16rem"><strong>${testo(r.controllo)}</strong> <span class="sottotesto">(ID ${r.idControllo})</span></td>
        <td style="white-space:normal"><details ${r.aperto ? 'open' : ''}><summary style="cursor:pointer">${(r.metodi || []).length} prove</summary>
            <ul style="margin:0.3rem 0 0;padding-left:1.2rem">${(r.metodi || []).map(m => `<li>${testoMetodo(m)}</li>`).join('')}</ul></details></td>
        <td class="numerico">${r.tempi}</td><td class="numerico">${euro(r.prezzoIvaInclusa)}</td>
        <td><button type="button" class="pulsante pulsante-tenue" data-togli="${i}">Togli</button></td></tr>`).join('')
        || '<tr><td colspan="5" class="sottotesto">Nessun controllo scelto.</td></tr>';
    $('tabella-righe').querySelectorAll('[data-togli]').forEach(b => b.addEventListener('click', () => {
        righe.splice(Number(b.dataset.togli), 1); disegnaRighe();
    }));
    if (!tempiToccati || !$('f-tempi').value) { $('f-tempi').value = tempiProposti(); tempiToccati = false; }
    aggiornaNotaTempi();
    aggiornaTotale();
    disegnaCatalogo();
}

const tempiProposti = () => righe.length ? Math.max(...righe.map(r => r.tempi)) : 10;

function aggiornaNotaTempi() {
    const p = tempiProposti();
    $('f-tempi-nota').textContent = Number($('f-tempi').value) === p
        ? `Tempi proposti: ${p} giorni lavorativi (10 per le prove interne, 20 se c'è Legionella o una prova esterna).`
        : `Tempi modificati a mano (proposti: ${p} giorni lavorativi).`;
}

function aggiornaGiudizio() {
    const si = $('f-giudizio').checked;
    $('riga-norma').classList.toggle('nascosto', !si);
    $('riga-supplemento').classList.toggle('nascosto', !si);
    aggiornaTotale();
}

// Totale IVA inclusa e scorporo (stessa regola del server): prove = prezzo commessa + IVA; supplemento già IVA inclusa.
function aggiornaTotale() {
    const aliquota = Number(catalogo.aliquotaIva || 22);
    const arrot = (v) => Math.round((v + Number.EPSILON) * 100) / 100;
    const catalogoPer = (r) => catalogo.controlli.find(c => c.idCommessa === r.idCommessa);
    let tot = righe.reduce((s, r) => s + Number(r.prezzoIvaInclusa || 0), 0);
    let imp = righe.reduce((s, r) => s + Number(r.prezzoImponibile ?? catalogoPer(r)?.prezzo ?? arrot(r.prezzoIvaInclusa / (1 + aliquota / 100))), 0);
    if ($('f-giudizio').checked) {
        const sup = Number($('f-supplemento').value || 0);
        tot += sup; imp += arrot(sup / (1 + aliquota / 100));
    }
    $('f-totale').textContent = euro(tot);
    $('f-imponibile').textContent = euro(imp);
    $('f-iva').textContent = euro(tot - imp);
}

async function caricaNorme() {
    const nome = $('f-descrizione').value.trim();
    const m = catalogo.matrici.find(x => (x.nome || '').toLowerCase() === nome.toLowerCase());
    let norme = [];
    if (nome) {
        try { norme = await chiamaApi(`/api/richieste-privati/norme?matrice=${encodeURIComponent(nome)}` + (m ? `&idMatrice=${m.id}` : '')); }
        catch { norme = []; }
    }
    $('lista-norme').innerHTML = norme.map(n => `<option value="${testo(n)}">`).join('');
}

// ── Salvataggio ────────────────────────────────────────────────
async function salva() {
    const nome = $('f-descrizione').value.trim();
    const matrice = catalogo.matrici.find(x => (x.nome || '').toLowerCase() === nome.toLowerCase());
    const cont = $('f-contenitore');
    const da = $('f-contenitore-da').value;
    const dataPrelievo = $('f-dataprelievo').value;
    const corpo = {
        idRichiestaAnalisiPrivato: corrente,
        nomeCognome: $('f-nome').value, codiceFiscale: $('f-cf').value, indirizzo: $('f-indirizzo').value,
        telefono: $('f-telefono').value, email: $('f-email').value,
        ritiroInLaboratorio: $('f-ritiro').value === '1', pagamento: Number($('f-pagamento').value),
        idMatriceArcLab: matrice ? matrice.id : null, descrizioneCampione: nome, puntoPrelievo: $('f-punto').value,
        dataOraPrelievo: dataPrelievo ? new Date(dataPrelievo).toISOString() : null,
        idImballaggioArcLab: cont.value ? Number(cont.value) : null,
        contenitore: cont.value ? cont.options[cont.selectedIndex].text : null,
        contenitoreDelLaboratorio: da === '' ? null : da === '1',
        temperaturaRicevimento: $('f-temperatura').value === '' ? null : Number($('f-temperatura').value),
        tempiConsegnaGiorni: $('f-tempi').value ? Number($('f-tempi').value) : null,
        giudizioRichiesto: $('f-giudizio').checked, normaGiudizio: $('f-norma').value,
        comunicazioneFuoriLimite: $('f-fuorilimite').value === '1',
        consensoComunicazioni: $('f-consenso').value === '' ? null : $('f-consenso').value === '1',
        supplementoGiudizio: $('f-supplemento').value === '' ? null : Number($('f-supplemento').value),
        note: $('f-note').value,
        righe: righe.map(r => ({ idControllo: r.idControllo, idCommessa: r.idCommessa }))
    };
    const fine = occupa($('btn-salva'), 'Salvataggio…');
    try {
        const e = await inviaJson('/api/richieste-privati/salva', corpo);
        corrente = e.idRichiestaAnalisiPrivato;
        $('form-titolo').textContent = 'Richiesta ' + e.numeroTesto;
        $('form-stato').textContent = 'Salvata.';
        $('btn-modulo').classList.remove('nascosto');
        $('btn-elimina').classList.remove('nascosto');
        avvisa(`Richiesta ${e.numeroTesto} salvata.`);
        await caricaRegistro();
    } catch (err) { avvisa(err.message, 'errore'); }
    finally { fine(); }
}

async function elimina() {
    if (!corrente || !confirm('Eliminare questa richiesta? Resta in archivio come eliminata.')) return;
    try {
        await inviaJson(`/api/richieste-privati/${corrente}/elimina`, {});
        chiudi(); await caricaRegistro();
    } catch (e) { avvisa(e.message, 'errore'); }
}

avvia();
