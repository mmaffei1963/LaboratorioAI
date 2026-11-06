// Committente dei rapporti di taratura (03/11/2026): cliente e insediamento ArcLab in campi propri,
// separati dallo strumento. Vuoto = taratura interna MARE.A. Comune a termometri, incubatori,
// mappatura e bilance. Propone anche gli strumenti del cliente già tarati in passato.
//
// Nel modulo servono: <input id="{p}-cliente" list="lista-{p}-clienti"> + <datalist id="lista-{p}-clienti">
// e <select id="{p}-insediamento">. «base» è il prefisso delle API del modulo (es. '/api/incubatori').

import { chiamaApi, testo } from '../comune.js';

export function creaCommittente(base, p = 'c', alCambio = null) {
    const $ = (id) => document.getElementById(id);
    let clienti = [];
    let strumentiCliente = [];
    let ultimoCliente = null;

    function cliente() {
        const nome = ($(p + '-cliente')?.value ?? '').trim();
        const trovato = clienti.find(c => (c.nome || '').toLowerCase() === nome.toLowerCase());
        return { nome, id: trovato ? trovato.id : null };
    }

    async function caricaInsediamenti(idSelezionato = null, nomeSelezionato = null) {
        const sel = $(p + '-insediamento');
        if (!sel) return;
        const c = cliente();
        let voci = [];
        if (c.id) { try { voci = await chiamaApi(`${base}/insediamenti/${c.id}`); } catch { voci = []; } }
        // Insediamento salvato ma non più tra quelli del cliente: lo si mostra comunque.
        if (idSelezionato && !voci.some(v => v.id === idSelezionato) && nomeSelezionato)
            voci = [{ id: idSelezionato, nome: nomeSelezionato }, ...voci];
        sel.innerHTML = '<option value="">—</option>' +
            voci.map(v => `<option value="${v.id}">${testo(v.nome)}</option>`).join('');
        sel.disabled = !c.nome;
        if (idSelezionato) sel.value = String(idSelezionato);
        else if (voci.length === 1) sel.value = String(voci[0].id);
    }

    async function caricaStrumentiCliente() {
        const c = cliente();
        strumentiCliente = [];
        if (c.id || c.nome) {
            const q = c.id ? `idCliente=${c.id}` : `committente=${encodeURIComponent(c.nome)}`;
            try { strumentiCliente = await chiamaApi(`${base}/strumenti-cliente?${q}`); } catch { strumentiCliente = []; }
        }
        if (alCambio) alCambio();
    }

    async function cambiato() {
        const c = cliente();
        const chiave = c.id ?? c.nome;
        if (chiave === ultimoCliente) return;
        ultimoCliente = chiave;
        await caricaInsediamenti();
        await caricaStrumentiCliente();
    }

    $(p + '-cliente')?.addEventListener('change', cambiato);
    $(p + '-cliente')?.addEventListener('blur', cambiato);

    return {
        async carica() {
            try { clienti = await chiamaApi(`${base}/clienti`); } catch { clienti = []; }
            const dl = $('lista-' + p + '-clienti');
            if (dl) dl.innerHTML = clienti.map(c => `<option value="${testo(c.nome)}">`).join('');
        },
        /** Campi da mandare al salvataggio. */
        leggi() {
            const c = cliente();
            const sel = $(p + '-insediamento');
            const idIns = sel && sel.value ? Number(sel.value) : null;
            const nomeIns = idIns ? sel.options[sel.selectedIndex].text : null;
            return {
                idClienteArcLab: c.id, committente: c.nome || null,
                idInsediamentoArcLab: c.nome ? idIns : null, insediamento: c.nome ? nomeIns : null
            };
        },
        /** Riempie i campi da una taratura aperta. */
        async imposta(t) {
            $(p + '-cliente').value = t?.committente ?? '';
            ultimoCliente = cliente().id ?? cliente().nome;
            await caricaInsediamenti(t?.idInsediamentoArcLab ?? null, t?.insediamento ?? null);
            await caricaStrumentiCliente();
        },
        async svuota() { await this.imposta(null); },
        /** C'è un cliente indicato? */
        haCliente() { return !!cliente().nome; },
        /** Strumenti del cliente già tarati: [{ identificazione, codice }]. */
        strumenti() { return strumentiCliente; }
    };
}
