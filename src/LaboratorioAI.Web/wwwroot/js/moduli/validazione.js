/* ═══════════════════════════════════════════════════════════
   Area Validazione
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaGiudizio,
         avvisa, occupa } from '../comune.js';

let campioni = [];
let selezionato = null;

// ── Elenco ─────────────────────────────────────────────────

async function caricaElenco() {
    const elenco = document.getElementById('elenco-campioni');
    elenco.innerHTML = '<p class="attesa">Caricamento…</p>';

    try {
        campioni = await chiamaApi('/api/validazione/campioni');
        disegnaElenco();
        aggiornaRiepilogo();
    } catch (errore) {
        elenco.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    }
}

function aggiornaRiepilogo() {
    const conAnalisi = campioni.filter(c => c.analisi).length;
    document.getElementById('riepilogo').textContent =
        `${campioni.length} campioni in attesa di validazione, ${conAnalisi} già analizzati.`;
}

function disegnaElenco() {
    const filtro = document.getElementById('filtro-elenco').value.trim().toLowerCase();
    const elenco = document.getElementById('elenco-campioni');

    const visibili = filtro
        ? campioni.filter(c => [c.ragioneSociale, c.denominazioneCampione,
                                c.matrice, c.numeroCampione]
            .some(v => v?.toLowerCase().includes(filtro)))
        : campioni;

    if (visibili.length === 0) {
        elenco.innerHTML = '<p class="vuoto">Nessun campione corrisponde al filtro.</p>';
        return;
    }

    elenco.innerHTML = visibili.map(c => `
        <article class="voce-elenco ${selezionato === c.idCampione ? 'selezionata' : ''}"
                 data-id="${c.idCampione}">
            <div class="voce-testata">
                <strong>${testo(c.ragioneSociale)}</strong>
                <span class="giacenza giacenza-${c.anzianita.toLowerCase()}">
                    ${c.giorniInLaboratorio} gg
                </span>
            </div>
            <div class="voce-corpo">
                <span class="codice">${testo(c.numeroCampione)}</span>
                ${testo(c.denominazioneCampione)}
            </div>
            <div class="voce-pie">
                <span>${testo(c.matrice)}</span>
                <span>${c.numeroProve} prove</span>
                ${c.analisi ? etichettaGiudizio(c.analisi.giudizio) : ''}
            </div>
        </article>`).join('');

    elenco.querySelectorAll('.voce-elenco').forEach(nodo =>
        nodo.addEventListener('click', () => apriCampione(Number(nodo.dataset.id))));
}

// ── Dettaglio ──────────────────────────────────────────────

async function apriCampione(idCampione) {
    selezionato = idCampione;
    disegnaElenco();

    document.getElementById('segnaposto-dettaglio').classList.add('nascosto');
    const pannello = document.getElementById('pannello-dettaglio');
    pannello.classList.remove('nascosto');

    document.getElementById('esito-analisi').classList.add('nascosto');

    try {
        const dati = await chiamaApi(`/api/validazione/campione/${idCampione}`);
        disegnaIntestazione(dati.campione);
        disegnaProve(dati.prove);
        configuraAzioni(dati.analisiArchiviata);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    }
}

function disegnaIntestazione(c) {
    document.getElementById('intestazione-campione').innerHTML = `
        <h2>${testo(c.numeroCampione)} — ${testo(c.denominazioneCampione)}</h2>
        <dl class="scheda-dati">
            <div><dt>Cliente</dt><dd>${testo(c.ragioneSociale)}</dd></div>
            <div><dt>Insediamento</dt><dd>${testo(c.nomeInsediamento) || '—'}</dd></div>
            <div><dt>Matrice</dt><dd>${testo(c.matrice)}</dd></div>
            <div><dt>Controllo</dt><dd>${testo(c.controllo) || '—'}</dd></div>
            <div><dt>Ricezione</dt><dd>${testo(c.dataRicezione)} (${c.giorniInLaboratorio} gg)</dd></div>
            <div><dt>Prelievo</dt><dd>${testo(c.dataPrelievo) || '—'}</dd></div>
            <div><dt>Luogo</dt><dd>${testo(c.luogoPrelievo) || '—'}</dd></div>
            <div><dt>Punto</dt><dd>${testo(c.puntoPrelievo) || '—'}</dd></div>
        </dl>`;
}

function disegnaProve(prove) {
    const corpo = document.querySelector('#tabella-prove tbody');

    corpo.innerHTML = prove.map(p => {
        const esito = p.verificaValore === true  ? '<span class="esito-ok">Verificato</span>'
                    : p.verificaValore === false ? '<span class="esito-ko">Non verificato</span>'
                    : '<span class="esito-neutro">—</span>';

        return `<tr>
            <td>
                <strong>${testo(p.parametro)}</strong>
                ${p.accreditato ? '<span class="accreditato" title="Metodo accreditato">A</span>' : ''}
                ${p.laboratorioEsterno
                    ? `<div class="sottotesto">Eseguito da ${testo(p.laboratorioEsterno)}</div>` : ''}
            </td>
            <td class="numerico">${testo(p.valore)}</td>
            <td>${testo(p.unitaMisura) || '—'}</td>
            <td class="numerico">${testo(p.intervallo) || '—'}</td>
            <td class="sottotesto">${testo(p.riferimentoNormativo) || '—'}</td>
            <td>${esito}</td>
        </tr>`;
    }).join('');
}

function configuraAzioni(analisiArchiviata) {
    const btnArchivio   = document.getElementById('btn-archivio');
    const btnRianalizza = document.getElementById('btn-rianalizza');
    const btnAnalizza   = document.getElementById('btn-analizza');

    if (analisiArchiviata) {
        btnArchivio.classList.remove('nascosto');
        btnArchivio.title = `Analisi del ${analisiArchiviata.data}`;
        btnRianalizza.classList.remove('nascosto');
        btnAnalizza.classList.add('nascosto');
    } else {
        btnArchivio.classList.add('nascosto');
        btnRianalizza.classList.add('nascosto');
        btnAnalizza.classList.remove('nascosto');
    }
}

// ── Analisi ────────────────────────────────────────────────

async function analizza(pulsante, forza) {
    if (!selezionato) return;

    const libera = occupa(pulsante, forza ? 'Rianalisi in corso…' : 'Analisi in corso…');

    try {
        const esito = await inviaJson('/api/validazione/analizza', {
            idCampione: selezionato,
            modalita: 1,
            forzaRiesecuzione: forza
        });

        mostraEsito(esito);

        // Aggiorna la scheda nell'elenco senza ricaricare tutto
        const campione = campioni.find(c => c.idCampione === selezionato);
        if (campione) {
            campione.analisi = {
                idAnalisi: esito.idAnalisi,
                data: esito.data,
                giudizio: esito.giudizio
            };
            disegnaElenco();
            aggiornaRiepilogo();
        }
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function mostraEsito(esito) {
    const nodo = document.getElementById('esito-analisi');
    nodo.classList.remove('nascosto');
    nodo.innerHTML = `
        <div class="esito-testata">
            ${etichettaGiudizio(esito.giudizio)}
            <span class="esito-meta">
                ${esito.daArchivio ? 'Analisi archiviata del' : 'Eseguita il'} ${testo(esito.data)}
                ${esito.eseguitaDa ? ` — ${testo(esito.eseguitaDa)}` : ''}
            </span>
        </div>
        <div class="esito-testo">${testo(esito.testo)}</div>`;
    nodo.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

// ── Analisi massiva ────────────────────────────────────────

async function analisiMassiva() {
    const daAnalizzare = campioni.filter(c => !c.analisi);

    if (daAnalizzare.length === 0) {
        avvisa('Tutti i campioni in elenco sono già stati analizzati.', 'info');
        return;
    }

    const conferma = confirm(
        `Verranno analizzati ${daAnalizzare.length} campioni.\n` +
        `I campioni già in archivio non vengono rianalizzati.\n\nProcedere?`);
    if (!conferma) return;

    const finestra = document.getElementById('finestra-massiva');
    const corpo = document.getElementById('corpo-massiva');
    finestra.classList.remove('nascosto');
    corpo.innerHTML = '<p class="attesa">Analisi in corso. L\'operazione può richiedere alcuni minuti…</p>';

    try {
        const esito = await inviaJson('/api/validazione/analizza-massivo', {
            idCampioni: daAnalizzare.map(c => c.idCampione)
        });

        corpo.innerHTML = `
            <div class="semaforo">
                <div class="semaforo-voce conforme"><strong>${esito.conformi}</strong><span>Conformi</span></div>
                <div class="semaforo-voce borderline"><strong>${esito.borderline}</strong><span>Borderline</span></div>
                <div class="semaforo-voce non-conforme"><strong>${esito.nonConformi}</strong><span>Non conformi</span></div>
                ${esito.nonRiusciti > 0
                    ? `<div class="semaforo-voce neutro"><strong>${esito.nonRiusciti}</strong><span>Non riusciti</span></div>`
                    : ''}
            </div>
            <table class="tabella">
                <thead><tr><th>Campione</th><th>Cliente</th><th>Esito</th></tr></thead>
                <tbody>${esito.righe.map(r => `
                    <tr class="riga-cliccabile" data-id="${r.idCampione}">
                        <td><span class="codice">${testo(r.numeroCampione)}</span></td>
                        <td>${testo(r.ragioneSociale)}</td>
                        <td>${r.errore
                                ? `<span class="esito-ko">${testo(r.errore)}</span>`
                                : etichettaGiudizio(r.giudizio)}</td>
                    </tr>`).join('')}
                </tbody>
            </table>`;

        corpo.querySelectorAll('.riga-cliccabile').forEach(riga =>
            riga.addEventListener('click', () => {
                finestra.classList.add('nascosto');
                apriCampione(Number(riga.dataset.id));
            }));

        await caricaElenco();
    } catch (errore) {
        corpo.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    }
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    caricaElenco();

    document.getElementById('btn-aggiorna')
        .addEventListener('click', caricaElenco);

    document.getElementById('filtro-elenco')
        .addEventListener('input', disegnaElenco);

    document.getElementById('btn-analizza')
        .addEventListener('click', e => analizza(e.currentTarget, false));

    document.getElementById('btn-rianalizza')
        .addEventListener('click', e => analizza(e.currentTarget, true));

    document.getElementById('btn-archivio')
        .addEventListener('click', e => analizza(e.currentTarget, false));

    document.getElementById('btn-massivo')
        ?.addEventListener('click', analisiMassiva);

    document.getElementById('chiudi-massiva')
        .addEventListener('click', () =>
            document.getElementById('finestra-massiva').classList.add('nascosto'));
});
