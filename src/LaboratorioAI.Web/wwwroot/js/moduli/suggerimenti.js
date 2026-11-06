// Elenco di suggerimenti sotto una casella di ricerca (15/10/2026). Sostituisce <datalist>, che il browser filtra per conto
// suo (testo esatto contenuto) e così nascondeva i risultati trovati dal server con la ricerca tollerante
// (es. «mare-a» non mostrava «MARE.A S.R.L.»). Tastiera: frecce su/giù, Invio per scegliere, Esc per chiudere.
//
// collegaSuggerimenti(input, { cerca: async testo => [{ testo, sotto?, valore }], scegli: voce => {}, minimo: 2 })

export function collegaSuggerimenti(input, { cerca, scegli, minimo = 2, attesaMs = 250 }) {
    input.removeAttribute('list');
    input.setAttribute('autocomplete', 'off');
    const contenitore = input.parentElement;
    if (getComputedStyle(contenitore).position === 'static') contenitore.style.position = 'relative';
    const lista = document.createElement('ul');
    lista.className = 'elenco-suggerimenti';
    lista.setAttribute('role', 'listbox');
    lista.hidden = true;
    input.insertAdjacentElement('afterend', lista);

    let voci = [], attiva = -1, timer = null, ultima = 0;
    const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    function chiudi() { lista.hidden = true; attiva = -1; }
    function evidenzia(i) {
        attiva = i;
        [...lista.children].forEach((li, j) => li.classList.toggle('attiva', j === i));
        lista.children[i]?.scrollIntoView({ block: 'nearest' });
    }
    function mostra() {
        if (!voci.length) {
            lista.innerHTML = '<li class="vuota">Nessun risultato</li>';
        } else {
            lista.innerHTML = voci.map((v, i) => `<li role="option" data-i="${i}">${esc(v.testo)}${v.sotto ? `<span>${esc(v.sotto)}</span>` : ''}</li>`).join('');
        }
        lista.hidden = false;
        attiva = -1;
    }
    function conferma(i) {
        const v = voci[i];
        if (!v) return;
        input.value = v.testo;
        chiudi();
        scegli(v);
    }

    input.addEventListener('input', () => {
        clearTimeout(timer);
        const t = input.value.trim();
        if (t.length < minimo) { chiudi(); return; }
        timer = setTimeout(async () => {
            const n = ++ultima;
            try {
                const r = await cerca(t);
                if (n !== ultima) return;   // è arrivata una risposta più vecchia: si ignora
                voci = r || [];
                mostra();
            } catch { /* non bloccante */ }
        }, attesaMs);
    });
    input.addEventListener('keydown', e => {
        if (lista.hidden) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); evidenzia(Math.min(attiva + 1, voci.length - 1)); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); evidenzia(Math.max(attiva - 1, 0)); }
        else if (e.key === 'Enter') { if (attiva >= 0) { e.preventDefault(); conferma(attiva); } else if (voci.length === 1) { e.preventDefault(); conferma(0); } }
        else if (e.key === 'Escape') chiudi();
    });
    lista.addEventListener('mousedown', e => {
        const li = e.target.closest('li[data-i]');
        if (li) { e.preventDefault(); conferma(Number(li.dataset.i)); }
    });
    input.addEventListener('blur', () => setTimeout(chiudi, 150));
    input.addEventListener('focus', () => { if (voci.length && input.value.trim().length >= minimo) mostra(); });
}
