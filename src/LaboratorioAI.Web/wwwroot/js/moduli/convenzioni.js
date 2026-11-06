// Convenzioni: filtro per nome o codice cliente sulla tabella (solo lato browser).
const campo = document.getElementById('filtro-convenzioni');
const tabella = document.getElementById('tabella-convenzioni');
const conteggio = document.getElementById('conteggio-convenzioni');

if (campo && tabella) {
    const righe = Array.from(tabella.tBodies[0].rows);
    campo.addEventListener('input', () => {
        const t = campo.value.trim().toLowerCase();
        let visibili = 0;
        for (const r of righe) {
            const ok = t === '' || r.cells[0].textContent.toLowerCase().includes(t);
            r.style.display = ok ? '' : 'none';
            if (ok) visibili++;
        }
        conteggio.textContent = t === '' ? '' : `${visibili} di ${righe.length}`;
    });
}
