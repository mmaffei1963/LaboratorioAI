// Cruscotto Direzione, fase 3 (13/10/2026): rendimento dei commerciali.
import { montaCommentoAi } from './commento-ai-direzione.js';
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const euro = v => v == null ? '—' : Math.round(Number(v)).toLocaleString('it-IT') + ' €';
const pct = v => v == null ? '—' : (Number(v) * 100).toLocaleString('it-IT', { maximumFractionDigits: 1 }) + '%';
const num = v => v == null ? '—' : Number(v).toLocaleString('it-IT', { maximumFractionDigits: 2 });
const dataOra = s => s ? new Date(s).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';
let elencoCaricato = false;

const RIGHE = [
    ['Costo stipendio (voce AZW)', a => euro(a.costoStipendio)],
    ['Provvigioni', a => euro(a.provvigioni)],
    ['Costo totale', a => euro(a.costoTotale), true],
    ['Portato (base provvigioni)', a => euro(a.portato), true],
    ['di cui clienti nuovi', a => euro(a.portatoNuovi), false, true],
    ['Clienti portati', a => a.clientiPortati, false, true],
    ['Seguito (codice venditore)', a => euro(a.seguito), true],
    ['Clienti seguiti: attivi · nuovi · persi', a => `${a.clientiSeguiti} · <span style="color:#1a7f37">${a.clientiSeguitiNuovi}</span> · <span style="color:#b3261e">${a.clientiSeguitiPersi}</span>`
        + (a.clientiSeguitiAcquisiti ? ` <span class="sottotesto" title="acquisiti da un nostro cliente: non persi">(+${a.clientiSeguitiAcquisiti} acquisiti)</span>` : ''), false, true],
    ['Incidenza costo su portato', a => pct(a.incidenzaSuPortato)],
    ['Incidenza costo su seguito', a => pct(a.incidenzaSuSeguito)],
    ['€ portati per € di costo', a => num(a.portatoPerEuro)],
    ['Margine di contribuzione % aree', a => pct(a.margineContribuzionePercento)],
    ['Margine portato', a => euro(a.marginePortato)],
    ['Contributo (margine portato − costo)', a => a.contributo == null ? '—'
        : `<span style="color:${a.contributo >= 0 ? '#1a7f37' : '#b3261e'};font-weight:600">${euro(a.contributo)}</span>`, true],
    ['Incassato sulle fatture dell\'anno (seguiti)', a => pct(a.percentualeIncassata)],
];

function parametri() {
    const p = new URLSearchParams();
    for (const f of ['commerciale', 'dal', 'al']) { const v = $('f-' + f).value; if (v) p.set(f, v); }
    return p;
}

async function carica() {
    $('schede').innerHTML = '<p class="sottotesto">Calcolo in corso… (le provvigioni richiedono qualche secondo)</p>';
    try {
        const r = await fetch('/api/direzione/commerciali?' + parametri().toString());
        const j = await r.json();
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        if (!elencoCaricato) {
            const sel = $('f-commerciale');
            for (const c of j.elenco) { const o = document.createElement('option'); o.value = c.idCommerciale; o.textContent = c.nome; sel.appendChild(o); }
            $('f-dal').value = j.anni[0]; $('f-al').value = j.anni[j.anni.length - 1];
            elencoCaricato = true;
        }
        $('aggiornamento').innerHTML = j.aggiornamento
            ? `Dati AZW del file aggiornato il <strong>${esc(dataOra(j.aggiornamento.file))}</strong>, importati il ${esc(dataOra(j.aggiornamento.importazione))}.`
              + (j.aggiornamento.vecchio ? ' <span style="color:#b00;font-weight:600">Attenzione: il file AZW non si aggiorna da più di un giorno lavorativo.</span>' : '')
            : '<span style="color:#b00">Nessuna importazione AZW riuscita.</span>';
        $('schede').innerHTML = j.commerciali.map(c => scheda(c, j.annoRiferimento)).join('') || '<p class="sottotesto">Nessun commerciale attivo.</p>';
    } catch (e) {
        $('schede').innerHTML = `<p style="color:#b00">${esc(e.message)}</p>`;
    }
}

function scheda(c, annoRif) {
    const anni = c.anni;
    const testa = '<tr><th></th>' + anni.map(a => `<th class="numerico"${c.annoInizio === a.anno ? ' style="border-left:2px solid #6b4fbb"' : ''}>${a.anno}</th>`).join('') + '</tr>';
    const corpo = RIGHE.map(([nome, f, forte, rientro]) => `<tr${forte ? ' style="font-weight:600"' : ''}><td style="white-space:nowrap${rientro ? ';padding-left:1.4rem' : ''}">${esc(nome)}</td>`
        + anni.map(a => `<td class="numerico"${c.annoInizio === a.anno ? ' style="border-left:2px solid #6b4fbb"' : ''}>${f(a)}</td>`).join('') + '</tr>').join('');
    const cum = c.cumulato.length
        ? '<p>Contributo cumulato dal primo anno di costo: ' + c.cumulato.map(x => `${x.anno} <strong style="color:${x.valore >= 0 ? '#1a7f37' : '#b3261e'}">${euro(x.valore)}</strong>`).join(' · ') + '</p>'
        : '';
    const aree = c.aree.length ? ' — aree: ' + c.aree.map(a => `${esc(a.centro)} ${pct(a.peso)}`).join(', ') : '';
    const avvisi = c.avvisi.length
        ? `<div style="background:#fff3cd;border:1px solid #e0c46c;padding:0.4rem 0.8rem;border-radius:6px;margin:0.4rem 0">${c.avvisi.map(esc).join('<br>')}</div>` : '';
    const clienti = c.clienti.length
        ? `<details style="margin-top:0.6rem"><summary style="cursor:pointer">Clienti portati (base provvigioni ${annoRif - 1} e ${annoRif})</summary>
            <div class="tabella-scorrevole"><table class="tabella"><thead><tr><th>Cliente</th><th>Prov.</th><th class="numerico">${annoRif - 1}</th><th class="numerico">${annoRif}</th></tr></thead><tbody>`
          + c.clienti.map(x => `<tr><td>${esc(x.nome)}</td><td>${esc(x.provincia || '')}</td><td class="numerico">${euro(x.annoPrima)}</td><td class="numerico">${euro(x.annoRiferimento)}</td></tr>`).join('')
          + `</tbody></table></div>${c.altriClienti ? `<p class="sottotesto">… e altri ${c.altriClienti} (tutti nell'Excel).</p>` : ''}</details>`
        : '';
    return `<section class="riquadro">
        <h2 style="margin-top:0">${esc(c.nome)}</h2>
        <p class="sottotesto">${c.voceCosto ? `Voce di costo AZW <strong>${esc(c.voceCosto)}</strong> (${esc(c.descrizioneVoceCosto || '')})` : 'Nessuna voce di costo AZW'}${aree}${c.annoInizio ? ` — costo dal ${c.annoInizio} (linea viola)` : ''}</p>
        ${avvisi}
        <div class="tabella-scorrevole"><table class="tabella"><thead>${testa}</thead><tbody>${corpo}</tbody></table></div>
        ${cum}${clienti}
    </section>`;
}

$('btn-cerca')?.addEventListener('click', carica);
$('btn-excel')?.addEventListener('click', () => { location.href = '/api/direzione/commerciali/excel?' + parametri().toString(); });
$('btn-importa')?.addEventListener('click', async (e) => {
    const b = e.currentTarget; b.disabled = true; const t = b.textContent; b.textContent = 'Importazione in corso…';
    try {
        const r = await fetch('/api/direzione/importa-azw', { method: 'POST' });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        await carica();
        $('aggiornamento').insertAdjacentHTML('afterbegin', `<span style="color:#1a7f37">${esc(j.messaggio)}</span> `);
    } catch (err) { $('aggiornamento').innerHTML = `<span style="color:#b00">${esc(err.message)}</span>`; }
    finally { b.disabled = false; b.textContent = t; }
});

const contenitoreAi = $('commento-ai');
if (contenitoreAi) montaCommentoAi({
    pagina: 'commerciali', contenitore: contenitoreAi,
    filtri: () => ({ commerciale: Number($('f-commerciale').value) || null, dal: Number($('f-dal').value) || null, al: Number($('f-al').value) || null })
});
carica();
