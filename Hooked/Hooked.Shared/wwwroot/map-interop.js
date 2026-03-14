window.hookedMap = (() => {
    const maps = {};

    const SPECIES_COLOURS = {
        'australian bass':     { bg: '#4ade80', border: '#16a34a', text: '#052e16' },
        'murray cod':          { bg: '#fb923c', border: '#ea580c', text: '#431407' },
        'dusky flathead':      { bg: '#facc15', border: '#ca8a04', text: '#422006' },
        'yellowfin tuna':      { bg: '#f472b6', border: '#db2777', text: '#500724' },
        'mulloway':            { bg: '#a78bfa', border: '#7c3aed', text: '#2e1065' },
        'yellowtail kingfish': { bg: '#38bdf8', border: '#0284c7', text: '#082f49' },
        'snapper':             { bg: '#f87171', border: '#dc2626', text: '#450a0a' },
        'bream':               { bg: '#34d399', border: '#059669', text: '#022c22' },
    };

    const DEFAULT_COLOUR = { bg: '#69d8ff', border: '#0e7490', text: '#082f49' };

    // SVG pin: 32 wide � 46 tall, geographic point = bottom-centre (16, 46)
    const PIN_W  = 32;
    const PIN_H  = 46;

    function getColour(species) {
        return SPECIES_COLOURS[(species ?? '').toLowerCase()] ?? DEFAULT_COLOUR;
    }

    function makePinSvg(bg, border, text, initial) {
        return `<svg xmlns="http://www.w3.org/2000/svg"
                     width="${PIN_W}" height="${PIN_H}"
                     viewBox="0 0 ${PIN_W} ${PIN_H}"
                     class="hk-pin-svg"
                     style="overflow:visible;display:block;transition:transform 0.15s ease">
            <circle cx="16" cy="16" r="14"
                    fill="${bg}" stroke="${border}" stroke-width="2"/>
            <polygon points="10,26 22,26 16,${PIN_H}"
                     fill="${bg}" stroke="${border}" stroke-width="1.5"
                     stroke-linejoin="round"/>
            <rect x="10" y="24" width="12" height="4" fill="${bg}"/>
            <text x="16" y="21"
                  font-family="Inter,-apple-system,BlinkMacSystemFont,sans-serif"
                  font-size="13" font-weight="800"
                  fill="${text}"
                  text-anchor="middle" dominant-baseline="middle"
                  pointer-events="none">${initial}</text>
        </svg>`;
    }

    function getStyleToggleIconSvg(iconName) {
        if (iconName === 'map') {
            return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M9 4 3 6.5v13L9 17l6 2.5 6-2.5V4.5L15 7 9 4Z"></path>
                        <path d="M9 4v13"></path>
                        <path d="M15 7v12.5"></path>
                    </svg>`;
        }

        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M20 14.5A8.5 8.5 0 1 1 9.5 4 7 7 0 0 0 20 14.5Z"></path>
                </svg>`;
    }

    // ?? Style toggle as a proper Mapbox IControl ?????????????????????????????
    function makeStyleToggleControl(map) {
        const STYLES = {
            outdoors: 'mapbox://styles/mapbox/outdoors-v12',
            dark:     'mapbox://styles/mapbox/dark-v11',
        };
        let currentKey = 'outdoors';

        const container = document.createElement('div');
        container.className = 'mapboxgl-ctrl mapboxgl-ctrl-group hk-style-ctrl';

        const btn = document.createElement('button');
        btn.className = 'hk-style-btn';
        btn.type      = 'button';
        btn.title     = 'Toggle map style';

        function updateLabel() {
            const isDark = currentKey === 'dark';
            btn.innerHTML = isDark
                ? `${getStyleToggleIconSvg('map')}<span>Terrain</span>`
                : `${getStyleToggleIconSvg('moon')}<span>Dark</span>`;
        }
        updateLabel();

        container.appendChild(btn);

        btn.addEventListener('click', () => {
            currentKey = currentKey === 'outdoors' ? 'dark' : 'outdoors';
            updateLabel();
            map.setStyle(STYLES[currentKey]);
            map.once('style.load', () => {
                (map._hookedMarkers ?? []).forEach(m => m.addTo(map));
            });
        });

        return {
            onAdd()    { return container; },
            onRemove() { container.remove(); }
        };
    }
    // ?????????????????????????????????????????????????????????????????????????

    function initMap(containerId, accessToken, centerLng, centerLat, zoom) {
        if (maps[containerId]) {
            maps[containerId].remove();
            delete maps[containerId];
        }

        mapboxgl.accessToken = accessToken;

        const map = new mapboxgl.Map({
            container: containerId,
            style: 'mapbox://styles/mapbox/outdoors-v12',
            center: [centerLng, centerLat],
            zoom: zoom ?? 7,
            attributionControl: false
        });

        map.addControl(new mapboxgl.NavigationControl({ showCompass: true }), 'top-right');
        map.addControl(makeStyleToggleControl(map), 'top-right');
        map.addControl(new mapboxgl.ScaleControl({ unit: 'metric' }), 'bottom-left');
        map.addControl(new mapboxgl.AttributionControl({ compact: true }), 'bottom-left');

        maps[containerId] = map;
        return new Promise(resolve => map.on('load', resolve));
    }

    function addMarkers(containerId, markers) {
        const map = maps[containerId];
        if (!map) return;

        (map._hookedMarkers ?? []).forEach(m => m.remove());
        map._hookedMarkers = [];

        // Legend lives outside the clipped map canvas � in the wrap overlay
        const wrapEl  = document.getElementById(containerId)?.parentElement;
        const oldLegend = wrapEl?.querySelector('.hk-legend');
        if (oldLegend) oldLegend.remove();

        const legendSpecies = new Map();

        markers.forEach(item => {
            if (item.lng == null || item.lat == null) return;

            const colour  = getColour(item.species);
            const initial = (item.species ?? '?')[0].toUpperCase();

            if (!legendSpecies.has(item.species)) {
                legendSpecies.set(item.species, colour.bg);
            }

            const details = [];
            if (item.length) details.push(`${item.length} m`);
            if (item.weight) details.push(`${item.weight} kg`);

            const popup = new mapboxgl.Popup({
                offset: [0, -(PIN_H + 6)],   // popup stem starts just above the pin tip
                closeButton: true,
                maxWidth: '240px',
                anchor: 'bottom'
            }).setHTML(`
                <div class="hk-popup">
                    <div class="hk-popup-swatch" style="background:${colour.bg}"></div>
                    <div class="hk-popup-body">
                        <div class="hk-popup-species">${item.species}</div>
                        ${details.length ? `<div class="hk-popup-meta">${details.join(' &bull; ')}</div>` : ''}
                        <div class="hk-popup-user">@${item.username}</div>
                        <div class="hk-popup-time">${item.time}</div>
                    </div>
                </div>`);

            // Wrapper div � fixed size, NO transform or filter here (Mapbox owns the transform)
            const el = document.createElement('div');
            el.style.cssText = `width:${PIN_W}px;height:${PIN_H}px;cursor:pointer`;
            el.title         = item.species;
            el.innerHTML     = makePinSvg(colour.bg, colour.border, colour.text, initial);

            // Hover animates the SVG child, never the wrapper � preserves Mapbox's translate
            const svg = el.querySelector('svg');
            el.addEventListener('mouseenter', () => { svg.style.transform = 'translateY(-4px) scale(1.15)'; });
            el.addEventListener('mouseleave', () => { svg.style.transform = ''; });

            // anchor:'bottom' ? Mapbox places the bottom-centre of el at the coordinate.
            // The SVG tip is drawn at (16, 46) = bottom-centre, so it lands exactly.
            const marker = new mapboxgl.Marker({ element: el, anchor: 'bottom' })
                .setLngLat([item.lng, item.lat])
                .setPopup(popup)
                .addTo(map);

            map._hookedMarkers.push(marker);
        });

        // Append legend to the wrap element (outside the overflow:hidden canvas)
        if (legendSpecies.size > 0 && wrapEl) {
            const legend = document.createElement('div');
            legend.className = 'hk-legend';

            legendSpecies.forEach((bg, name) => {
                const row = document.createElement('div');
                row.className = 'hk-legend-row';
                row.innerHTML = `<span class="hk-legend-dot" style="background:${bg}"></span>
                                 <span class="hk-legend-label">${name}</span>`;
                legend.appendChild(row);
            });

            wrapEl.appendChild(legend);
        }
    }

    function flyTo(containerId, lng, lat, zoom) {
        const map = maps[containerId];
        if (!map) return;
        map.flyTo({ center: [lng, lat], zoom: zoom ?? 11, speed: 1.4, curve: 1.2 });
    }

    function destroyMap(containerId) {
        const map = maps[containerId];
        if (!map) return;
        map.remove();
        delete maps[containerId];
    }

    return { initMap, addMarkers, flyTo, destroyMap };
})();
