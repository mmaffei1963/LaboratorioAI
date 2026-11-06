// Tabelle ordinabili in tutto il sito (05/11/2026, richiesta di Zatterin): un clic sull'intestazione di una colonna
// ordina le righe (crescente, poi decrescente). Vale anche per le tabelle disegnate dal JavaScript delle pagine, perché
// l'ordinamento si fa al momento del clic sulle righe presenti. Riconosce numeri all'italiana (1.234,56 €, −12%),
// date (gg/mm/aa o gg/mm/aaaa) e testo. Una cella può indicare il valore da usare con data-ordina="…".
// Escluse: tabelle con data-non-ordinabile, colonne senza titolo o con data-non-ordinabile.
(function () {
    'use strict';

    function valoreCella(td) {
        if (!td) return '';
        if (td.dataset && td.dataset.ordina !== undefined) return td.dataset.ordina;
        // Prima riga del testo (la cella può avere un sottotesto sotto)
        return (td.innerText || td.textContent || '').split('\n').map(function (s) { return s.trim(); }).filter(Boolean)[0] || '';
    }

    function comeData(t) {
        var m = /^(\d{1,2})\/(\d{1,2})\/(\d{2,4})(?:\s+(\d{1,2}):(\d{2}))?/.exec(t);
        if (!m) return null;
        var anno = +m[3]; if (anno < 100) anno += 2000;
        return new Date(anno, +m[2] - 1, +m[1], +(m[4] || 0), +(m[5] || 0)).getTime();
    }

    function comeNumero(t) {
        var s = t.replace(/[€%\s]/g, '').replace(/[−–]/g, '-');
        if (!/^[+-]?(\d{1,3}(\.\d{3})+|\d+)(,\d+)?$/.test(s) && !/^[+-]?\d+(\.\d+)?$/.test(s)) return null;
        if (s.indexOf(',') >= 0 || /\.\d{3}(\D|$)/.test(s)) s = s.replace(/\./g, '').replace(',', '.');
        var n = parseFloat(s);
        return isNaN(n) ? null : n;
    }

    function chiave(t, grezzo) {
        // data-ordina numerico («-0.123», «1234.5»): numero col punto decimale, senza interpretazioni
        if (grezzo && t !== '' && !isNaN(Number(t))) return { tipo: 1, v: Number(t) };
        if (t === '' || t === '—' || t === '-' || t === '--') return { tipo: 3, v: 0 };   // vuoti sempre in fondo
        var d = comeData(t);
        if (d !== null) return { tipo: 0, v: d };
        var n = comeNumero(t);
        if (n !== null) return { tipo: 1, v: n };
        return { tipo: 2, v: t.toLocaleLowerCase('it') };
    }

    function confronta(a, b) {
        if (a.tipo !== b.tipo) return a.tipo - b.tipo;
        if (a.tipo === 2) return a.v.localeCompare(b.v, 'it', { numeric: true });
        return a.v - b.v;
    }

    function ordina(th) {
        var tabella = th.closest('table');
        var tbody = tabella && tabella.tBodies[0];
        if (!tbody) return;
        var riga = th.parentElement;
        // Posizione della colonna (tiene conto dei colspan dell'intestazione)
        var col = 0;
        for (var i = 0; i < riga.cells.length && riga.cells[i] !== th; i++) col += riga.cells[i].colSpan || 1;
        var righe = Array.prototype.slice.call(tbody.rows);
        // Righe «speciali» (es. «Nessun risultato», dettagli con colspan) restano in fondo nell'ordine attuale
        var normali = righe.filter(function (r) { return r.cells.length > col && !(r.cells.length === 1 && r.cells[0].colSpan > 1); });
        if (normali.length < 2) return;
        var altre = righe.filter(function (r) { return normali.indexOf(r) < 0; });
        var crescente = th.getAttribute('aria-sort') !== 'ascending';
        var chiavi = new Map();
        normali.forEach(function (r) {
            var td = r.cells[col];
            chiavi.set(r, chiave(valoreCella(td), !!(td && td.dataset && td.dataset.ordina !== undefined)));
        });
        normali.sort(function (a, b) {
            var ka = chiavi.get(a), kb = chiavi.get(b);
            if (ka.tipo === 3 || kb.tipo === 3) return ka.tipo === kb.tipo ? 0 : (ka.tipo === 3 ? 1 : -1);
            var c = confronta(ka, kb);
            return crescente ? c : -c;
        });
        var frammento = document.createDocumentFragment();
        normali.concat(altre).forEach(function (r) { frammento.appendChild(r); });
        tbody.appendChild(frammento);
        tabella.querySelectorAll('thead th[aria-sort]').forEach(function (x) { x.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', crescente ? 'ascending' : 'descending');
    }

    function ordinabile(th) {
        if (!th || th.tagName !== 'TH') return false;
        var tabella = th.closest('table');
        if (!tabella || tabella.hasAttribute('data-non-ordinabile') || th.hasAttribute('data-non-ordinabile')) return false;
        if (!th.closest('thead')) return false;
        if (!(th.textContent || '').trim()) return false;
        return !!tabella.tBodies[0];
    }

    document.addEventListener('click', function (e) {
        var th = e.target.closest && e.target.closest('th');
        if (!ordinabile(th)) return;
        if (e.target.closest('a, button, input, select, label')) return;   // controlli dentro l'intestazione
        ordina(th);
    });

    // Suggerimento al passaggio del mouse
    document.addEventListener('mouseover', function (e) {
        var th = e.target.closest && e.target.closest('th');
        if (ordinabile(th) && !th.title) th.title = 'Clic per ordinare';
    });
})();
