// Módulo JS para el mapa de lotes / riesgo climático (Leaflet + OpenStreetMap)
// Se carga dinámicamente vía IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/climateMap.js")
// No requiere agregar <script> en _Host.cshtml ni en App.razor.

let map = null;
let markers = [];
let leafletLoadingPromise = null;

// Enlace bidireccional mapa ↔ tablas:
// - markersPorCodigo permite que un clic en una fila lleve al lote en el mapa.
// - window.__guarinoVerFilas lleva del popup del mapa a las filas del lote en las
//   tablas. Se resuelve 100% en el cliente (sin DotNetObjectReference): es
//   síncrono, no depende del estado del circuito Blazor y no puede quedar
//   invalidado tras una reconexión.
const markersPorCodigo = {};

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
        // Botón de enlace inverso: del popup del mapa a las filas del lote en las
        // tablas. onclick inline → llama a la función global definida abajo en este
        // mismo módulo (sin pasar por .NET: no puede fallar por circuito caído).
        const botonTabla = m.codigo
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
 * «↗ Ver en la tabla» (popup del mapa) → encuentra las filas de ese lote en las
 * tablas de la página, las resalta con parpadeo y lleva la vista hasta ellas.
 * 100% cliente: las filas se identifican por la celda del código de lote
 * (columna 2 de ambas tablas: Fecha | Lote | ...).
 * Devuelve false si el lote no tiene filas visibles (Top-30 por fecha).
 */
export function verFilasDelLote(codigo) {
    if (!codigo) return false;

    const filas = Array.from(document.querySelectorAll('.mud-table-body tr'))
        .filter(tr => {
            const celdas = tr.querySelectorAll('td');
            return celdas.length > 1 && celdas[1].textContent.trim() === codigo;
        });

    if (filas.length === 0) {
        mostrarAviso(`El lote ${codigo} no tiene filas visibles en las tablas (se muestran los 30 eventos más recientes de cada tabla).`);
        return false;
    }

    // Limpia el resaltado anterior para que la animación pueda repetirse.
    document.querySelectorAll('tr.fila-lote-flash').forEach(t => t.classList.remove('fila-lote-flash'));
    filas.forEach(f => f.classList.add('fila-lote-flash'));
    filas[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
    mostrarAviso(`${filas.length} fila(s) del lote ${codigo} resaltadas en la tabla.`);
    // Quita el resaltado al terminar la animación CSS (para poder repetir el clic).
    setTimeout(() => filas.forEach(f => f.classList.remove('fila-lote-flash')), 2600);
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
}
