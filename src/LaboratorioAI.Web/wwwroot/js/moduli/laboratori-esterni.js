/* ═══════════════════════════════════════════════════════════
   Laboratori esterni
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, etichettaStato,
         avvisa, occupa, popolaSelect } from '../comune.js';

let insediamentiTutti = [];
let ultimiCriteri = null;

// ── Avvio filtri ───────────────────────────────────────────

function impostaPeriodoPredefinito() {
    const oggi = new Date();
    const dodiciMesiFa = new Date(oggi);
    dodiciMesiFa.setFullYear(oggi.getFullYear() - 1);

    document.getElementById('f-a').value  = oggi.toISOString().slice(0, 10);
    document.getElementById('f-da').value = dodiciMesiFa.toISOString().slice(0, 10);
}

async function caricaAnagrafiche() {
    try {
        const [laboratori, matrici, clienti, insediamenti] = await Promise.all([
            chiamaApi('/api/laboratori-esterni/elenco'),
            chiamaApi('/api/anagrafiche/matrici'),
            chiamaApi('/api/anagrafiche/clienti'),
            chiamaApi('/api/anagrafiche/insediamenti')
        ]);

        insediamentiTutti = insediamenti;

        popolaSelect(document.getElementById('f-laboratorio'),  laboratori);
        popolaSelect(document.getElementById('f-matrice'),      matrici, 'Tutte');
        popolaSelect(document.getElementById('f-cliente'),      clienti);
        popolaSelect(document.getElementById('f-insediamento'), insediamenti);
    } catch (errore) {
        avvisa(`Caricamento filtri non riuscito: ${errore.message}`, 'errore');
    }
}

function filtraInsediamenti() {
    const idCliente = Number(document.getElementById('f-cliente').value) || null;
    popolaSelect(document.getElementById('f-insediamento'),
        idCliente ? insediamentiTutti.filter(i => i.idCliente === idCliente) : insediamentiTutti);
}

function leggiCriteri() {
    const numero = id => Number(document.getElementById(id).value) || null;

    return {
        dataDa: document.getElementById('f-da').value,
        dataA:  document.getElementById('f-a').value,
        idLaboratorioEsterno: numero('f-laboratorio'),
        idMatrice:            numero('f-matrice'),
        idCliente:            numero('f-cliente'),
        idInsediamento:       numero('f-insediamento'),
        numeroLotto:          document.getElementById('f-lotto').value.trim() || null,
        soloNonCertificati:   document.getElementById('f-non-certificati').checked,
        limite: 1000
    };
}

// ── Interrogazione ─────────────────────────────────────────

async function interroga(pulsante) {
    const criteri = leggiCriteri();

    if (!criteri.dataDa || !criteri.dataA) {
        avvisa('Indicare il periodo di analisi.', 'info');
        return;
    }

    const libera = occupa(pulsante, 'Interrogazione…');

    try {
        const esito = await inviaJson('/api/laboratori-esterni/interroga', criteri);
        ultimiCriteri = criteri;
        disegna(esito);

        const btnEsporta = document.getElementById('btn-esporta');
        if (btnEsporta) btnEsporta.disabled = esito.totaleProve === 0;
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function disegna(esito) {
    // Sintesi
    const sintesi = document.getElementById('pannello-sintesi');
    sintesi.classList.remove('nascosto');
    sintesi.innerHTML = `
        <div class="scheda scheda-dato"><h2>${esito.totaleProve}</h2><p>Prove nel periodo</p></div>
        <div class="scheda scheda-dato"><h2>${esito.certificate}</h2><p>Certificate</p></div>
        <div class="scheda scheda-dato"><h2>${esito.inCorso}</h2><p>Non certificati</p></div>
        <div class="scheda scheda-dato"><h2>${esito.laboratoriDistinti}</h2><p>Laboratori coinvolti</p></div>`;

    document.getElementById('pannello-esiti').style.display = 'grid';

    // Riepilogo
    document.getElementById('conteggio-riepilogo').textContent =
        `${esito.riepilogo.length} combinazioni`;

    document.querySelector('#tabella-riepilogo tbody').innerHTML =
        esito.riepilogo.length === 0
            ? '<tr><td colspan="5" class="vuoto">Nessuna prova nel periodo.</td></tr>'
            : esito.riepilogo.map(r => `
                <tr>
                    <td>${testo(r.laboratorioEsterno)}</td>
                    <td>${testo(r.matrice) || '—'}</td>
                    <td><strong>${testo(r.denominazioneRiga)}</strong>
                        <div class="sottotesto">${testo(r.riferimentoNormativo) || ''}</div></td>
                    <td class="numerico"><strong>${r.numeroProve}</strong></td>
                    <td class="numerico">${r.numeroCertificate}</td>
                </tr>`).join('');

    // Elenco prove
    document.getElementById('conteggio-prove').textContent = `${esito.prove.length} prove`;
    const avviso = document.getElementById('avviso-troncato');
    avviso.classList.toggle('nascosto', !esito.troncato);
    if (esito.troncato)
        avviso.textContent =
            `Elenco troncato a ${esito.prove.length} prove: `
            + `l'esportazione in Excel contiene comunque tutte le righe.`;

    document.querySelector('#tabella-prove tbody').innerHTML =
        esito.prove.length === 0
            ? '<tr><td colspan="7" class="vuoto">Nessuna prova nel periodo.</td></tr>'
            : esito.prove.map(p => `
                <tr>
                    <td>${etichettaStato(p.stato)}</td>
                    <td class="codice">${testo(p.numeroCampione)}</td>
                    <td>${testo(p.ragioneSociale)}</td>
                    <td>${testo(p.dataAnalisi) || '—'}</td>
                    <td>${testo(p.matrice) || '—'}</td>
                    <td>${testo(p.denominazioneRiga)}
                        ${p.accreditatoEsterno
                            ? '<span class="etichetta etichetta-conforme">accreditato</span>' : ''}</td>
                    <td>${testo(p.laboratorioEsterno)}</td>
                </tr>`).join('');
}

// ── Esportazione ───────────────────────────────────────────

async function esporta(pulsante) {
    if (!ultimiCriteri) return;

    const libera = occupa(pulsante, 'Generazione…');

    try {
        const risposta = await fetch('/api/laboratori-esterni/esporta', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ultimiCriteri)
        });

        if (!risposta.ok) throw new Error('Generazione del foglio non riuscita.');

        // Il nome file arriva dall'intestazione di risposta
        const intestazione = risposta.headers.get('content-disposition') ?? '';
        const nome = intestazione.match(/filename=([^;]+)/)?.[1]?.trim()
                  ?? 'LaboratoriEsterni.xlsx';

        const contenuto = await risposta.blob();
        const url = URL.createObjectURL(contenuto);

        const collegamento = document.createElement('a');
        collegamento.href = url;
        collegamento.download = nome;
        collegamento.click();

        URL.revokeObjectURL(url);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

// ── Laboratori esterni monitorati (ACCREDIA) ───────────────────

let laboratoriMonitorati = [];

// Stato nella banca dati ACCREDIA, letto ogni giorno dall'agente (08/10/2026).
function etichettaStatoAccredia(stato) {
    if (stato === 'Accreditato') return '<span class="etichetta etichetta-conforme">accreditato</span>';
    // La sospensione ACCREDIA riguarda singole prove (asterisco sulla riga della prova), non tutto il laboratorio.
    if (stato === 'Sospeso') return '<span class="etichetta etichetta-attesa" title="Sospensione attiva su una o più prove dell\'elenco: verificare quali su www.accredia.it">sospensione su alcune prove</span>';
    if (stato === 'NonTrovato') return '<span class="etichetta etichetta-non-conforme">NON TROVATO</span>';
    return '<span class="sottotesto">non ancora letto</span>';
}

async function caricaLaboratoriMonitorati() {
    const corpo = document.querySelector('#tabella-laboratori-monitorati tbody');
    const conteggio = document.getElementById('conteggio-laboratori-monitorati');
    const gestione = document.getElementById('pannello-laboratori-monitorati')?.dataset.gestione === 'true';
    const tutti = gestione && document.getElementById('mostra-disattivati')?.checked;

    try {
        const laboratori = await chiamaApi(tutti
            ? '/api/laboratori-esterni/accreditamenti/tutti'
            : '/api/laboratori-esterni/accreditamenti/elenco');
        laboratoriMonitorati = laboratori;

        const attivi = laboratori.filter(l => l.attivo !== false).length;
        conteggio.textContent = `${attivi} monitorati` + (tutti ? `, ${laboratori.length - attivi} disattivati` : '');

        corpo.innerHTML = laboratori.map(l => `
            <tr${l.attivo === false ? ' style="opacity:.55"' : ''}>
                <td>${testo(l.nomeLaboratorio)}${l.attivo === false ? ' <span class="sottotesto">(disattivato)</span>' : ''}</td>
                <td>${testo(l.numeroAccreditamento)}</td>
                <td>${testo(l.sede)}</td>
                <td title="${testo(l.dataStatoAccredia ? 'Letto il ' + l.dataStatoAccredia : '')}">${etichettaStatoAccredia(l.statoAccredia)}</td>
                <td>${testo(l.ultimaRevisioneConfermata ?? '— mai confermata')}</td>
                <td>${testo(l.note ?? '')}</td>
                <td><button class="pulsante pulsante-tenue" data-verifica-metodi="${l.idAccreditamento}"
                        data-nome="${testo(l.nomeLaboratorio)}" data-numero="${testo(l.numeroAccreditamento)}"
                        data-sede="${testo(l.sede)}">Verifica metodi</button>
                    ${gestione ? `<button class="pulsante pulsante-tenue" data-modifica-lab="${l.idAccreditamento}">Modifica</button>` : ''}</td>
            </tr>`).join('');

        corpo.querySelectorAll('[data-modifica-lab]').forEach(pulsante =>
            pulsante.addEventListener('click', () => apriFinestraLaboratorio(Number(pulsante.dataset.modificaLab))));

        corpo.querySelectorAll('[data-verifica-metodi]').forEach(pulsante =>
            pulsante.addEventListener('click', () =>
                apriFinestraMetodi(
                    pulsante.dataset.verificaMetodi, pulsante.dataset.nome,
                    pulsante.dataset.numero, pulsante.dataset.sede)));
    } catch (errore) {
        avvisa(`Caricamento laboratori monitorati non riuscito: ${errore.message}`, 'errore');
    }
}

// ── Accreditamenti da sistemare (sola lettura) ─────────────

async function caricaDaSistemare() {
    const conteggio = document.getElementById('conteggio-da-sistemare');
    const boxControllo = document.getElementById('da-sistemare-controllo');
    const boxStati = document.getElementById('da-sistemare-stati');
    const boxDiscrepanze = document.getElementById('da-sistemare-discrepanze');
    const boxNumeri = document.getElementById('da-sistemare-numeri');
    try {
        const d = await chiamaApi('/api/laboratori-esterni/accreditamenti/da-sistemare');
        conteggio.textContent = d.totale === 0 ? 'nulla da sistemare' : `${d.totale} da sistemare`;

        const c = d.controllo;
        boxControllo.innerHTML = c.fermo
            ? `<p class="avviso">Controllo ACCREDIA fermo: ${c.ultimoControllo
                    ? `ultimo controllo il ${testo(c.ultimoControllo)} (${testo(c.modo)})`
                    : 'nessun controllo registrato'}, oltre ${c.giorniMassimi} giorni fa. Verificare che l'Agente ACCREDIA
                giri ogni giorno sul PC interno (attività pianificata di Windows), oppure usare «Esegui controllo ora».</p>`
            : `<p class="sottotesto">Ultimo controllo ACCREDIA: ${testo(c.ultimoControllo)} (${testo(c.modo)}).</p>`;

        // Una riga per prova sospesa (ACCREDIA sospende singole prove, non tutto l'elenco), con i nostri metodi che
        // citano la stessa norma: è un suggerimento da verificare, non una decisione.
        const stati = d.statiNonValidi ?? [];
        const rigaStato = x => {
            const lab = `<td>${testo(x.laboratorio)}<br><span class="sottotesto">${testo(x.numero)} / ${testo(x.sede)}</span></td>`;
            if (x.stato === 'NonTrovato')
                return `<tr>${lab}<td colspan="3">${etichettaStatoAccredia(x.stato)} accreditamento revocato, ritirato o rinunciato:
                    nessuna prova di questo laboratorio è più accreditata</td><td>${testo(x.dataStato ?? '')}</td></tr>`;
            if ((x.prove ?? []).length === 0)
                return `<tr>${lab}<td colspan="3">${etichettaStatoAccredia(x.stato)} elenco delle prove sospese non ancora letto
                    dall'Agente ACCREDIA: verificare su www.accredia.it</td><td>${testo(x.dataStato ?? '')}</td></tr>`;
            return x.prove.map(p => `<tr>${lab}
                <td><strong>${testo(p.prova)}</strong><br><span class="sottotesto">${testo(p.matrice)}${p.tecnica ? ' — ' + testo(p.tecnica) : ''}</span></td>
                <td>${testo(p.norma)}</td>
                <td>${(p.metodi ?? []).length === 0
                    ? '<span class="etichetta etichetta-neutro">nessun metodo nostro con questa norma</span>'
                    : '<span class="etichetta etichetta-non-conforme">possibile metodo nostro coinvolto</span>' + p.metodi.map(m =>
                        `<br>${testo(m.codMetodo ?? '')} — ${testo(m.nomeParametro ?? '')} <span class="sottotesto">(${testo(m.riferimentoNormativo ?? '')})</span>`).join('')}</td>
                <td>${testo(p.rilevata)}</td></tr>`).join('');
        };
        boxStati.innerHTML = stati.length === 0 ? '' :
            `<h3>Prove sospese o accreditamento non più presente in ACCREDIA (${stati.length})</h3>
             <p class="sottotesto">Dalla banca dati ACCREDIA dei laboratori di prova, letta ogni giorno dall'Agente ACCREDIA.
               La sospensione riguarda solo le prove elencate (asterisco nell'elenco prove); il resto dell'elenco resta accreditato.
               «Possibile metodo nostro coinvolto»: un metodo che affidiamo al laboratorio cita la stessa norma; verificare se è
               proprio quella prova (matrice e parametro) e, se sì, non dichiararla accreditata finché dura la sospensione.
               «Rilevata il» è il giorno in cui l'abbiamo vista, non l'inizio della sospensione.</p>
             <div class="tabella-scorrevole"><table class="tabella">
               <thead><tr><th>Laboratorio</th><th>Prova sospesa</th><th>Norma</th><th>Nostri metodi</th><th>Rilevata il</th></tr></thead>
               <tbody>${stati.map(rigaStato).join('')}</tbody></table></div>`;

        const etichetta = v => v === 'Accreditato' ? 'Accreditato' : 'Non accreditato';
        boxDiscrepanze.innerHTML = d.discrepanze.length === 0
            ? '<p class="sottotesto">Nessuna discrepanza tra le verifiche fatte qui e il flag di ArcLab.</p>'
            : `<h3>Verifica diversa dal flag di ArcLab (${d.discrepanze.length})</h3>
               <p class="sottotesto">Certificati e ricerche usano il flag di ArcLab: va corretto in ArcLab (tabella dei metodi del laboratorio esterno).</p>
               <div class="tabella-scorrevole"><table class="tabella">
                 <thead><tr><th>Laboratorio</th><th>Parametro — riferimento normativo</th><th>Cod. metodo</th>
                   <th>Verifica LabAI</th><th>Flag ArcLab</th><th>Verificato il</th><th>Note</th></tr></thead>
                 <tbody>${d.discrepanze.map(x => `<tr>
                   <td>${testo(x.laboratorio)}</td>
                   <td>${testo(x.nomeParametro ?? '')} — ${testo(x.riferimentoNormativo ?? '')}</td>
                   <td>${testo(x.codMetodo ?? '')}</td>
                   <td><strong>${etichetta(x.verifica)}</strong></td>
                   <td>${x.flagArcLab ? 'Accreditato' : 'Non accreditato'}</td>
                   <td>${testo(x.dataVerifica ?? '')}</td>
                   <td>${testo(x.note ?? '')}</td></tr>`).join('')}</tbody></table></div>`;

        boxNumeri.innerHTML = d.numeriDiversi.length === 0
            ? '<p class="sottotesto">I numeri ACCREDIA in ArcLab coincidono con quelli monitorati.</p>'
            : `<h3>Numero ACCREDIA diverso in ArcLab (${d.numeriDiversi.length})</h3>
               <p class="sottotesto">Il numero di ArcLab è quello stampato sui certificati: va aggiornato in ArcLab (anagrafica laboratori esterni).</p>
               <div class="tabella-scorrevole"><table class="tabella">
                 <thead><tr><th>Laboratorio</th><th>Numero in ArcLab</th><th>Numero ACCREDIA monitorato</th></tr></thead>
                 <tbody>${d.numeriDiversi.map(x => `<tr>
                   <td>${testo(x.laboratorio)}</td>
                   <td>${x.numeroArcLab ? testo(x.numeroArcLab) : '<em>mancante</em>'}</td>
                   <td>${testo(x.numeroMonitorato)} / ${testo(x.sede)}</td></tr>`).join('')}</tbody></table></div>`;
    } catch (errore) {
        conteggio.textContent = '';
        boxControllo.innerHTML = `<p class="avviso">Controlli non disponibili: ${testo(errore.message)}</p>`;
    }
}

// ── Aggiunta / modifica di un laboratorio monitorato ───────

let idLaboratorioInModifica = null;

async function apriFinestraLaboratorio(idAccreditamento) {
    const select = document.getElementById('lab-arclab');
    if (select.options.length <= 1) {
        try {
            const elenco = await chiamaApi('/api/laboratori-esterni/elenco');
            for (const l of elenco) {
                const o = document.createElement('option');
                o.value = l.idLabEsterno ?? l.id ?? l.IdLabEsterno;
                o.textContent = l.nome ?? l.labEsterno ?? l.nomeLaboratorio ?? `Laboratorio ${o.value}`;
                select.append(o);
            }
        } catch (errore) {
            avvisa(`Elenco laboratori di ArcLab non disponibile: ${errore.message}`, 'errore');
        }
    }
    let lab = null;
    if (idAccreditamento) {
        lab = laboratoriMonitorati.find(l => l.idAccreditamento === idAccreditamento) ?? null;
        if (lab && lab.attivo === undefined) lab.attivo = true;
    }
    idLaboratorioInModifica = lab?.idAccreditamento ?? null;
    document.getElementById('titolo-finestra-laboratorio').textContent =
        lab ? `Modifica: ${lab.nomeLaboratorio}` : 'Aggiungi un laboratorio da monitorare';
    select.value = lab ? String(lab.idLabEsternoArcLab) : '';
    select.disabled = !!lab;
    document.getElementById('lab-numero').value = lab?.numeroAccreditamento ?? '';
    document.getElementById('lab-sede').value = lab?.sede ?? 'A';
    document.getElementById('lab-note').value = lab?.note ?? '';
    document.getElementById('lab-attivo').checked = lab ? lab.attivo !== false : true;
    document.getElementById('finestra-laboratorio').classList.remove('nascosto');
}

async function salvaLaboratorio(pulsante) {
    const idArcLab = Number(document.getElementById('lab-arclab').value);
    if (!idArcLab) { avvisa('Scegli il laboratorio di ArcLab.', 'errore'); return; }
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        await inviaJson('/api/laboratori-esterni/accreditamenti/laboratorio', {
            idAccreditamento: idLaboratorioInModifica,
            idLabEsternoArcLab: idArcLab,
            numeroAccreditamento: document.getElementById('lab-numero').value,
            sede: document.getElementById('lab-sede').value,
            note: document.getElementById('lab-note').value,
            attivo: document.getElementById('lab-attivo').checked
        });
        document.getElementById('finestra-laboratorio').classList.add('nascosto');
        avvisa('Laboratorio salvato.', 'successo');
        await Promise.all([caricaLaboratoriMonitorati(), caricaDaSistemare()]);
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

// ── Verifica metodi vs elenco prove ACCREDIA (per laboratorio) ─

const ETICHETTE_STATO_METODO = {
    DaVerificare:   'Da verificare',
    Accreditato:    'Accreditato',
    NonAccreditato: 'Non accreditato'
};

async function apriFinestraMetodi(idAccreditamento, nomeLaboratorio, numeroAccreditamento, sede) {
    document.getElementById('titolo-finestra-metodi').textContent =
        `Verifica metodi — ${nomeLaboratorio} (ACCREDIA n. ${numeroAccreditamento}/${sede})`;
    document.getElementById('finestra-metodi').classList.remove('nascosto');
    document.getElementById('finestra-metodi').dataset.idAccreditamento = idAccreditamento;
    await caricaMetodi(idAccreditamento);
}

async function caricaMetodi(idAccreditamento) {
    const corpo = document.querySelector('#tabella-metodi tbody');
    const avvisoNessunaRevisione = document.getElementById('metodi-nessuna-revisione');
    const linkPdf = document.getElementById('metodi-link-pdf');
    corpo.innerHTML = '<tr><td colspan="7" class="attesa">Caricamento…</td></tr>';

    try {
        const risposta = await chiamaApi(`/api/laboratori-esterni/accreditamenti/${idAccreditamento}/metodi`);

        avvisoNessunaRevisione.classList.toggle('nascosto', !!risposta.idRevisioneConfermata);
        linkPdf.innerHTML = risposta.idRevisioneConfermata
            ? `PDF confermato in uso: <a href="/api/laboratori-esterni/accreditamenti/revisioni/${risposta.idRevisioneConfermata}/pdf"
                   target="_blank" rel="noopener">revisione n. ${risposta.numeroRevisioneConfermata}</a>`
            : '';

        if (risposta.metodi.length === 0) {
            corpo.innerHTML = '<tr><td colspan="7" class="vuoto">Nessun metodo affidato a questo laboratorio in ArcLab.</td></tr>';
            return;
        }

        corpo.innerHTML = risposta.metodi.map(m => `
            <tr data-riga-metodo="${m.idLabEstMetodo}">
                <td>${testo(m.nomeParametro ?? '')} — ${testo(m.riferimentoNormativo ?? '')}</td>
                <td>${testo(m.codMetodo ?? '')}</td>
                <td>${m.accreditatoArcLab ? 'Accreditato (ArcLab)' : 'Non accreditato (ArcLab)'}</td>
                <td style="max-width: 380px">${m.suggerimentoTesto
                    ? testo(m.suggerimentoTesto)
                        + (m.punteggioSuggerimento != null ? ` <span class="sottotesto">(corrispondenza ${Math.round(m.punteggioSuggerimento * 100)}%)</span>` : '')
                    : '<span class="sottotesto">Nessuna voce simile trovata nel PDF</span>'}</td>
                <td>
                    <select data-stato>
                        <option value="DaVerificare"${m.stato === 'DaVerificare' ? ' selected' : ''}>Da verificare</option>
                        <option value="Accreditato"${m.stato === 'Accreditato' ? ' selected' : ''}>Accreditato</option>
                        <option value="NonAccreditato"${m.stato === 'NonAccreditato' ? ' selected' : ''}>Non accreditato</option>
                    </select>
                </td>
                <td><input type="text" data-note value="${testo(m.note ?? '')}" placeholder="Nota facoltativa" /></td>
                <td><button class="pulsante pulsante-tenue" data-salva-metodo>Salva</button></td>
            </tr>`).join('');

        corpo.querySelectorAll('[data-salva-metodo]').forEach(pulsante =>
            pulsante.addEventListener('click', () => salvaVerificaMetodo(idAccreditamento, pulsante)));
    } catch (errore) {
        corpo.innerHTML = `<tr><td colspan="7" class="errore">${testo(errore.message)}</td></tr>`;
    }
}

async function salvaVerificaMetodo(idAccreditamento, pulsante) {
    const riga = pulsante.closest('tr');
    const idLabEstMetodo = riga.dataset.rigaMetodo;
    const stato = riga.querySelector('[data-stato]').value;
    const note = riga.querySelector('[data-note]').value.trim() || null;

    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        await inviaJson(
            `/api/laboratori-esterni/accreditamenti/${idAccreditamento}/metodi/${idLabEstMetodo}/verifica`,
            { stato, note });
        avvisa('Metodo classificato.', 'successo');
        caricaDaSistemare();
        libera();
    } catch (errore) {
        avvisa(errore.message, 'errore');
        libera();
    }
}

// ── Accreditamenti ACCREDIA da confermare ─────────────────────

const PUO_CONFERMARE_ACCREDITAMENTI =
    document.querySelector('#tabella-accreditamenti thead th:last-child')?.textContent === 'Conferma';

async function caricaAccreditamentiDaConfermare() {
    const corpo = document.querySelector('#tabella-accreditamenti tbody');
    const conteggio = document.getElementById('conteggio-accreditamenti');

    try {
        const [daConfermare, laboratori] = await Promise.all([
            chiamaApi('/api/laboratori-esterni/accreditamenti/da-confermare'),
            chiamaApi('/api/laboratori-esterni/accreditamenti/elenco')
        ]);

        const nomiPerAccreditamento = new Map(laboratori.map(l => [l.idAccreditamento, l.nomeLaboratorio]));

        conteggio.textContent = daConfermare.length
            ? `${daConfermare.length} in attesa di conferma`
            : 'Nessuna revisione in attesa';

        corpo.innerHTML = '';
        for (const r of daConfermare) {
            const riga = document.createElement('tr');
            const nomeLab = nomiPerAccreditamento.get(r.idAccreditamento) ?? `Laboratorio #${r.idAccreditamento}`;

            riga.innerHTML = `
                <td>${testo(nomeLab)}</td>
                <td>${testo(r.numeroRevisione)}</td>
                <td>${testo(r.dataEmissione ?? '—')}</td>
                <td>${testo(r.dataRilevazione)}</td>
                <td><a href="/api/laboratori-esterni/accreditamenti/revisioni/${r.idRevisione}/pdf"
                       target="_blank" rel="noopener">Apri PDF</a></td>
                ${PUO_CONFERMARE_ACCREDITAMENTI
                    ? `<td><button class="pulsante pulsante-tenue" data-conferma="${r.idRevisione}">Conferma</button></td>`
                    : ''}`;
            corpo.appendChild(riga);
        }

        corpo.querySelectorAll('[data-conferma]').forEach(pulsante =>
            pulsante.addEventListener('click', () => confermaAccreditamento(pulsante)));
    } catch (errore) {
        avvisa(`Caricamento accreditamenti non riuscito: ${errore.message}`, 'errore');
    }
}

async function confermaAccreditamento(pulsante) {
    const idRevisione = pulsante.dataset.conferma;
    if (!confirm('Confermare questa revisione come elenco prove ufficiale del laboratorio?')) return;

    const libera = occupa(pulsante, 'Conferma…');
    try {
        await inviaJson(`/api/laboratori-esterni/accreditamenti/revisioni/${idRevisione}/conferma`, {});
        avvisa('Revisione confermata. I metodi di questo laboratorio sono ora da riverificare.', 'successo');
        await Promise.all([caricaAccreditamentiDaConfermare(), caricaLaboratoriMonitorati(), caricaDaSistemare()]);
    } catch (errore) {
        avvisa(errore.message, 'errore');
        libera();
    }
}

// Il controllo (via ACCREDIA, uno o più laboratori in sequenza) può richiedere più tempo di
// quanto Cloudflare lasci aperta la richiesta davanti al sito (errore 524 osservato in
// produzione quando ACCREDIA è lenta). Il pulsante quindi avvia il controllo sul server e resta
// occupato interrogandone lo stato a intervalli, invece di aspettare la risposta di un'unica
// chiamata: il controllo prosegue comunque sul server anche se l'interfaccia smette di aspettare
// (il server stesso ha un tetto massimo di 15 minuti, oltre il quale si interrompe da solo).
//
// Le chiamate di stato sono leggere (solo lettura di uno stato in memoria) ma passano comunque
// da Cloudflare: un errore isolato di rete (osservato: un 522 su una singola richiesta di stato,
// non sul controllo vero) non deve far perdere di vista un controllo che sul server sta ancora
// proseguendo regolarmente — per questo un errore di rete viene ritentato prima di arrendersi.
const INTERVALLO_POLLING_CONTROLLO_MS = 5000;
const TENTATIVI_MASSIMI_POLLING_CONTROLLO = 200; // ~16-17 minuti di attesa lato interfaccia
const ERRORI_CONSECUTIVI_MASSIMI_POLLING = 3;    // errori di rete isolati sulla sola chiamata di stato

async function eseguiTestAccreditamenti(pulsante) {
    const libera = occupa(pulsante, 'Controllo in corso…');
    try {
        const esito = await inviaJson('/api/laboratori-esterni/accreditamenti/test', {});
        if (!esito.avviato)
            avvisa('Un controllo era già in corso: resto in attesa del suo completamento.', 'info');
        await pollaStatoControlloAccreditamenti(libera);
    } catch (errore) {
        avvisa(`Avvio del controllo non riuscito: ${errore.message}`, 'errore');
        libera();
    }
}

async function pollaStatoControlloAccreditamenti(libera) {
    let erroriConsecutivi = 0;

    for (let tentativo = 0; tentativo < TENTATIVI_MASSIMI_POLLING_CONTROLLO; tentativo++) {
        await new Promise(risolvi => setTimeout(risolvi, INTERVALLO_POLLING_CONTROLLO_MS));

        let stato;
        try {
            stato = await chiamaApi('/api/laboratori-esterni/accreditamenti/test/stato');
            erroriConsecutivi = 0;
        } catch (errore) {
            erroriConsecutivi++;
            if (erroriConsecutivi < ERRORI_CONSECUTIVI_MASSIMI_POLLING)
                continue; // probabile intoppo isolato (es. un 522 sulla sola chiamata di stato): riprova
            avvisa(
                `Verifica dello stato del controllo non riuscita più volte di seguito: ${errore.message}. `
                + 'Il controllo può comunque proseguire sul server: ricaricare la pagina fra qualche minuto per saperlo.',
                'errore'
            );
            libera();
            return;
        }

        if (stato.stato === 'InCorso') continue;

        if (stato.stato === 'Completato' && stato.ultimoRiepilogo) {
            const r = stato.ultimoRiepilogo;
            avvisa(
                `Controllo completato: ${r.totaleLaboratori} laboratori, ` +
                `${r.nuoveRevisioni} nuove revisioni, ${r.errori} errori` +
                (r.emailInviata ? ' (email di riepilogo inviata).' : '.'),
                r.errori > 0 ? 'errore' : 'successo'
            );
            await Promise.all([caricaAccreditamentiDaConfermare(), caricaLaboratoriMonitorati(), caricaDaSistemare()]);
        } else if (stato.stato === 'Errore') {
            avvisa(`Controllo non riuscito: ${stato.ultimoErrore ?? 'errore sconosciuto'}`, 'errore');
        }
        libera();
        return;
    }

    // Superati i tentativi lato interfaccia (il server comunque si sarebbe già fermato da solo
    // al proprio tetto massimo, 15 minuti): l'esito si vedrà ricaricando la pagina fra poco.
    avvisa(
        'Il controllo sta impiegando più del previsto (probabile lentezza di ACCREDIA): prosegue '
        + 'comunque sul server, l\'esito comparirà ricaricando la pagina fra qualche minuto.',
        'info'
    );
    libera();
}

async function riestraiTestoMancante(pulsante) {
    const libera = occupa(pulsante, 'Ri-estrazione in corso…');
    try {
        const risultati = await inviaJson('/api/laboratori-esterni/accreditamenti/riestrai-testo-mancante', {});

        const riusciti = risultati.filter(r => r.esito === 'TestoEstratto').length;
        const senzaTesto = risultati.filter(r => r.esito === 'NessunTestoEstratto').length;
        const nonTrovati = risultati.filter(r => r.esito === 'FileNonTrovato').length;
        const errori = risultati.filter(r => r.esito === 'Errore').length;

        if (risultati.length === 0) {
            avvisa('Nessuna revisione con testo mancante: niente da fare.', 'successo');
        } else {
            const dettaglio = [];
            if (senzaTesto) dettaglio.push(`${senzaTesto} senza testo nel PDF`);
            if (nonTrovati) dettaglio.push(`${nonTrovati} file non trovato`);
            if (errori) dettaglio.push(`${errori} errori`);

            avvisa(
                `Ri-estrazione completata: ${riusciti} di ${risultati.length} revisioni aggiornate` +
                (dettaglio.length ? ` (${dettaglio.join(', ')}).` : '.'),
                (errori > 0 || nonTrovati > 0) ? 'errore' : 'successo'
            );
        }
    } catch (errore) {
        avvisa(`Ri-estrazione non riuscita: ${errore.message}`, 'errore');
    } finally {
        libera();
    }
}

async function riscaricaPdfCorrotti(pulsante) {
    if (!confirm('Cercare le revisioni con PDF mancante o corrotto e riscaricarle da ACCREDIA?')) return;

    const libera = occupa(pulsante, 'Ricerca e ri-scarico in corso…');
    try {
        const risultati = await inviaJson('/api/laboratori-esterni/accreditamenti/riscarica-pdf-corrotti', {});

        const riusciti = risultati.filter(r => r.esito === 'PdfRiscaricato').length;
        const errori = risultati.filter(r => r.esito === 'Errore').length;

        if (risultati.length === 0) {
            avvisa('Nessun PDF mancante o corrotto trovato: niente da fare.', 'successo');
        } else {
            avvisa(
                `Ri-scarico completato: ${riusciti} di ${risultati.length} PDF corretti` +
                (errori ? ` (${errori} errori — vedi log applicazione).` : '.'),
                errori > 0 ? 'errore' : 'successo'
            );
        }
    } catch (errore) {
        avvisa(`Ri-scarico non riuscito: ${errore.message}`, 'errore');
    } finally {
        libera();
    }
}

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    impostaPeriodoPredefinito();
    caricaAnagrafiche();
    caricaDaSistemare();
    caricaLaboratoriMonitorati();
    caricaAccreditamentiDaConfermare();
    document.getElementById('btn-test-accreditamenti')?.addEventListener('click', e => eseguiTestAccreditamenti(e.currentTarget));
    document.getElementById('btn-riscarica-pdf-corrotti')?.addEventListener('click', e => riscaricaPdfCorrotti(e.currentTarget));
    document.getElementById('btn-riestrai-testo-mancante')?.addEventListener('click', e => riestraiTestoMancante(e.currentTarget));
    document.getElementById('chiudi-metodi')?.addEventListener('click', () => document.getElementById('finestra-metodi').classList.add('nascosto'));
    document.getElementById('chiudi-laboratorio')?.addEventListener('click', () => document.getElementById('finestra-laboratorio').classList.add('nascosto'));
    document.getElementById('btn-aggiungi-laboratorio')?.addEventListener('click', () => apriFinestraLaboratorio(null));
    document.getElementById('btn-salva-laboratorio')?.addEventListener('click', e => salvaLaboratorio(e.currentTarget));
    document.getElementById('mostra-disattivati')?.addEventListener('change', caricaLaboratoriMonitorati);

    document.getElementById('f-cliente').addEventListener('change', filtraInsediamenti);
    document.getElementById('btn-cerca').addEventListener('click', e => interroga(e.currentTarget));
    document.getElementById('btn-esporta')?.addEventListener('click', e => esporta(e.currentTarget));

    document.getElementById('btn-azzera').addEventListener('click', () => {
        ['f-laboratorio', 'f-matrice', 'f-cliente', 'f-insediamento', 'f-lotto']
            .forEach(id => document.getElementById(id).value = '');
        document.getElementById('f-non-certificati').checked = false;
        impostaPeriodoPredefinito();
        filtraInsediamenti();
        document.getElementById('pannello-sintesi').classList.add('nascosto');
        document.getElementById('pannello-esiti').style.display = 'none';
    });
});
