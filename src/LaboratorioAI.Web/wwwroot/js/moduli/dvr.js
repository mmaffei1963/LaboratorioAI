/* ═══════════════════════════════════════════════════════════
   DVR Legionellosi
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

let strutture = [];
let selezionata = null;
let punti = [];
let sondaggioLotto = null;

// ── Elenco strutture ───────────────────────────────────────

async function caricaStrutture() {
    const elenco = document.getElementById('elenco-strutture');
    elenco.innerHTML = '<p class="attesa">Caricamento…</p>';

    try {
        strutture = await chiamaApi('/api/dvr/strutture');
        disegnaElenco();
    } catch (errore) {
        elenco.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    }
}

function disegnaElenco() {
    const filtro = document.getElementById('filtro-strutture').value.trim().toLowerCase();
    const elenco = document.getElementById('elenco-strutture');
    const conLotto = document.getElementById('seleziona-tutte') !== null;

    const visibili = filtro
        ? strutture.filter(s => [s.ragioneSociale, s.nomeInsediamento, s.comune]
            .some(v => v?.toLowerCase().includes(filtro)))
        : strutture;

    if (visibili.length === 0) {
        elenco.innerHTML = '<p class="vuoto">Nessuna struttura corrisponde al filtro.</p>';
        return;
    }

    elenco.innerHTML = visibili.map(s => `
        <article class="voce-elenco ${selezionata === s.idInsediamento ? 'selezionata' : ''}"
                 data-id="${s.idInsediamento}">
            ${conLotto ? `<input type="checkbox" class="scelta-lotto"
                                 data-id="${s.idInsediamento}" />` : ''}
            <div class="voce-testata">
                <strong>${testo(s.ragioneSociale)}</strong>
                ${s.regionePresunta
                    ? `<span class="etichetta etichetta-neutro">${testo(s.regionePresunta)}</span>`
                    : '<span class="etichetta etichetta-borderline">regione da definire</span>'}
            </div>
            <div class="voce-corpo">${testo(s.nomeInsediamento) || '—'}</div>
            <div class="voce-pie">
                <span>${testo(s.comune) || ''} ${s.provincia ? `(${testo(s.provincia)})` : ''}</span>
                ${s.ultimoCampionamento
                    ? `<span>ultimo prelievo ${testo(s.ultimoCampionamento)}</span>` : ''}
            </div>
        </article>`).join('');

    elenco.querySelectorAll('.voce-elenco').forEach(nodo =>
        nodo.addEventListener('click', evento => {
            if (evento.target.classList.contains('scelta-lotto')) return;
            apri(Number(nodo.dataset.id));
        }));

    elenco.querySelectorAll('.scelta-lotto').forEach(casella =>
        casella.addEventListener('change', aggiornaPulsanteLotto));
}

function aggiornaPulsanteLotto() {
    const pulsante = document.getElementById('btn-lotto');
    if (!pulsante) return;

    const scelte = document.querySelectorAll('.scelta-lotto:checked').length;
    pulsante.disabled = scelte === 0;
    pulsante.textContent = scelte === 0
        ? 'Genera selezionate'
        : `Genera ${scelte} document${scelte === 1 ? 'o' : 'i'}`;
}

// ── Scheda ─────────────────────────────────────────────────

async function apri(idInsediamento) {
    selezionata = idInsediamento;
    disegnaElenco();

    document.getElementById('segnaposto-scheda').classList.add('nascosto');
    document.getElementById('pannello-scheda').classList.remove('nascosto');
    document.getElementById('esito-generazione').classList.add('nascosto');

    try {
        const dati = await chiamaApi(`/api/dvr/struttura/${idInsediamento}`);

        document.getElementById('titolo-scheda').textContent =
            `${dati.ragioneSociale} — ${dati.nomeInsediamento ?? ''}`;

        valore('c-ragione',      dati.ragioneSociale);
        valore('c-insediamento', dati.nomeInsediamento);
        valore('c-indirizzo',    dati.indirizzo);
        valore('c-comune',       dati.comune);
        valore('c-provincia',    dati.provincia);
        valore('c-cap',          dati.cap);

        if (dati.regionePresunta)
            document.getElementById('c-regione').value = dati.regionePresunta;

        // I punti già usati in passato fanno da proposta iniziale
        punti = (dati.puntiPrelievo ?? []).map(p => ({
            descrizione: p.descrizione ?? '',
            ubicazione:  p.ubicazione ?? '',
            tipologia:   p.tipologia ?? ''
        }));
        disegnaPunti();

    } catch (errore) {
        avvisa(errore.message, 'errore');
    }
}

const valore = (id, v) => { document.getElementById(id).value = v ?? ''; };
const leggi  = id => document.getElementById(id).value.trim() || null;
const numero = id => { const v = document.getElementById(id).value; return v === '' ? null : Number(v); };
const spunta = id => document.getElementById(id).checked;

// ── Punti di prelievo ──────────────────────────────────────

function disegnaPunti() {
    const contenitore = document.getElementById('elenco-punti');

    if (punti.length === 0) {
        contenitore.innerHTML = '<p class="sottotesto">Nessun punto definito.</p>';
        return;
    }

    contenitore.innerHTML = punti.map((p, i) => `
        <div class="riga-punto">
            <input type="text" data-indice="${i}" data-campo="descrizione"
                   value="${testo(p.descrizione)}" placeholder="Descrizione" />
            <input type="text" data-indice="${i}" data-campo="ubicazione"
                   value="${testo(p.ubicazione)}" placeholder="Ubicazione" />
            <input type="text" data-indice="${i}" data-campo="tipologia"
                   value="${testo(p.tipologia)}" placeholder="Tipologia" />
            <button type="button" class="rimuovi" data-indice="${i}"
                    aria-label="Rimuovi punto">&times;</button>
        </div>`).join('');

    contenitore.querySelectorAll('input').forEach(campo =>
        campo.addEventListener('input', e => {
            punti[Number(e.target.dataset.indice)][e.target.dataset.campo] = e.target.value;
        }));

    contenitore.querySelectorAll('.rimuovi').forEach(pulsante =>
        pulsante.addEventListener('click', e => {
            punti.splice(Number(e.target.dataset.indice), 1);
            disegnaPunti();
        }));
}

// ── Composizione della scheda ──────────────────────────────

function componiScheda(struttura) {
    return {
        idCliente:      struttura?.idCliente ?? null,
        idInsediamento: struttura?.idInsediamento ?? selezionata,
        regione:        document.getElementById('c-regione').value,

        ragioneSociale:   struttura?.ragioneSociale ?? leggi('c-ragione'),
        nomeInsediamento: struttura?.nomeInsediamento ?? leggi('c-insediamento'),
        indirizzo:        struttura?.indirizzo ?? leggi('c-indirizzo'),
        comune:           struttura?.comune ?? leggi('c-comune'),
        provincia:        struttura?.provincia ?? leggi('c-provincia'),
        cap:              struttura?.cap ?? leggi('c-cap'),

        legaleRappresentante:  leggi('c-legale'),
        responsabileStruttura: leggi('c-responsabile'),
        dataSopralluogo:       leggi('c-sopralluogo'),

        tipologiaStruttura:        leggi('c-tipologia'),
        numeroCamere:              numero('c-camere'),
        numeroPianiFuoriTerra:     numero('c-piani'),
        annoCostruzione:           numero('c-anno'),
        annoUltimaRistrutturazione: numero('c-ristrutturazione'),
        periodoApertura:           leggi('c-apertura'),
        stagionale:                spunta('c-stagionale'),

        produzioneAcqua:            Number(document.getElementById('c-produzione').value),
        serbatoiAccumuloAcquaFredda: spunta('c-serbatoi'),
        ricircoloAcquaCalda:        spunta('c-ricircolo'),
        temperaturaMandata:         numero('c-mandata'),
        temperaturaRitorno:         numero('c-ritorno'),
        trattamentiAcqua:           leggi('c-trattamenti'),

        torriEvaporative:          spunta('c-torri'),
        fontaneDecorative:         spunta('c-fontane'),
        piscineOIdromassaggio:     spunta('c-piscine'),
        umidificatoriAria:         spunta('c-umidificatori'),
        impiantiAntincendioAdUmido: spunta('c-antincendio'),
        altriElementiRischio:      leggi('c-altri-rischi'),

        puntiPrelievo: punti.filter(p => p.descrizione?.trim()),

        noteSopralluogo:  leggi('c-note'),
        misureGiaAdottate: leggi('c-misure')
    };
}

// ── Generazione singola ────────────────────────────────────

async function genera(evento) {
    evento.preventDefault();

    const pulsante = document.getElementById('btn-genera');
    const libera = occupa(pulsante, 'Generazione…');

    try {
        const esito = await inviaJson('/api/dvr/genera', componiScheda(null));

        const nodo = document.getElementById('esito-generazione');
        nodo.classList.remove('nascosto');
        nodo.innerHTML = `
            <div class="esito-testata">
                <span class="etichetta etichetta-conforme">Documento prodotto</span>
                <span class="esito-meta">${testo(esito.nomeFile)}</span>
            </div>
            ${esito.campiNonPopolati?.length
                ? `<div class="avviso">
                       Il modello contiene ${esito.campiNonPopolati.length} controlli
                       rimasti senza valore. Verificare la corrispondenza fra modello e scheda.
                       <details><summary>Elenco</summary>
                           <ul>${esito.campiNonPopolati.map(c => `<li>${testo(c)}</li>`).join('')}</ul>
                       </details>
                   </div>`
                : ''}`;
        nodo.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

// ── Generazione massiva ────────────────────────────────────

async function generaLotto() {
    const scelte = [...document.querySelectorAll('.scelta-lotto:checked')]
        .map(c => Number(c.dataset.id));

    if (scelte.length === 0) return;

    const conferma = confirm(
        `Verranno generati ${scelte.length} documenti.\n` +
        `I dati non presenti in anagrafica resteranno vuoti nella scheda.\n\nProcedere?`);
    if (!conferma) return;

    const schede = scelte
        .map(id => strutture.find(s => s.idInsediamento === id))
        .filter(Boolean)
        .map(s => ({
            idCliente: s.idCliente,
            idInsediamento: s.idInsediamento,
            regione: s.regionePresunta ?? 'EmiliaRomagna',
            ragioneSociale: s.ragioneSociale,
            nomeInsediamento: s.nomeInsediamento,
            indirizzo: s.indirizzo,
            comune: s.comune,
            provincia: s.provincia,
            cap: s.cap,
            puntiPrelievo: []
        }));

    apriFinestra('Generazione massiva',
        '<p class="attesa">Avvio dell\'elaborazione…</p>');

    try {
        const { idLotto } = await inviaJson('/api/dvr/genera-lotto', schede);
        sorvegliaLotto(idLotto);
    } catch (errore) {
        document.getElementById('corpo-finestra').innerHTML =
            `<p class="errore">${testo(errore.message)}</p>`;
    }
}

/** Interroga periodicamente lo stato del lotto in corso. */
function sorvegliaLotto(idLotto) {
    clearInterval(sondaggioLotto);

    sondaggioLotto = setInterval(async () => {
        try {
            const stato = await chiamaApi(`/api/dvr/lotto/${idLotto}`);
            disegnaStatoLotto(stato);

            if (stato.concluso) {
                clearInterval(sondaggioLotto);
                await caricaStrutture();
            }
        } catch {
            clearInterval(sondaggioLotto);
        }
    }, 1500);
}

function disegnaStatoLotto(stato) {
    document.getElementById('corpo-finestra').innerHTML = `
        <div class="avanzamento">
            <div class="barra-avanzamento">
                <div class="barra-riempimento" style="width:${stato.percentualeAvanzamento}%"></div>
            </div>
            <p class="sottotesto">
                ${stato.completati} di ${stato.totale} —
                ${stato.riusciti} prodotti, ${stato.falliti} non riusciti
                ${stato.elaborazioneCorrente
                    ? `<br />In lavorazione: ${testo(stato.elaborazioneCorrente)}` : ''}
            </p>
        </div>
        ${stato.righe?.length ? `
            <table class="tabella">
                <thead><tr><th>Struttura</th><th>Esito</th></tr></thead>
                <tbody>${stato.righe.map(r => `
                    <tr>
                        <td>${testo(r.ragioneSociale)}
                            <div class="sottotesto">${testo(r.nomeInsediamento) || ''}</div></td>
                        <td>${r.riuscito
                                ? `<span class="esito-ok">${testo(r.nomeFile)}</span>`
                                : `<span class="esito-ko">${testo(r.messaggio)}</span>`}</td>
                    </tr>`).join('')}
                </tbody>
            </table>` : ''}`;
}

// ── Registro e diagnosi ────────────────────────────────────

async function mostraRegistro() {
    apriFinestra('Registro dei documenti prodotti', '<p class="attesa">Caricamento…</p>');

    try {
        const righe = await chiamaApi('/api/dvr/registro');

        document.getElementById('corpo-finestra').innerHTML = righe.length === 0
            ? '<p class="vuoto">Nessun documento prodotto finora.</p>'
            : `<table class="tabella">
                   <thead><tr><th>Data</th><th>Struttura</th><th>Regione</th>
                              <th>Operatore</th><th>File</th><th>Esito</th></tr></thead>
                   <tbody>${righe.map(r => `
                       <tr>
                           <td>${testo(r.data)}</td>
                           <td>${testo(r.ragioneSociale)}
                               <div class="sottotesto">${testo(r.nomeInsediamento) || ''}</div></td>
                           <td>${testo(r.regione)}</td>
                           <td>${testo(r.operatore) || '—'}</td>
                           <td class="sottotesto">${testo(r.nomeFile)}</td>
                           <td>${r.esito === 'Successo'
                                   ? '<span class="esito-ok">Prodotto</span>'
                                   : `<span class="esito-ko">${testo(r.note)}</span>`}</td>
                       </tr>`).join('')}
                   </tbody>
               </table>`;
    } catch (errore) {
        document.getElementById('corpo-finestra').innerHTML =
            `<p class="errore">${testo(errore.message)}</p>`;
    }
}

async function ispezionaModelli() {
    apriFinestra('Controlli presenti nei modelli', '<p class="attesa">Lettura…</p>');

    const sezioni = [];

    for (const regione of ['EmiliaRomagna', 'Marche']) {
        try {
            const esito = await chiamaApi(`/api/dvr/ispeziona/${regione}`);
            sezioni.push(`
                <h3>${testo(esito.regione)}</h3>
                <p class="sottotesto">${esito.controlli.length} controlli individuati</p>
                <ul class="elenco-controlli">
                    ${esito.controlli.map(c => `<li><code>${testo(c)}</code></li>`).join('')}
                </ul>`);
        } catch (errore) {
            sezioni.push(`<h3>${regione}</h3><p class="errore">${testo(errore.message)}</p>`);
        }
    }

    document.getElementById('corpo-finestra').innerHTML = sezioni.join('<hr />');
}

// ── Finestra ───────────────────────────────────────────────

function apriFinestra(titolo, contenuto) {
    document.getElementById('titolo-finestra').textContent = titolo;
    document.getElementById('corpo-finestra').innerHTML = contenuto;
    document.getElementById('finestra').classList.remove('nascosto');
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    caricaStrutture();

    document.getElementById('filtro-strutture').addEventListener('input', disegnaElenco);
    document.getElementById('scheda-dvr').addEventListener('submit', genera);

    document.getElementById('btn-aggiungi-punto').addEventListener('click', () => {
        punti.push({ descrizione: '', ubicazione: '', tipologia: '' });
        disegnaPunti();
    });

    document.getElementById('btn-lotto')?.addEventListener('click', generaLotto);
    document.getElementById('btn-registro')?.addEventListener('click', mostraRegistro);
    document.getElementById('btn-ispeziona').addEventListener('click', ispezionaModelli);

    document.getElementById('seleziona-tutte')?.addEventListener('change', e => {
        document.querySelectorAll('.scelta-lotto').forEach(c => c.checked = e.target.checked);
        aggiornaPulsanteLotto();
    });

    document.getElementById('chiudi-finestra').addEventListener('click', () => {
        clearInterval(sondaggioLotto);
        document.getElementById('finestra').classList.add('nascosto');
    });
});
