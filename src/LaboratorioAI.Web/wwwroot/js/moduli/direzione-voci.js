// Cruscotto Direzione (13/10/2026): classificazione delle voci AZW.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const euro = v => Number(v || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
const modifica = $('tabella-voci').dataset.modifica === '1';
let dati = null;

async function carica() {
    const r = await fetch('/api/direzione/voci');
    dati = await r.json();
    disegna();
}

function disegna() {
    const tb = $('tabella-voci').tBodies[0];
    const ricavi = $('f-ricavi').checked, verifica = $('f-verifica').checked;
    const voci = dati.voci.filter(v => (ricavi || !v.ricavo) && (!verifica || v.daVerificare || (!v.ricavo && !v.tipoCosto)));
    if (!voci.length) { tb.innerHTML = '<tr><td colspan="7" class="sottotesto">Nessuna voce.</td></tr>'; return; }
    tb.innerHTML = '';
    for (const v of voci) {
        const tr = document.createElement('tr');
        if (v.daVerificare || (!v.ricavo && !v.tipoCosto)) tr.style.background = '#fff8e1';
        const dis = (!modifica || v.ricavo) ? 'disabled' : '';
        const tipo = v.ricavo ? '<span class="sottotesto">ricavo</span>'
            : `<select class="tipo" ${dis}><option value="">— nessuno —</option>
                ${['Fisso', 'Variabile'].map(t => `<option ${v.tipoCosto === t ? 'selected' : ''}>${t}</option>`).join('')}</select>`;
        const macro = v.ricavo ? '<span class="sottotesto">Ricavo</span>'
            : `<select class="macro" ${dis}>${dati.macrocategorie.filter(m => m !== 'Ricavo')
                .map(m => `<option ${v.macrocategoria === m ? 'selected' : ''}>${esc(m)}</option>`).join('')}</select>`;
        const mp = v.ricavo ? '' : `<input type="checkbox" class="mp" ${v.materiaPrima ? 'checked' : ''} ${dis} />`;
        tr.innerHTML = `<td><strong>${esc(v.codiceVoce)}</strong>${v.daVerificare ? ' <span class="etichetta" style="background:#fff3cd;color:#7a5b00">nuova</span>' : ''}</td>
            <td>${esc(v.descrizione)}</td><td>${tipo}</td><td>${macro}</td><td>${mp}</td>
            <td class="numerico">${euro(v.importoUltimiAnni)}</td>
            <td>${modifica && !v.ricavo ? '<button type="button" class="pulsante pulsante-tenue salva">Salva</button>' : ''}<span class="sottotesto esito"></span></td>`;
        tr.querySelector('.salva')?.addEventListener('click', () => salva(v, tr));
        tb.appendChild(tr);
    }
}

async function salva(v, tr) {
    const out = tr.querySelector('.esito');
    out.textContent = ' salvataggio…';
    try {
        const corpo = { tipoCosto: tr.querySelector('.tipo').value || null, macrocategoria: tr.querySelector('.macro').value, materiaPrima: tr.querySelector('.mp').checked };
        const r = await fetch(`/api/direzione/voci/${v.idClassificazioneVoce}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        Object.assign(v, corpo, { daVerificare: false });
        out.textContent = ' salvato';
        out.style.color = '#1a7f37';
        tr.style.background = !corpo.tipoCosto ? '#fff8e1' : '';
    } catch (e) { out.textContent = ' ' + e.message; out.style.color = '#b00'; }
}

$('f-ricavi')?.addEventListener('change', disegna);
$('f-verifica')?.addEventListener('change', disegna);
carica();
