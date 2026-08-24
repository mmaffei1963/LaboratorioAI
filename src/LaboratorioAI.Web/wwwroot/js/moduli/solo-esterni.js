// Solo esterni — confronto certificati esterni/interni + fatturazione (sola lettura).
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

function criteri() {
    return {
        anno: $('f-anno').value ? +$('f-anno').value : null,
        mese: $('f-mese').value ? +$('f-mese').value : null,
        dataDa: $('f-da').value || null,
        dataA: $('f-a').value || null,
        ragioneSociale: $('f-rag').value || null,
        soloSenzaInterno: $('f-senza').checked,
        statoFattura: $('f-fatt').value || null,
        statoCampione: $('f-camp').value || null,
        limite: 1000
    };
}

async function post(url, corpo) {
    const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return r;
}

async function cerca() {
    const btn = $('btn-cerca'); btn.disabled = true; const testo = btn.textContent; btn.textContent = 'Ricerca…';
    try {
        const d = await (await post('/api/solo-esterni/cerca', criteri())).json();
        $('riepilogo').innerHTML = `<strong>${d.totale}</strong> esterni · <span style="color:#b26a00">${d.senzaInterno}</span> senza certificato interno · <span style="color:#b21a1a">${d.nonFatturati}</span> non fatturati`;
        $('tab-esterni').tBodies[0].innerHTML = d.righe.map(r => `<tr>
            <td>${esc(r.anno)}</td><td>${esc(r.lotto)}</td><td>${esc(r.campione)}</td>
            <td>${esc(r.ragioneSociale)}</td><td>${esc(r.dataUpload)}</td>
            <td class="sottotesto" title="${esc(r.nomeFile)}">${esc((r.nomeFile || '').length > 40 ? r.nomeFile.slice(0, 40) + '…' : r.nomeFile)}</td>
            <td style="font-weight:600;color:${r.haInterno ? '#1a7f37' : '#b21a1a'}">${r.haInterno ? 'Sì' : 'NO'}</td>
            <td style="font-weight:600;color:${r.haCampione ? '#1a7f37' : '#b21a1a'}" title="${esc(r.idCampione)}">${r.haCampione ? 'Sì' : 'NO'}</td>
            <td>${esc(r.nCertificato)}</td><td>${esc(r.dataFattura) || '—'}</td>
            <td style="font-weight:600;color:${r.fatturato ? '#1a7f37' : '#b21a1a'}">${r.fatturato ? 'Sì' : 'NO'}</td>
            <td>${esc(r.idControllo)}</td><td>${esc(r.controllo)}</td>
            <td class="numerico">${r.prezzoControllo != null ? Number(r.prezzoControllo).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—'}</td>
        </tr>`).join('') || '<tr><td colspan="14">Nessun risultato con questi filtri.</td></tr>';
    } catch (e) { $('riepilogo').innerHTML = `<span style="color:#b21a1a">${esc(e.message)}</span>`; }
    finally { btn.disabled = false; btn.textContent = testo; }
}

async function esporta() {
    try {
        const resp = await post('/api/solo-esterni/esporta', criteri());
        const blob = await resp.blob();
        const cd = resp.headers.get('content-disposition') || '';
        const nome = (cd.match(/filename="?([^"]+)"?/) || [])[1] || 'SoloEsterni.xlsx';
        const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nome; a.click();
        URL.revokeObjectURL(a.href);
    } catch (e) { alert(e.message); }
}

$('btn-cerca')?.addEventListener('click', cerca);
$('btn-esporta')?.addEventListener('click', esporta);
$('f-rag')?.addEventListener('keydown', e => { if (e.key === 'Enter') cerca(); });
