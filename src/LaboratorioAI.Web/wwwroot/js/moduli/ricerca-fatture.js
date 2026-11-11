// Ricerca documenti AZW per descrizione (10/11/2026; costi, aree, commesse, zone e riquadri dall'11/11/2026).
import { chiamaApi, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const euro = (v) => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const num = (v) => v == null ? '' : Number(v).toLocaleString('it-IT', { maximumFractionDigits: 3 });
const iso = (d) => d.toISOString().slice(0, 10);

function criteri() {
    const aree = [...$('f-aree').selectedOptions].map(o => o.value).join(',');
    return new URLSearchParams({
        dal: $('f-dal').value, al: $('f-al').value, testo: $('f-testo').value.trim(), cliente: $('f-cliente').value.trim(),
        tipo: $('f-tipo').value, aree, commessa: $('f-commessa').value, zona: $('f-zona').value
    }).toString();
}

function cifra(titolo, valore, nota, negativo = false) {
    return `<div><div class="sottotesto">${testo(titolo)}</div><div class="valore${negativo ? ' neg' : ''}">€ ${euro(valore)}</div>
        <div class="sottotesto">${nota}</div></div>`;
}

function mostraRiepilogo(r, tipo) {
    const s = r.riepilogo;
    const conRicavi = tipo !== 'costi';
    const conCosti = tipo === 'costi' || tipo === 'tutti';
    const pezzi = [];
    if (conRicavi) pezzi.push(cifra('Ricavi netti', s.ricavi, `${s.fatture} fatture` + (s.noteCredito ? `, ${s.noteCredito} note di credito` : ''), s.ricavi < 0));
    if (conCosti) pezzi.push(cifra('Costi', s.costi, `${s.documentiCosto} registrazioni`));
    if (conRicavi && conCosti) pezzi.push(cifra('Differenza ricavi − costi', s.differenza, 'sui dati filtrati', s.differenza < 0));
    const cf = [conRicavi ? `${s.clienti} clienti` : '', conCosti ? `${s.fornitori} fornitori` : ''].filter(Boolean).join(' · ');
    pezzi.push(`<div><div class="sottotesto">Righe trovate</div><div class="valore">${s.righe.toLocaleString('it-IT')}</div><div class="sottotesto">${cf}</div></div>`);
    $('cifre').innerHTML = pezzi.join('');
    $('tabella-aree').querySelector('tbody').innerHTML = s.perArea.map(a =>
        `<tr><td>${testo(a.area)}</td><td class="numerico">${conRicavi ? euro(a.ricavi) : ''}</td><td class="numerico">${conCosti ? euro(a.costi) : ''}</td></tr>`).join('')
        || '<tr><td colspan="3" class="sottotesto">Nessun dato.</td></tr>';
    $('pannello-cifre').classList.remove('nascosto');
}

function tipoDoc(v) {
    if (v.costo) return 'Costo';
    return v.tipoDocumento === 'CE' ? 'NC' : '';
}

async function cerca() {
    const fine = occupa($('btn-cerca'), 'Ricerca…');
    try {
        const tipo = $('f-tipo').value;
        const r = await chiamaApi('/api/ricerca-fatture/cerca?' + criteri());
        mostraRiepilogo(r, tipo);
        $('riepilogo').innerHTML = `Elenco <span class="sottotesto">${r.troncato
            ? `— mostrate le prime ${r.massimo.toLocaleString('it-IT')} righe di ${r.riepilogo.righe.toLocaleString('it-IT')}: i riquadri e l'Excel le contano tutte`
            : ''}</span>`;
        $('tabella').querySelector('tbody').innerHTML = r.righe.map(v => `<tr class="${v.costo ? 'costo' : ''}${v.importo < 0 ? ' neg' : ''}">
            <td data-valore="${v.data}">${new Date(v.data).toLocaleDateString('it-IT')}</td>
            <td>${testo(tipoDoc(v))} ${testo(v.numero)}</td>
            <td style="white-space:normal">${testo(v.controparte ?? '')}${v.codiceControparte ? ` <span class="sottotesto">(${testo(v.codiceControparte)})</span>` : ''}</td>
            <td style="white-space:normal">${testo(v.descrizione)}</td>
            <td class="numerico">${num(v.quantita)}</td><td class="numerico">${v.prezzo == null ? '' : euro(v.prezzo)}</td><td>${testo(v.sconto ?? '')}</td>
            <td class="numerico importo" data-valore="${v.importo}">${euro(v.importo)}</td>
            <td>${testo(v.area)} <span class="sottotesto">${testo([v.centro, v.voce].filter(Boolean).join('/'))}</span></td>
            <td>${testo(v.commessa ?? '')}</td><td>${testo(v.zona ?? '')}</td></tr>`).join('')
            || '<tr><td colspan="11" class="sottotesto">Nessun documento trovato.</td></tr>';
        $('pannello').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

async function caricaOpzioni() {
    try {
        const o = await chiamaApi('/api/ricerca-fatture/opzioni');
        $('aggiornato').textContent = o.aggiornatoAl ? '(' + new Date(o.aggiornatoAl).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) + ')' : '';
        $('f-aree').innerHTML = o.aree.map(a => `<option value="${testo(a.codice)}">${testo(a.nome)}</option>`).join('');
        $('f-commessa').insertAdjacentHTML('beforeend', o.commesse.map(c => `<option value="${testo(c.codice)}">${testo(c.nome)}</option>`).join(''));
        $('f-zona').insertAdjacentHTML('beforeend', o.zone.map(z => `<option value="${testo(z.codice)}">${testo(z.nome)}</option>`).join(''));
    } catch (e) { avvisa(e.message, 'errore'); }
}

let attesa;
function suggerisci() {
    clearTimeout(attesa);
    const q = $('f-cliente').value.trim();
    if (q.length < 2) return;
    attesa = setTimeout(async () => {
        try {
            const elenco = await chiamaApi('/api/ricerca-fatture/controparti?q=' + encodeURIComponent(q));
            $('elenco-controparti').innerHTML = elenco.map(c =>
                `<option value="${testo(c.codice)}">${testo(c.nome ?? '')} — ${testo(c.tipo)}</option>`).join('');
        } catch { /* i suggerimenti sono facoltativi */ }
    }, 250);
}

function azzera() {
    $('f-testo').value = ''; $('f-cliente').value = ''; $('f-tipo').value = 'ricavi';
    $('f-commessa').value = ''; $('f-zona').value = '';
    for (const o of $('f-aree').options) o.selected = false;
}

function avvia() {
    const oggi = new Date();
    $('f-dal').value = `${oggi.getFullYear()}-01-01`;
    $('f-al').value = iso(oggi);
    $('btn-cerca').addEventListener('click', cerca);
    $('btn-pulisci').addEventListener('click', azzera);
    $('btn-excel').addEventListener('click', () => { window.location.href = '/api/ricerca-fatture/excel?' + criteri(); });
    $('f-cliente').addEventListener('input', suggerisci);
    for (const id of ['f-testo', 'f-cliente']) $(id).addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); cerca(); } });
    caricaOpzioni();
}

avvia();
