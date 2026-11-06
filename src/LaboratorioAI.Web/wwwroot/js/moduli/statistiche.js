/* ═══════════════════════════════════════════════════════════
   Statistiche — ricerca trasversale a livello di singola prova
   ═══════════════════════════════════════════════════════════ */
import { inviaJson, testo, avvisa, occupa } from '../comune.js';

let esitoCorrente = null;
const LIMITE_VISTA = 3000;

function impostaPeriodoPredefinito() {
    const oggi = new Date();
    const inizioAnno = new Date(oggi.getFullYear(), 0, 1);
    document.getElementById('f-ric-a').value  = oggi.toISOString().slice(0, 10);
    document.getElementById('f-ric-da').value = inizioAnno.toISOString().slice(0, 10);
}

function leggiCriteri() {
    const acc = document.getElementById('f-accreditato').value;
    return {
        dataRicDa: document.getElementById('f-ric-da').value,
        dataRicA: document.getElementById('f-ric-a').value,
        dataPrelievoDa: document.getElementById('f-prel-da').value || null,
        dataPrelievoA: document.getElementById('f-prel-a').value || null,
        cliente: document.getElementById('f-cliente').value.trim() || null,
        insediamento: document.getElementById('f-insediamento').value.trim() || null,
        citta: document.getElementById('f-citta').value.trim() || null,
        provincia: document.getElementById('f-provincia').value.trim() || null,
        cap: document.getElementById('f-cap').value.trim() || null,
        descrIncarico: document.getElementById('f-descrincarico').value.trim() || null,
        metodo: document.getElementById('f-metodo').value.trim() || null,
        denominazioneCampione: document.getElementById('f-denominazione').value.trim() || null,
        matrice: document.getElementById('f-matrice').value.trim() || null,
        nCertificato: document.getElementById('f-certificato').value.trim() || null,
        numeroCampione: document.getElementById('f-numcampione').value.trim() || null,
        accreditato: acc === 'si' ? true : acc === 'no' ? false : null,
        valoreCalcolatoDa: document.getElementById('f-valore-da').value !== ''
            ? Number(document.getElementById('f-valore-da').value) : null,
        valoreCalcolatoA: document.getElementById('f-valore-a').value !== ''
            ? Number(document.getElementById('f-valore-a').value) : null,
        limite: 20000
    };
}

// ── Interrogazione ─────────────────────────────────────────

async function interroga(pulsante) {
    const criteri = leggiCriteri();
    if (!criteri.dataRicDa || !criteri.dataRicA) {
        avvisa('Indicare il periodo (data ricezione).', 'info');
        return;
    }
    const libera = occupa(pulsante, 'Interrogazione…');
    try {
        const esito = await inviaJson('/api/statistiche/interroga', criteri);
        esitoCorrente = esito;
        document.getElementById('pannello-esiti').style.display = 'block';
        aggiorna();
        document.getElementById('btn-esporta').disabled = esito.totale === 0;
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function euro(n) {
    return (n || 0).toLocaleString('it-IT', { minimumFractionDigits: 0, maximumFractionDigits: 4 });
}

function aggiorna() {
    if (!esitoCorrente) return;
    const righe = esitoCorrente.righe;

    const clienti = new Set(righe.map(r => r.idCliente)).size;
    const campioni = new Set(righe.map(r => r.idCampione)).size;

    const sintesi = document.getElementById('pannello-sintesi');
    sintesi.classList.remove('nascosto');
    sintesi.innerHTML = `
        <div class="scheda scheda-dato"><h2>${righe.length}</h2><p>Prove</p></div>
        <div class="scheda scheda-dato"><h2>${campioni}</h2><p>Campioni</p></div>
        <div class="scheda scheda-dato"><h2>${clienti}</h2><p>Clienti</p></div>`;

    renderDettaglio(righe);
}

function renderDettaglio(righe) {
    const capNota = righe.length > LIMITE_VISTA ? ` (mostrate le prime ${LIMITE_VISTA})` : '';
    document.getElementById('conteggio-dettaglio').textContent = `${righe.length} prove${capNota}`;

    const avviso = document.getElementById('avviso-troncato');
    avviso.classList.toggle('nascosto', !esitoCorrente.troncato);
    if (esitoCorrente.troncato)
        avviso.textContent =
            `Elenco troncato a ${esitoCorrente.totale} prove: affina i filtri per un conteggio completo.`;

    document.querySelector('#tabella-dettaglio tbody').innerHTML =
        righe.length === 0
            ? '<tr><td colspan="16" class="vuoto">Nessuna prova con i filtri impostati.</td></tr>'
            : righe.slice(0, LIMITE_VISTA).map(r => `
                <tr>
                    <td>${testo(r.cliente)}</td>
                    <td>${testo(r.insediamento) || '—'}</td>
                    <td>${testo(r.citta) || '—'}</td>
                    <td>${testo(r.commessa) || '—'}</td>
                    <td>${testo(r.nCertificato) || '—'}</td>
                    <td class="codice">${testo(r.numeroCampione)}</td>
                    <td>${testo(r.denominazioneCampione) || '—'}</td>
                    <td>${testo(r.matrice) || '—'}</td>
                    <td>${testo(r.dataRicezione) || '—'}</td>
                    <td>${testo(r.dataPrelievo) || '—'}</td>
                    <td class="numerico">${testo(r.tempPrelievo) || '—'}</td>
                    <td>${testo(r.parametro) || '—'}</td>
                    <td>${testo(r.metodo) || '—'}</td>
                    <td class="numerico">${r.valoreCalcolato != null ? (r.minoreDi ? '&lt; ' : '') + euro(r.valoreCalcolato) : '—'}</td>
                    <td>${testo(r.unitaMisura) || '—'}</td>
                    <td>${r.accreditato
                        ? '<strong style="color:#1a7f37">Sì</strong>'
                        : '<span style="color:#9a9a9a">No</span>'}</td>
                </tr>`).join('');
}

// ── Esportazione ───────────────────────────────────────────

async function esporta(pulsante) {
    if (!esitoCorrente) return;
    const criteri = leggiCriteri();
    if (!criteri.dataRicDa || !criteri.dataRicA) {
        avvisa('Indicare il periodo (data ricezione).', 'info');
        return;
    }
    const libera = occupa(pulsante, 'Generazione…');

    try {
        const risposta = await fetch('/api/statistiche/esporta', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(criteri)
        });
        if (!risposta.ok) throw new Error('Generazione del foglio non riuscita.');

        const intestazione = risposta.headers.get('content-disposition') ?? '';
        const nome = intestazione.match(/filename=([^;]+)/)?.[1]?.trim() ?? 'Statistiche.xlsx';
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

// ── Avvio ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    impostaPeriodoPredefinito();

    document.getElementById('btn-cerca').addEventListener('click', e => interroga(e.currentTarget));
    document.getElementById('btn-esporta')?.addEventListener('click', e => esporta(e.currentTarget));
});
