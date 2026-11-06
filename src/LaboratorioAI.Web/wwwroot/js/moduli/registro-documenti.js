// Registro di controllo dei documenti destinati a Palomar (Ordini di Accettazione, certificati esterni).
// Modulo condiviso: disegna la tabella del registro e il riepilogo di un'esecuzione.
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

const STILI = {
    Ok: ['#e7f5e7', '#256029', 'OK'],
    GiaEmesso: ['#e8eef7', '#1f3b63', 'Già emesso'],
    NonPronto: ['#fff3cd', '#7a5b00', 'Non pronto'],
    Parziale: ['#ffe5cc', '#8a4500', 'Parziale'],
    Errore: ['#f8d7da', '#842029', 'Errore'],
    Annullato: ['#eceff1', '#455a64', 'Annullato']
};

// Certificati esterni (12/10/2026): tipo e stato dei valori in ArcLab per i misti.
const STATI_ARCLAB = {
    DaInserire: ['#fff3cd', '#7a5b00', 'da inserire in ArcLab'],
    Inserito: ['#e7f5e7', '#256029', 'inserito in ArcLab'],
    NonVerificabile: ['#eee', '#333', 'da controllare']
};

function cellaTipo(r) {
    if (!r.tipo) return '<span class="sottotesto">—</span>';
    let h = r.tipo === 'Misto'
        ? '<span class="etichetta" style="background:#efe3f7;color:#5b2a86">Misto</span>'
        : '<span class="etichetta" style="background:#e8eef7;color:#1f3b63">Solo esterni</span>';
    const st = STATI_ARCLAB[r.statoArcLab];
    if (st) h += `<div style="margin-top:.2rem"><span class="etichetta" style="background:${st[0]};color:${st[1]}" title="${esc(r.dettaglioArcLab || '')}">${esc(st[2])}</span></div>`
        + (r.statoArcLab === 'DaInserire' ? `<div class="sottotesto">${esc(r.dettaglioArcLab || '')}</div>` : '');
    return h;
}

export function etichettaEsito(esito) {
    const [sf, col, testo] = STILI[esito] || ['#eee', '#333', esito];
    return `<span class="etichetta" style="background:${sf};color:${col}">${esc(testo)}</span>`;
}

const dataOra = s => {
    if (!s) return '';
    const d = new Date(s);
    return d.toLocaleDateString('it-IT') + ' ' + d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
};

/**
 * Disegna le righe del registro nella tabella indicata.
 * opzioni.riprova(riga) → se presente, mostra il pulsante "Riprova" sulle righe Parziale/Errore non ancora risolte.
 * opzioni.colonnaTipo → aggiunge dopo l'esito la colonna tipo certificato / stato ArcLab (certificati esterni).
 * opzioni.annulla(riga, motivo, rimettiInArrivo) → Promise; se presente, pulsante "Annulla" con motivo obbligatorio.
 */
export function disegnaRegistro(tabella, righe, opzioni = {}) {
    const tb = tabella.querySelector('tbody');
    tb.innerHTML = '';
    if (!righe.length) {
        const n = tabella.querySelectorAll('thead th').length || 6;
        tb.innerHTML = `<tr><td colspan="${n}" class="sottotesto">Nessuna riga nel periodo.</td></tr>`;
        return;
    }
    for (const r of righe) {
        const problema = r.esito === 'Errore' || r.esito === 'Parziale';
        const risolto = problema && r.statoAttuale === 'Inviato';
        const tr = document.createElement('tr');
        if (r.annullato) tr.style.opacity = '0.55';
        else if (problema && !risolto) tr.style.background = '#fff7f7';
        let azione = '';
        if (r.annullato) azione = '';
        else if (risolto) azione = '<span class="sottotesto">risolto in seguito</span>';
        else if (problema && opzioni.riprova && r.idLotto) azione = '<button type="button" class="pulsante pulsante-tenue btn-riprova">Riprova</button>';
        if (opzioni.annulla && !r.annullato) azione += ' <button type="button" class="pulsante pulsante-tenue btn-annulla" title="Toglie il certificato (motivo obbligatorio); la riga resta tracciata">Annulla</button>';
        const annullo = r.annullato
            ? `<div style="margin-top:.2rem">${etichettaEsito('Annullato')} <span class="sottotesto">${esc(dataOra(r.annullatoIl))}: ${esc(r.motivoAnnullamento || '')}</span></div>` : '';
        tr.innerHTML = `
            <td style="white-space:nowrap">${esc(dataOra(r.dataOra))}</td>
            <td>${etichettaEsito(r.esito)}</td>
            ${opzioni.colonnaTipo ? `<td>${cellaTipo(r)}</td>` : ''}
            <td>${esc(r.riferimento)}</td>
            <td>${esc(r.messaggio)}${annullo}</td>
            <td class="sottotesto" style="word-break:break-all">${esc(r.filePalomar || '')}</td>
            <td style="white-space:nowrap">${azione}<div class="sottotesto">${esc(r.origine || '')}</div><div class="form-annulla"></div></td>`;
        tr.querySelector('.btn-riprova')?.addEventListener('click', () => opzioni.riprova(r));
        tr.querySelector('.btn-annulla')?.addEventListener('click', () => mostraFormAnnulla(tr, r, opzioni.annulla));
        tb.appendChild(tr);
    }
}

/** Testo di riepilogo di un'esecuzione (conteggi). */
export function testoRiepilogo(r) {
    const parti = [`${r.ok} ok`];
    if (r.parziali) parti.push(`${r.parziali} parziali (non in Palomar)`);
    if (r.errori) parti.push(`${r.errori} errori`);
    if (r.nonPronti) parti.push(`${r.nonPronti} non pronti`);
    if (r.giaEmessi) parti.push(`${r.giaEmessi} già emessi`);
    let t = `Esecuzione ${r.idEsecuzione}: ${r.totale} elaborati — ${parti.join(', ')}.`;
    if (r.emailInviata) t += ' Inviata mail di riepilogo degli errori.';
    return t;
}

// Riquadro in riga per l'annullamento: motivo obbligatorio, poi conferma.
function mostraFormAnnulla(tr, r, annulla) {
    const box = tr.querySelector('.form-annulla');
    if (box.innerHTML) { box.innerHTML = ''; return; }
    const daCartella = r.origine === 'cartella';
    box.innerHTML = `<div style="margin-top:.3rem;white-space:normal;min-width:16rem">
        <input type="text" class="motivo" maxlength="500" style="width:100%" placeholder="motivo (es. certificato misto caricato per errore)"/>
        ${daCartella ? '<label class="casella"><input type="checkbox" class="rimetti" checked/> rimetti l\'originale in arrivo</label>' : ''}
        <div style="margin-top:.2rem"><button type="button" class="pulsante conferma-annulla">Conferma annullamento</button>
        <button type="button" class="pulsante pulsante-tenue chiudi-annulla">Chiudi</button></div>
        <div class="sottotesto esito-annulla"></div></div>`;
    box.querySelector('.chiudi-annulla').addEventListener('click', () => { box.innerHTML = ''; });
    box.querySelector('.conferma-annulla').addEventListener('click', async () => {
        const motivo = box.querySelector('.motivo').value.trim();
        const out = box.querySelector('.esito-annulla');
        if (motivo.length < 3) { out.textContent = 'Scrivi il motivo.'; out.style.color = '#b00'; return; }
        const btn = box.querySelector('.conferma-annulla'); btn.disabled = true;
        try { await annulla(r, motivo, box.querySelector('.rimetti')?.checked ?? true); }
        catch (e) { out.textContent = e.message; out.style.color = '#b00'; btn.disabled = false; }
    });
    box.querySelector('.motivo').focus();
}
