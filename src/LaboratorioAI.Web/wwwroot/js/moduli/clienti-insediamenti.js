// Clienti e insediamenti (17/11/2026): anagrafiche ArcLab non allineate con il portale clienti (sola lettura).
import { chiamaApi, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const giorno = (v) => v ? new Date(v).toLocaleDateString('it-IT') : '';
const anagrafiche = {
    intest: ['Problema', 'Cliente ArcLab', 'Insediamento ArcLab', 'Campioni', 'Ultimo ricevimento', 'Certificati spediti', 'Ultima spedizione', 'Sul portale', 'In ArcLab'],
    riga: (a) => `<td class="allarme" style="white-space:normal">${testo(a.descrizione)}</td>
            <td style="white-space:normal">${testo((a.ragioneSociale ?? '').trim())} <span class="sottotesto">(${a.idCliente}${a.codContabilita ? ' · ' + testo(a.codContabilita) : ''})</span></td>
            <td style="white-space:normal">${testo((a.insediamentoArcLab ?? '').trim())} <span class="sottotesto">(${a.idInsediamento})</span></td>
            <td class="numerico">${a.campioni}</td><td>${giorno(a.ultimoRicevimento)}</td>
            <td class="numerico">${a.certificati}</td><td>${giorno(a.ultimaSpedizione)}</td>
            <td style="white-space:normal">${a.idInsediamentoPortale
                ? `${testo((a.insediamentoPortale ?? '').trim())} <span class="sottotesto">(insed. ${a.idInsediamentoPortale}${a.clientePortale ? ' · cliente ' + testo(a.clientePortale.trim()) + ' ' + a.idClientePortale : ''})</span>`
                : '<span class="sottotesto">non c\'è</span>'}</td>
            <td class="${a.statoArcLab === 'attivo' ? '' : 'sottotesto'}">${testo(a.statoArcLab)}</td>`
};

let ultime = [];

// 17/11/2026: la spunta nasconde i disattivati sul portale (insediamento o cliente obsoleto) senza rifare la ricerca.
function mostra() {
    const nascondi = $('f-nascondi-disattivati').checked;
    // 17/11/2026: anche i non attivi in ArcLab (cliente non attivo o insediamento non in uso).
    const nascondiArcLab = $('f-nascondi-arclab').checked;
    const righe = ultime.filter(a => !(nascondi && a.disattivatoSulPortale)
        && !(nascondiArcLab && !(a.clienteAttivoArcLab && a.insediamentoInUsoArcLab)));
    const nascoste = ultime.length - righe.length;
    const conta = {};
    for (const a of righe) conta[a.descrizione] = (conta[a.descrizione] ?? 0) + 1;
    $('anag-riepilogo').innerHTML = (righe.length
        ? `<strong>${righe.length}</strong> insediamenti da sistemare sul portale: ` +
          Object.entries(conta).map(([d, n]) => `${testo(d)} <strong>${n}</strong>`).join(' · ')
        : 'Nessun problema: tutti gli insediamenti con campioni nel periodo sono attivi e collegati sul portale.')
        + (nascoste ? ` <span class="sottotesto">(${nascoste} nascosti dai filtri)</span>` : '');
    $('anag-tabella').querySelector('thead').innerHTML = `<tr>${anagrafiche.intest.map(h => `<th>${testo(h)}</th>`).join('')}</tr>`;
    $('anag-tabella').querySelector('tbody').innerHTML = righe.map(a => `<tr>${anagrafiche.riga(a)}</tr>`).join('')
        || `<tr><td colspan="${anagrafiche.intest.length}" class="sottotesto">Nessuna riga: tutto in ordine.</td></tr>`;
    $('anag-pannello').classList.remove('nascosto');
}

async function controllaAnagrafiche() {
    const fine = occupa($('btn-anagrafiche'), 'Controllo…');
    try {
        const r = await chiamaApi('/api/clienti-insediamenti/cerca?mesi=' + encodeURIComponent($('f-mesi').value));
        ultime = r.righe;
        mostra();
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

function avvia() {
    $('btn-anagrafiche').addEventListener('click', controllaAnagrafiche);
    $('btn-anag-excel').addEventListener('click', () => { window.location.href = '/api/clienti-insediamenti/excel?mesi=' + encodeURIComponent($('f-mesi').value)
        + '&nascondiDisattivati=' + $('f-nascondi-disattivati').checked + '&nascondiNonAttiviArcLab=' + $('f-nascondi-arclab').checked; });
    $('f-nascondi-disattivati').addEventListener('change', mostra);
    $('f-nascondi-arclab').addEventListener('change', mostra);
    controllaAnagrafiche();
}

avvia();
