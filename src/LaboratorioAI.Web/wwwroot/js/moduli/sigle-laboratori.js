// Amministrazione — Sigle laboratori esterni (nome certificati esterni per Palomar).
// Una riga per laboratorio ArcLab; "Attivo" è una spunta, mai una cancellazione fisica.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return r.json();
}

function riga(v) {
    const tr = document.createElement('tr');
    tr.dataset.id = v.idLabEsterno;
    tr.innerHTML = `
        <td class="numerico">${v.idLabEsterno}</td>
        <td>${esc(v.nome)}${v.salvata ? '' : ' <span class="etichetta" title="proposta non ancora salvata">da salvare</span>'}</td>
        <td><input type="text" class="s-sigla" value="${esc(v.sigla)}" maxlength="20" style="width:7rem;text-transform:uppercase" /></td>
        <td><input type="text" class="s-parole" value="${esc(v.paroleChiave)}" maxlength="500" style="width:100%" /></td>
        <td style="text-align:center"><input type="checkbox" class="s-attivo" ${v.attivo ? 'checked' : ''} /></td>
        <td><button type="button" class="pulsante pulsante-tenue s-salva">Salva</button> <span class="s-esito sottotesto"></span></td>`;
    tr.querySelector('.s-salva').addEventListener('click', () => salva(tr));
    return tr;
}

async function salva(tr) {
    const esito = tr.querySelector('.s-esito');
    const dto = {
        idLabEsterno: Number(tr.dataset.id),
        sigla: tr.querySelector('.s-sigla').value.trim().toUpperCase(),
        paroleChiave: tr.querySelector('.s-parole').value.trim(),
        attivo: tr.querySelector('.s-attivo').checked
    };
    try {
        const r = await api('/api/sigle-laboratori', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto) });
        tr.querySelector('.s-sigla').value = r.sigla;
        tr.querySelector('.s-parole').value = r.paroleChiave || '';
        tr.querySelector('.etichetta')?.remove();
        esito.style.color = '#1a7f37'; esito.textContent = 'salvata';
    } catch (e) { esito.style.color = '#b00'; esito.textContent = e.message; }
}

async function carica() {
    const voci = await api('/api/sigle-laboratori');
    const corpo = $('corpo-sigle');
    corpo.innerHTML = '';
    voci.forEach(v => corpo.appendChild(riga(v)));
    const daSalvare = voci.filter(v => !v.salvata).length;
    $('msg-sigle').textContent = `${voci.length} laboratori` + (daSalvare ? ` · ${daSalvare} ancora da salvare` : '');
}

carica().catch(e => { $('msg-sigle').textContent = e.message; $('msg-sigle').style.color = '#b00'; });
