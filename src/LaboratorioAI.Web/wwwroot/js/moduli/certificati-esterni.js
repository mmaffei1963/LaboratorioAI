// Triage e rinomina dei certificati dei laboratori esterni.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

let fileCorrente = null;
let anteprima = null;

async function analizza() {
    const f = $('file-cert').files[0];
    if (!f) { $('stato').textContent = 'Seleziona un PDF.'; return; }
    fileCorrente = f;
    $('stato').textContent = 'Analisi in corso…';
    const fd = new FormData(); fd.append('file', f);
    try {
        const r = await fetch('/api/certificati-esterni/analizza', { method: 'POST', body: fd });
        if (!r.ok) { const m = (await r.json().catch(() => ({}))).messaggio || 'Errore'; throw new Error(m); }
        anteprima = await r.json();
        render(anteprima);
        $('stato').textContent = '';
    } catch (e) { $('stato').textContent = e.message; }
}

function render(a) {
    const e = a.estrazione || {};
    $('lab').textContent = (e.laboratorio || '') + (a.labAcronimo ? '  ·  ' + a.labAcronimo : '');

    // avvertenze di processo
    $('avvertenze').innerHTML = (a.avvertenze || []).map(x =>
        `<div class="riquadro-tenue" style="border-left:6px solid #b26a00">${esc(x)}</div>`).join('');

    // criticità
    const crit = a.criticita || [];
    $('criticita').innerHTML = crit.length
        ? `<div class="riquadro-tenue" style="border-left:6px solid #b3261e;margin-top:0.4rem">
             <strong style="color:#b3261e">Criticità rilevate</strong>
             <ul>${crit.map(c => `<li>${esc(c)}</li>`).join('')}</ul></div>`
        : `<div class="riquadro-tenue" style="border-left:6px solid #1a7f37;margin-top:0.4rem">
             <strong style="color:#1a7f37">Nessuna criticità rilevata</strong></div>`;

    // dati campione ArcLab
    const c = a.campione;
    const riga = (k, v) => `<tr><td style="width:14rem"><strong>${k}</strong></td><td>${esc(v ?? '—')}</td></tr>`;
    $('dati-campione').innerHTML =
        riga('Nostro codice', e.codiceNostro) +
        riga('RdP esterno', e.rdpEsterno) +
        (c
            ? riga('Cliente', c.ragioneSociale) + riga('Insediamento', (c.idInsediamento ?? '') + ' · ' + (c.nomeInsediamento ?? '')) +
              riga('Prodotto', c.denominazioneProdotto) + riga('Matrice', c.matrice)
            : riga('ArcLab', 'campione non agganciato'));

    // avviso: default = suggerito
    const avv = !!a.avvisoSuggerito;
    document.querySelector('input[name=avviso][value=avv]').checked = avv;
    document.querySelector('input[name=avviso][value=std]').checked = !avv;
    aggiornaNome();

    $('esito-salva').textContent = '';
    $('pannello-esito').classList.remove('nascosto');
}

function aggiornaNome() {
    if (!anteprima) return;
    const avv = document.querySelector('input[name=avviso][value=avv]').checked;
    const nome = avv ? anteprima.nomeConAvviso : anteprima.nomeStandard;
    // se ArcLab non ha agganciato, il nome è null: si compila a mano
    $('nome-file').value = nome || $('nome-file').value || '';
}

async function conferma() {
    if (!fileCorrente) { $('esito-salva').textContent = 'Carica e analizza prima un file.'; return; }
    const nome = $('nome-file').value.trim();
    if (!nome) { $('esito-salva').textContent = 'Nome file mancante.'; return; }
    const c = anteprima?.campione;
    const data = c?.dataRicezione ? new Date(c.dataRicezione).toISOString().slice(0, 10) : '';
    const fd = new FormData();
    fd.append('file', fileCorrente);
    fd.append('nomeFinale', nome);
    fd.append('labAcronimo', anteprima?.labAcronimo || '');
    fd.append('data', data);
    $('esito-salva').textContent = 'Salvataggio…';
    try {
        const r = await fetch('/api/certificati-esterni/conferma', { method: 'POST', body: fd });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore salvataggio');
        $('esito-salva').innerHTML = 'Salvato: <strong>' + esc(j.nome) + '</strong> → ' +
            (j.percorsi || []).map(esc).join(' · ');
    } catch (e) { $('esito-salva').textContent = e.message; }
}

$('btn-analizza')?.addEventListener('click', analizza);
$('btn-conferma')?.addEventListener('click', conferma);
document.querySelectorAll('input[name=avviso]').forEach(r => r.addEventListener('change', aggiornaNome));
