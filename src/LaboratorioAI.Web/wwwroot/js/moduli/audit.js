// Visore del registro attività (audit).
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

async function cerca() {
    const p = new URLSearchParams();
    if ($('area').value) p.set('area', $('area').value);
    if ($('utente').value.trim()) p.set('utente', $('utente').value.trim());
    if ($('azione').value.trim()) p.set('azione', $('azione').value.trim());
    if ($('dal').value) p.set('dal', $('dal').value);
    if ($('al').value) p.set('al', $('al').value);
    p.set('limite', '500');
    $('conteggio').textContent = 'ricerca…';
    try {
        const r = await fetch('/api/audit?' + p.toString());
        if (!r.ok) throw new Error('Errore');
        const righe = await r.json();
        $('tabella-audit').tBodies[0].innerHTML = righe.map(v => `<tr>
            <td>${new Date(v.dataOra).toLocaleString('it-IT')}</td>
            <td>${esc(v.emailUtente)}</td>
            <td>${esc(v.area)}</td>
            <td>${esc(v.azione)}</td>
            <td>${esc([v.tipoEntita, v.idEntita].filter(Boolean).join(' '))}</td>
            <td>${v.esito === 'Successo' ? '<span style="color:#1a7f37">' + esc(v.esito) + '</span>' : '<span style="color:#b3261e">' + esc(v.esito) + '</span>'}</td>
            <td class="sottotesto" style="max-width:40rem;white-space:normal">${esc(v.dettaglio)}</td>
        </tr>`).join('') || '<tr><td colspan="7">Nessuna voce.</td></tr>';
        $('conteggio').textContent = righe.length + ' voci';
    } catch (e) { $('conteggio').textContent = e.message; }
}

$('btn-cerca')?.addEventListener('click', cerca);
cerca(); // primo caricamento
