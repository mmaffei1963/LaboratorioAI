/* ═══════════════════════════════════════════════════════════
   Area Validazione
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaGiudizio,
         avvisa, occupa } from '../comune.js';

let campioni = [];
let selezionato = null;
// 29/10/2026: analisi AI in corso, per campione. Il risultato va sempre al campione analizzato, anche se nel
// frattempo se ne è aperto un altro (prima finiva nel campione aperto in quel momento).
const analisiInCorso = new Set();
// Limiti normativi fase 1b (18/10/2026): colori calcolati sulle schede limite validate. Separati dal giudizio AI.
let segnalazioni = {};
// Validazione da LaboratorioAI (18/10/2026): giudizio + chiusura analisi da riportare in ArcLab (via Palomar).
let validati = {};
const STATI_VALIDAZIONE = {
    DaRiportare: ['Validato · attesa ArcLab', 'val-attesa'],
    Riportato: ['In ArcLab', 'val-ok'],
    Conflitto: ['Giudizio diverso in ArcLab', 'val-conflitto'],
};
// 26/10/2026: «azzurro» = nessuna scheda limite applicabile al campione (non è «Nei limiti»).
const ORDINE_COLORI = { viola: 0, rosso: 1, arancione: 2, grigio: 3, verde: 4, azzurro: 5 };
const NOMI_COLORI = {
    viola: 'Da correggere', rosso: 'Limite superato', arancione: 'Da segnalare', grigio: 'Non confrontabile', verde: 'Nei limiti',
    azzurro: 'Nessun limite di legge'
};

// ── Elenco ─────────────────────────────────────────────────

async function caricaElenco(aggiorna = false) {
    const elenco = document.getElementById('elenco-campioni');
    elenco.innerHTML = '<p class="attesa">Caricamento…</p>';

    try {
        campioni = await chiamaApi('/api/validazione/campioni');
        disegnaElenco();
        aggiornaRiepilogo();
        caricaSegnalazioni(aggiorna);
        caricaValidati();
    } catch (errore) {
        elenco.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    }
}

async function caricaSegnalazioni(aggiorna) {
    const stato = document.getElementById('stato-limiti');
    stato.textContent = 'Controllo dei limiti normativi in corso…';
    try {
        segnalazioni = await chiamaApi('/api/validazione/segnalazioni' + (aggiorna ? '?aggiorna=true' : ''));
        const conteggi = {};
        campioni.forEach(c => { const col = segnalazioni[c.idCampione]?.colore; if (col) conteggi[col] = (conteggi[col] || 0) + 1; });
        stato.innerHTML = Object.keys(ORDINE_COLORI).filter(k => conteggi[k])
            .map(k => `<span class="limiti limiti-${k}">${conteggi[k]} ${NOMI_COLORI[k].toLowerCase()}</span>`).join(' ');
        disegnaElenco();
        if (selezionato) disegnaSegnalazioni(selezionato);
    } catch (errore) {
        segnalazioni = {};
        stato.textContent = errore.message;
    }
}

async function caricaValidati() {
    try {
        const righe = await chiamaApi('/api/validazione/validati');
        validati = {};
        righe.forEach(r => validati[r.idCampione] = r);
        disegnaElenco();
    } catch { validati = {}; }
}

function etichettaValidazione(idCampione) {
    const v = validati[idCampione];
    if (!v || !STATI_VALIDAZIONE[v.stato]) return '';
    const [nome, classe] = STATI_VALIDAZIONE[v.stato];
    return `<span class="limiti ${classe}" title="${testo(`${v.codGiudizio} — ${v.validatoDa}, ${v.dataValidazione}`)}">${nome}</span>`;
}

function etichettaLimiti(idCampione) {
    const s = segnalazioni[idCampione];
    if (!s) return '';
    const n = raggruppaRilievi(s.rilievi).length;
    return `<span class="limiti limiti-${s.colore}" title="${testo(s.rilievi.map(r => r.testo).join('\n'))}">`
        + `${NOMI_COLORI[s.colore]}${n > 1 ? ` (${n})` : ''}</span>`;
}

function aggiornaRiepilogo() {
    const conAnalisi = campioni.filter(c => c.analisi).length;
    document.getElementById('riepilogo').textContent =
        `${campioni.length} campioni in attesa di validazione, ${conAnalisi} già analizzati.`;
}

function disegnaElenco() {
    const filtro = document.getElementById('filtro-elenco').value.trim().toLowerCase();
    const elenco = document.getElementById('elenco-campioni');

    const colore = document.getElementById('filtro-colore').value;
    const perGravita = document.getElementById('ordina-elenco').value === 'gravita';

    let visibili = filtro
        ? campioni.filter(c => [c.ragioneSociale, c.denominazioneCampione,
                                c.matrice, c.numeroCampione]
            .some(v => v?.toLowerCase().includes(filtro)))
        : campioni;
    if (colore) visibili = visibili.filter(c => segnalazioni[c.idCampione]?.colore === colore);
    if (perGravita) {
        const peso = c => ORDINE_COLORI[segnalazioni[c.idCampione]?.colore] ?? 6;
        visibili = visibili.map((c, i) => [c, i]).sort((a, b) => peso(a[0]) - peso(b[0]) || a[1] - b[1]).map(x => x[0]);
    }

    if (visibili.length === 0) {
        // 19/10/2026: si dice quale filtro svuota l'elenco e si può toglierlo con un clic
        const attivi = [];
        if (filtro) attivi.push(`testo «${testo(filtro)}»`);
        if (colore) attivi.push(`colore «${testo(colore)}»`);
        elenco.innerHTML = campioni.length === 0
            ? '<p class="vuoto">Nessun campione in attesa di validazione.</p>'
            : `<p class="vuoto">Nessuno dei ${campioni.length} campioni corrisponde al filtro${attivi.length ? ' (' + attivi.join(', ') + ')' : ''}.</p>
               <button type="button" class="pulsante pulsante-tenue" id="togli-filtri">Togli i filtri</button>`;
        document.getElementById('togli-filtri')?.addEventListener('click', () => {
            document.getElementById('filtro-elenco').value = '';
            document.getElementById('filtro-colore').value = '';
            disegnaElenco();
        });
        return;
    }

    elenco.innerHTML = visibili.map(c => `
        <article class="voce-elenco ${selezionato === c.idCampione ? 'selezionata' : ''} ${segnalazioni[c.idCampione] ? 'bordo-' + segnalazioni[c.idCampione].colore : ''}"
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
                ${etichettaValidazione(c.idCampione)}
                ${etichettaLimiti(c.idCampione)}
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
        if (selezionato !== idCampione) return;   // 29/10/2026: nel frattempo è stato aperto un altro campione
        disegnaIntestazione(dati.campione);
        disegnaProve(dati.prove);
        disegnaSegnalazioni(idCampione);
        caricaGiudizio(idCampione);
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

function disegnaSegnalazioni(idCampione) {
    const box = document.getElementById('segnalazioni-limiti');
    const s = segnalazioni[idCampione];
    document.querySelectorAll('#tabella-prove tbody tr').forEach(tr => tr.className = '');
    if (!s) { box.classList.add('nascosto'); return; }
    box.className = `riquadro-limiti limiti-bordo-${s.colore}`;
    box.innerHTML = s.colore === 'azzurro'
        ? `<strong>Limiti normativi:</strong> nessuna scheda limite validata si applica a questo campione: non c'è un limite di legge con cui confrontarlo.`
        : s.rilievi.length === 0
        ? `<strong>Limiti normativi:</strong> nessun rilievo sulle schede limite validate.`
        : `<strong>Limiti normativi</strong> <span class="sottotesto">(controllo automatico sulle schede validate, separato dal giudizio AI)</span>
           <ul>${raggruppaRilievi(s.rilievi).map(r => `<li><span class="limiti limiti-${r.colore}">${NOMI_COLORI[r.colore]}</span> ${testo(r.testo)}${r.volte > 1 ? ` <strong>(${r.volte} prove)</strong>` : ''}</li>`).join('')}</ul>`;
    // evidenzia le prove con rilievi (il colore più grave per prova)
    const perProva = {};
    s.rilievi.filter(r => r.idProva).forEach(r => {
        if (!perProva[r.idProva] || ORDINE_COLORI[r.colore] < ORDINE_COLORI[perProva[r.idProva]]) perProva[r.idProva] = r.colore;
    });
    document.querySelectorAll('#tabella-prove tbody tr').forEach(tr => {
        const col = perProva[tr.dataset.idProva];
        if (col) tr.className = `riga-limiti-${col}`;
    });
}

// ── Validazione (giudizio) ─────────────────────────────────

async function caricaGiudizio(idCampione) {
    const box = document.getElementById('riquadro-validazione');
    box.classList.remove('nascosto');
    box.innerHTML = '<p class="attesa">Lettura dello stato in ArcLab…</p>';
    try {
        const g = await chiamaApi(`/api/validazione/campione/${idCampione}/giudizio`);
        if (selezionato !== idCampione) return;
        disegnaGiudizio(idCampione, g);
    } catch (errore) {
        box.innerHTML = `<p class="errore">${testo(errore.message)}</p>`;
    }
}

function disegnaGiudizio(idCampione, g) {
    const box = document.getElementById('riquadro-validazione');
    const a = g.arcLab;
    const chip = (ok, testoSi, testoNo) => `<span class="limiti ${ok ? 'val-ok' : 'limiti-grigio'}">${ok ? testoSi : testoNo}</span>`;
    let html = `<div class="val-testata"><strong>Validazione</strong>`;
    if (a) {
        html += ` <span class="sottotesto">in ArcLab:</span>
            ${chip(a.giudizi.length > 0, 'Giudizio: ' + testo(a.giudizi.map(x => x.codGiudizio).join(', ')), 'Giudizio: nessuno')}
            ${chip(a.analisiChiusa, 'Analisi chiusa', 'Analisi aperta')}
            <span class="sottotesto">· cliente ${a.giudizioConf ? '<strong>richiede</strong> il giudizio di conformità' : 'non richiede il giudizio di conformità'}</span>`;
    }
    html += '</div>';

    const v = g.validazione;
    if (v) {
        const [nome, classe] = STATI_VALIDAZIONE[v.stato] || [v.stato, 'limiti-grigio'];
        html += `<p>Validato da <strong>${testo(v.validatoDa)}</strong> il ${testo(v.dataValidazione)} con
            <strong>«${testo(v.codGiudizio)}»</strong>${v.automatico ? ' (212 automatico)' : ''}
            <span class="limiti ${classe}">${nome}</span>
            ${v.stato === 'Riportato' && v.dataRiportato ? `<span class="sottotesto">· visto in ArcLab il ${testo(v.dataRiportato)}</span>` : ''}</p>
            ${v.stato === 'Conflitto' ? '<p class="errore">In ArcLab c\'è un giudizio diverso da quello validato qui: controllare in ArcLab.</p>' : ''}
            ${v.stato === 'DaRiportare' && g.puoValidare ? '<button class="pulsante pulsante-tenue" id="btn-annulla-validazione">Annulla validazione</button>' : ''}`;
    } else if (g.motivoBlocco) {
        html += `<p class="sottotesto">${testo(g.motivoBlocco)}</p>`;
    } else if (!g.puoValidare) {
        html += '<p class="sottotesto">Da validare (la validazione la fanno i responsabili).</p>';
    } else {
        const obbligatorio = a?.giudizioConf;
        const opzioni = g.proposti.map(p => `<option value="${p.idGiudizio}" title="${testo(p.giudizio)}">${testo(p.codGiudizio)}${p.giudizioConformita ? ' — conformità' : ''}</option>`).join('');
        html += `<div class="val-scelta">
            <select id="scelta-giudizio">
                <option value="">${obbligatorio ? '— scegli il giudizio (obbligatorio) —' : '— nessuna scelta: 212 «Senza giudizio» in automatico —'}</option>
                ${opzioni}
            </select>
            <button class="pulsante" id="btn-valida" ${obbligatorio ? 'disabled' : ''}>Valida</button>
        </div>
        <p class="sottotesto" id="testo-giudizio"></p>
        ${g.coloreLimiti === 'rosso' || g.coloreLimiti === 'viola' ? `<p class="sottotesto">Campione segnalato in <span class="limiti limiti-${g.coloreLimiti}">${NOMI_COLORI[g.coloreLimiti]}</span>: alla validazione verrà chiesta una conferma.</p>` : ''}`;
    }
    box.innerHTML = html;

    const sel = document.getElementById('scelta-giudizio');
    if (sel) {
        sel.addEventListener('change', () => {
            const p = g.proposti.find(x => x.idGiudizio === Number(sel.value));
            document.getElementById('testo-giudizio').textContent = p ? p.giudizio : '';
            document.getElementById('btn-valida').disabled = a?.giudizioConf && !sel.value;
        });
        document.getElementById('btn-valida').addEventListener('click', e => valida(idCampione, sel.value ? Number(sel.value) : null, e.currentTarget));
    }
    document.getElementById('btn-annulla-validazione')?.addEventListener('click', () => annullaValidazione(idCampione, v.id));
}

async function valida(idCampione, idGiudizio, pulsante, confermaColore = false) {
    const libera = occupa(pulsante, 'Validazione…');
    try {
        const r = await inviaJson('/api/validazione/valida', { idCampione, idGiudizio, confermaColore });
        if (r.chiedeConferma) {
            libera();
            if (confirm(r.messaggio)) return valida(idCampione, idGiudizio, pulsante, true);
            return;
        }
        avvisa(r.messaggio, r.riuscita ? 'info' : 'errore');
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
    await caricaGiudizio(idCampione);
    await caricaValidati();
}

async function annullaValidazione(idCampione, id) {
    const nota = prompt('Annullare la validazione? Motivo (facoltativo):');
    if (nota === null) return;
    try {
        const r = await inviaJson('/api/validazione/annulla', { id, nota });
        avvisa(r.messaggio, r.riuscita ? 'info' : 'errore');
    } catch (errore) { avvisa(errore.message, 'errore'); }
    await caricaGiudizio(idCampione);
    await caricaValidati();
}

function disegnaProve(prove) {
    const corpo = document.querySelector('#tabella-prove tbody');

    corpo.innerHTML = prove.map(p => {
        const esito = p.verificaValore === true  ? '<span class="esito-ok">Verificato</span>'
                    : p.verificaValore === false ? '<span class="esito-ko">Non verificato</span>'
                    : '<span class="esito-neutro">—</span>';

        return `<tr data-id-prova="${p.idProva}">
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
    aggiornaPulsantiAnalisi();
}

// 29/10/2026: i pulsanti di analisi riflettono lo stato del campione aperto, non dell'ultimo campione analizzato.
function aggiornaPulsantiAnalisi() {
    const occupato = selezionato != null && analisiInCorso.has(selezionato);
    for (const [id, testoBase] of [['btn-analizza', 'Analizza con AI'], ['btn-rianalizza', 'Riesegui'], ['btn-archivio', 'Usa analisi archiviata']]) {
        const b = document.getElementById(id);
        if (!b) continue;
        if (!b.dataset.testoBase) b.dataset.testoBase = b.textContent.trim() || testoBase;
        b.disabled = occupato;
        b.textContent = occupato ? 'Analisi in corso…' : b.dataset.testoBase;
    }
}

// ── Analisi ────────────────────────────────────────────────

async function analizza(pulsante, forza) {
    // 29/10/2026: si lavora sempre sul campione su cui si è premuto, memorizzato qui: se intanto se ne apre un
    // altro, il risultato non deve finire lì.
    const idCampione = selezionato;
    if (!idCampione || analisiInCorso.has(idCampione)) return;
    const campione = campioni.find(c => c.idCampione === idCampione);
    const numero = campione?.numeroCampione ?? idCampione;

    analisiInCorso.add(idCampione);
    aggiornaPulsantiAnalisi();

    try {
        const esito = await inviaJson('/api/validazione/analizza', {
            idCampione,
            modalita: 1,
            forzaRiesecuzione: forza
        });

        // Aggiorna la scheda nell'elenco senza ricaricare tutto
        if (campione) {
            campione.analisi = {
                idAnalisi: esito.idAnalisi,
                data: esito.data,
                giudizio: esito.giudizio
            };
            disegnaElenco();
            aggiornaRiepilogo();
        }

        if (selezionato === idCampione) mostraEsito(esito);
        else avvisa(`Analisi AI del campione ${numero} completata: aprilo per vedere il risultato.`, 'info');
    } catch (errore) {
        avvisa(`Campione ${numero}: ${errore.message}`, 'errore');
    } finally {
        analisiInCorso.delete(idCampione);
        aggiornaPulsantiAnalisi();
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
let interrotta = false;

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
    // A blocchi di pochi campioni (15/10/2026): una sola richiesta per tutti superava i 100 secondi di Cloudflare
    // (errore 524 in produzione). Ogni blocco è una richiesta breve; i risultati si sommano man mano.
    const BLOCCO = 2;
    const ids = daAnalizzare.map(c => c.idCampione);
    const esito = { conformi: 0, nonConformi: 0, borderline: 0, nonRiusciti: 0, righe: [] };
    interrotta = false;
    const avanzamento = () => {
        corpo.innerHTML = `<p class="attesa">Analisi in corso: ${esito.righe.length} di ${ids.length} campioni…
            Puoi lasciare aperta questa finestra; chiudendola l'analisi si ferma dopo il blocco in corso.</p>`;
    };

    try {
        avanzamento();
        for (let i = 0; i < ids.length && !interrotta; i += BLOCCO) {
            const parte = ids.slice(i, i + BLOCCO);
            try {
                const r = await inviaJson('/api/validazione/analizza-massivo', { idCampioni: parte });
                esito.conformi += r.conformi; esito.nonConformi += r.nonConformi;
                esito.borderline += r.borderline; esito.nonRiusciti += r.nonRiusciti;
                esito.righe.push(...r.righe);
            } catch (e) {
                // Un blocco non riuscito non ferma gli altri: i suoi campioni risultano «non riusciti».
                for (const id of parte) {
                    const c = daAnalizzare.find(x => x.idCampione === id);
                    esito.nonRiusciti++;
                    esito.righe.push({ idCampione: id, numeroCampione: c?.numeroCampione, ragioneSociale: c?.ragioneSociale, errore: e.message });
                }
            }
            if (!interrotta) avanzamento();
        }
        if (interrotta) { await caricaElenco(); return; }

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
    // 19/10/2026: il browser può ripristinare i filtri della visita precedente (testo, colore): si riparte sempre puliti
    document.getElementById('filtro-elenco').value = '';
    document.getElementById('filtro-colore').value = '';
    // collegamento dalla maschera di ArcLab: /Validazione?campione=<IdCampione>
    const iniziale = Number(document.getElementById('dati-validazione').dataset.campione);
    caricaElenco().then(() => { if (iniziale) apriCampione(iniziale); });

    document.getElementById('btn-aggiorna')
        .addEventListener('click', () => caricaElenco(true));

    document.getElementById('filtro-elenco')
        .addEventListener('input', disegnaElenco);
    document.getElementById('filtro-colore').addEventListener('change', disegnaElenco);
    document.getElementById('ordina-elenco').addEventListener('change', disegnaElenco);

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
            { interrotta = true; document.getElementById('finestra-massiva').classList.add('nascosto'); });
});

// 05/11/2026: rilievi identici (stesso colore e testo, es. la stessa unità campionaria ripetuta) in una sola riga con il numero di prove.
function raggruppaRilievi(rilievi) {
    const gruppi = new Map();
    for (const r of rilievi) {
        const k = r.colore + '|' + r.testo;
        const g = gruppi.get(k);
        if (g) g.volte++; else gruppi.set(k, { ...r, volte: 1 });
    }
    return [...gruppi.values()];
}
