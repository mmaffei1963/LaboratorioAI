// Ricerca fatture di vendita AZW per descrizione (10/11/2026).
import { chiamaApi, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const euro = (v) => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const num = (v) => Number(v || 0).toLocaleString('it-IT', { maximumFractionDigits: 3 });
const iso = (d) => d.toISOString().slice(0, 10);

function criteri() {
    const p = new URLSearchParams({ dal: $('f-dal').value, al: $('f-al').value, testo: $('f-testo').value.trim(),
        cliente: $('f-cliente').value.trim(), tipo: $('f-tipo').value });
    return p.toString();
}

async function cerca() {
    const fine = occupa($('btn-cerca'), 'Ricerca…');
    try {
        const r = await chiamaApi('/api/ricerca-fatture/cerca?' + criteri());
        $('aggiornato').textContent = r.aggiornatoAl ? '(' + new Date(r.aggiornatoAl).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) + ')' : '';
        $('riepilogo').innerHTML = `Totale imponibile € ${euro(r.totale)} <span class="sottotesto">— ${r.numeroVoci} voci, ${r.fatture} fatture` +
            (r.note ? `, ${r.note} note di credito` : '') + `, ${r.clienti} clienti${r.troncato ? ' (mostrate le prime 5000 righe; l\'Excel le contiene tutte)' : ''}</span>`;
        $('tabella').querySelector('tbody').innerHTML = r.voci.map(v => `<tr${v.tipoDocumento === 'CE' ? ' style="color:#b00"' : ''}>
            <td data-valore="${v.dataDocumento}">${new Date(v.dataDocumento).toLocaleDateString('it-IT')}</td>
            <td>${v.tipoDocumento === 'CE' ? 'NC ' : ''}${testo(v.numero)}</td>
            <td style="white-space:normal">${testo(v.ragioneSociale ?? '')} <span class="sottotesto">(${testo(v.codiceCliente)})</span></td>
            <td style="white-space:normal">${testo(v.descrizione)}</td>
            <td class="numerico">${num(v.quantita)}</td><td class="numerico">${euro(v.prezzo)}</td><td>${testo(v.sconto ?? '')}</td>
            <td class="numerico" data-valore="${v.importo}">${euro(v.importo)}</td>
            <td>${testo([v.centro, v.voce].filter(Boolean).join('/'))}</td></tr>`).join('')
            || '<tr><td colspan="9" class="sottotesto">Nessuna fattura trovata.</td></tr>';
        $('pannello').classList.remove('nascosto');
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

function avvia() {
    const oggi = new Date();
    $('f-dal').value = `${oggi.getFullYear()}-01-01`;
    $('f-al').value = iso(oggi);
    $('btn-cerca').addEventListener('click', cerca);
    $('btn-excel').addEventListener('click', () => { window.location.href = '/api/ricerca-fatture/excel?' + criteri(); });
    for (const id of ['f-testo', 'f-cliente']) $(id).addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); cerca(); } });
}

avvia();
