// Controllo pubblicazione certificati (16/11/2026): ArcLab (DataSped) contro portale clienti (Entries).
import { chiamaApi, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const giorno = (v) => v ? new Date(v).toLocaleDateString('it-IT') : '';
const oraria = (v) => v ? new Date(v).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '';
const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

const sezioni = {
    dateDiverse: {
        titolo: 'Date diverse', nota: 'spedizione, caricamento e firma non coincidono',
        intest: ['N. certificato', 'Spedizione', 'Caricamento', 'Firma', 'Gg caric.', 'Gg firma', 'Nota', 'Nome file'],
        riga: (d) => `<td>${testo(d.nCert)}</td><td>${giorno(d.dataSped)}</td><td>${oraria(d.dataUpload)}</td><td>${oraria(d.dataFirma)}</td>
            <td class="numerico ${d.giorniUpload ? 'allarme' : ''}">${d.giorniUpload ?? ''}</td>
            <td class="numerico ${d.giorniFirma ? 'allarme' : ''}">${d.giorniFirma ?? 'senza firma'}</td>
            <td class="${d.firmatoDopo ? 'allarme' : ''}">${d.firmatoDopo ? 'firmato dopo la pubblicazione' : ''}</td>
            <td style="white-space:normal">${testo(d.nomeFile ?? '')}</td>`
    },
    soloArcLab: {
        titolo: 'Solo ArcLab', nota: 'spediti ma non sul portale',
        intest: ['N. certificato', 'Spedizione', 'Tipo cert.', 'Sul portale', 'File trovato'],
        riga: (s) => `<td>${testo(s.nCert)}</td><td>${giorno(s.dataSped)}</td><td>${s.idTipoCertificato ?? ''}</td>
            <td class="allarme" style="white-space:normal">${testo(s.sulPortale)}</td><td style="white-space:normal">${testo(s.nomeFilePortale ?? '')}</td>`
    },
    soloPortale: {
        titolo: 'Solo portale', nota: 'caricati ma non spediti in ArcLab',
        intest: ['Tipo', 'Nome file', 'Caricamento', 'Firma', 'Certificato ArcLab', 'Spedizione ArcLab'],
        riga: (s) => `<td>${testo(s.tipo ?? '')}</td><td style="white-space:normal">${testo(s.nomeFile ?? '')}</td>
            <td>${oraria(s.dataUpload)}</td><td>${oraria(s.dataFirma)}</td><td>${testo(s.certificatoArcLab ?? '')}</td>
            <td class="${s.certificatoArcLab && !s.dataSpedArcLab ? 'allarme' : ''}">${s.certificatoArcLab ? (giorno(s.dataSpedArcLab) || 'non spedito') : ''}</td>`
    },
    doppioni: {
        titolo: 'Caricati più volte', nota: 'la versione inglese non conta come doppione',
        intest: ['N. certificato', 'Spedizione', 'Caricamenti', 'Di cui inglese', 'Primo', 'Ultimo'],
        riga: (d) => `<td>${testo(d.nCert)}</td><td>${giorno(d.dataSped)}</td><td class="numerico">${d.caricamenti}</td>
            <td class="numerico">${d.diCuiInglese}</td><td>${oraria(d.primoUpload)}</td><td>${oraria(d.ultimoUpload)}</td>`
    },
    // 17/11/2026: insediamenti con certificati spediti nel periodo che sul portale non possono riceverli
    // (senza date: pagina «Clienti e insediamenti»).
    anagrafiche: {
        titolo: 'Clienti e insediamenti', nota: 'non allineati con il portale: i certificati non arrivano',
        intest: ['Problema', 'Cliente ArcLab', 'Insediamento ArcLab', 'Certificati', 'Ultima spedizione', 'Sul portale', 'In ArcLab'],
        riga: (a) => `<td class="allarme" style="white-space:normal">${testo(a.descrizione)}</td>
            <td style="white-space:normal">${testo((a.ragioneSociale ?? '').trim())} <span class="sottotesto">(${a.idCliente}${a.codContabilita ? ' · ' + testo(a.codContabilita) : ''})</span></td>
            <td style="white-space:normal">${testo((a.insediamentoArcLab ?? '').trim())} <span class="sottotesto">(${a.idInsediamento})</span></td>
            <td class="numerico">${a.certificati}</td><td>${giorno(a.ultimaSpedizione)}</td>
            <td style="white-space:normal">${a.idInsediamentoPortale
                ? `${testo((a.insediamentoPortale ?? '').trim())} <span class="sottotesto">(insed. ${a.idInsediamentoPortale}${a.clientePortale ? ' · cliente ' + testo(a.clientePortale.trim()) + ' ' + a.idClientePortale : ''})</span>`
                : '<span class="sottotesto">non c\'è</span>'}</td>
            <td class="${a.statoArcLab === 'attivo' ? '' : 'sottotesto'}">${testo(a.statoArcLab)}</td>`
    }
};


let esito = null;
let attiva = 'dateDiverse';

function parametri() {
    return new URLSearchParams({ dal: $('f-dal').value, al: $('f-al').value, escludiEsterni: $('f-esterni').checked }).toString();
}

function mostra(chiave) {
    attiva = chiave;
    const s = sezioni[chiave];
    const righe = esito[chiave] ?? [];
    $('cifre').querySelectorAll('button').forEach(b => b.classList.toggle('attivo', b.dataset.k === chiave));
    $('titolo-elenco').innerHTML = `${testo(s.titolo)} <span class="sottotesto">— ${testo(s.nota)}</span>`;
    $('tabella').querySelector('thead').innerHTML = `<tr>${s.intest.map(h => `<th>${testo(h)}</th>`).join('')}</tr>`;
    $('tabella').querySelector('tbody').innerHTML = righe.length
        ? righe.map(r => `<tr>${s.riga(r)}</tr>`).join('')
        : `<tr><td colspan="${s.intest.length}" class="sottotesto">Nessuna riga: tutto in ordine.</td></tr>`;
}

async function cerca() {
    const fine = occupa($('btn-cerca'), 'Controllo…');
    try {
        esito = await chiamaApi('/api/controllo-pubblicazione/cerca?' + parametri());
        const firmeDopo = (esito.dateDiverse ?? []).filter(d => d.firmatoDopo).length;
        $('cifre').innerHTML = Object.entries(sezioni).map(([k, s]) => {
            const n = (esito[k] ?? []).length;
            const extra = k === 'dateDiverse' && firmeDopo ? `<div class="sottotesto">${firmeDopo} firmati dopo la pubblicazione</div>` : `<div class="sottotesto">${testo(s.nota)}</div>`;
            return `<button type="button" data-k="${k}"><div class="sottotesto">${testo(s.titolo)}</div><div class="valore">${n}</div>${extra}</button>`;
        }).join('');
        $('cifre').querySelectorAll('button').forEach(b => b.addEventListener('click', () => mostra(b.dataset.k)));
        $('pannello').classList.remove('nascosto');
        mostra(attiva);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { fine(); }
}

function avvia() {
    // Periodo predefinito: il mese scorso.
    const oggi = new Date();
    $('f-dal').value = iso(new Date(oggi.getFullYear(), oggi.getMonth() - 1, 1));
    $('f-al').value = iso(new Date(oggi.getFullYear(), oggi.getMonth(), 0));
    $('btn-cerca').addEventListener('click', cerca);
    $('btn-excel').addEventListener('click', () => { window.location.href = '/api/controllo-pubblicazione/excel?' + parametri(); });
}

avvia();
