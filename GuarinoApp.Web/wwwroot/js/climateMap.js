// Módulo JS para el mapa de lotes / riesgo climático (Leaflet + OpenStreetMap)
// Se carga dinámicamente vía IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/climateMap.js")
// No requiere agregar <script> en _Host.cshtml ni en App.razor.

let map = null;
let markers = [];
let leafletLoadingPromise = null;

// Enlace bidireccional mapa ↔ tablas:
// - markersPorCodigo permite que un clic en una fila lleve al lote en el mapa.
// - dotNetHelper recibe el clic en «Ver en la tabla» del popup y resalta las filas.
const markersPorCodigo = {};
let dotNetHelper = null;
let listenersDelegadosBindeados = false;

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

export async function initMap(elementId, markerData, dotNetRef) {
    await ensureLeafletLoaded();
    dotNetHelper = dotNetRef || null;
    bindListenersDelegados();

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
        // Botón de enlace inverso: del popup del marcador a las filas del lote
        // en las tablas (la página recibe el click vía DotNetObjectReference).
        const botonTabla = m.codigo
            ? `<br/><button type="button" class="btn-ver-filas" data-lote="${safeCodigo}">↗ Ver en la tabla</button>`
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

function bindListenersDelegados() {
    if (listenersDelegadosBindeados) return;
    listenersDelegadosBindeados = true;

    document.addEventListener('click', (ev) => {
        const target = ev.target;
        if (!target || !target.closest) return;

        // Clic en el botón «↗ Ver en la tabla» del popup → avisar a la página
        // (vía DotNetObjectReference) para que resalte y lleve a las filas de ese lote.
        // El enlace inverso (fila → mapa) lo resuelve MudTable.RowClick en la página.
        const btn = target.closest('button.btn-ver-filas');
        if (btn) {
            const codigo = btn.getAttribute('data-lote');
            if (codigo && dotNetHelper) {
                dotNetHelper.invokeMethodAsync('ResaltarFilasDelLote', codigo)
                    .catch(() => { /* la página puede haber navegado fuera */ });
            }
        }
    });
}

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

/**
 * Desplaza la vista a la fila resaltada (clase `fila-lote-flash`, que la página
 * aplica vía RowClassFunc al recibir «↗ Ver en la tabla»). Devuelve false si no
 * hay ninguna fila resaltada visible.
 */
export function scrollToFilaResaltada() {
    const fila = document.querySelector('tr.fila-lote-flash');
    if (!fila) return false;
    fila.scrollIntoView({ behavior: 'smooth', block: 'center' });
    return true;
}

export function dispose() {
    if (map) {
        map.remove();
        map = null;
    }
    markers = [];
    Object.keys(markersPorCodigo).forEach(k => delete markersPorCodigo[k]);
    dotNetHelper = null;
}
