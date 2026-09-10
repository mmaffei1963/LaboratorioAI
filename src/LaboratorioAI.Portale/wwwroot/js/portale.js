/* ═══════════════════════════════════════════════════════════
   Portale Clienti MARE.A
   ═══════════════════════════════════════════════════════════ */

const CHIAVE_TOKEN  = 'marea_sessione';
const CHIAVE_CODICE = 'marea_codice';
const INTESTAZIONE  = 'X-Sessione-Portale';

let sessione = null;
let insediamentiTutti = [];

// ── Comunicazione ──────────────────────────────────────────

/**
 * Il token viaggia in un'intestazione dedicata, non in un cookie:
 * evita la superficie di attacco delle richieste cross-site.
 */
async function api(url, opzioni = {}) {
    const token = sessionStorage.getItem(CHIAVE_TOKEN);

    const risposta = await fetch(url, {
        ...opzioni,
        headers: {
            'Accept': 'application/json',
            ...(opzioni.body ? { 'Content-Type': 'application/json' } : {}),
            ...(token ? { [INTESTAZIONE]: token } : {}),
            ...(opzioni.headers || {})
        }
    });

    // Una sessione scaduta si riconosce solo se un token era stato inviato:
    // in fase di accesso il 401 significa credenziali errate, non sessione
    // scaduta, e il messaggio del server va mostrato così com'è.
    if (risposta.status === 401 && token) {
        esci(false);
        throw new Error('Sessione scaduta.');
    }

    if (!risposta.ok) {
        let messaggio = `Errore ${risposta.status}`;
        try { messaggio = (await risposta.json()).messaggio ?? messaggio; } catch { }
        throw new Error(messaggio);
    }

    return risposta.status === 204 ? null : risposta.json();
}

const invia = (url, corpo) => api(url, { method: 'POST', body: JSON.stringify(corpo) });

function testo(valore) {
    if (valore === null || valore === undefined) return '';
    return String(valore)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function etichettaStato(stato) {
    const mappa = {
        Certificato:      ['verde',   'Certificato'],
        InCorso:          ['arancio', 'In corso'],
        SpeditoOriginale: ['viola',   'Spedito in originale']
    };
    const [classe, etichetta] = mappa[stato] ?? ['grigio', stato];
    return `<span class="stato stato-${classe}">${etichetta}</span>`;
}

// ── Accesso ────────────────────────────────────────────────

async function entra(evento) {
    evento.preventDefault();

    const codice      = document.getElementById('codice').value.trim();
    const credenziale = document.getElementById('credenziale').value;
    const errore      = document.getElementById('errore-accesso');
    const pulsante    = document.getElementById('btn-entra');

    errore.classList.add('nascosto');
    pulsante.disabled = true;
    pulsante.textContent = 'Accesso in corso…';

    try {
        // Le credenziali viaggiano nel corpo della richiesta, mai nell'indirizzo
        const esito = await invia('/api/accesso/entra', {
            codiceContabilita: codice,
            credenziale
        });

        sessionStorage.setItem(CHIAVE_TOKEN, esito.token);

        if (document.getElementById('ricorda').checked) {
            localStorage.setItem(CHIAVE_CODICE, codice);
        } else {
            localStorage.removeItem(CHIAVE_CODICE);
        }

        // Il campo viene svuotato subito: il valore non deve restare
        // nel documento più del necessario
        document.getElementById('credenziale').value = '';

        sessione = esito;
        await mostraApplicazione();

    } catch (e) {
        errore.textContent = e.message;
        errore.classList.remove('nascosto');
    } finally {
        pulsante.disabled = false;
        pulsante.textContent = 'Accedi';
    }
}

async function esci(chiamaServer = true) {
    if (chiamaServer) {
        try { await api('/api/accesso/esci', { method: 'POST' }); } catch { }
    }

    sessionStorage.removeItem(CHIAVE_TOKEN);
    sessione = null;

    document.getElementById('applicazione').classList.add('nascosto');
    document.getElementById('schermata-accesso').classList.remove('nascosto');
}

// ── Applicazione ───────────────────────────────────────────

async function mostraApplicazione() {
    document.getElementById('schermata-accesso').classList.add('nascosto');
    document.getElementById('applicazione').classList.remove('nascosto');
    document.getElementById('nome-cliente').textContent = sessione.ragioneSociale ?? '';

    if (sessione.operatore) {
        document.getElementById('barra-operatore').classList.remove('nascosto');
        await caricaClienti();
    }

    impostaPeriodoPredefinito();
    await caricaFiltri();
    await cerca();
}

function impostaPeriodoPredefinito() {
    const oggi = new Date();
    const seiMesiFa = new Date(oggi);
    seiMesiFa.setMonth(oggi.getMonth() - 6);

    document.getElementById('f-a').value  = oggi.toISOString().slice(0, 10);
    document.getElementById('f-da').value = seiMesiFa.toISOString().slice(0, 10);
}

async function caricaClienti() {
    const clienti = await api('/api/campioni/clienti');
    const select = document.getElementById('scelta-cliente');
    select.innerHTML = '<option value="">Tutti i clienti</option>' +
        clienti.map(c => `<option value="${c.id}">${testo(c.nome)}</option>`).join('');
}

async function caricaFiltri() {
    const idCliente = document.getElementById('scelta-cliente')?.value;
    const url = idCliente ? `/api/campioni/filtri?idCliente=${idCliente}` : '/api/campioni/filtri';

    const filtri = await api(url);
    insediamentiTutti = filtri.insediamenti;

    popola('f-insediamento', filtri.insediamenti, 'Tutte');
    popola('f-matrice',      filtri.matrici,      'Tutte');
    popola('f-controllo',    filtri.controlli,    'Tutti');
}

function popola(id, voci, etichettaVuota) {
    const select = document.getElementById(id);
    select.innerHTML = `<option value="">${etichettaVuota}</option>` +
        voci.map(v => `<option value="${v.id}">${testo(v.nome)}</option>`).join('');
}

// ── Ricerca ────────────────────────────────────────────────

function leggiCriteri() {
    const valore = id => document.getElementById(id).value || null;
    const numero = id => Number(document.getElementById(id).value) || null;

    return {
        dataDa: valore('f-da'),
        dataA:  valore('f-a'),
        idCliente:      numero('scelta-cliente'),
        idInsediamento: numero('f-insediamento'),
        idMatrice:      numero('f-matrice'),
        idControllo:    numero('f-controllo'),
        luogoPrelievo:   valore('f-luogo'),
        puntoPrelievo:   valore('f-punto'),
        verbalePrelievo: valore('f-verbale'),
        testoLibero:     valore('f-testo'),
        stato: valore('f-stato'),
        limite: 200
    };
}

async function cerca() {
    const pulsante = document.getElementById('btn-cerca');
    pulsante.disabled = true;

    try {
        const esito = await invia('/api/campioni/cerca', leggiCriteri());

        document.getElementById('s-totale').textContent      = esito.sintesi.totale;
        document.getElementById('s-certificati').textContent = esito.sintesi.certificati;
        document.getElementById('s-corso').textContent       = esito.sintesi.inCorso;

        document.getElementById('conteggio').textContent =
            esito.sintesi.totale === 0
                ? 'Nessun campione corrisponde ai criteri impostati.'
                : `${esito.sintesi.totale} campioni`;

        document.getElementById('avviso-troncato')
            .classList.toggle('nascosto', !esito.troncato);

        disegna(esito.campioni);

    } catch (e) {
        document.getElementById('conteggio').textContent = e.message;
    } finally {
        pulsante.disabled = false;
    }
}

async function esporta() {
    const pulsante = document.getElementById('btn-esporta');
    pulsante.disabled = true;
    try {
        const token = sessionStorage.getItem(CHIAVE_TOKEN);
        const risposta = await fetch('/api/campioni/esporta', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { [INTESTAZIONE]: token } : {})
            },
            body: JSON.stringify(leggiCriteri())
        });
        if (risposta.status === 401 && token) { esci(false); throw new Error('Sessione scaduta.'); }
        if (!risposta.ok) throw new Error(`Errore ${risposta.status}`);

        const blob = await risposta.blob();
        const cd = risposta.headers.get('content-disposition') || '';
        const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || 'Campioni.xlsx';
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = nome;
        a.click();
        URL.revokeObjectURL(a.href);
    } catch (e) {
        document.getElementById('conteggio').textContent = e.message;
    } finally {
        pulsante.disabled = false;
    }
}

function disegna(campioni) {
    const cliccabile = sessione?.operatore === true;

    document.querySelector('#tabella-campioni tbody').innerHTML = campioni.map(c => `
        <tr class="${cliccabile ? 'riga-cliccabile' : ''}"
            ${cliccabile ? `data-id="${c.idCampione}" data-numero="${testo(c.numeroCampione)}"` : ''}>
            <td>${etichettaStato(c.stato)}</td>
            <td class="codice">${testo(c.numeroCampione)}</td>
            <td>${testo(c.dataRicezione) || '—'}</td>
            <td>${testo(c.dataPrelievo) || '—'}</td>
            <td>${testo(c.nomeInsediamento) || '—'}</td>
            <td>${testo(c.denominazioneCampione)}</td>
            <td>${testo(c.matrice) || '—'}</td>
            <td>${testo(c.controllo) || '—'}</td>
            <td>${testo(c.luogoPrelievo) || '—'}</td>
            <td>${testo(c.puntoPrelievo) || '—'}</td>
            <td>${testo(c.verbalePrelievo) || '—'}</td>
        </tr>`).join('');

    if (cliccabile) {
        document.querySelectorAll('#tabella-campioni .riga-cliccabile').forEach(riga =>
            riga.addEventListener('click', () =>
                mostraProve(Number(riga.dataset.id), riga.dataset.numero)));
    }
}

// ── Dettaglio prove (solo operatore) ───────────────────────

async function mostraProve(idCampione, numero) {
    const pannello = document.getElementById('pannello-prove');
    pannello.classList.remove('nascosto');
    document.getElementById('titolo-prove').textContent = `Campione ${numero}`;
    pannello.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    const corpo = document.querySelector('#tabella-prove tbody');
    corpo.innerHTML = '<tr><td colspan="7" class="attesa">Caricamento…</td></tr>';

    try {
        const prove = await api(`/api/campioni/${idCampione}/prove`);

        corpo.innerHTML = prove.map(p => `
            <tr>
                <td><strong>${testo(p.parametro)}</strong>
                    ${p.accreditato ? '<span class="accreditato" title="Metodo accreditato">A</span>' : ''}</td>
                <td class="numerico">${testo(p.valore)}</td>
                <td>${testo(p.unitaMisura) || '—'}</td>
                <td class="numerico">${testo(p.intervallo) || '—'}</td>
                <td class="sottotesto">${testo(p.riferimentoNormativo) || '—'}</td>
                <td>${p.verificaValore === true  ? '<span class="ok">Verificato</span>'
                    : p.verificaValore === false ? '<span class="ko">Non verificato</span>'
                    : '<span class="neutro">—</span>'}</td>
                <td class="sottotesto">${testo(p.laboratorioEsterno) || 'MARE.A'}</td>
            </tr>`).join('');

    } catch (e) {
        corpo.innerHTML = `<tr><td colspan="7" class="errore">${testo(e.message)}</td></tr>`;
    }
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    const codiceSalvato = localStorage.getItem(CHIAVE_CODICE);
    if (codiceSalvato) {
        document.getElementById('codice').value = codiceSalvato;
        document.getElementById('ricorda').checked = true;
        document.getElementById('credenziale').focus();
    }

    // Ripristino della sessione dopo un ricaricamento della pagina
    if (sessionStorage.getItem(CHIAVE_TOKEN)) {
        try {
            sessione = await api('/api/accesso/stato');
            await mostraApplicazione();
        } catch { /* sessione non più valida: resta la schermata di accesso */ }
    }

    document.getElementById('modulo-accesso').addEventListener('submit', entra);
    document.getElementById('btn-esci').addEventListener('click', () => esci(true));
    document.getElementById('btn-cerca').addEventListener('click', cerca);
    document.getElementById('btn-esporta').addEventListener('click', esporta);

    document.getElementById('scelta-cliente')?.addEventListener('change', async () => {
        await caricaFiltri();
        await cerca();
    });

    document.getElementById('chiudi-prove').addEventListener('click', () =>
        document.getElementById('pannello-prove').classList.add('nascosto'));

    document.getElementById('btn-azzera').addEventListener('click', async () => {
        document.querySelectorAll('.griglia-filtri input').forEach(c => c.value = '');
        document.querySelectorAll('.griglia-filtri select').forEach(s => s.value = '');
        impostaPeriodoPredefinito();
        await cerca();
    });

    document.querySelectorAll('.griglia-filtri input').forEach(campo =>
        campo.addEventListener('keydown', e => { if (e.key === 'Enter') cerca(); }));
});
