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

    function makePinSvg(bg, border, text, initial, isFriend) {
        const friendRing = isFriend
            ? `<circle cx="16" cy="16" r="15.5" fill="none" stroke="rgba(255,255,255,0.7)" stroke-width="1.5" stroke-dasharray="4 3"/>`
            : '';
        return `<svg xmlns="http://www.w3.org/2000/svg"
                     width="${PIN_W}" height="${PIN_H}"
                     viewBox="0 0 ${PIN_W} ${PIN_H}"
                     class="hk-pin-svg"
                     style="overflow:visible;display:block;transition:transform 0.15s ease">
            ${friendRing}
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

    // Inline SVG icons � font-independent, match the toolbar's sun/moon intent
    const ICON_SUN = `<svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><line x1="12" y1="2" x2="12" y2="4"/><line x1="12" y1="20" x2="12" y2="22"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="2" y1="12" x2="4" y2="12"/><line x1="20" y1="12" x2="22" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>`;
    const ICON_MOON = `<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>`;

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

        function updateLabel() {
            const isDark = currentKey === 'dark';
            btn.innerHTML = isDark ? ICON_SUN : ICON_MOON;
            btn.title = isDark ? 'Switch to terrain style' : 'Switch to dark style';
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
            const isFriend = !!item.isFriend;

            if (!legendSpecies.has(item.species)) {
                legendSpecies.set(item.species, colour.bg);
            }

            const details = [];
            if (item.length) details.push(`${item.length} m`);
            if (item.weight) details.push(`${item.weight} kg`);

            const friendLabel = isFriend
                ? `<div class="hk-popup-friend-badge">Following</div>`
                : '';

            const popup = new mapboxgl.Popup({
                offset: [0, -(PIN_H + 6)],
                closeButton: true,
                maxWidth: '240px',
                anchor: 'bottom'
            }).setHTML(`
                <div class="hk-popup">
                    <div class="hk-popup-swatch" style="background:${colour.bg}"></div>
                    <div class="hk-popup-body">
                        <div class="hk-popup-species">${item.species}</div>
                        ${details.length ? `<div class="hk-popup-meta">${details.join(' &bull; ')}</div>` : ''}
                        <div class="hk-popup-user">@${item.username}${friendLabel}</div>
                        <div class="hk-popup-time">${item.time}</div>
                    </div>
                </div>`);

            // Wrapper div � fixed size, NO transform or filter here (Mapbox owns the transform)
            const el = document.createElement('div');
            el.style.cssText = `width:${PIN_W}px;height:${PIN_H}px;cursor:pointer`;
            el.title         = item.species;
            el.innerHTML     = makePinSvg(colour.bg, colour.border, colour.text, initial, isFriend);

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
