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
        <div class="scheda scheda-dato"><h2>${esito.inCorso}</h2><p>In corso</p></div>
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

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    impostaPeriodoPredefinito();
    caricaAnagrafiche();

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
