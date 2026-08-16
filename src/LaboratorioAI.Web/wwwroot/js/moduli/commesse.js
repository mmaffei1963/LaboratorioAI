/* ═══════════════════════════════════════════════════════════
   Commesse
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaStato,
         avvisa, occupa, popolaSelect } from '../comune.js';

const TIPO_LABORATORIO = 3;

let insediamentiTutti = [];
let mostraPrezzi = false;

// ── Filtri ─────────────────────────────────────────────────

async function caricaAnagrafiche() {
    try {
        const [tipi, clienti, insediamenti, controlli] = await Promise.all([
            chiamaApi('/api/commesse/tipi'),
            chiamaApi('/api/anagrafiche/clienti'),
            chiamaApi('/api/anagrafiche/insediamenti'),
            chiamaApi('/api/anagrafiche/controlli')
        ]);

        insediamentiTutti = insediamenti;

        const selTipo = document.getElementById('f-tipo');
        selTipo.innerHTML = tipi.map(t =>
            `<option value="${t.id}">${testo(t.nome)}</option>`).join('');
        selTipo.value = TIPO_LABORATORIO;      // il laboratorio è il caso più frequente

        popolaSelect(document.getElementById('f-cliente'),      clienti);
        popolaSelect(document.getElementById('f-insediamento'), insediamenti);
        popolaSelect(document.getElementById('f-controllo'),    controlli);

        adattaFiltri();
    } catch (errore) {
        avvisa(`Caricamento filtri non riuscito: ${errore.message}`, 'errore');
    }
}

/** Mostra i filtri pertinenti alla tipologia scelta. */
function adattaFiltri() {
    const diLaboratorio = Number(document.getElementById('f-tipo').value) === TIPO_LABORATORIO;
    document.getElementById('cella-controllo').classList.toggle('nascosto', !diLaboratorio);
    document.getElementById('cella-concluse').classList.toggle('nascosto', diLaboratorio);
}

function filtraInsediamenti() {
    const idCliente = Number(document.getElementById('f-cliente').value) || null;
    popolaSelect(document.getElementById('f-insediamento'),
        idCliente ? insediamentiTutti.filter(i => i.idCliente === idCliente) : insediamentiTutti);
}

function leggiCriteri() {
    const valore = id => document.getElementById(id).value || null;
    const numero = id => Number(document.getElementById(id).value) || null;

    return {
        idTipoCommessa:       numero('f-tipo'),
        idCliente:            numero('f-cliente'),
        idInsediamento:       numero('f-insediamento'),
        responsabileCommessa: valore('f-responsabile'),
        idControllo:          numero('f-controllo'),
        descrizioneIncarico:  valore('f-descrizione'),
        numeroContratto:      valore('f-contratto'),
        includiConcluse:      document.getElementById('f-concluse').value === 'true',
        limite: 300
    };
}

// ── Ricerca ────────────────────────────────────────────────

async function cerca(pulsante) {
    const libera = occupa(pulsante, 'Ricerca…');

    try {
        const esito = await inviaJson('/api/commesse/cerca', leggiCriteri());
        mostraPrezzi = esito.mostraPrezzi;
        disegna(esito);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function disegna(esito) {
    document.getElementById('pannello-risultati').classList.remove('nascosto');
    document.getElementById('pannello-esploso').classList.add('nascosto');

    document.getElementById('conteggio').textContent =
        esito.totale === 0 ? 'Nessuna commessa trovata'
                           : `${esito.totale} commesse`;

    const diLaboratorio = Number(document.getElementById('f-tipo').value) === TIPO_LABORATORIO;

    document.querySelector('#tabella-commesse thead').innerHTML = diLaboratorio
        ? `<tr><th>ID</th><th>Cliente</th><th>Insediamento</th><th>Controllo</th>
               ${mostraPrezzi ? '<th class="numerico">Importo</th>' : ''}
               <th>Data</th><th>Contratto</th><th>Responsabile</th><th></th></tr>`
        : `<tr><th>ID</th><th>Cliente</th><th>Insediamento</th><th>Incarico</th>
               ${mostraPrezzi ? '<th class="numerico">Importo</th>' : ''}
               <th>Data</th><th>Contratto</th><th class="numerico">Durata</th>
               <th>Responsabile</th><th></th></tr>`;

    const euro = v => v == null ? '—'
        : v.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' });

    document.querySelector('#tabella-commesse tbody').innerHTML = esito.commesse.map(c => `
        <tr>
            <td class="codice">${c.idCommessa}</td>
            <td><strong>${testo(c.ragioneSociale)}</strong></td>
            <td>${testo(c.nomeInsediamento) || '—'}</td>
            <td>${testo(diLaboratorio ? c.controllo : c.descrizioneIncarico) || '—'}</td>
            ${mostraPrezzi ? `<td class="numerico">${euro(c.prezzoControllo)}</td>` : ''}
            <td>${testo(c.dataCommessa) || '—'}</td>
            <td class="codice">${testo(c.numeroContratto) || '—'}</td>
            ${diLaboratorio ? '' : `<td class="numerico">${c.durata ?? '—'}</td>`}
            <td>${testo(c.responsabileCommessa) || '—'}</td>
            <td><button class="pulsante pulsante-tenue minuto"
                        data-id="${c.idCommessa}"
                        data-lab="${c.diLaboratorio}">
                    ${c.diLaboratorio ? 'Campioni' : 'Prestazioni'}
                </button></td>
        </tr>`).join('');

    document.querySelectorAll('#tabella-commesse .minuto').forEach(b =>
        b.addEventListener('click', () =>
            esplodi(Number(b.dataset.id), b.dataset.lab === 'true',
                    b.closest('tr').children[1].textContent)));
}

// ── Esplosione ─────────────────────────────────────────────

async function esplodi(idCommessa, diLaboratorio, cliente) {
    const pannello = document.getElementById('pannello-esploso');
    pannello.classList.remove('nascosto');
    pannello.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    document.getElementById('titolo-esploso').textContent =
        `${diLaboratorio ? 'Campioni' : 'Prestazioni'} — ${cliente}`;

    try {
        const percorso = diLaboratorio ? 'campioni' : 'prestazioni';
        const righe = await chiamaApi(`/api/commesse/${idCommessa}/${percorso}`);

        if (righe.length === 0) {
            document.querySelector('#tabella-esploso thead').innerHTML = '';
            document.querySelector('#tabella-esploso tbody').innerHTML =
                `<tr><td class="vuoto">Nessun elemento collegato a questa commessa.</td></tr>`;
            return;
        }

        diLaboratorio ? disegnaCampioni(righe) : disegnaPrestazioni(righe);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    }
}

function disegnaCampioni(righe) {
    document.querySelector('#tabella-esploso thead').innerHTML = `
        <tr><th>Stato</th><th>N° campione</th><th>Ricezione</th><th>Prelievo</th>
            <th>Denominazione</th><th>Matrice</th><th>Luogo</th>
            <th class="numerico">Prove</th></tr>`;

    document.querySelector('#tabella-esploso tbody').innerHTML = righe.map(c => `
        <tr>
            <td>${etichettaStato(c.stato)}</td>
            <td class="codice">${testo(c.numeroCampione)}</td>
            <td>${testo(c.dataRicezione) || '—'}</td>
            <td>${testo(c.dataPrelievo) || '—'}</td>
            <td>${testo(c.denominazioneCampione)}</td>
            <td>${testo(c.matrice) || '—'}</td>
            <td>${testo(c.luogoPrelievo) || '—'}</td>
            <td class="numerico">${c.numeroProve}</td>
        </tr>`).join('');
}

function disegnaPrestazioni(righe) {
    document.querySelector('#tabella-esploso thead').innerHTML = `
        <tr><th>Prestazione</th><th>Inizio</th><th>Fine</th><th>Scadenza</th>
            <th>Responsabile</th><th>Fatturata</th></tr>`;

    document.querySelector('#tabella-esploso tbody').innerHTML = righe.map(p => `
        <tr class="${p.inRitardo ? 'riga-attenzione' : ''}">
            <td><strong>${testo(p.descrizione) || '—'}</strong></td>
            <td>${testo(p.dataInizio) || '—'}</td>
            <td>${testo(p.dataFine) || '—'}</td>
            <td>${p.scadenzaCalcolata
                    ? `<span class="${p.inRitardo ? 'esito-ko' : ''}">${testo(p.scadenzaCalcolata)}</span>`
                    : '—'}</td>
            <td>${testo(p.responsabile) || '—'}</td>
            <td>${p.fatturata
                    ? `<span class="esito-ok">Sì</span>
                       <div class="sottotesto">${testo(p.dataFattura) || ''}</div>`
                    : '<span class="esito-neutro">No</span>'}</td>
        </tr>`).join('');
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    caricaAnagrafiche();

    document.getElementById('f-tipo').addEventListener('change', adattaFiltri);
    document.getElementById('f-cliente').addEventListener('change', filtraInsediamenti);
    document.getElementById('btn-cerca').addEventListener('click', e => cerca(e.currentTarget));

    document.getElementById('btn-azzera').addEventListener('click', () => {
        document.querySelectorAll('.griglia-filtri input').forEach(c => c.value = '');
        ['f-cliente', 'f-insediamento', 'f-controllo'].forEach(id =>
            document.getElementById(id).value = '');
        document.getElementById('f-concluse').value = 'false';
        filtraInsediamenti();
        document.getElementById('pannello-risultati').classList.add('nascosto');
        document.getElementById('pannello-esploso').classList.add('nascosto');
    });

    document.getElementById('chiudi-esploso').addEventListener('click', () =>
        document.getElementById('pannello-esploso').classList.add('nascosto'));
});
