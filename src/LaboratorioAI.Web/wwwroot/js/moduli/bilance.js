// Taratura bilance (EURAMET cg-18): anagrafica pesiere, esecuzione (eccentricità,
// ripetibilità, linearità), calcolo, rapporto, duplica, analisi AI, validazione.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const num = (v) => { const x = parseFloat(String(v).replace(',', '.')); return isNaN(x) ? 0 : x; };
const gg = (x, d = 5) => (x == null ? '—' : Number(x).toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d }));

let pesiere = [], clienti = [], strumenti = [], masse = [], eccLetture = [], ripLetture = [], linPunti = [], masseSel = [];

// Pesiera predefinita MARE.A (pre-compilazione, da verificare e salvare).
// [nominale, v.convenzionale, U(mg), u(mg), mpe(mg), classe, cert, dataCert, scadenza, centro]
const PESIERA_MAREA = [
    [0.1, 0.099987, 0.0032, 0.0016, 0.016, 'E2', '22/M/2551', '2022-04-08', '2027-04-08', 117],
    [1, 0.9999984, 0.0045, 0.00225, 0.03, 'E2', '22/M/2551', '2022-04-08', '2027-04-08', 117],
    [10, 10.0000109, 0.0075, 0.00375, 0.06, 'E2', '22/M/2551', '2022-04-08', '2027-04-08', 117],
    [20, 20.000023, 0.011, 0.0055, 0.08, 'E2', '22/M/2551', '2022-04-08', '2027-04-08', 117],
    [100, 100.000014, 0.051, 0.0255, 0.16, 'E2', '25/M/2900', '2025-08-06', '2030-08-06', 117],
    [1000, 999.9969, 1.5, 0.75, 0.5, 'F1', '25/M/2899', '2025-08-06', '2030-08-06', 117],
    [2000, 2000.0031, 2.9, 1.45, 1.0, 'F1', '25/M/2899', '2025-08-06', '2030-08-06', 117],
    [2000, 2000.0056, 2.9, 1.45, 1.0, 'F1', '25/M/2899', '2025-08-06', '2030-08-06', 117]
];

// ══ Caricamenti ══
async function caricaRegistro() {
    try {
        const r = await chiamaApi('/api/bilance/elenco');
        $('conteggio-registro').textContent = `${r.length} tarature`;
        $('tabella-registro').querySelector('tbody').innerHTML = r.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessuna taratura.</td></tr>'
            : r.map(t => `<tr class="riga-cliccabile" data-id="${t.idTaratura}"><td>${testo(t.numeroRapporto)}</td>
                <td>${testo(t.bilanciaIdentificazione)}</td><td class="numerico">${gg(t.portata, 2)}</td>
                <td>${testo(t.dataTaratura)}</td><td>${t.dataScadenza ? testo(t.dataScadenza) : '—'} ${t.scaduta ? '<strong style="color:#b3261e">scaduta</strong>' : ''}</td></tr>`).join('');
        $('tabella-registro').querySelectorAll('.riga-cliccabile').forEach(x => x.addEventListener('click', () => apri(Number(x.dataset.id))));
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaPesiere() {
    try {
        pesiere = await chiamaApi('/api/bilance/pesiere');
        $('tabella-pesiere').querySelector('tbody').innerHTML = pesiere.length === 0
            ? '<tr><td colspan="2" class="vuoto">Nessuna pesiera.</td></tr>'
            : pesiere.map(p => `<tr class="riga-cliccabile" data-id="${p.idPesiera}"><td>${testo(p.identificazione)}</td><td class="numerico">${p.masse}</td></tr>`).join('');
        $('tabella-pesiere').querySelectorAll('.riga-cliccabile').forEach(x => x.addEventListener('click', () => apriPesiera(Number(x.dataset.id))));
        $('t-pesiera').innerHTML = '<option value="">— seleziona —</option>' + pesiere.map(p => `<option value="${p.idPesiera}">${testo(p.identificazione)}</option>`).join('');
    } catch (e) { avvisa(e.message, 'errore'); }
}

async function caricaAnagrafiche() {
    try { clienti = await chiamaApi('/api/bilance/clienti'); } catch { clienti = []; }
    try { strumenti = await chiamaApi('/api/bilance/strumenti'); } catch { strumenti = []; }
    aggiornaDatalist();
}
function aggiornaDatalist() {
    const voci = $('b-origine').value === 'interno' ? strumenti : clienti;
    $('lista-b').innerHTML = voci.map(v => `<option value="${testo(v.nome)}">`).join('');
    // Il termometro si sceglie sempre tra le apparecchiature interne ArcLab (valore libero comunque consentito)
    const lt = $('lista-termo');
    if (lt) lt.innerHTML = strumenti.map(v => `<option value="${testo(v.nome)}">`).join('');
}

// ══ Pesiera ══
function nuovaPesiera() {
    $('p-id').value = ''; $('p-identificazione').value = ''; $('p-note').value = '';
    masse = [massaVuota()]; renderMasse();
    $('pesiera-titolo').textContent = 'Nuova pesiera';
    $('pannello-pesiera').classList.remove('nascosto'); $('pannello-taratura').classList.add('nascosto');
    $('p-identificazione').focus();
}
function massaVuota() { return { valoreNominale: 0, valoreConvenzionale: 0, incertezzaEstesa: 0, incertezzaTipo: 0, mpe: 0, classeOiml: '', certificatoLat: '', dataCertificato: '', dataScadenza: '', centroLat: '' }; }
function caricaPesieraPredefinita() {
    if (!$('p-identificazione').value.trim()) $('p-identificazione').value = 'Pesiera MARE.A';
    masse = PESIERA_MAREA.map(m => ({ valoreNominale: m[0], valoreConvenzionale: m[1], incertezzaEstesa: m[2], incertezzaTipo: m[3], mpe: m[4], classeOiml: m[5], certificatoLat: m[6], dataCertificato: m[7], dataScadenza: m[8], centroLat: m[9] }));
    renderMasse();
    avvisa('Pesiera MARE.A pre-compilata: controlla i valori (in particolare mpe/classe) e salva.', 'successo');
}
function renderMasse() {
    const campi = [['valoreNominale', 0.0001], ['valoreConvenzionale', 0.0000001], ['incertezzaEstesa', 0.0001], ['incertezzaTipo', 0.0001], ['mpe', 0.0001]];
    $('tabella-masse').querySelector('tbody').innerHTML = masse.map((m, i) => `<tr>
        ${campi.map(([c, s]) => `<td><input type="text" inputmode="decimal" data-i="${i}" data-c="${c}" value="${m[c]}" style="width:6rem" /></td>`).join('')}
        <td><input type="text" data-i="${i}" data-c="classeOiml" value="${testo(m.classeOiml)}" style="width:3.5rem" /></td>
        <td><input type="text" data-i="${i}" data-c="certificatoLat" value="${testo(m.certificatoLat)}" style="width:6rem" /></td>
        <td><input type="date" data-i="${i}" data-c="dataCertificato" value="${(m.dataCertificato || '').substring(0, 10)}" /></td>
        <td><input type="date" data-i="${i}" data-c="dataScadenza" value="${(m.dataScadenza || '').substring(0, 10)}" /></td>
        <td><input type="number" data-i="${i}" data-c="centroLat" value="${m.centroLat}" style="width:4rem" /></td>
        <td><button type="button" class="pulsante pulsante-tenue massa-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-masse').querySelectorAll('input').forEach(inp => inp.addEventListener('input', () => {
        const c = inp.dataset.c; const numerico = ['valoreNominale', 'valoreConvenzionale', 'incertezzaEstesa', 'incertezzaTipo', 'mpe', 'centroLat'].includes(c);
        masse[Number(inp.dataset.i)][c] = numerico ? num(inp.value) : inp.value;
    }));
    $('tabella-masse').querySelectorAll('.massa-rm').forEach(b => b.addEventListener('click', () => { masse.splice(Number(b.dataset.i), 1); renderMasse(); }));
}

// Carica le masse della pesiera selezionata (per la risoluzione dei carichi).
async function caricaMasseSelezionate() {
    const id = $('t-pesiera').value;
    masseSel = [];
    if (id) { try { const p = await chiamaApi('/api/bilance/pesiera/' + id); masseSel = p.masse ?? []; } catch { masseSel = []; } }
    linPunti.forEach(p => risolviPunto(p)); renderLin();
    risolviEccentricita();
}

// Trova la combinazione di masse che somma al nominale (subset-sum, min numero di masse).
function risolviCarico(nominale) {
    if (nominale === 0) return { valoreConvenzionale: 0, incertezzaMassa: 0, mpeMassa: 0, composizione: 'zero' };
    if (!masseSel.length) return null;
    const tol = 1e-6; let best = null;
    const n = masseSel.length;
    // ricerca ricorsiva delle combinazioni (masse ≤ 12 → esaustiva)
    function cerca(idx, somma, scelte) {
        if (Math.abs(somma - nominale) <= tol && scelte.length) {
            if (!best || scelte.length < best.length) best = [...scelte];
            return;
        }
        if (idx >= n || somma - nominale > tol) return;
        cerca(idx + 1, somma + masseSel[idx].valoreNominale, [...scelte, idx]);
        cerca(idx + 1, somma, scelte);
    }
    cerca(0, 0, []);
    if (!best) return null;
    const sel = best.map(i => masseSel[i]);
    return {
        valoreConvenzionale: sel.reduce((a, m) => a + m.valoreConvenzionale, 0),
        incertezzaMassa: sel.reduce((a, m) => a + (m.incertezzaTipo || 0), 0) / 1000, // mg → g
        mpeMassa: sel.reduce((a, m) => a + (m.mpe || 0), 0) / 1000,                    // mg → g
        composizione: sel.map(m => gg(m.valoreNominale, m.valoreNominale < 1 ? 3 : 0)).join(' + ')
    };
}
function risolviPunto(p) {
    const r = risolviCarico(p.caricoNominale);
    if (r) { p.valoreConvenzionale = r.valoreConvenzionale; p.incertezzaMassa = r.incertezzaMassa; p.mpeMassa = r.mpeMassa; p.composizione = r.composizione; }
    else { p.composizione = p.caricoNominale === 0 ? 'zero' : 'nessuna combinazione'; }
}
function risolviEccentricita() {
    const c = num($('e-carico').value);
    const r = risolviCarico(c);
    if (r) $('e-conv').value = r.valoreConvenzionale;
}
function aggiornaHints() {
    const portata = num($('t-portata').value);
    if (portata > 0) {
        $('hint-ecc').textContent = `Massa consigliata per l'eccentricità: circa ${gg(portata / 3, 0)} g (portata/3).`;
        $('hint-lin').textContent = `Carichi consigliati per la linearità: circa ${gg(portata * 0.25, 0)}, ${gg(portata * 0.5, 0)}, ${gg(portata * 0.75, 0)}, ${gg(portata, 0)} g (25/50/75/100% della portata), più lo zero.`;
    } else { $('hint-ecc').textContent = ''; $('hint-lin').textContent = ''; }
}
function autoCoeff() {
    const portata = num($('t-portata').value), uf = num($('t-uf').value);
    if (portata > 0 && uf > 0) {
        const nDiv = portata / uf;
        $('t-coeff').value = nDiv < 60000 ? 0.000008 : (nDiv > 300000 ? 0.000002 : 0.000005);
    }
    aggiornaHints();
}
async function apriPesiera(id) {
    try {
        const p = await chiamaApi('/api/bilance/pesiera/' + id);
        $('p-id').value = p.idPesiera; $('p-identificazione').value = p.identificazione ?? ''; $('p-note').value = p.note ?? '';
        masse = (p.masse ?? []).map(m => ({ ...m, dataCertificato: m.dataCertificato ?? '', dataScadenza: m.dataScadenza ?? '', certificatoLat: m.certificatoLat ?? '', centroLat: m.centroLat ?? '' }));
        if (masse.length === 0) masse = [massaVuota()];
        renderMasse();
        $('pesiera-titolo').textContent = 'Modifica pesiera';
        $('pannello-pesiera').classList.remove('nascosto');
        $('pannello-pesiera').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}
async function salvaPesiera(pulsante) {
    if (!$('p-identificazione').value.trim()) { avvisa('Identificazione obbligatoria.', 'errore'); return; }
    const corpo = { id: $('p-id').value ? Number($('p-id').value) : null, identificazione: $('p-identificazione').value, note: $('p-note').value, masse: masse };
    const libera = occupa(pulsante, 'Salvataggio…');
    try { const e = await inviaJson('/api/bilance/pesiera/salva', corpo); $('p-id').value = e.idPesiera; avvisa('Pesiera salvata.', 'successo'); $('pannello-pesiera').classList.add('nascosto'); await caricaPesiere(); }
    catch (err) { avvisa(err.message, 'errore'); } finally { libera(); }
}

// ══ Taratura ══
function nuova() {
    ['t-id', 't-numero', 't-scadenza', 'b-identificazione', 'b-modello', 'b-tipo', 'b-sda', 't-termometro', 't-portata', 't-campo', 'e-carico', 'e-conv'].forEach(id => $(id).value = '');
    $('t-pesiera').value = ''; $('t-istruzione').value = 'IO 4-05'; $('b-origine').value = 'interno';
    $('t-uf').value = '0.0001'; $('t-coeff').value = '0.0000015'; $('t-maxu').value = '0.02'; $('t-ttar').value = '21'; $('t-tuso').value = '22';
    $('t-stab').value = '2'; $('t-intervallo').value = '30'; $('t-maxvar').value = '0';
    $('t-data').value = new Date().toISOString().substring(0, 10);
    aggiornaDatalist();
    masseSel = [];
    eccLetture = [0, 0, 0, 0, 0]; ripLetture = [0, 0, 0, 0, 0]; linPunti = [puntoLin(0)];
    renderEcc(); renderRip(); renderLin(); aggiornaHints();
    $('taratura-numero').textContent = '';
    $('pannello-risultati-taratura').classList.add('nascosto'); $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura';
    $('pannello-taratura').classList.remove('nascosto'); $('pannello-pesiera').classList.add('nascosto');
    $('b-identificazione').focus();
}
function puntoLin(c) { return { caricoNominale: c, valoreConvenzionale: 0, lettura: 0, incertezzaMassa: 0, mpeMassa: 0 }; }

function renderEcc() {
    $('tabella-ecc').querySelector('tbody').innerHTML = eccLetture.map((v, i) =>
        `<tr><td>Pos. ${i + 1}</td><td class="numerico"><input type="text" inputmode="decimal" data-i="${i}" value="${v}" style="width:8rem" /></td>
        <td><button type="button" class="pulsante pulsante-tenue ecc-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-ecc').querySelectorAll('input').forEach(inp => inp.addEventListener('input', () => eccLetture[Number(inp.dataset.i)] = num(inp.value)));
    $('tabella-ecc').querySelectorAll('.ecc-rm').forEach(b => b.addEventListener('click', () => { eccLetture.splice(Number(b.dataset.i), 1); renderEcc(); }));
}
function renderRip() {
    $('tabella-rip').querySelector('tbody').innerHTML = ripLetture.map((v, i) =>
        `<tr><td>${i + 1}</td><td class="numerico"><input type="text" inputmode="decimal" data-i="${i}" value="${v}" style="width:8rem" /></td>
        <td><button type="button" class="pulsante pulsante-tenue rip-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-rip').querySelectorAll('input').forEach(inp => inp.addEventListener('input', () => ripLetture[Number(inp.dataset.i)] = num(inp.value)));
    $('tabella-rip').querySelectorAll('.rip-rm').forEach(b => b.addEventListener('click', () => { ripLetture.splice(Number(b.dataset.i), 1); renderRip(); }));
}
function renderLin() {
    $('tabella-lin').querySelector('tbody').innerHTML = linPunti.map((p, i) => `<tr>
        <td><input type="text" inputmode="decimal" data-i="${i}" data-c="caricoNominale" value="${p.caricoNominale}" style="width:7rem" /></td>
        <td class="numerico"><input type="text" inputmode="decimal" data-i="${i}" data-c="lettura" value="${p.lettura}" style="width:8rem" /></td>
        <td class="numerico">${gg(p.valoreConvenzionale, 4)}</td>
        <td class="numerico">${gg(p.incertezzaMassa, 6)}</td>
        <td class="numerico">${gg(p.mpeMassa, 6)}</td>
        <td class="sottotesto">${testo(p.composizione) || '—'}</td>
        <td><button type="button" class="pulsante pulsante-tenue lin-rm" data-i="${i}">×</button></td></tr>`).join('');
    $('tabella-lin').querySelectorAll('input').forEach(inp => inp.addEventListener('input', () => {
        const p = linPunti[Number(inp.dataset.i)];
        p[inp.dataset.c] = num(inp.value);
        if (inp.dataset.c === 'caricoNominale') { risolviPunto(p); renderLin(); }
    }));
    $('tabella-lin').querySelectorAll('.lin-rm').forEach(b => b.addEventListener('click', () => { linPunti.splice(Number(b.dataset.i), 1); renderLin(); }));
}

async function apri(id) {
    try {
        const t = await chiamaApi('/api/bilance/' + id);
        $('t-id').value = t.idTaratura; $('t-pesiera').value = t.idPesiera ?? '';
        $('b-origine').value = t.bilanciaInterna ? 'interno' : 'cliente'; aggiornaDatalist();
        $('b-identificazione').value = t.bilanciaIdentificazione ?? ''; $('b-modello').value = t.bilanciaModello ?? '';
        $('b-tipo').value = t.tipo ?? ''; $('b-sda').value = t.sda ?? ''; $('t-termometro').value = t.termometroUtilizzato ?? '';
        $('t-portata').value = t.portata; $('t-campo').value = t.campoTaratura || ''; $('t-uf').value = t.unitaFormato; $('t-coeff').value = t.coefficienteSensibilitaTermica;
        $('t-maxu').value = t.maxIncertezzaAmmessa; $('t-ttar').value = t.temperaturaTaratura; $('t-tuso').value = t.temperaturaUtilizzo;
        $('t-stab').value = t.tempoStabilizzazione ?? 2; $('t-intervallo').value = t.tempoIntervalloLettura ?? 30; $('t-maxvar').value = t.maxVariazioneTemperatura ?? 0;
        $('t-istruzione').value = t.riferimentoIstruzione ?? ''; $('t-responsabile').value = t.responsabile ?? '';
        $('t-data').value = (t.dataTaratura ?? '').substring(0, 10); $('t-scadenza').value = (t.dataScadenza ?? '').substring(0, 10);
        $('t-numero').value = t.numeroRapporto ?? '';
        const pr = t.prove || {};
        $('e-carico').value = pr.eccentricita?.caricoNominale ?? '';
        $('e-conv').value = pr.eccentricita?.valoreConvenzionale ?? '';
        eccLetture = pr.eccentricita?.letture ?? [0, 0, 0, 0, 0];
        ripLetture = pr.ripetibilita ?? [0, 0, 0, 0, 0];
        linPunti = (pr.linearita ?? [puntoLin(0)]).map(p => ({ ...p }));
        renderEcc(); renderRip(); renderLin(); aggiornaHints();
        await caricaMasseSelezionate();
        $('taratura-numero').textContent = t.numeroRapporto ? 'N° ' + t.numeroRapporto : '';
        $('pannello-risultati-taratura').classList.add('nascosto'); $('pannello-analisi-ai').classList.add('nascosto');
        $('taratura-titolo').textContent = 'Modifica taratura';
        $('pannello-taratura').classList.remove('nascosto'); $('pannello-pesiera').classList.add('nascosto');
        $('pannello-taratura').scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) { avvisa(e.message, 'errore'); }
}

function proveDto() {
    return {
        eccentricita: { caricoNominale: num($('e-carico').value), valoreConvenzionale: num($('e-conv').value), letture: eccLetture },
        ripetibilita: ripLetture,
        linearita: linPunti
    };
}
function corpoBase() {
    return {
        portata: num($('t-portata').value), unitaFormato: num($('t-uf').value),
        coefficienteSensibilitaTermica: num($('t-coeff').value), maxIncertezzaAmmessa: num($('t-maxu').value),
        temperaturaTaratura: num($('t-ttar').value), temperaturaUtilizzo: num($('t-tuso').value), prove: proveDto()
    };
}

function renderRisultato(r) {
    $('risultati-sintesi').innerHTML =
        `<p>Eccentricità: scostamento max ${gg(r.eccentricitaScostamentoMax)} g — <strong>${r.eccentricitaConforme ? 'conforme' : 'NON conforme'}</strong>. Ripetibilità: ${gg(r.ripetibilitaScartoTipo, 6)} g.</p>`
        + `<p>Incertezza estesa massima <strong>${gg(r.incertezzaEstesaMax)} g</strong> — <strong style="color:${r.conforme ? '#1a7f37' : '#b3261e'}">${r.conforme ? 'CONFORME' : 'NON CONFORME'}</strong></p>`;
    $('tabella-risultati').querySelector('tbody').innerHTML = r.punti.map(p =>
        `<tr><td>${gg(p.caricoNominale, 1)}</td><td class="numerico">${gg(p.errore)}</td><td class="numerico">${gg(p.incertezzaEstesa)}</td><td class="numerico">${gg(p.incertezzaEstesaUso)}</td></tr>`).join('');
    $('pannello-risultati-taratura').classList.remove('nascosto');
}
async function calcola(pulsante) {
    const libera = occupa(pulsante, 'Calcolo…');
    try { renderRisultato(await inviaJson('/api/bilance/calcola', corpoBase())); }
    catch (e) { avvisa(e.message, 'errore'); } finally { libera(); }
}

function risolviBilancia() {
    const interno = $('b-origine').value === 'interno'; const nome = $('b-identificazione').value.trim();
    const lista = interno ? strumenti : clienti; const trovato = lista.find(v => (v.nome || '').toLowerCase() === nome.toLowerCase());
    return { interna: interno, idStrumentoArcLab: interno && trovato ? trovato.id : null, idClienteArcLab: !interno && trovato ? trovato.id : null };
}
async function salva(pulsante) {
    if (!$('b-identificazione').value.trim()) { avvisa('Identificazione bilancia obbligatoria.', 'errore'); return; }
    const b = risolviBilancia();
    const corpo = Object.assign(corpoBase(), {
        id: $('t-id').value ? Number($('t-id').value) : null,
        idPesiera: $('t-pesiera').value ? Number($('t-pesiera').value) : null,
        bilanciaIdentificazione: $('b-identificazione').value, bilanciaModello: $('b-modello').value,
        tipo: $('b-tipo').value || null, sda: $('b-sda').value || null, termometroUtilizzato: $('t-termometro').value || null,
        campoTaratura: num($('t-campo').value), tempoStabilizzazione: num($('t-stab').value),
        tempoIntervalloLettura: num($('t-intervallo').value), maxVariazioneTemperatura: num($('t-maxvar').value),
        bilanciaInterna: b.interna, bilanciaIdStrumentoArcLab: b.idStrumentoArcLab, bilanciaIdClienteArcLab: b.idClienteArcLab,
        numeroRapporto: $('t-numero').value || null, dataTaratura: $('t-data').value, dataScadenza: $('t-scadenza').value || null,
        riferimentoIstruzione: $('t-istruzione').value, responsabile: $('t-responsabile').value || null
    });
    const libera = occupa(pulsante, 'Salvataggio…');
    try { const e = await inviaJson('/api/bilance/salva', corpo); $('t-id').value = e.idTaratura; $('t-numero').value = e.numeroRapporto; $('taratura-numero').textContent = 'N° ' + e.numeroRapporto; avvisa('Taratura salvata.', 'successo'); await caricaRegistro(); }
    catch (err) { avvisa(err.message, 'errore'); } finally { libera(); }
}

function duplica() {
    const copia = $('t-copia-letture')?.checked ?? true;
    const stato = { pesiera: $('t-pesiera').value, portata: $('t-portata').value, uf: $('t-uf').value, coeff: $('t-coeff').value, maxu: $('t-maxu').value, ttar: $('t-ttar').value, tuso: $('t-tuso').value, ist: $('t-istruzione').value, data: $('t-data').value, scad: $('t-scadenza').value, resp: $('t-responsabile').value, ecarico: $('e-carico').value, econv: $('e-conv').value };
    const ecc = copia ? [...eccLetture] : eccLetture.map(() => 0);
    const rip = copia ? [...ripLetture] : ripLetture.map(() => 0);
    const lin = linPunti.map(p => copia ? { ...p } : { ...p, lettura: 0 });
    ['t-id', 't-numero', 'b-identificazione', 'b-modello', 'b-tipo', 'b-sda'].forEach(id => $(id).value = '');
    $('t-pesiera').value = stato.pesiera; $('t-portata').value = stato.portata; $('t-uf').value = stato.uf; $('t-coeff').value = stato.coeff; $('t-maxu').value = stato.maxu; $('t-ttar').value = stato.ttar; $('t-tuso').value = stato.tuso; $('t-istruzione').value = stato.ist; $('t-data').value = stato.data; $('t-scadenza').value = stato.scad; $('t-responsabile').value = stato.resp; $('e-carico').value = stato.ecarico; $('e-conv').value = stato.econv;
    $('b-origine').value = 'interno'; aggiornaDatalist();
    eccLetture = ecc; ripLetture = rip; linPunti = lin; renderEcc(); renderRip(); renderLin();
    $('taratura-numero').textContent = ''; $('pannello-risultati-taratura').classList.add('nascosto'); $('pannello-analisi-ai').classList.add('nascosto');
    $('taratura-titolo').textContent = 'Nuova taratura (duplicata)'; $('b-identificazione').focus();
    avvisa('Duplicata: inserisci la nuova bilancia.', 'successo');
}

async function scaricaRapporto(pulsante, formato) {
    const id = $('t-id').value; if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Genero…');
    try {
        const risposta = await fetch(`/api/bilance/${id}/rapporto`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ formato, responsabile: $('t-responsabile').value }) });
        if (!risposta.ok) { let m = `Errore ${risposta.status}`; try { const c = await risposta.json(); if (c.messaggio) m = c.messaggio; } catch { } throw new Error(m); }
        const blob = await risposta.blob(); const cd = risposta.headers.get('Content-Disposition') || ''; const mm = cd.match(/filename="?([^"]+)"?/);
        const nome = mm ? mm[1] : (formato === 'word' ? 'Rapporto_Bilancia.rtf' : 'Rapporto_Bilancia.pdf');
        const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = nome; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
        avvisa('Rapporto generato.', 'successo');
    } catch (e) { avvisa(e.message, 'errore'); } finally { libera(); }
}

async function analizza(pulsante) {
    const id = $('t-id').value; if (!id) { avvisa('Salva prima la taratura.', 'errore'); return; }
    const libera = occupa(pulsante, 'Analisi…');
    try {
        const r = await inviaJson(`/api/bilance/${id}/analizza`, {});
        $('pannello-analisi-ai').classList.remove('nascosto');
        $('analisi-ai-badge').textContent = (r.daArchivio ? 'da archivio · ' : '') + (r.dataAnalisi || '');
        const an = r.anomalie || [];
        $('analisi-ai-anomalie').innerHTML = an.length === 0 ? '<p class="sottotesto">Nessuna anomalia dai controlli automatici.</p>'
            : '<ul>' + an.map(a => `<li><strong style="color:${a.livello === 'Errore' ? '#b3261e' : '#9a6700'}">${testo(a.livello)}</strong>${a.punto ? ' — ' + testo(a.punto) : ''}: ${testo(a.messaggio)}</li>`).join('') + '</ul>';
        $('analisi-ai-testo').textContent = r.testo || '';
    } catch (e) { avvisa(e.message, 'errore'); } finally { libera(); }
}

// ══ Validazione ══
async function caricaStatoValidazione() {
    const el = $('validazione-stato'); if (!el) return;
    try {
        const s = await chiamaApi('/api/bilance/validazione/stato'); const av = $('validazione-avviso');
        if (!s.eseguitaMai) { el.textContent = 'mai eseguita'; av.textContent = 'La validazione del calcolo non è mai stata eseguita.'; av.classList.remove('nascosto'); }
        else { el.textContent = `ultima: ${s.data} — ${s.validato ? 'VALIDATO' : 'NON VALIDATO'}`;
            if (s.scaduta) { av.textContent = `Ultima validazione del ${s.data}: oltre ${s.mesiValidita} mesi fa.`; av.classList.remove('nascosto'); }
            else if (!s.validato) { av.textContent = 'L\'ultima validazione risulta NON VALIDATA.'; av.classList.remove('nascosto'); }
            else av.classList.add('nascosto'); }
    } catch { /* non bloccante */ }
}
async function caricaRegistroValidazioni() {
    const tb = $('tabella-validazioni'); if (!tb) return;
    try {
        const r = await chiamaApi('/api/bilance/validazione/elenco');
        tb.querySelector('tbody').innerHTML = r.length === 0 ? '<tr><td colspan="7" class="vuoto">Nessuna validazione.</td></tr>'
            : r.map(v => `<tr><td>${testo(v.data)}</td><td>${v.validato ? '<strong style="color:#1a7f37">VALIDATO</strong>' : '<strong style="color:#b3261e">NON VALIDATO</strong>'}</td>
                <td class="numerico">${v.numeroOk}/${v.numeroKo}</td><td>${testo(v.versioneSet)}</td><td>${testo(v.versioneApplicazione) || '—'}</td><td>${testo(v.operatore) || '—'}</td>
                <td><a href="/api/bilance/validazione/${v.idEsito}/rapporto" target="_blank" rel="noopener">PDF</a></td></tr>`).join('');
    } catch { /* non bloccante */ }
}
async function valida(pulsante) {
    const libera = occupa(pulsante, 'Validazione…');
    try {
        const e = await inviaJson('/api/bilance/validazione/esegui', {});
        $('validazione-esito').classList.remove('nascosto');
        $('validazione-badge').innerHTML = e.validato ? `<p><strong style="color:#1a7f37">VALIDATO</strong> — ${e.numeroOk} confronti OK (${testo(e.data)})</p>` : `<p><strong style="color:#b3261e">NON VALIDATO</strong> — ${e.numeroKo} KO su ${e.numeroOk + e.numeroKo}</p>`;
        avvisa(e.validato ? 'Validazione superata.' : 'Validazione NON superata.', e.validato ? 'successo' : 'errore');
        await caricaStatoValidazione(); await caricaRegistroValidazioni();
    } catch (err) { avvisa(err.message, 'errore'); } finally { libera(); }
}

// ══ Avvio ══
$('b-origine')?.addEventListener('change', aggiornaDatalist);
$('t-pesiera')?.addEventListener('change', caricaMasseSelezionate);
$('t-portata')?.addEventListener('input', autoCoeff);
$('t-uf')?.addEventListener('input', autoCoeff);
$('e-carico')?.addEventListener('input', risolviEccentricita);
$('btn-nuova-pesiera')?.addEventListener('click', nuovaPesiera);
$('btn-massa-riga')?.addEventListener('click', () => { masse.push(massaVuota()); renderMasse(); });
$('btn-pesiera-predefinita')?.addEventListener('click', caricaPesieraPredefinita);
$('btn-pesiera-annulla')?.addEventListener('click', () => $('pannello-pesiera').classList.add('nascosto'));
$('btn-pesiera-salva')?.addEventListener('click', (e) => salvaPesiera(e.currentTarget));
$('btn-nuova-taratura')?.addEventListener('click', nuova);
$('btn-taratura-annulla')?.addEventListener('click', () => $('pannello-taratura').classList.add('nascosto'));
$('btn-ecc-riga')?.addEventListener('click', () => { eccLetture.push(0); renderEcc(); });
$('btn-rip-riga')?.addEventListener('click', () => { ripLetture.push(0); renderRip(); });
$('btn-lin-riga')?.addEventListener('click', () => { linPunti.push(puntoLin(0)); renderLin(); });
$('btn-calcola')?.addEventListener('click', (e) => calcola(e.currentTarget));
$('btn-taratura-salva')?.addEventListener('click', (e) => salva(e.currentTarget));
$('btn-taratura-duplica')?.addEventListener('click', duplica);
$('btn-taratura-pdf')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'pdf'));
$('btn-taratura-word')?.addEventListener('click', (e) => scaricaRapporto(e.currentTarget, 'word'));
$('btn-taratura-analizza')?.addEventListener('click', (e) => analizza(e.currentTarget));
$('btn-valida')?.addEventListener('click', (e) => valida(e.currentTarget));

caricaAnagrafiche();
caricaPesiere();
caricaRegistro();
caricaStatoValidazione();
caricaRegistroValidazioni();
