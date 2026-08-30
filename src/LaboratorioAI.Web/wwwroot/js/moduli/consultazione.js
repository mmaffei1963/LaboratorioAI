/* ═══════════════════════════════════════════════════════════
   Area Consultazione — ricerca campioni
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaStato,
         avvisa, occupa, popolaSelect } from '../comune.js';

let insediamentiTutti = [];

// ── Filtri ─────────────────────────────────────────────────

async function caricaAnagrafiche() {
    try {
        const [clienti, insediamenti, matrici, controlli, laboratori] = await Promise.all([
            chiamaApi('/api/anagrafiche/clienti'),
            chiamaApi('/api/anagrafiche/insediamenti'),
            chiamaApi('/api/anagrafiche/matrici'),
            chiamaApi('/api/anagrafiche/controlli'),
            chiamaApi('/api/anagrafiche/laboratori-esterni')
        ]);

        insediamentiTutti = insediamenti;

        popolaSelect(document.getElementById('f-cliente'),      clienti);
        popolaSelect(document.getElementById('f-insediamento'), insediamenti);
        popolaSelect(document.getElementById('f-matrice'),      matrici,    'Tutte');
        popolaSelect(document.getElementById('f-controllo'),    controlli);
        popolaSelect(document.getElementById('f-labesterno'),   laboratori);
    } catch (errore) {
        avvisa(`Caricamento filtri non riuscito: ${errore.message}`, 'errore');
    }
}

/** Filtra gli insediamenti in base al cliente scelto. */
function filtraInsediamenti() {
    const idCliente = Number(document.getElementById('f-cliente').value) || null;
    const voci = idCliente
        ? insediamentiTutti.filter(i => i.idCliente === idCliente)
        : insediamentiTutti;

    popolaSelect(document.getElementById('f-insediamento'), voci);
    document.getElementById('f-insediamento').value = '';
}

function leggiCriteri() {
    const valore = id => document.getElementById(id).value || null;
    const numero = id => Number(document.getElementById(id).value) || null;

    return {
        ricezioneDa: valore('f-ricezione-da'),
        ricezioneA:  valore('f-ricezione-a'),
        prelievoDa:  valore('f-prelievo-da'),
        prelievoA:   valore('f-prelievo-a'),

        idCliente:      numero('f-cliente'),
        idInsediamento: numero('f-insediamento'),
        idMatrice:      numero('f-matrice'),
        idControllo:    numero('f-controllo'),

        codiceLim:             valore('f-codice'),
        verbalePrelievo:       valore('f-verbale'),
        denominazioneCampione: valore('f-denominazione'),
        luogoPrelievo:         valore('f-luogo'),
        prelevatore:           valore('f-prelevatore'),
        ricevitore:            valore('f-ricevitore'),
        email:                 valore('f-email'),

        idLaboratorioEsterno: numero('f-labesterno'),
        stato: valore('f-stato'),
        limite: 500
    };
}

function azzera() {
    document.querySelectorAll('.griglia-filtri input, .griglia-filtri select')
        .forEach(campo => campo.value = '');
    filtraInsediamenti();
    document.getElementById('pannello-risultati').classList.add('nascosto');
}

// ── Ricerca ────────────────────────────────────────────────

async function cerca(pulsante) {
    const libera = occupa(pulsante, 'Ricerca…');

    try {
        const esito = await inviaJson('/api/consultazione/cerca', leggiCriteri());
        disegnaRisultati(esito);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function euro(n) {
    return (n || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function disegnaRisultati(esito) {
    document.getElementById('pannello-risultati').classList.remove('nascosto');

    document.getElementById('conteggio').textContent =
        esito.totaleCompleto === 0 ? 'Nessun campione trovato'
                                   : `${esito.totaleCompleto} campioni trovati`;

    const avviso = document.getElementById('avviso-troncato');
    avviso.classList.toggle('nascosto', !esito.troncato);
    if (esito.troncato)
        avviso.textContent =
            `Risultati troncati alle prime ${esito.campioni.length} righe (su ${esito.totaleCompleto}): `
            + `affina i criteri per un elenco completo.`;

    // Le card riflettono l'intera selezione (calcolo lato server), non solo le righe mostrate
    document.getElementById('pannello-sintesi').innerHTML = `
        <div class="scheda scheda-dato"><h2>${esito.totaleCompleto}</h2><p>Campioni</p></div>
        <div class="scheda scheda-dato"><h2>€ ${euro(esito.totalePrezzo)}</h2><p>Totale prezzo controllo</p></div>`;

    document.querySelector('#tabella-risultati tbody').innerHTML =
        esito.campioni.map(c => `
            <tr class="riga-cliccabile" data-id="${c.idCampione}" data-num="${testo(c.numeroCampione)}">
                <td>${etichettaStato(c.stato)}</td>
                <td><span class="codice">${testo(c.numeroCampione)}</span>
                    ${c.haAnalisi ? '<span class="pallino" title="Analisi AI in archivio"></span>' : ''}</td>
                <td>${testo(c.dataRicezione)}</td>
                <td>${testo(c.dataPrelievo) || '—'}</td>
                <td>${testo(c.ragioneSociale)}</td>
                <td><span class="codice">${testo(c.codContabilita) || '—'}</span></td>
                <td>${testo(c.email) || '—'}</td>
                <td>${testo(c.nomeInsediamento) || '—'}</td>
                <td>${testo(c.denominazioneCampione)}</td>
                <td>${testo(c.matrice)}</td>
                <td>${testo(c.controllo) || '—'}</td>
                <td class="numerico">${c.idControllo ?? '—'}</td>
                <td>${testo(c.luogoPrelievo) || '—'}</td>
                <td>${testo(c.verbalePrelievo) || '—'}</td>
                <td>${testo(c.prelevatore) || '—'}</td>
                <td class="numerico">${c.prezzoControllo != null ? euro(c.prezzoControllo) : '—'}</td>
            </tr>`).join('');

    document.querySelectorAll('#tabella-risultati .riga-cliccabile').forEach(riga =>
        riga.addEventListener('click', () =>
            mostraProve(Number(riga.dataset.id), riga.dataset.num)));
}

// ── Esportazione in Excel ──────────────────────────────────

async function esporta(pulsante, url = '/api/consultazione/esporta', nomeDefault = 'Consultazione.xlsx') {
    const criteri = leggiCriteri();
    const libera = occupa(pulsante, 'Generazione…');

    try {
        const risposta = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(criteri)
        });
        if (!risposta.ok) {
            const err = await risposta.json().catch(() => null);
            throw new Error(err?.messaggio ?? 'Generazione del foglio non riuscita.');
        }

        const intestazione = risposta.headers.get('content-disposition') ?? '';
        const nome = intestazione.match(/filename=([^;]+)/)?.[1]?.trim() ?? nomeDefault;
        const blob = await risposta.blob();
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = decodeURIComponent(nome);
        a.click();
        URL.revokeObjectURL(a.href);
    } catch (e) {
        avvisa(e.message, 'errore');
    } finally {
        libera();
    }
}

// ── Prove del campione (click dall'elenco o dal lookup) ────
function accreditamento(p) {
    if (p.accreditato)        return '<strong style="color:#1a7f37">MARE.A</strong>';
    if (p.accreditatoEsterno) return '<strong style="color:#1a7f37">esterno</strong>';
    return '<span style="color:#9a9a9a">no</span>';
}

async function mostraProve(idCampione, numero) {
    const pannello = document.getElementById('pannello-prove');
    const corpo = document.querySelector('#tabella-prove tbody');
    document.getElementById('titolo-prove').textContent = numero ? `Campione ${numero}` : '';
    corpo.innerHTML = '<tr><td colspan="5" class="attesa">Caricamento…</td></tr>';
    pannello.classList.remove('nascosto');
    pannello.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    try {
        const prove = await chiamaApi(`/api/consultazione/campione/${idCampione}/prove`);
        corpo.innerHTML = prove.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessuna prova.</td></tr>'
            : prove.map(p => `
                <tr>
                    <td><strong>${testo(p.metodo) || '—'}</strong></td>
                    <td>${accreditamento(p)}</td>
                    <td>${p.laboratorioEsterno
                        ? `${testo(p.laboratorioEsterno)}${p.numeroLaboratorioEsterno ? ` (n° ${testo(p.numeroLaboratorioEsterno)})` : ''}`
                        : 'MARE.A'}</td>
                    <td class="numerico codice">${p.idProva}</td>
                    <td class="numerico codice">${p.idRigaMetodo}</td>
                </tr>`).join('');
    } catch (e) {
        corpo.innerHTML = `<tr><td colspan="5" class="errore">${testo(e.message)}</td></tr>`;
    }
}

// ── Identificativi tecnici ─────────────────────────────────

async function lookup(pulsante) {
    const lotto    = Number(document.getElementById('lk-lotto').value);
    const campione = Number(document.getElementById('lk-campione').value);
    const esito    = document.getElementById('esito-lookup');

    if (!lotto || !campione) {
        avvisa('Indicare sia il codice di lotto sia quello di campione.', 'info');
        return;
    }

    const libera = occupa(pulsante, 'Ricerca…');

    try {
        const dati = await chiamaApi(
            `/api/consultazione/identificativi?codiceLotto=${lotto}&codiceCampione=${campione}`);

        esito.innerHTML = `
            <dl class="scheda-dati scheda-tecnica">
                <div><dt>IdLotto</dt><dd class="codice">${dati.idLotto}</dd></div>
                <div><dt>IdCampione</dt><dd class="codice">${dati.idCampione}</dd></div>
                <div><dt>IdCommessa</dt><dd class="codice">${dati.idCommessa ?? '—'}</dd></div>
                <div><dt>IdControllo</dt><dd class="codice">${dati.idControllo ?? '—'}</dd></div>
                <div><dt>IdCertificato</dt><dd class="codice">${dati.idCertificato ?? '—'}</dd></div>
                <div><dt>N° certificato</dt><dd>${testo(dati.numeroCertificato) || '—'}</dd></div>
                <div><dt>Analisi chiusa</dt><dd>${dati.analisiChiusa ? 'Sì' : 'No'}</dd></div>
                <div><dt>Prove</dt><dd>${dati.proveVerificate} verificate su ${dati.numeroProve}</dd></div>
                <div class="larga"><dt>Laboratori esterni</dt>
                    <dd>${testo(dati.laboratoriEsterni) || 'nessuno'}</dd></div>
            </dl>`;

        // Mostra anche le singole prove del campione trovato
        mostraProve(dati.idCampione, `${lotto}/${campione}`);
    } catch (errore) {
        esito.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    } finally {
        libera();
    }
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    caricaAnagrafiche();

    document.getElementById('f-cliente').addEventListener('change', filtraInsediamenti);
    document.getElementById('btn-cerca').addEventListener('click', e => cerca(e.currentTarget));
    document.getElementById('btn-azzera').addEventListener('click', azzera);
    document.getElementById('btn-lookup').addEventListener('click', e => lookup(e.currentTarget));
    document.getElementById('btn-esporta').addEventListener('click', e => esporta(e.currentTarget));
    document.getElementById('btn-esporta-risultati')?.addEventListener('click',
        e => esporta(e.currentTarget, '/api/consultazione/esporta-risultati', 'RisultatiRDP.xlsx'));

    // Invio nei campi di testo avvia la ricerca
    document.querySelectorAll('.griglia-filtri input').forEach(campo =>
        campo.addEventListener('keydown', e => {
            if (e.key === 'Enter') document.getElementById('btn-cerca').click();
        }));
});
