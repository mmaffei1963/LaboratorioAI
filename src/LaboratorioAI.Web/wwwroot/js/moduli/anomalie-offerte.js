// Anomalie offerte (17/11/2026): stesso controllo dello stesso cliente su insediamenti diversi (casi B e D).
import { chiamaApi, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const euro = (v) => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';

async function carica() {
    const fine = occupa($('btn-aggiorna'), 'Caricamento…');
    try {
        const elenco = await chiamaApi('/api/anomalie-offerte');
        const b = elenco.filter(a => a.caso === 'B').length, d = elenco.length - b;
        const clienti = new Set(elenco.map(a => a.idCliente)).size;
        $('anom-riepilogo').innerHTML = elenco.length
            ? `<strong>${elenco.length}</strong> anomalie su <strong>${clienti}</strong> clienti: B (stesso prezzo, descrizioni diverse) <strong>${b}</strong> · D (stessa descrizione, prezzi diversi) <strong>${d}</strong>`
            : 'Nessuna anomalia: le commesse dei clienti sono coerenti.';
        $('anom-tabella').querySelector('tbody').innerHTML = elenco.map(a => a.commesse.map((c, i) => `<tr class="${i === 0 ? 'gruppo' : ''}">
            ${i === 0 ? `<td class="caso" rowspan="${a.commesse.length}" title="${testo(a.descrizione)}">${a.caso}</td>
                <td rowspan="${a.commesse.length}" style="white-space:normal">${testo(a.cliente ?? '')} <span class="sottotesto">(${testo(a.codContabilita ?? a.idCliente)})</span>
                    <div class="sottotesto">${testo(a.descrizione)}</div></td>
                <td rowspan="${a.commesse.length}" style="white-space:normal">${a.idControllo} <span class="sottotesto">${testo(a.controllo ?? '')}</span></td>` : ''}
            <td style="white-space:normal">${testo(c.insediamento ?? '')}</td>
            <td>${c.idCommessa}${c.nonFatturare ? ' <span class="sottotesto">(non fatturare)</span>' : ''}</td>
            <td style="white-space:normal">${testo(c.incarico)}</td>
            <td class="numerico">${euro(c.prezzo)}</td>
            <td>${testo(c.dataCommessa ?? '')}</td></tr>`).join('')).join('')
            || '<tr><td colspan="8" class="sottotesto">Nessuna anomalia.</td></tr>';
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

$('btn-aggiorna').addEventListener('click', carica);
$('btn-excel').addEventListener('click', () => { window.location.href = '/api/anomalie-offerte/excel'; });
carica();
