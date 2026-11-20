// Tabelle con class "ordinabile": clic sull'intestazione per ordinare (di nuovo per invertire)
// e, sotto l'intestazione, una casella di filtro per ogni colonna. Nessuna libreria.
(function () {
    function valore(testo) {
        var t = (testo || '').trim();
        var d = /^(\d{2})\/(\d{2})\/(\d{4})/.exec(t);
        if (d) return { n: Number(d[3] + d[2] + d[1]), t: t.toLowerCase() };
        var num = /^-?\d+([.,]\d+)?/.exec(t.replace(/\s/g, ''));
        if (num) return { n: Number(num[0].replace(',', '.')), t: t.toLowerCase() };
        return { n: null, t: t.toLowerCase() };
    }
    function confronta(a, b) {
        if (a.n !== null && b.n !== null && a.n !== b.n) return a.n - b.n;
        return a.t.localeCompare(b.t, 'it');
    }
    document.querySelectorAll('table.ordinabile').forEach(function (tabella) {
        var testa = tabella.tHead, corpo = tabella.tBodies[0];
        if (!testa || !corpo || testa.rows.length === 0) return;
        var intestazioni = Array.prototype.slice.call(testa.rows[0].cells);
        var filtri = document.createElement('tr');
        var direzione = {};

        function applica() {
            var caselle = Array.prototype.slice.call(filtri.querySelectorAll('input'));
            Array.prototype.forEach.call(corpo.rows, function (riga) {
                if (riga.cells.length < intestazioni.length) return;
                var visibile = caselle.every(function (c) {
                    var q = c.value.trim().toLowerCase();
                    if (!q) return true;
                    var cella = riga.cells[Number(c.getAttribute('data-colonna'))];
                    return cella && cella.textContent.toLowerCase().indexOf(q) >= 0;
                });
                riga.style.display = visibile ? '' : 'none';
            });
        }

        function ordina(indice) {
            direzione[indice] = direzione[indice] === 1 ? -1 : 1;
            var righe = Array.prototype.slice.call(corpo.rows).filter(function (r) { return r.cells.length >= intestazioni.length; });
            righe.sort(function (r1, r2) {
                return direzione[indice] * confronta(valore(r1.cells[indice].textContent), valore(r2.cells[indice].textContent));
            });
            righe.forEach(function (r) { corpo.appendChild(r); });
            intestazioni.forEach(function (th, i) {
                th.textContent = th.textContent.replace(/ [▲▼]$/, '');
                if (i === indice) th.textContent += direzione[indice] === 1 ? ' ▲' : ' ▼';
            });
        }

        intestazioni.forEach(function (th, i) {
            var cella = document.createElement('th');
            if (!th.hasAttribute('data-nofiltro')) {
                var casella = document.createElement('input');
                casella.type = 'search';
                casella.placeholder = 'filtra';
                casella.setAttribute('data-colonna', String(i));
                casella.setAttribute('aria-label', 'Filtra ' + th.textContent.trim());
                casella.style.cssText = 'width:100%;min-width:4rem;font-size:.8rem;padding:2px 4px;box-sizing:border-box';
                casella.addEventListener('input', applica);
                cella.appendChild(casella);
                th.style.cursor = 'pointer';
                th.title = 'Clic per ordinare';
                th.addEventListener('click', function () { ordina(i); });
            }
            filtri.appendChild(cella);
        });
        testa.appendChild(filtri);
    });
})();
