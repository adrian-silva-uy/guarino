// Módulo JS para el mapa de lotes / riesgo climático (Leaflet + OpenStreetMap)
// Se carga dinámicamente vía IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/climateMap.js")
// No requiere agregar <script> en _Host.cshtml ni en App.razor.

let map = null;
let markers = [];
let leafletLoadingPromise = null;

// Enlace bidireccional mapa ↔ tablas, resuelto 100% en el cliente (sin
// DotNetObjectReference ni handlers de Blazor): no depende del estado del
// circuito y no puede quedar invalidado tras una reconexión.
// - Fila de tabla → mapa: delegated listener (irAlLoteEnMapa).
// - Popup del mapa → filas: onclick inline del botón (verFilasDelLote), que
//   FILTRA la tabla por el lote (los buscadores #buscar-pronostico /
//   #buscar-recientes de la página) para que sus filas siempre estén a la vista,
//   aunque la tabla esté paginada o el lote no esté entre las primeras filas.
const markersPorCodigo = {};
// Desglose de eventos por lote (mismo corte que el popup, del handler del mapa):
// { ocurridos, pronosticados } — decide en qué tabla filtrar y si el marcador
// muestra el botón (los verdes, sin eventos, no lo muestran).
const datosPorCodigo = {};

function escaparHtml(texto) {
    return (texto || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function ensureLeafletLoaded() {
    if (window.L) {
        return Promise.resolve();
    }
    if (leafletLoadingPromise) {
        return leafletLoadingPromise;
    }

    leafletLoadingPromise = new Promise((resolve, reject) => {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
        document.head.appendChild(link);

        const script = document.createElement('script');
        script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('No se pudo cargar Leaflet desde el CDN. Verificar acceso a internet del navegador cliente.'));
        document.head.appendChild(script);
    });

    return leafletLoadingPromise;
}

export async function initMap(elementId, markerData) {
    await ensureLeafletLoaded();
    bindFilaClickListener();

    if (map) {
        map.remove();
        map = null;
    }

    const el = document.getElementById(elementId);
    if (!el) {
        console.warn(`climateMap: no se encontró el elemento #${elementId}`);
        return;
    }

    // Centro por defecto: Uruguay
    map = L.map(elementId);
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics',
        maxZoom: 19
    }).addTo(map);

    // Capa de etiquetas (nombres de rutas/ciudades) superpuesta a la imagen satelital
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19,
        opacity: 0.9
    }).addTo(map);

    setMarkers(markerData);
}

export function setMarkers(markerData) {
    if (!map) return;

    markers.forEach(m => map.removeLayer(m));
    markers = [];
    Object.keys(markersPorCodigo).forEach(k => delete markersPorCodigo[k]);
    Object.keys(datosPorCodigo).forEach(k => delete datosPorCodigo[k]);

    if (!markerData || markerData.length === 0) {
        map.setView([-32.5, -56.0], 7);
        return;
    }

    const bounds = [];
    markerData.forEach(m => {
        const marker = L.circleMarker([m.lat, m.lng], {
            radius: 9,
            fillColor: m.color || '#1976d2',
            color: '#333',
            weight: 1,
            fillOpacity: 0.85
        }).addTo(map);

        const safeLabel = escaparHtml(m.label);
        const safeInfo = escaparHtml(m.info);
        const safeCodigo = escaparHtml(m.codigo);

        // Desglose de eventos del lote (lo manda el servidor en cada marcador).
        const ocurridos = m.ocurridos | 0;
        const pronosticados = m.hoyPronosticados | 0;
        if (m.codigo) {
            datosPorCodigo[m.codigo] = { ocurridos, pronosticados };
        }

        // Botón de enlace inverso: del popup del mapa a las filas del lote en las
        // tablas. onclick inline → llama a la función global definida abajo en este
        // mismo módulo (sin pasar por .NET: no puede fallar por circuito caído).
        // Solo si el lote tiene eventos: en un marcador verde (sin eventos) no hay
        // filas que mostrar y el botón sería confuso.
        const botonTabla = (m.codigo && (ocurridos + pronosticados) > 0)
            ? `<br/><button type="button" class="btn-ver-filas" data-lote="${safeCodigo}" onclick="window.__guarinoVerFilas(this.getAttribute('data-lote'))">↗ Ver en la tabla</button>`
            : '';
        marker.bindPopup(`<strong>${safeLabel}</strong><br/>${safeInfo}${botonTabla}`);

        if (m.codigo) {
            markersPorCodigo[m.codigo] = marker;
        }
        markers.push(marker);
        bounds.push([m.lat, m.lng]);
    });

    if (bounds.length === 1) {
        map.setView(bounds[0], 12);
    } else {
        map.fitBounds(bounds, { padding: [30, 30] });
    }
}

// --- Enlace bidireccional mapa ↔ tablas -------------------------------------

let filaClickBindeado = false;

/**
 * Clic en una fila de las tablas de riesgos → llevar al lote en el mapa.
 * Delegado en document (una sola vez). Es inofensivo fuera de esta página: solo
 * actúa si el mapa está inicializado y el código de la 2ª columna (Lote) de la
 * fila clickeada corresponde a un marcador del mapa.
 */
function bindFilaClickListener() {
    if (filaClickBindeado) return;
    filaClickBindeado = true;

    document.addEventListener('click', (ev) => {
        const target = ev.target;
        if (!target || !target.closest || !map) return;
        if (target.closest('button')) return; // los botones de la fila no navegan
        const fila = target.closest('.mud-table-body tr');
        if (!fila) return;
        const celdas = fila.querySelectorAll('td');
        if (celdas.length < 2) return;
        const codigo = celdas[1].textContent.trim();
        if (!codigo || !markersPorCodigo[codigo]) return;
        irAlLoteEnMapa(codigo);
    });
}

/**
 * Ubica el lote en el mapa. Si el mapa está fuera de la pantalla (la tabla quedó
 * scrolleada hacia abajo), primero lleva la vista al mapa y recién ahí vuela al
 * marcador, para que el usuario VEA la ubicación del lote.
 */
function irAlLoteEnMapa(codigo) {
    const elMapa = document.getElementById('mapa-riesgos-climaticos');
    let mapaVisible = true;
    if (elMapa) {
        const r = elMapa.getBoundingClientRect();
        mapaVisible = r.top >= 0 && r.bottom <= (window.innerHeight || document.documentElement.clientHeight);
    }

    const volar = () => {
        if (focusLote(codigo)) {
            mostrarAviso(`Lote ${codigo} ubicado en el mapa.`);
        } else {
            mostrarAviso(`El lote ${codigo} no tiene coordenadas en el mapa.`);
        }
    };

    if (elMapa && !mapaVisible) {
        elMapa.scrollIntoView({ behavior: 'smooth', block: 'center' });
        // Espera a que termine el scroll para que flyTo y el popup se vean.
        setTimeout(volar, 550);
    } else {
        volar();
    }
}

/**
 * Muestra un aviso flotante breve (feedback para el usuario cuando una acción
 * no tiene resultado visible, p. ej. un lote cuyas filas no están en la tabla).
 */
function mostrarAviso(texto) {
    let aviso = document.getElementById('guarino-aviso-mapa');
    if (!aviso) {
        aviso = document.createElement('div');
        aviso.id = 'guarino-aviso-mapa';
        aviso.style.cssText = 'position:fixed;bottom:16px;left:50%;transform:translateX(-50%);'
            + 'background:#323232;color:#fff;padding:8px 16px;border-radius:6px;z-index:3000;'
            + 'font-size:13px;box-shadow:0 2px 8px rgba(0,0,0,.35);max-width:80%;text-align:center;';
        document.body.appendChild(aviso);
    }
    aviso.textContent = texto;
    aviso.style.display = 'block';
    clearTimeout(aviso._ocultar);
    aviso._ocultar = setTimeout(() => { aviso.style.display = 'none'; }, 3500);
}

/**
 * Fija el texto de un buscador de tabla (#buscar-pronostico / #buscar-recientes)
 * y dispara el evento input para que el enlace @bind-Value de Blazor aplique el
 * filtro. Devuelve false si el buscador no existe en la página.
 */
function fijarFiltroTabla(id, valor) {
    let el = document.getElementById(id);
    if (!el) return false;
    if (el.tagName !== 'INPUT') el = el.querySelector('input');
    if (!el) return false;
    // SIEMPRE setea y dispara (nunca saltear por "ya coincide"): el texto de la
    // caja puede quedar desincronizado del filtro real tras limpiar con el chip, y
    // un salteo dejaría el botón sin efecto para siempre. El input es nativo de
    // Blazor (@bind:event="oninput") y también se atiende 'change' por seguridad.
    el.value = valor || '';
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    return true;
}

/** Filas del lote en el DOM actual (2ª columna = código de lote en ambas tablas). */
function buscarFilasDelLote(codigo) {
    return Array.from(document.querySelectorAll('.mud-table-body tr'))
        .filter(tr => {
            const celdas = tr.querySelectorAll('td');
            return celdas.length > 1 && celdas[1].textContent.trim() === codigo;
        });
}

/**
 * «↗ Ver en la tabla» (popup del mapa) → filtra la(s) tabla(s) donde el lote
 * tiene eventos por su código, espera a que Blazor pinte las filas filtradas,
 * las resalta con parpadeo y lleva la vista hasta ellas. 100% cliente: el filtro
 * se aplica escribiendo en el buscador de la tabla (sin DotNetObjectReference).
 * Así el botón funciona SIEMPRE que el lote tenga eventos, aunque esté paginado
 * fuera de pantalla o fuera de las primeras filas visibles.
 */
export function verFilasDelLote(codigo) {
    if (!codigo) return false;

    const datos = datosPorCodigo[codigo];
    if (datos && (datos.ocurridos + datos.pronosticados) === 0) {
        mostrarAviso(`El lote ${codigo} no tiene eventos en la ventana (30 días + pronóstico): no hay filas que mostrar.`);
        return false;
    }

    // Filtra SOLO la(s) tabla(s) donde el lote tiene filas; la otra se muestra
    // completa. Sin desglose disponible (cache vieja): filtra ambas por las dudas.
    if (datos) {
        fijarFiltroTabla('buscar-pronostico', datos.pronosticados > 0 ? codigo : '');
        fijarFiltroTabla('buscar-recientes', datos.ocurridos > 0 ? codigo : '');
    } else {
        fijarFiltroTabla('buscar-pronostico', codigo);
        fijarFiltroTabla('buscar-recientes', codigo);
    }

    // Espera a que Blazor repinte las filas filtradas y resalta cuando ya están
    // TODAS: se espera el conteo exacto que reporta el marcador (ocurridos +
    // hoy/pronóstico) para no avisar con un número incompleto si el filtro aún no
    // aterrizó. Si el lote está paginado en varias páginas, se resalta lo visible
    // y se dice exactamente cuántas de cuántas hay.
    const esperado = datos ? (datos.ocurridos + datos.pronosticados) : -1;
    const esperar = (intento) => {
        const filas = buscarFilasDelLote(codigo);
        const completo = filas.length > 0 && (esperado < 0 || filas.length >= esperado);

        if (completo || intento >= 15) { // ~3 s máximo
            if (filas.length === 0) {
                mostrarAviso(`No se encontraron filas del lote ${codigo} en las tablas.`);
                return false;
            }
            document.querySelectorAll('tr.fila-lote-flash').forEach(t => t.classList.remove('fila-lote-flash'));
            filas.forEach(f => f.classList.add('fila-lote-flash'));
            filas[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
            mostrarAviso(esperado >= 0 && filas.length < esperado
                ? `El lote ${codigo}: ${filas.length} de ${esperado} fila(s) visibles (el resto está en otra página de la tabla).`
                : `El lote ${codigo}: ${filas.length} fila(s) visibles en la tabla.`);
            // Quita el resaltado al terminar la animación (para poder repetir el clic).
            setTimeout(() => filas.forEach(f => f.classList.remove('fila-lote-flash')), 2600);
            return completo;
        }
        setTimeout(() => esperar(intento + 1), 200);
    };
    esperar(0);
    return true;
}

// Global invocada por el onclick inline del botón de cada popup.
window.__guarinoVerFilas = verFilasDelLote;

/**
 * Ubica el lote en el mapa: vuela a su marcador y abre su popup.
 * Devuelve false si el lote no está en el mapa (sin coordenadas o fuera de pantalla).
 */
export function focusLote(codigo) {
    if (!map || !codigo || !markersPorCodigo[codigo]) return false;
    const marker = markersPorCodigo[codigo];
    map.flyTo(marker.getLatLng(), Math.max(map.getZoom(), 13), { duration: 0.7 });
    marker.openPopup();
    return true;
}

export function dispose() {
    if (map) {
        map.remove();
        map = null;
    }
    markers = [];
    Object.keys(markersPorCodigo).forEach(k => delete markersPorCodigo[k]);
    Object.keys(datosPorCodigo).forEach(k => delete datosPorCodigo[k]);
}
