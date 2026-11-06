/* ═══════════════════════════════════════════════════════════
   Metodi ↔ Controlli
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, avvisa, occupa, popolaSelect } from '../comune.js';

let metodiDisponibili = [];
let metodiScelti = [];
let controlliTrovati = [];

// ── Suggerimenti ───────────────────────────────────────────

function suggerisci() {
    const query = document.getElementById('ricerca-metodo').value.trim().toLowerCase();
    const box = document.getElementById('suggerimenti');

    if (query.length < 2) { box.classList.add('nascosto'); return; }

    const scelti = new Set(metodiScelti.map(m => m.idMetodo));
    const trovati = metodiDisponibili
        .filter(m => !scelti.has(m.idMetodo) && m.testoRicerca.toLowerCase().includes(query))
        .slice(0, 25);

    if (trovati.length === 0) { box.classList.add('nascosto'); return; }

    box.innerHTML = trovati.map(m => `
        <div class="suggerimento" data-id="${m.idMetodo}">
            <strong>${testo(m.riferimentoNormativo) || '(senza riferimento)'}</strong>
            ${m.accreditato ? '<span class="accreditato" title="Accreditato MARE.A">A</span>' : ''}
            ${m.laboratorioEsterno
                ? `<span class="etichetta etichetta-outsourcing">${testo(m.laboratorioEsterno)}</span>` : ''}
            <div class="sottotesto">${testo(m.nomeParametro) || testo(m.denominazioneRiga)}</div>
            ${dettagliMetodo(m)}
        </div>`).join('');

    box.classList.remove('nascosto');

    box.querySelectorAll('.suggerimento').forEach(nodo =>
        nodo.addEventListener('mousedown', e => {
            e.preventDefault();
            scegli(Number(nodo.dataset.id));
        }));
}

// 05/11/2026 (Zatterin): metodi con la stessa norma e lo stesso parametro (es. prove su superfici) sembravano uguali.
// Sotto si mostrano ID del metodo, denominazione della riga (se diversa dal parametro), sigla, unità di misura e dettagli.
function dettagliMetodo(m) {
    const parti = [`ID ${m.idMetodo}`];
    if (m.denominazioneRiga && m.denominazioneRiga !== m.nomeParametro) parti.push(testo(m.denominazioneRiga));
    if (m.acronimo) parti.push(testo(m.acronimo));
    if (m.rifMetodo && m.rifMetodo !== m.riferimentoNormativo) parti.push(testo(m.rifMetodo));
    if (m.unitaMisura) parti.push(testo(m.unitaMisura));
    if (m.dettagliMetodo) parti.push(testo(m.dettagliMetodo.length > 90 ? m.dettagliMetodo.slice(0, 90) + '…' : m.dettagliMetodo));
    return `<div class="sottotesto" style="opacity:.85">${parti.join(' · ')}</div>`;
}

function scegli(idMetodo) {
    const metodo = metodiDisponibili.find(m => m.idMetodo === idMetodo);
    if (!metodo || metodiScelti.some(m => m.idMetodo === idMetodo)) return;

    metodiScelti.push(metodo);
    document.getElementById('ricerca-metodo').value = '';
    document.getElementById('suggerimenti').classList.add('nascosto');
    disegnaScelti();
}

function rimuovi(idMetodo) {
    metodiScelti = metodiScelti.filter(m => m.idMetodo !== idMetodo);
    disegnaScelti();
}

function disegnaScelti() {
    const contenitore = document.getElementById('contenitore-scelte');
    const campo = document.getElementById('ricerca-metodo');

    contenitore.querySelectorAll('.scelta').forEach(n => n.remove());

    metodiScelti.forEach(m => {
        const nodo = document.createElement('span');
        nodo.className = 'scelta';
        nodo.innerHTML = `${testo(m.riferimentoNormativo || m.nomeParametro)}${m.idMetodo ? ` <small>(ID ${m.idMetodo})</small>` : ''}
                          <button type="button" aria-label="Rimuovi">&times;</button>`;
        nodo.querySelector('button').addEventListener('click', () => rimuovi(m.idMetodo));
        contenitore.insertBefore(nodo, campo);
    });
}

// ── Metodi presi da un controllo (03/11/2026) ──────────────
// Si sceglie un controllo noto (es. con le prove A B C): i suoi metodi entrano tra quelli scelti, si aggiungono
// le altre prove (D) e «Cerca controlli» con «tutti i metodi» trova i controlli che li contengono tutti.

function idDaTestoControllo(testoScelto) {
    const t = (testoScelto || '').trim();
    const m = t.match(/\(ID\s*(\d+)\)\s*$/i);
    if (m) return Number(m[1]);
    return /^\d+$/.test(t) ? Number(t) : null;
}

async function aggiungiMetodiDelControllo(pulsante) {
    const id = idDaTestoControllo(document.getElementById('controllo-base').value);
    if (!id) { avvisa('Scegli il controllo dall\'elenco o scrivi il suo ID.', 'info'); return; }
    const libera = occupa(pulsante, 'Carico…');
    try {
        const c = await chiamaApi(`/api/metodi/controllo/${id}`);
        let aggiunti = 0;
        for (const m of c.metodi) {
            if (metodiScelti.some(x => x.idMetodo === m.idMetodo)) continue;
            // Il metodo può non essere tra quelli attivi della ricerca: si usa quello del controllo.
            metodiScelti.push(metodiDisponibili.find(x => x.idMetodo === m.idMetodo) ?? m);
            aggiunti++;
        }
        disegnaScelti();
        document.querySelector('input[name="logica"][value="Tutti"]').checked = true;
        document.getElementById('controllo-base').value = '';
        avvisa(`Aggiunti ${aggiunti} metodi di «${c.controllo}» (ID ${c.idControllo}). Aggiungi le altre prove e premi «Cerca controlli».`, 'successo');
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

// ── Ricerca controlli ──────────────────────────────────────

async function cercaControlli(pulsante) {
    if (metodiScelti.length === 0) {
        avvisa('Selezionare almeno un metodo.', 'info');
        return;
    }

    const logica = document.querySelector('input[name="logica"]:checked').value;
    const libera = occupa(pulsante, 'Ricerca…');

    try {
        controlliTrovati = await inviaJson('/api/metodi/cerca-controlli', {
            idMetodi: metodiScelti.map(m => m.idMetodo),
            logica
        });
        disegnaControlli();
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function disegnaControlli() {
    apriEsito();

    const filtro = document.getElementById('filtro-esito');
    filtro.classList.remove('nascosto');
    filtro.value = '';

    document.getElementById('riquadro-prezzi').classList.add('nascosto');
    document.getElementById('titolo-esito').textContent = 'Controlli trovati';

    disegnaTabellaControlli(controlliTrovati);
}

function disegnaTabellaControlli(righe) {
    document.getElementById('info-esito').textContent =
        `${righe.length} di ${controlliTrovati.length} controlli`;

    document.querySelector('#tabella-esito thead').innerHTML = `
        <tr><th>ID</th><th>Controllo</th><th class="numerico">Metodi</th>
            <th>Stato</th><th></th></tr>`;

    document.querySelector('#tabella-esito tbody').innerHTML = righe.length === 0
        ? '<tr><td colspan="5" class="vuoto">Nessun controllo soddisfa i criteri.</td></tr>'
        : righe.map(c => `
            <tr>
                <td class="codice">${c.idControllo}</td>
                <td><strong>${testo(c.controllo)}</strong></td>
                <td class="numerico">${c.numeroMetodi}</td>
                <td>${c.attivo ? '<span class="esito-ok">Attivo</span>'
                               : '<span class="esito-neutro">Non attivo</span>'}</td>
                <td><button class="pulsante pulsante-tenue minuto"
                            data-id="${c.idControllo}">Esplodi</button></td>
            </tr>`).join('');

    document.querySelectorAll('#tabella-esito .minuto').forEach(b =>
        b.addEventListener('click', () => esplodi(Number(b.dataset.id))));
}

function filtraControlli() {
    const query = document.getElementById('filtro-esito').value.trim().toLowerCase();
    disegnaTabellaControlli(query
        ? controlliTrovati.filter(c => c.controllo.toLowerCase().includes(query))
        : controlliTrovati);
}

// ── Esplosione del controllo ───────────────────────────────

async function esplodi(idControllo) {
    if (!idControllo) {
        avvisa('Selezionare un controllo o indicarne l\'identificativo.', 'info');
        return;
    }

    try {
        const dati = await chiamaApi(`/api/metodi/controllo/${idControllo}`);
        apriEsito();

        document.getElementById('filtro-esito').classList.add('nascosto');
        document.getElementById('titolo-esito').textContent = `${dati.controllo} (Id ${idControllo})`;
        document.getElementById('info-esito').textContent = `${dati.metodi.length} metodi`;

        disegnaPrezzi(dati.prezzi);

        document.querySelector('#tabella-esito thead').innerHTML = `
            <tr><th>Metodo</th><th>Riferimento normativo</th><th>Parametro</th>
                <th>Accreditamento</th><th>Subappalto</th><th>Stato</th></tr>`;

        document.querySelector('#tabella-esito tbody').innerHTML = dati.metodi.map(m => `
            <tr class="${m.attivo ? '' : 'riga-inattiva'}">
                <td><strong>${testo(m.denominazioneRiga) || '—'}</strong></td>
                <td class="sottotesto">${testo(m.riferimentoNormativo) || '—'}</td>
                <td>${testo(m.nomeParametro) || '—'}</td>
                <td>${m.accreditato
                        ? '<span class="etichetta etichetta-conforme">MARE.A</span>'
                        : '<span class="esito-neutro">—</span>'}</td>
                <td>${m.laboratorioEsterno
                        ? `<span class="etichetta etichetta-outsourcing">${testo(m.laboratorioEsterno)}</span>
                           ${m.accreditatoEsterno
                                ? '<div class="sottotesto">accreditato</div>'
                                : '<div class="sottotesto">non accreditato</div>'}`
                        : '<span class="esito-neutro">—</span>'}</td>
                <td>${m.attivo ? '<span class="esito-ok">Attivo</span>'
                               : '<span class="esito-ko">Dismesso</span>'}</td>
            </tr>`).join('');

    } catch (errore) {
        avvisa(errore.message, 'errore');
    }
}

function disegnaPrezzi(prezzi) {
    const box = document.getElementById('riquadro-prezzi');

    if (!prezzi) { box.classList.add('nascosto'); return; }

    const euro = v => v?.toLocaleString('it-IT',
        { style: 'currency', currency: 'EUR' }) ?? '—';

    box.classList.remove('nascosto');
    box.innerHTML = prezzi.prezzoUnico
        ? `<div class="prezzo-voce">
               <span class="prezzo-etichetta">Prezzo unico</span>
               <strong>${euro(prezzi.prezzoMinimo)}</strong>
               <span class="sottotesto">applicato da ${prezzi.clientiTotali} clienti</span>
           </div>`
        : `<div class="prezzo-voce">
               <span class="prezzo-etichetta">Minimo</span>
               <strong>${euro(prezzi.prezzoMinimo)}</strong>
               <span class="sottotesto">${testo(prezzi.clientePrezzoMinimo)}
                   ${prezzi.clientiAlMinimo > 1 ? ` e altri ${prezzi.clientiAlMinimo - 1}` : ''}</span>
           </div>
           <div class="prezzo-voce">
               <span class="prezzo-etichetta">Massimo</span>
               <strong>${euro(prezzi.prezzoMassimo)}</strong>
               <span class="sottotesto">${testo(prezzi.clientePrezzoMassimo)}
                   ${prezzi.clientiAlMassimo > 1 ? ` e altri ${prezzi.clientiAlMassimo - 1}` : ''}</span>
           </div>
           <div class="prezzo-voce">
               <span class="prezzo-etichetta">Clienti</span>
               <strong>${prezzi.clientiTotali}</strong>
               <span class="sottotesto">commesse in vigore</span>
           </div>`;
}

function apriEsito() {
    document.getElementById('segnaposto-esito').classList.add('nascosto');
    document.getElementById('pannello-esito').classList.remove('nascosto');
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    try {
        const [metodi, controlli] = await Promise.all([
            chiamaApi('/api/metodi/elenco'),
            chiamaApi('/api/metodi/controlli')
        ]);
        metodiDisponibili = metodi;
        popolaSelect(document.getElementById('scelta-controllo'), controlli, 'Seleziona…');
        document.getElementById('lista-controlli-base').innerHTML =
            controlli.map(c => `<option value="${testo(c.nome)} (ID ${c.id})">`).join('');
    } catch (errore) {
        avvisa(`Caricamento non riuscito: ${errore.message}`, 'errore');
    }

    const campo = document.getElementById('ricerca-metodo');
    campo.addEventListener('input', suggerisci);
    campo.addEventListener('blur', () =>
        setTimeout(() => document.getElementById('suggerimenti').classList.add('nascosto'), 150));

    document.getElementById('btn-cerca-controlli')
        .addEventListener('click', e => cercaControlli(e.currentTarget));

    document.getElementById('btn-controllo-base')
        .addEventListener('click', e => aggiungiMetodiDelControllo(e.currentTarget));
    document.getElementById('controllo-base').addEventListener('keydown', e => {
        if (e.key === 'Enter') { e.preventDefault(); aggiungiMetodiDelControllo(document.getElementById('btn-controllo-base')); }
    });

    document.getElementById('btn-azzera-metodi').addEventListener('click', () => {
        metodiScelti = [];
        disegnaScelti();
    });

    document.getElementById('filtro-esito').addEventListener('input', filtraControlli);

    document.getElementById('scelta-controllo').addEventListener('change', e => {
        if (e.target.value) esplodi(Number(e.target.value));
    });

    document.getElementById('btn-esplodi').addEventListener('click', () => {
        const perId   = Number(document.getElementById('id-controllo').value);
        const perNome = Number(document.getElementById('scelta-controllo').value);
        esplodi(perId || perNome);
    });
});
