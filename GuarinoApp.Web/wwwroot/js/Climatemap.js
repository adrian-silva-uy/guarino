// Módulo JS para el mapa de lotes / riesgo climático (Leaflet + OpenStreetMap)
// Se carga dinámicamente vía IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/climateMap.js")
// No requiere agregar <script> en _Host.cshtml ni en App.razor.

let map = null;
let markers = [];
let leafletLoadingPromise = null;

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

        const safeLabel = (m.label || '').replace(/</g, '&lt;');
        const safeInfo = (m.info || '').replace(/</g, '&lt;');
        marker.bindPopup(`<strong>${safeLabel}</strong><br/>${safeInfo}`);

        markers.push(marker);
        bounds.push([m.lat, m.lng]);
    });

    if (bounds.length === 1) {
        map.setView(bounds[0], 12);
    } else {
        map.fitBounds(bounds, { padding: [30, 30] });
    }
}

export function dispose() {
    if (map) {
        map.remove();
        map = null;
    }
    markers = [];
}
