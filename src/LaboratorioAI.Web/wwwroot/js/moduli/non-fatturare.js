/* ═══════════════════════════════════════════════════════════
   Non fatturare — conteggio campioni per prelevatore
   ═══════════════════════════════════════════════════════════ */
import { inviaJson, testo, avvisa, occupa } from '../comune.js';

let esitoCorrente = null;
let prelevatoreSel = null;   // prelevatore selezionato dal riepilogo (o null)
let ricevitoreSel = null;    // ricevitore selezionato dal riepilogo (o null)

function impostaPeriodoPredefinito() {
    const oggi = new Date();
    const inizioAnno = new Date(oggi.getFullYear(), 0, 1);
    document.getElementById('f-a').value  = oggi.toISOString().slice(0, 10);
    document.getElementById('f-da').value = inizioAnno.toISOString().slice(0, 10);
}

function leggiCriteri() {
    return {
        dataDa: document.getElementById('f-da').value,
        dataA:  document.getElementById('f-a').value,
        limite: 50000
    };
}

// ── Interrogazione ─────────────────────────────────────────

async function interroga(pulsante) {
    const criteri = leggiCriteri();
    if (!criteri.dataDa || !criteri.dataA) {
        avvisa('Indicare il periodo (data ricezione).', 'info');
        return;
    }
    const libera = occupa(pulsante, 'Interrogazione…');
    try {
        const esito = await inviaJson('/api/non-fatturare/interroga', criteri);
        esitoCorrente = esito;
        prelevatoreSel = null;
        document.getElementById('pannello-esiti').style.display = 'block';
        popolaPrelevatori(esito.campioni);
        popolaRicevitori(esito.campioni);
        aggiorna();
        document.getElementById('btn-esporta').disabled = esito.totale === 0;
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

// ── Filtri ─────────────────────────────────────────────────

function nomePrel(c) {
    const n = (c.prelevatore || '').trim().replace(/\s+/g, ' ');
    return n || '(nessuno)';
}

function nomeRic(c) {
    const n = (c.ricevitore || '').trim().replace(/\s+/g, ' ');
    return n || '(nessuno)';
}

function controlliSelezionati() {
    const sel = [];
    if (document.getElementById('c-180').checked)  sel.push(180);
    if (document.getElementById('c-203').checked)  sel.push(203);
    if (document.getElementById('c-2513').checked) sel.push(2513);
    const altro = Number(document.getElementById('c-altro').value);
    if (altro) sel.push(altro);
    return sel;
}

function popolaPrelevatori(campioni) {
    const nomi = [...new Set(campioni.map(nomePrel))].sort();
    document.getElementById('f-prelevatore').innerHTML =
        '<option value="">Tutti</option>' +
        nomi.map(n => `<option value="${testo(n)}">${testo(n)}</option>`).join('');
}

function popolaRicevitori(campioni) {
    const nomi = [...new Set(campioni.map(c => (c.ricevitore || '').trim()).filter(n => n))].sort();
    document.getElementById('f-ricevitore').innerHTML =
        '<option value="">Tutti</option>' +
        nomi.map(n => `<option value="${testo(n)}">${testo(n)}</option>`).join('');
}

function contiene(valore, filtro) {
    if (!filtro) return true;
    return (valore || '').toLowerCase().includes(filtro.toLowerCase());
}

function baseFiltrata() {
    const cli  = document.getElementById('f-cliente').value.trim();
    const ins  = document.getElementById('f-insediamento').value.trim();
    const hac  = document.getElementById('f-haccp').value;
    const prel = document.getElementById('f-prelevatore').value;
    const ric  = document.getElementById('f-ricevitore').value;
    const controlli = controlliSelezionati();

    return esitoCorrente.campioni.filter(c =>
        contiene(c.ragioneSociale, cli) &&
        contiene(c.nomeInsediamento, ins) &&
        (hac === '' || (hac === 'si' && c.contrattoHaccp) || (hac === 'no' && !c.contrattoHaccp)) &&
        (prel === '' || nomePrel(c) === prel) &&
        (ric === '' || (c.ricevitore || '').trim() === ric) &&
        (controlli.length === 0 || controlli.includes(c.idControllo)));
}

function euro(n) {
    return (n || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function riepiloga(campioni, chiaveFn) {
    const mappa = new Map();
    for (const c of campioni) {
        const k = chiaveFn(c);
        let r = mappa.get(k);
        if (!r) { r = { chiave: k, campioni: 0, inContratto: 0, totale: 0 }; mappa.set(k, r); }
        r.campioni++;
        if (c.contrattoHaccp) r.inContratto++;
        r.totale += (c.prezzoControllo || 0);
    }
    return [...mappa.values()].sort((a, b) => b.campioni - a.campioni
                                          || a.chiave.localeCompare(b.chiave));
}

function aggiorna() {
    if (!esitoCorrente) return;
    const base = baseFiltrata();
    const riepPrel = riepiloga(base, nomePrel);
    const riepRic  = riepiloga(base, nomeRic);

    if (prelevatoreSel != null && !riepPrel.some(r => r.chiave === prelevatoreSel)) prelevatoreSel = null;
    if (ricevitoreSel  != null && !riepRic.some(r => r.chiave === ricevitoreSel))  ricevitoreSel = null;

    const sintesi = document.getElementById('pannello-sintesi');
    sintesi.classList.remove('nascosto');
    sintesi.innerHTML = `
        <div class="scheda scheda-dato"><h2>${base.length}</h2><p>Campioni non fatturare</p></div>
        <div class="scheda scheda-dato"><h2>${base.filter(c => c.contrattoHaccp).length}</h2><p>In contratto HACCP</p></div>
        <div class="scheda scheda-dato"><h2>${riepPrel.length}</h2><p>Prelevatori</p></div>
        <div class="scheda scheda-dato"><h2>€ ${euro(base.reduce((s, c) => s + (c.prezzoControllo || 0), 0))}</h2><p>Totale prezzo</p></div>`;

    renderRiepilogo(riepPrel, 'tabella-riepilogo', 'conteggio-riepilogo', prelevatoreSel, 'prelevatori',
        v => { prelevatoreSel = (prelevatoreSel === v) ? null : v; aggiorna(); });
    renderRiepilogo(riepRic, 'tabella-riepilogo-ric', 'conteggio-riepilogo-ric', ricevitoreSel, 'ricevitori',
        v => { ricevitoreSel = (ricevitoreSel === v) ? null : v; aggiorna(); });
    renderDettaglio(base);
}

function renderRiepilogo(riep, tabellaId, contatoreId, selCorrente, etichetta, onClick) {
    document.getElementById(contatoreId).textContent =
        `${riep.length} ${etichetta} · clicca una riga per filtrare`;

    const corpo = document.querySelector(`#${tabellaId} tbody`);
    if (riep.length === 0) {
        corpo.innerHTML = '<tr><td colspan="4" class="vuoto">Nessun campione.</td></tr>';
        return;
    }

    const totCamp  = riep.reduce((s, r) => s + r.campioni, 0);
    const totHaccp = riep.reduce((s, r) => s + r.inContratto, 0);
    const totEuro  = riep.reduce((s, r) => s + r.totale, 0);

    corpo.innerHTML = riep.map(r => `
        <tr class="riga-cliccabile ${r.chiave === selCorrente ? 'riga-attiva' : ''}"
            data-chiave="${testo(r.chiave)}">
            <td>${testo(r.chiave)}</td>
            <td class="numerico"><strong>${r.campioni}</strong></td>
            <td class="numerico">${r.inContratto}</td>
            <td class="numerico">${euro(r.totale)}</td>
        </tr>`).join('') + `
        <tr style="background:var(--grigio-100)">
            <td><strong>Totale generale</strong></td>
            <td class="numerico"><strong>${totCamp}</strong></td>
            <td class="numerico"><strong>${totHaccp}</strong></td>
            <td class="numerico"><strong>€ ${euro(totEuro)}</strong></td>
        </tr>`;

    document.querySelectorAll(`#${tabellaId} .riga-cliccabile`).forEach(riga =>
        riga.addEventListener('click', () => onClick(riga.dataset.chiave)));
}

function renderDettaglio(base) {
    let righe = base;
    if (prelevatoreSel != null) righe = righe.filter(c => nomePrel(c) === prelevatoreSel);
    if (ricevitoreSel  != null) righe = righe.filter(c => nomeRic(c) === ricevitoreSel);

    const parti = [];
    if (prelevatoreSel != null) parti.push(`prel. ${testo(prelevatoreSel)}`);
    if (ricevitoreSel  != null) parti.push(`ric. ${testo(ricevitoreSel)}`);
    const capNota = righe.length > 3000 ? ' (mostrate le prime 3000)' : '';
    document.getElementById('conteggio-dettaglio').textContent =
        `${righe.length} campioni${parti.length ? ' — ' + parti.join(', ') : ''}${capNota}`;

    document.getElementById('btn-mostra-tutti').classList.toggle('nascosto',
        prelevatoreSel == null && ricevitoreSel == null);
    const avviso = document.getElementById('avviso-troncato');
    avviso.classList.toggle('nascosto', !esitoCorrente.troncato);
    if (esitoCorrente.troncato)
        avviso.textContent =
            `Elenco troncato a ${esitoCorrente.totale} campioni: affina il periodo per un conteggio completo.`;

    document.querySelector('#tabella-dettaglio tbody').innerHTML =
        righe.length === 0
            ? '<tr><td colspan="9" class="vuoto">Nessun campione con i filtri impostati.</td></tr>'
            : righe.slice(0, 3000).map(c => `
                <tr>
                    <td>${testo(nomePrel(c))}</td>
                    <td>${testo(nomeRic(c))}</td>
                    <td>${testo(c.ragioneSociale)}</td>
                    <td>${testo(c.nomeInsediamento) || '—'}</td>
                    <td class="codice">${testo(c.numeroCampione)}</td>
                    <td>${testo(c.controllo) || '—'}${c.idControllo ? ` <span style="color:#9a9a9a">(${c.idControllo})</span>` : ''}</td>
                    <td>${testo(c.dataRicezione) || '—'}</td>
                    <td>${c.contrattoHaccp
                        ? '<strong style="color:#1a7f37">Sì</strong>'
                        : '<span style="color:#9a9a9a">No</span>'}</td>
                    <td class="numerico">${c.prezzoControllo != null ? euro(c.prezzoControllo) : '—'}</td>
                </tr>`).join('');
}

// ── Esportazione ───────────────────────────────────────────

async function esporta(pulsante) {
    if (!esitoCorrente) return;
    const libera = occupa(pulsante, 'Generazione…');

    const hac = document.getElementById('f-haccp').value;
    const criteri = {
        dataDa: document.getElementById('f-da').value,
        dataA:  document.getElementById('f-a').value,
        cliente: document.getElementById('f-cliente').value.trim() || null,
        insediamento: document.getElementById('f-insediamento').value.trim() || null,
        soloHaccp: hac === 'si' ? true : hac === 'no' ? false : null,
        prelevatore: document.getElementById('f-prelevatore').value || null,
        ricevitore: document.getElementById('f-ricevitore').value || null,
        controlli: controlliSelezionati()
    };

    try {
        const risposta = await fetch('/api/non-fatturare/esporta', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(criteri)
        });
        if (!risposta.ok) throw new Error('Generazione del foglio non riuscita.');

        const intestazione = risposta.headers.get('content-disposition') ?? '';
        const nome = intestazione.match(/filename=([^;]+)/)?.[1]?.trim() ?? 'NonFatturare.xlsx';
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

    ['f-cliente', 'f-insediamento'].forEach(id =>
        document.getElementById(id).addEventListener('input', aggiorna));
    document.getElementById('f-haccp').addEventListener('change', aggiorna);
    document.getElementById('f-prelevatore').addEventListener('change', aggiorna);
    document.getElementById('f-ricevitore').addEventListener('change', aggiorna);
    ['c-180', 'c-203', 'c-2513'].forEach(id =>
        document.getElementById(id).addEventListener('change', aggiorna));
    document.getElementById('c-altro').addEventListener('input', aggiorna);
    document.getElementById('btn-mostra-tutti').addEventListener('click', () => {
        prelevatoreSel = null;
        ricevitoreSel = null;
        aggiorna();
    });
});
