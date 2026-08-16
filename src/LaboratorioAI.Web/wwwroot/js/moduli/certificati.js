/* ═══════════════════════════════════════════════════════════
   Area Consultazione — certificati emessi
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaGiudizio,
         avvisa, occupa } from '../comune.js';

let selezionato = null;   // { idCertificato, idCampione }

async function cerca(pulsante) {
    const testoRicerca = document.getElementById('testo-ricerca').value.trim();
    const elenco = document.getElementById('elenco-certificati');
    const libera = occupa(pulsante, 'Ricerca…');

    try {
        const righe = await chiamaApi(
            `/api/certificati/cerca?testo=${encodeURIComponent(testoRicerca)}`);

        if (righe.length === 0) {
            elenco.innerHTML = '<p class="vuoto">Nessun certificato trovato.</p>';
            return;
        }

        elenco.innerHTML = righe.map(c => `
            <article class="voce-elenco" data-id="${c.idCertificato}">
                <div class="voce-testata">
                    <strong>${testo(c.numeroCertificato)}</strong>
                    <span class="sottotesto">${testo(c.dataCertificato)}</span>
                </div>
                <div class="voce-corpo">
                    <span class="codice">${testo(c.numeroCampione)}</span>
                    ${testo(c.denominazioneCampione)}
                </div>
                <div class="voce-pie">
                    <span>${testo(c.ragioneSociale)}</span>
                    ${c.giorniRisposta != null ? `<span>${c.giorniRisposta} gg</span>` : ''}
                </div>
            </article>`).join('');

        elenco.querySelectorAll('.voce-elenco').forEach(nodo =>
            nodo.addEventListener('click', () => apri(Number(nodo.dataset.id))));

    } catch (errore) {
        elenco.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    } finally {
        libera();
    }
}

async function apri(idCertificato) {
    document.getElementById('segnaposto-dettaglio').classList.add('nascosto');
    document.getElementById('pannello-dettaglio').classList.remove('nascosto');
    document.getElementById('esito-analisi').classList.add('nascosto');

    try {
        const dati = await chiamaApi(`/api/certificati/${idCertificato}`);
        selezionato = { idCertificato, idCampione: dati.campione.idCampione };

        document.getElementById('intestazione-certificato').innerHTML = `
            <h2>${testo(dati.certificato.numeroCertificato)}</h2>
            <dl class="scheda-dati">
                <div><dt>Emesso il</dt><dd>${testo(dati.certificato.dataCertificato)}</dd></div>
                <div><dt>Tempo di risposta</dt>
                     <dd>${dati.certificato.giorniRisposta ?? '—'} giorni</dd></div>
                <div><dt>Campione</dt><dd class="codice">${testo(dati.campione.numeroCampione)}</dd></div>
                <div><dt>Cliente</dt><dd>${testo(dati.campione.ragioneSociale)}</dd></div>
                <div><dt>Denominazione</dt><dd>${testo(dati.campione.denominazioneCampione)}</dd></div>
                <div><dt>Matrice</dt><dd>${testo(dati.campione.matrice)}</dd></div>
            </dl>`;

        document.querySelector('#tabella-prove tbody').innerHTML =
            dati.prove.map(p => `
                <tr>
                    <td><strong>${testo(p.parametro)}</strong>
                        ${p.accreditato ? '<span class="etichetta etichetta-conforme">accreditato MARE.A</span>' : ''}
                        ${p.accreditatoEsterno ? '<span class="etichetta etichetta-conforme">accreditato lab esterno</span>' : ''}
                        <div class="sottotesto">Eseguito da ${p.laboratorioEsterno
                            ? `${testo(p.laboratorioEsterno)}${p.numeroLaboratorioEsterno ? ` — n° ${testo(p.numeroLaboratorioEsterno)}` : ''}`
                            : 'MARE.A'}</div></td>
                    <td class="numerico">${testo(p.valore)}</td>
                    <td>${testo(p.unitaMisura) || '—'}</td>
                    <td class="numerico">${testo(p.intervallo) || '—'}</td>
                    <td class="sottotesto">${testo(p.riferimentoNormativo) || '—'}</td>
                    <td>${p.verificaValore === true  ? '<span class="esito-ok">Verificato</span>'
                        : p.verificaValore === false ? '<span class="esito-ko">Non verificato</span>'
                        : '<span class="esito-neutro">—</span>'}</td>
                </tr>`).join('');

        const btnArchivio = document.getElementById('btn-archivio');
        const btnAnalizza = document.getElementById('btn-analizza');

        if (dati.analisiArchiviata) {
            btnArchivio.classList.remove('nascosto');
            btnArchivio.title = `Analisi del ${dati.analisiArchiviata.data}`;
            btnAnalizza.textContent = 'Rianalizza';
        } else {
            btnArchivio.classList.add('nascosto');
            btnAnalizza.textContent = 'Analizza con AI';
        }

    } catch (errore) {
        avvisa(errore.message, 'errore');
    }
}

async function analizza(pulsante, forza) {
    if (!selezionato) return;

    const libera = occupa(pulsante, 'Analisi in corso…');

    try {
        const esito = await inviaJson('/api/validazione/analizza', {
            idCampione: selezionato.idCampione,
            idCertificato: selezionato.idCertificato,
            modalita: 2,                 // ModalitaAnalisi.Certificato
            forzaRiesecuzione: forza
        });

        const nodo = document.getElementById('esito-analisi');
        nodo.classList.remove('nascosto');
        nodo.innerHTML = `
            <div class="esito-testata">
                ${etichettaGiudizio(esito.giudizio)}
                <span class="esito-meta">
                    ${esito.daArchivio ? 'Analisi archiviata del' : 'Eseguita il'} ${testo(esito.data)}
                </span>
            </div>
            <div class="esito-testo">${testo(esito.testo)}</div>`;
        nodo.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('btn-cerca-cert')
        .addEventListener('click', e => cerca(e.currentTarget));

    document.getElementById('testo-ricerca')
        .addEventListener('keydown', e => {
            if (e.key === 'Enter') document.getElementById('btn-cerca-cert').click();
        });

    document.getElementById('btn-analizza')
        .addEventListener('click', e => analizza(e.currentTarget, true));

    document.getElementById('btn-archivio')
        .addEventListener('click', e => analizza(e.currentTarget, false));
});
