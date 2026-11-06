/* ═══════════════════════════════════════════════════════════
   Caselle azzerabili (01/10/2026)
   Aggiunge una piccola × ai campi di ricerca/filtro: svuota SOLO quel campo, senza dover
   azzerare tutti i filtri. Vale per:
     - input testo/numero/data e select dentro .griglia-filtri (esclusi i moduli di
       inserimento dati dell'accettazione: .lotto, .campione);
     - ogni input type="search";
     - qualunque campo con l'attributo data-azzerabile.
   Dopo lo svuotamento scatena "input" e "change", così i filtri collegati (es. Cliente →
   Insediamento) si aggiornano come se l'utente avesse cancellato a mano.
   Script normale (non modulo), caricato da _Layout su tutte le pagine.
   ═══════════════════════════════════════════════════════════ */
(function () {
    const SELETTORE =
        '.griglia-filtri input[type=text], .griglia-filtri input[type=number], .griglia-filtri input[type=date], ' +
        '.griglia-filtri input[type=email], .griglia-filtri input:not([type]), .griglia-filtri select, ' +
        'input[type=search], [data-azzerabile]';

    function haValore(campo) {
        return campo.tagName === 'SELECT' ? campo.selectedIndex > 0 && campo.value !== '' : campo.value !== '';
    }

    function aggiorna(campo) {
        const contenitore = campo.parentElement;
        if (contenitore && contenitore.classList.contains('campo-azzerabile'))
            contenitore.classList.toggle('pieno', haValore(campo) && !campo.disabled && !campo.readOnly);
    }

    function svuota(campo) {
        if (campo.tagName === 'SELECT') {
            const vuota = Array.from(campo.options).findIndex(o => o.value === '');
            campo.selectedIndex = vuota >= 0 ? vuota : 0;
        } else {
            campo.value = '';
        }
        campo.dispatchEvent(new Event('input', { bubbles: true }));
        campo.dispatchEvent(new Event('change', { bubbles: true }));
        aggiorna(campo);
        campo.focus();
    }

    function prepara(campo) {
        if (campo.dataset.azzerabilePronto || campo.closest('.lotto, .campione')) return;
        if (campo.type === 'checkbox' || campo.type === 'radio' || campo.type === 'hidden') return;
        campo.dataset.azzerabilePronto = '1';

        const involucro = document.createElement('span');
        involucro.className = 'campo-azzerabile';
        campo.parentNode.insertBefore(involucro, campo);
        involucro.appendChild(campo);

        const bottone = document.createElement('button');
        bottone.type = 'button';
        bottone.className = 'azzera-campo';
        bottone.title = 'Svuota questo campo';
        bottone.setAttribute('aria-label', 'Svuota questo campo');
        bottone.textContent = '×';
        bottone.addEventListener('click', e => { e.preventDefault(); svuota(campo); });
        involucro.appendChild(bottone);

        campo.addEventListener('input', () => aggiorna(campo));
        campo.addEventListener('change', () => aggiorna(campo));
        aggiorna(campo);
    }

    function prepTutti() { document.querySelectorAll(SELETTORE).forEach(prepara); }
    function aggiornaTutti() { document.querySelectorAll('.campo-azzerabile > input, .campo-azzerabile > select').forEach(aggiorna); }

    document.addEventListener('DOMContentLoaded', () => {
        prepTutti();
        // I filtri a volte vengono svuotati o riempiti dal codice (pulsante "Azzera", liste caricate dopo):
        // un controllo leggero ogni secondo tiene allineata la × senza toccare i moduli di pagina.
        setInterval(() => { prepTutti(); aggiornaTutti(); }, 1000);
    });
})();
