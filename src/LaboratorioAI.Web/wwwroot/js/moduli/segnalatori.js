// Segnalatori: filtro della tabella e commesse del cliente scelto nel modulo di registrazione.
const $ = id => document.getElementById(id);

const filtro = $('filtro-segnalazioni');
const tabella = $('tabella-segnalazioni');
if (filtro && tabella) {
    const righe = Array.from(tabella.tBodies[0].rows);
    filtro.addEventListener('input', () => {
        const t = filtro.value.trim().toLowerCase();
        let n = 0;
        for (const r of righe) {
            const ok = t === '' || (r.cells[0].textContent + ' ' + r.cells[1].textContent).toLowerCase().includes(t);
            r.style.display = ok ? '' : 'none';
            if (ok) n++;
        }
        $('conteggio-segnalazioni').textContent = t === '' ? '' : `${n} di ${righe.length}`;
    });
}

const cliente = $('campo-cliente');
const condizione = $('campo-condizione');
const commessa = $('campo-commessa');
const rigaCommessa = $('riga-commessa');

function aggiornaVisibilita() {
    if (!condizione || !rigaCommessa) return;
    const opt = condizione.options[condizione.selectedIndex];
    rigaCommessa.style.display = opt && opt.dataset.ambito === '2' ? 'none' : '';
}

async function caricaCommesse() {
    if (!cliente || !commessa) return;
    const codice = cliente.value.trim();
    commessa.innerHTML = '';
    const vuota = document.createElement('option');
    vuota.value = '';
    vuota.textContent = codice === '' ? '— scegli prima il cliente —' : '— automatica (la più vicina alla data) —';
    commessa.appendChild(vuota);
    if (codice === '') return;
    try {
        const r = await fetch('/Segnalatori/CommesseCliente?codice=' + encodeURIComponent(codice));
        if (!r.ok) return;
        for (const c of await r.json()) {
            const o = document.createElement('option');
            o.value = c.id;
            o.textContent = c.testo;
            commessa.appendChild(o);
        }
    } catch { /* elenco non disponibile: resta la scelta automatica */ }
}

cliente?.addEventListener('change', caricaCommesse);
condizione?.addEventListener('change', aggiornaVisibilita);
aggiornaVisibilita();
