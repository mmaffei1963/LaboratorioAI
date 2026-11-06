// Compensi soci (21/10/2026): registrazioni AZW dietro una cifra (finestra), stampa, conferme.
const dlg = document.getElementById('cs-dettaglio');
const titolo = document.getElementById('cs-dettaglio-titolo');
const corpo = document.getElementById('cs-dettaglio-corpo');

document.getElementById('cs-chiudi')?.addEventListener('click', () => dlg.close());
document.getElementById('cs-stampa')?.addEventListener('click', () => window.print());

document.addEventListener('click', async (e) => {
    const cella = e.target.closest('[data-reg-tipo]');
    if (!cella || !dlg) return;
    const p = new URLSearchParams({ anno: cella.dataset.anno, tipo: cella.dataset.regTipo });
    if (cella.dataset.socio) p.set('idSocio', cella.dataset.socio);
    if (cella.dataset.voce) p.set('voce', cella.dataset.voce);
    titolo.textContent = cella.dataset.titolo ?? '';
    corpo.innerHTML = '<p>Caricamento…</p>';
    dlg.showModal();
    try {
        const r = await fetch('/CompensiSoci/Registrazioni?' + p.toString(), { headers: { 'X-Requested-With': 'fetch' } });
        corpo.innerHTML = r.ok ? await r.text() : `<p>Dettaglio non disponibile (errore ${r.status}).</p>`;
    } catch {
        corpo.innerHTML = '<p>Dettaglio non disponibile: connessione non riuscita.</p>';
    }
});

// Pulsanti che chiedono conferma (chiusura anno, rimborso tolto): la conferma è nella pagina, non una finestra del browser.
for (const f of document.querySelectorAll('form[data-conferma]')) {
    f.addEventListener('submit', (e) => {
        const b = f.querySelector('button[type=submit]');
        if (b && b.dataset.confermato !== '1') {
            e.preventDefault();
            b.dataset.confermato = '1';
            b.dataset.testo = b.textContent;
            b.textContent = f.dataset.conferma;
            b.classList.add('pulsante-attenzione');
            setTimeout(() => { b.dataset.confermato = ''; b.textContent = b.dataset.testo; b.classList.remove('pulsante-attenzione'); }, 5000);
        }
    });
}

// Gestione: aggiunge una riga vuota copiando l'ultima riga «nuova» di una tabella.
for (const b of document.querySelectorAll('[data-aggiungi-riga]')) {
    b.addEventListener('click', () => {
        const tb = document.getElementById(b.dataset.aggiungiRiga);
        const modello = tb?.querySelector('tr[data-modello]');
        if (!modello) return;
        const i = tb.querySelectorAll('tr').length;
        const nuova = modello.cloneNode(true);
        nuova.removeAttribute('data-modello');
        nuova.hidden = false;
        for (const el of nuova.querySelectorAll('[name]')) {
            el.name = el.name.replace(/\[\d+\]/, `[${i}]`);
            el.disabled = false;
        }
        tb.appendChild(nuova);
    });
}

// Rimborsi: i km solo per le categorie che li chiedono.
const cat = document.getElementById('rim-categoria');
const km = document.getElementById('rim-km-riga');
function aggiornaKm() { if (cat && km) km.hidden = cat.selectedOptions[0]?.dataset.km !== '1'; }
cat?.addEventListener('change', aggiornaKm);
aggiornaKm();
