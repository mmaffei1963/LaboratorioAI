/* ═══════════════════════════════════════════════════════════
   Tamponi in contratto — prefatturazione
   ═══════════════════════════════════════════════════════════ */
import { inviaJson, testo, avvisa, occupa } from '../comune.js';

let ultimiCriteri = null;
let esitoCorrente = null;
let insediamentoSel = null;   // id insediamento selezionato dal riepilogo (o null)

function impostaPeriodoPredefinito() {
    const oggi = new Date();
    const inizioAnno = new Date(oggi.getFullYear(), 0, 1);
    document.getElementById('f-a').value  = oggi.toISOString().slice(0, 10);
    document.getElementById('f-da').value = inizioAnno.toISOString().slice(0, 10);
}

function leggiCriteri() {
    const controlli = [];
    if (document.getElementById('c-180').checked)  controlli.push(180);
    if (document.getElementById('c-203').checked)  controlli.push(203);
    if (document.getElementById('c-2513').checked) controlli.push(2513);
    const altro = Number(document.getElementById('c-altro').value);
    if (altro) controlli.push(altro);

    return {
        dataDa: document.getElementById('f-da').value,
        dataA:  document.getElementById('f-a').value,
        controlli,
        fatturazione: Number(document.getElementById('f-fatturazione').value),
        limite: 50000
    };
}

// ── Interrogazione ─────────────────────────────────────────

async function interroga(pulsante) {
    const criteri = leggiCriteri();

    if (!criteri.dataDa || !criteri.dataA) {
        avvisa('Indicare il periodo (data certificato).', 'info');
        return;
    }

    const libera = occupa(pulsante, 'Interrogazione…');

    try {
        const esito = await inviaJson('/api/tamponi-contratto/interroga', criteri);
        ultimiCriteri = criteri;
        disegna(esito);
        document.getElementById('btn-esporta').disabled = esito.totaleTamponi === 0;
    } catch (errore) {
        avvisa(errore.message, 'errore');
    } finally {
        libera();
    }
}

function siNo(valore) {
    return valore
        ? '<strong style="color:#1a7f37">Sì</strong>'
        : '<span style="color:#9a9a9a">No</span>';
}

function disegna(esito) {
    esitoCorrente = esito;
    insediamentoSel = null;

    document.getElementById('pannello-esiti').style.display = 'block';
    popolaPrelevatori(esito.tamponi);
    popolaRicevitori(esito.tamponi);
    aggiorna();
}

// ── Filtri ─────────────────────────────────────────────────

function passaHaccp(riga) {
    const f = document.getElementById('f-haccp').value;
    return f === '' || (f === 'si' && riga.contrattoHaccp) || (f === 'no' && !riga.contrattoHaccp);
}

function popolaPrelevatori(tamponi) {
    const nomi = [...new Set(tamponi.map(t => t.prelevatore).filter(n => n && n.trim()))].sort();
    document.getElementById('f-prelevatore').innerHTML =
        '<option value="">Tutti</option>' +
        nomi.map(n => `<option value="${testo(n)}">${testo(n)}</option>`).join('');
}

function popolaRicevitori(tamponi) {
    const nomi = [...new Set(tamponi.map(t => t.ricevitore).filter(n => n && n.trim()))].sort();
    document.getElementById('f-ricevitore').innerHTML =
        '<option value="">Tutti</option>' +
        nomi.map(n => `<option value="${testo(n)}">${testo(n)}</option>`).join('');
}

// Tamponi filtrati per HACCP e prelevatore (l'insediamento è il drill-down
// del solo dettaglio, non un filtro della lista).
function baseFiltrata() {
    const prel = document.getElementById('f-prelevatore').value;
    const ric  = document.getElementById('f-ricevitore').value;
    let righe = esitoCorrente.tamponi.filter(passaHaccp);
    if (prel) righe = righe.filter(t => (t.prelevatore || '') === prel);
    if (ric)  righe = righe.filter(t => (t.ricevitore || '') === ric);
    return righe;
}

// Ricostruisce il riepilogo per insediamento dai tamponi filtrati.
function riepiloga(tamponi) {
    const mappa = new Map();
    for (const t of tamponi) {
        let r = mappa.get(t.idInsediamento);
        if (!r) {
            r = { idInsediamento: t.idInsediamento, ragioneSociale: t.ragioneSociale,
                  nomeInsediamento: t.nomeInsediamento, contrattoHaccp: false,
                  tamponiInclusi: null, tamponiFatti: 0, prel: new Set() };
            mappa.set(t.idInsediamento, r);
        }
        r.tamponiFatti++;
        if (t.contrattoHaccp) r.contrattoHaccp = true;
        if (t.tamponiInclusi != null)
            r.tamponiInclusi = Math.max(r.tamponiInclusi ?? 0, t.tamponiInclusi);
        if (t.prelevatore && t.prelevatore.trim()) r.prel.add(t.prelevatore);
    }
    return [...mappa.values()]
        .map(r => ({
            ...r,
            differenza: r.tamponiInclusi == null ? null : r.tamponiFatti - r.tamponiInclusi,
            prelevatori: [...r.prel].join(', ')
        }))
        .sort((a, b) => (a.ragioneSociale || '').localeCompare(b.ragioneSociale || '')
                     || (a.nomeInsediamento || '').localeCompare(b.nomeInsediamento || ''));
}

// Aggiorna sintesi + riepilogo + dettaglio secondo i filtri correnti.
function aggiorna() {
    if (!esitoCorrente) return;
    const base = baseFiltrata();
    const riep = riepiloga(base);

    // Se l'insediamento selezionato è uscito dal set filtrato, azzera.
    if (insediamentoSel != null && !riep.some(r => r.idInsediamento === insediamentoSel))
        insediamentoSel = null;

    const sintesi = document.getElementById('pannello-sintesi');
    sintesi.classList.remove('nascosto');
    sintesi.innerHTML = `
        <div class="scheda scheda-dato"><h2>${base.length}</h2><p>Tamponi</p></div>
        <div class="scheda scheda-dato"><h2>${base.filter(t => t.contrattoHaccp).length}</h2><p>In contratto HACCP</p></div>
        <div class="scheda scheda-dato"><h2>${base.filter(t => !t.contrattoHaccp).length}</h2><p>Fuori contratto</p></div>
        <div class="scheda scheda-dato"><h2>${riep.length}</h2><p>Insediamenti</p></div>`;

    renderRiepilogo(riep);
    renderDettaglio(base, riep);
}

function renderRiepilogo(riep) {
    document.getElementById('conteggio-riepilogo').textContent =
        `${riep.length} insediamenti · clicca una riga per filtrare`;

    document.querySelector('#tabella-riepilogo tbody').innerHTML =
        riep.length === 0
            ? '<tr><td colspan="7" class="vuoto">Nessun insediamento.</td></tr>'
            : riep.map(r => `
                <tr class="riga-cliccabile ${r.idInsediamento === insediamentoSel ? 'riga-attiva' : ''}"
                    data-ins="${r.idInsediamento}">
                    <td>${testo(r.ragioneSociale)}</td>
                    <td>${testo(r.nomeInsediamento) || '—'}</td>
                    <td>${siNo(r.contrattoHaccp)}</td>
                    <td class="numerico">${r.tamponiInclusi ?? '—'}</td>
                    <td class="numerico"><strong>${r.tamponiFatti}</strong></td>
                    <td class="numerico">${r.differenza ?? '—'}</td>
                    <td>${testo(r.prelevatori) || '—'}</td>
                </tr>`).join('');

    document.querySelectorAll('#tabella-riepilogo .riga-cliccabile').forEach(riga =>
        riga.addEventListener('click', () => {
            const id = Number(riga.dataset.ins);
            insediamentoSel = (insediamentoSel === id) ? null : id;
            aggiorna();
        }));
}

function renderDettaglio(base, riep) {
    let righe = base;
    if (insediamentoSel != null) righe = righe.filter(t => t.idInsediamento === insediamentoSel);

    const sel = insediamentoSel != null
        ? riep.find(r => r.idInsediamento === insediamentoSel)
        : null;
    const capNota = righe.length > 3000 ? ' (mostrate le prime 3000)' : '';
    document.getElementById('conteggio-dettaglio').textContent =
        (sel ? `${righe.length} tamponi — ${testo(sel.nomeInsediamento) || testo(sel.ragioneSociale)}`
             : `${righe.length} tamponi`) + capNota;

    document.getElementById('btn-mostra-tutti').classList.toggle('nascosto', insediamentoSel == null);
    const avviso = document.getElementById('avviso-troncato');
    avviso.classList.toggle('nascosto', !esitoCorrente.troncato);
    if (esitoCorrente.troncato)
        avviso.textContent =
            `Elenco troncato a ${esitoCorrente.totaleTamponi} tamponi a video: `
            + `affina i criteri (l'esportazione in Excel contiene comunque tutte le righe).`;

    document.querySelector('#tabella-dettaglio tbody').innerHTML =
        righe.length === 0
            ? '<tr><td colspan="10" class="vuoto">Nessun tampone con i filtri impostati.</td></tr>'
            : righe.slice(0, 3000).map(t => `
                <tr>
                    <td>${testo(t.ragioneSociale)}</td>
                    <td>${testo(t.nomeInsediamento) || '—'}</td>
                    <td class="codice">${testo(t.numeroCampione)}</td>
                    <td>${testo(t.controllo) || '—'}${t.idControllo ? ` <span style="color:#9a9a9a">(${t.idControllo})</span>` : ''}</td>
                    <td>${testo(t.numeroCertificato) || '—'}</td>
                    <td>${testo(t.dataCertificato) || '—'}</td>
                    <td>${testo(t.prelevatore) || '—'}</td>
                    <td>${t.nonFatturare ? '<strong style="color:#6B21A8">Non fatt.</strong>' : '—'}</td>
                    <td>${siNo(t.contrattoHaccp)}</td>
                    <td class="numerico">${t.tamponiInclusi ?? '—'}</td>
                </tr>`).join('');
}

// ── Esportazione ───────────────────────────────────────────

async function esporta(pulsante) {
    if (!ultimiCriteri) return;

    const libera = occupa(pulsante, 'Generazione…');

    // L'Excel rispecchia i filtri impostati a video
    const hac = document.getElementById('f-haccp').value;
    const criteriExport = {
        ...ultimiCriteri,
        soloHaccp: hac === 'si' ? true : hac === 'no' ? false : null,
        prelevatore: document.getElementById('f-prelevatore').value || null,
        ricevitore: document.getElementById('f-ricevitore').value || null,
        idInsediamento: insediamentoSel
    };

    try {
        const risposta = await fetch('/api/tamponi-contratto/esporta', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(criteriExport)
        });

        if (!risposta.ok) throw new Error('Generazione del foglio non riuscita.');

        const intestazione = risposta.headers.get('content-disposition') ?? '';
        const nome = intestazione.match(/filename=([^;]+)/)?.[1]?.trim()
                  ?? 'TamponiInContratto.xlsx';

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

    document.getElementById('btn-cerca').addEventListener('click', e => interroga(e.currentTarget));
    document.getElementById('btn-esporta')?.addEventListener('click', e => esporta(e.currentTarget));

    document.getElementById('f-haccp').addEventListener('change', aggiorna);
    document.getElementById('f-prelevatore').addEventListener('change', aggiorna);
    document.getElementById('f-ricevitore').addEventListener('change', aggiorna);
    document.getElementById('btn-mostra-tutti').addEventListener('click', () => {
        insediamentoSel = null;
        aggiorna();
    });
});
