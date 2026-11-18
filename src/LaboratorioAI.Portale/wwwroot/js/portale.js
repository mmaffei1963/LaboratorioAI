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
            credenziale,
            tokenDispositivo: leggiBrowser(codice)
        });

        // 18/11/2026: primo accesso da questo browser con la partita IVA → codice via email.
        if (esito.verifica) {
            document.getElementById('credenziale').value = '';
            apriVerifica(codice, esito);
            return;
        }

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
    // 17/11/2026: il cambio password è per i clienti, non per l'accesso operatore.
    document.getElementById('btn-password').classList.toggle('nascosto', !!sessione.operatore);

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

// ── Offerte (17/11/2026) ───────────────────────────────────

const euro = (v) => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';

function mostraScheda(id) {
    document.querySelectorAll('.scheda').forEach(b => b.classList.toggle('attiva', b.dataset.scheda === id));
    document.getElementById('sezione-campioni').classList.toggle('nascosto', id !== 'sezione-campioni');
    document.getElementById('sezione-offerte').classList.toggle('nascosto', id !== 'sezione-offerte');
    if (id === 'sezione-offerte') caricaOfferta();
}

function clienteOfferta() {
    const v = document.getElementById('scelta-cliente')?.value;
    return sessione?.operatore ? (v || null) : null;
}

async function caricaOfferta() {
    const stato = document.getElementById('offerta-stato');
    const corpo = document.getElementById('offerta-contenuto');
    const pdf = document.getElementById('btn-offerta-pdf');
    pdf.disabled = true;
    corpo.innerHTML = '';
    if (sessione?.operatore && !clienteOfferta()) {
        stato.textContent = 'Accesso operatore: scegli un cliente in alto per vedere la sua offerta.';
        return;
    }
    stato.textContent = 'Caricamento dell\'offerta…';
    try {
        const id = clienteOfferta();
        const o = await api('/api/offerte' + (id ? `?idCliente=${id}` : ''));
        stato.textContent = `Offerta di ${o.cliente ?? ''} — situazione al ${o.generataIl}`;
        const t = o.testi;
        let html = `<p class="oggetto">${testo(t.oggetto)}</p>` + t.presentazione.map(p => `<p>${testo(p)}</p>`).join('');

        html += `<h2>${testo(t.titoloAllegato)}</h2>`;
        html += o.righe.length === 0 ? '<p>Nessun incarico di analisi attivo.</p>' : `
            <div class="tabella-scorrevole"><table class="tabella">
                <thead><tr><th>Cod. Controllo</th><th>Incarico</th><th>Costo</th><th>Data incarico</th><th>Ult. modifica</th></tr></thead>
                <tbody>${o.righe.map(r => `<tr>
                    <td>${r.idControllo}</td>
                    <td>${testo(r.incarico)}</td>
                    <td class="importo">${euro(r.prezzo)}</td>
                    <td>${testo(r.dataIncarico ?? '—')}</td>
                    <td>${testo(r.ultimaModifica ?? '—')}${r.etichettaModifica ? `<span class="etichetta-modifica">${testo(r.etichettaModifica)}</span>` : ''}</td>
                </tr>`).join('')}</tbody>
            </table></div>
            <p class="piccolo">Prezzi IVA esclusa.</p>`;

        html += `<h2>${testo(t.titoloDettaglio)}</h2>`;
        for (const c of o.dettagli.controlli) {
            html += `<h3>${testo(c.titolo)}</h3>`;
            html += c.metodi.length === 0 ? '<p>Nessuna prova associata.</p>' : `
                <div class="tabella-scorrevole"><table class="tabella">
                    <thead><tr><th>Denominazione</th><th>Metodo</th><th>Accr.</th></tr></thead>
                    <tbody>${c.metodi.map(m => `<tr><td>${testo(m.denominazione)}</td><td>${testo(m.metodo)}</td><td>${testo(m.accr)}</td></tr>`).join('')}</tbody>
                </table></div>`;
        }
        html += o.dettagli.frasi.map(f => `<p class="piccolo">${testo(f)}</p>`).join('');
        if (o.dettagli.frasi.length) html += `<div class="nota-subappalto">${testo(t.notaSubappalto)}</div>`;

        // L'indirizzo delle Condizioni generali diventa un collegamento al sito (nuova scheda).
        const link = (p) => testo(p).replace(testo(t.indirizzoCondizioni),
            `<a href="${testo(t.indirizzoCondizioni)}" target="_blank" rel="noopener">${testo(t.indirizzoCondizioni)}</a>`);
        for (const c of t.condizioni)
            html += `<h3>${testo(c.titolo)}</h3>` + c.paragrafi.map(p => `<p>${link(p)}</p>`).join('');

        corpo.innerHTML = html;
        pdf.disabled = false;
    } catch (e) {
        stato.textContent = e.message;
    }
}

async function scaricaOffertaPdf() {
    const pulsante = document.getElementById('btn-offerta-pdf');
    pulsante.disabled = true;
    try {
        const token = sessionStorage.getItem(CHIAVE_TOKEN);
        const id = clienteOfferta();
        const risposta = await fetch('/api/offerte/pdf' + (id ? `?idCliente=${id}` : ''), {
            headers: token ? { [INTESTAZIONE]: token } : {}
        });
        if (risposta.status === 401 && token) { esci(false); throw new Error('Sessione scaduta.'); }
        if (!risposta.ok) throw new Error(`Errore ${risposta.status}`);
        const blob = await risposta.blob();
        const cd = risposta.headers.get('content-disposition') || '';
        const nome = (cd.match(/filename="?([^";]+)"?/) || [])[1] || 'Offerta_laboratorio.pdf';
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = nome;
        a.click();
        URL.revokeObjectURL(a.href);
    } catch (e) {
        document.getElementById('offerta-stato').textContent = e.message;
    } finally {
        pulsante.disabled = false;
    }
}

// ── Verifica del browser nuovo (18/11/2026) ────────────────

const CHIAVE_BROWSER = 'labai-portale-browser-';
let verificaInCorso = null;

function leggiBrowser(codice) {
    try { return localStorage.getItem(CHIAVE_BROWSER + codice.trim().toUpperCase()) || null; } catch { return null; }
}

function salvaBrowser(codice, token) {
    try { if (token) localStorage.setItem(CHIAVE_BROWSER + codice.trim().toUpperCase(), token); } catch { /* facoltativo */ }
}

function apriVerifica(codice, esito) {
    verificaInCorso = { codice, token: esito.tokenVerifica };
    document.getElementById('ver-avviso').textContent = esito.messaggio;
    document.getElementById('ver-codice').value = '';
    document.getElementById('btn-ver-conferma').disabled = false;
    messaggio('ver-messaggio', '');
    document.getElementById('dlg-verifica').showModal();
    document.getElementById('ver-codice').focus();
}

async function confermaVerifica() {
    if (!verificaInCorso) return;
    const pulsante = document.getElementById('btn-ver-conferma');
    pulsante.disabled = true;
    try {
        const esito = await invia('/api/accesso/verifica', {
            tokenVerifica: verificaInCorso.token,
            codice: document.getElementById('ver-codice').value.trim(),
            ricordaBrowser: document.getElementById('ver-ricorda').checked
        });
        salvaBrowser(verificaInCorso.codice, esito.tokenDispositivo);
        if (document.getElementById('ricorda').checked) localStorage.setItem(CHIAVE_CODICE, verificaInCorso.codice);
        sessionStorage.setItem(CHIAVE_TOKEN, esito.token);
        verificaInCorso = null;
        document.getElementById('dlg-verifica').close();
        sessione = esito;
        await mostraApplicazione();
        return;
    } catch (e) { messaggio('ver-messaggio', e.message); }
    pulsante.disabled = false;
}

// ── Password (17/11/2026) ──────────────────────────────────

function messaggio(id, testoMsg, ok) {
    const el = document.getElementById(id);
    el.textContent = testoMsg || '';
    el.className = 'messaggio-finestra ' + (ok ? 'ok-msg' : 'errore-msg');
}

function apriRecupero() {
    document.getElementById('rec-codice').value = document.getElementById('codice').value.trim();
    document.getElementById('recupero-passo1').classList.remove('nascosto');
    document.getElementById('recupero-passo2').classList.add('nascosto');
    ['rec-token', 'rec-nuova', 'rec-ripetuta'].forEach(id => document.getElementById(id).value = '');
    messaggio('rec-messaggio', '');
    document.getElementById('btn-rec-conferma').disabled = false;
    document.getElementById('dlg-recupero').showModal();
}

async function inviaCodiceRecupero() {
    const pulsante = document.getElementById('btn-rec-invia');
    pulsante.disabled = true;
    try {
        const r = await invia('/api/password/recupero/richiedi', { codiceContabilita: document.getElementById('rec-codice').value.trim() });
        document.getElementById('rec-avviso').textContent = r.messaggio;
        document.getElementById('recupero-passo1').classList.add('nascosto');
        document.getElementById('recupero-passo2').classList.remove('nascosto');
        messaggio('rec-messaggio', '');
        document.getElementById('rec-token').focus();
    } catch (e) { messaggio('rec-messaggio', e.message); }
    finally { pulsante.disabled = false; }
}

async function confermaRecupero() {
    const pulsante = document.getElementById('btn-rec-conferma');
    pulsante.disabled = true;
    try {
        const r = await invia('/api/password/recupero/conferma', {
            codiceContabilita: document.getElementById('rec-codice').value.trim(),
            tokenRecupero: document.getElementById('rec-token').value.trim(),
            passwordNuova: document.getElementById('rec-nuova').value,
            passwordRipetuta: document.getElementById('rec-ripetuta').value
        });
        ['rec-token', 'rec-nuova', 'rec-ripetuta'].forEach(id => document.getElementById(id).value = '');
        document.getElementById('codice').value = document.getElementById('rec-codice').value.trim();
        messaggio('rec-messaggio', r.messaggio, true);
        document.getElementById('btn-rec-conferma').disabled = true;
        setTimeout(() => { document.getElementById('dlg-recupero').close(); document.getElementById('credenziale').focus(); }, 2500);
        return;
    } catch (e) { messaggio('rec-messaggio', e.message); }
    pulsante.disabled = false;
}

function apriCambioPassword() {
    ['pw-attuale', 'pw-nuova', 'pw-ripetuta'].forEach(id => document.getElementById(id).value = '');
    messaggio('pw-messaggio', '');
    document.getElementById('btn-pw-salva').disabled = false;
    document.getElementById('dlg-password').showModal();
}

async function salvaPassword() {
    const pulsante = document.getElementById('btn-pw-salva');
    pulsante.disabled = true;
    try {
        const r = await invia('/api/password/cambia', {
            passwordAttuale: document.getElementById('pw-attuale').value,
            passwordNuova: document.getElementById('pw-nuova').value,
            passwordRipetuta: document.getElementById('pw-ripetuta').value
        });
        ['pw-attuale', 'pw-nuova', 'pw-ripetuta'].forEach(id => document.getElementById(id).value = '');
        messaggio('pw-messaggio', r.messaggio, true);
        return;
    } catch (e) { messaggio('pw-messaggio', e.message); }
    pulsante.disabled = false;
}

// Operatore: stato delle credenziali del cliente scelto e ripristino della partita IVA.
async function statoPasswordCliente() {
    const stato = document.getElementById('stato-password');
    const btn = document.getElementById('btn-ripristina');
    const id = document.getElementById('scelta-cliente')?.value;
    stato.classList.add('nascosto'); btn.classList.add('nascosto');
    if (!sessione?.operatore || !id) return;
    try {
        const r = await api(`/api/password/stato?idCliente=${id}`);
        stato.textContent = r.personale ? `Il cliente usa una password personale (dal ${r.dal}).` : 'Il cliente entra con la partita IVA.';
        stato.classList.remove('nascosto');
        btn.classList.toggle('nascosto', !r.personale);
    } catch { /* informazione facoltativa */ }
}

async function ripristinaCredenziali() {
    const id = document.getElementById('scelta-cliente')?.value;
    if (!id) return;
    const nome = document.getElementById('scelta-cliente').selectedOptions[0]?.textContent ?? '';
    if (!window.confirm(`Ripristinare le credenziali originali di ${nome}? Il cliente tornerà a entrare con la partita IVA e la sua password personale non varrà più.`)) return;
    try {
        const r = await invia('/api/password/ripristina', { idCliente: Number(id) });
        document.getElementById('stato-password').textContent = r.messaggio;
        document.getElementById('btn-ripristina').classList.add('nascosto');
    } catch (e) { document.getElementById('stato-password').textContent = e.message; }
}

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
        if (!document.getElementById('sezione-offerte').classList.contains('nascosto')) caricaOfferta();
        statoPasswordCliente();
    });

    document.getElementById('btn-dimenticata').addEventListener('click', apriRecupero);
    document.getElementById('btn-ver-conferma').addEventListener('click', confermaVerifica);
    document.getElementById('ver-codice').addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); confermaVerifica(); } });
    document.getElementById('btn-rec-invia').addEventListener('click', inviaCodiceRecupero);
    document.getElementById('btn-rec-conferma').addEventListener('click', confermaRecupero);
    document.getElementById('btn-password').addEventListener('click', apriCambioPassword);
    document.getElementById('btn-pw-salva').addEventListener('click', salvaPassword);
    document.getElementById('btn-ripristina').addEventListener('click', ripristinaCredenziali);
    document.querySelectorAll('dialog [data-chiudi]').forEach(b => b.addEventListener('click', () => b.closest('dialog').close()));

    document.querySelectorAll('.scheda').forEach(b => b.addEventListener('click', () => mostraScheda(b.dataset.scheda)));
    document.getElementById('btn-offerta-pdf').addEventListener('click', scaricaOffertaPdf);

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
