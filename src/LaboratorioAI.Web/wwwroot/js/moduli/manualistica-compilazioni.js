// Manualistica: le check list compilate per i clienti (10/11/2026).
// Elenco e apertura di una check list nuova. Nessuna libreria esterna.

const $ = (s) => document.querySelector(s);

let compilazioni = [];
let puoCompilare = false;

const esc = (v) => String(v == null ? '' : v)
  .replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const ETICHETTE_STATO = {
  Bozza: 'in lavorazione',
  Generata: 'documenti generati',
  InAttesaFirma: 'in attesa di firma',
  Inviata: 'consegnata',
  Annullata: 'annullata'
};

async function carica() {
  const r = await fetch('/api/manualistica/compilazioni');
  if (!r.ok) { avviso('Non riesco a leggere l\'elenco.'); return; }
  const d = await r.json();
  compilazioni = d.compilazioni || [];
  puoCompilare = d.puoCompilare;
  disegna();
}

function filtra() {
  const s = $('#f-stato').value;
  const t = $('#f-testo').value.trim().toLowerCase();
  return compilazioni.filter((c) => {
    if (s && c.stato !== s) return false;
    if (t && !c.ragioneSociale.toLowerCase().includes(t)) return false;
    return true;
  });
}

function disegna() {
  const righe = filtra();
  const corpo = $('#tabella-compilazioni tbody');

  $('#riepilogo').textContent = compilazioni.length
    ? `${compilazioni.length} check list.`
    : 'Nessuna check list: la prima si apre con «Nuova check list».';

  if (!righe.length) {
    corpo.innerHTML = '<tr><td colspan="5">Nessuna check list con questi filtri.</td></tr>';
    return;
  }

  corpo.innerHTML = righe.map((c) => `<tr>
      <td><a href="/Manualistica/Compila/${c.idCompilazione}"><strong>${esc(c.ragioneSociale)}</strong></a>
          <div class="nota-riga">${esc(c.creatoDa || '')}</div></td>
      <td>${esc(c.data)}<div class="nota-riga">${esc(c.revisione)}</div></td>
      <td>${c.risposte}</td>
      <td>${esc(ETICHETTE_STATO[c.stato] || c.stato)}</td>
      <td class="nota-riga">${esc(c.tocco)}</td>
    </tr>`).join('');
}

// ── Apertura di una check list nuova ─────────────────────────────────

async function riempiClienti() {
  const sel = $('#f-cliente');
  if (!sel) return;
  const r = await fetch('/api/manualistica/compilazioni/clienti');
  if (!r.ok) return;
  const clienti = await r.json();
  // In ArcLab capita di trovare più schede quasi uguali per la stessa
  // società: senza partita IVA e comune non si distinguono, e aprire la
  // check list su un doppione vuoto si scopre mesi dopo.
  sel.innerHTML = '<option value="">— cliente nuovo, non ancora nei gestionali —</option>'
    + clienti.map((c) => {
        const segni = [
          c.partitaIva || 'senza P.IVA',
          c.comune || '',
          c.insediamenti === 1 ? '1 insediamento' : `${c.insediamenti} insediamenti`,
          c.haEmail ? '' : 'senza e-mail'
        ].filter(Boolean).join(' · ');
        return `<option value="${c.id}">${esc(c.nome)} — ${esc(segni)}</option>`;
      }).join('');
}

async function crea() {
  const idCliente = $('#f-cliente').value;
  const ragione = $('#f-ragione').value.trim();

  if (!idCliente && !ragione) {
    avviso('Scegli un cliente, oppure scrivi la ragione sociale di quello nuovo.');
    return;
  }

  const btn = $('#btn-crea');
  btn.disabled = true;
  btn.textContent = 'Apro…';
  try {
    const r = await fetch('/api/manualistica/compilazioni', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        idClienteArcLab: idCliente ? Number(idCliente) : null,
        ragioneSociale: ragione || null,
        dataDocumenti: $('#f-data').value || null,
        revisione: Number($('#f-revisione').value || 0)
      })
    });
    if (!r.ok) { avviso(await r.text()); return; }
    $('#pannello-nuova').hidden = true;
    await carica();
  } finally {
    btn.disabled = false;
    btn.textContent = 'Apri la check list';
  }
}

function avviso(messaggio) {
  $('#avviso-corpo').innerHTML = `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-avviso').showModal();
}

// ── Avvio ────────────────────────────────────────────────────────────

$('#btn-ricarica').addEventListener('click', carica);
$('#f-stato').addEventListener('change', disegna);
$('#f-testo').addEventListener('input', disegna);

if ($('#btn-nuova')) {
  $('#btn-nuova').addEventListener('click', async () => {
    $('#pannello-nuova').hidden = false;
    if (!$('#f-data').value) $('#f-data').valueAsDate = new Date();
    await riempiClienti();
    $('#f-cliente').focus();
  });

  $('#btn-annulla').addEventListener('click', () => { $('#pannello-nuova').hidden = true; });
  $('#btn-crea').addEventListener('click', crea);

  // La ragione sociale si scrive solo per un cliente che in ArcLab non c'è.
  $('#f-cliente').addEventListener('change', (e) => {
    const nuovo = !e.target.value;
    $('#f-ragione').disabled = !nuovo;
    if (!nuovo) $('#f-ragione').value = '';
  });
}

await carica();
