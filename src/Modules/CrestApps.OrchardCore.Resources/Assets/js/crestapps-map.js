/* global L */
// A small wrapper around Leaflet for CrestApps pages: a map with the site's tiles, read-only shapes, markers (in
// clusters when the cluster plugin is loaded), a draggable pin, and polygon drawing with Leaflet-Geoman that writes
// GeoJSON into a form field.
window.crestappsMap = (function () {
    'use strict';

    const number = (value, fallback) => {
        const parsed = parseFloat(value);

        return Number.isFinite(parsed) ? parsed : fallback;
    };

    // Creates a map in an element. Options come from the element's data attributes (data-tile-url,
    // data-attribution, data-latitude, data-longitude, data-zoom) and can be overridden.
    const create = (element, options) => {
        const el = typeof element === 'string' ? document.getElementById(element) : element;

        if (!el || typeof L === 'undefined') {
            return null;
        }

        const settings = Object.assign({
            tileUrl: el.dataset.tileUrl || 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
            attribution: el.dataset.attribution || '',
            latitude: number(el.dataset.latitude, 0),
            longitude: number(el.dataset.longitude, 0),
            zoom: number(el.dataset.zoom, 2),
        }, options || {});

        const map = L.map(el, { scrollWheelZoom: false }).setView([settings.latitude, settings.longitude], settings.zoom);

        // Tile servers such as OpenStreetMap's refuse requests without a referrer, and admin pages send none by
        // default; the site's origin is enough for them.
        L.tileLayer(settings.tileUrl, { attribution: settings.attribution, maxZoom: 19, referrerPolicy: 'strict-origin-when-cross-origin' }).addTo(map);

        // Wheel zoom only once the map is clicked, so the page still scrolls over it.
        map.on('click', () => map.scrollWheelZoom.enable());
        map.on('mouseout', () => map.scrollWheelZoom.disable());

        return map;
    };

    const parse = (text) => {
        if (!text || !text.trim()) {
            return null;
        }

        try {
            return JSON.parse(text);
        } catch {
            return null;
        }
    };

    // Shows GeoJSON shapes, each with an optional popup, and fits the map to them.
    const showShapes = (map, shapes, style) => {
        const group = L.featureGroup().addTo(map);

        (shapes || []).forEach((shape) => {
            const geoJson = typeof shape.geoJson === 'string' ? parse(shape.geoJson) : shape.geoJson;

            if (!geoJson) {
                return;
            }

            // Shown shapes can't be edited by the drawing tools.
            const layer = L.geoJSON(geoJson, { pmIgnore: true, style: Object.assign({ weight: 2, fillOpacity: 0.15 }, style || {}, shape.style || {}) });

            if (shape.label) {
                layer.bindTooltip(shape.label, { sticky: true });
            }

            group.addLayer(layer);
        });

        if (group.getLayers().length) {
            map.fitBounds(group.getBounds(), { padding: [16, 16] });
        }

        return group;
    };

    // Adds markers ({ latitude, longitude, label }) and, unless options.fit is false, fits the map to them.
    const addMarkers = (map, markers, options) => {
        const group = typeof L.markerClusterGroup === 'function' ? L.markerClusterGroup() : L.featureGroup();

        (markers || []).forEach((marker) => {
            const layer = L.marker([marker.latitude, marker.longitude]);

            if (marker.label) {
                layer.bindPopup(marker.label);
            }

            group.addLayer(layer);
        });

        group.addTo(map);

        if (group.getLayers().length && (!options || options.fit !== false)) {
            map.fitBounds(group.getBounds(), { padding: [24, 24], maxZoom: 15 });
        }

        return group;
    };

    // A draggable pin that writes its position into two inputs.
    const pin = (map, latitudeInput, longitudeInput) => {
        let marker = null;

        const write = (latLng) => {
            latitudeInput.value = latLng.lat.toFixed(6);
            longitudeInput.value = latLng.lng.toFixed(6);
            latitudeInput.dispatchEvent(new Event('change', { bubbles: true }));
        };

        const place = (latLng) => {
            if (marker) {
                marker.setLatLng(latLng);
            } else {
                marker = L.marker(latLng, { draggable: true }).addTo(map);
                marker.on('dragend', () => write(marker.getLatLng()));
            }
        };

        const latitude = parseFloat(latitudeInput.value);
        const longitude = parseFloat(longitudeInput.value);

        if (Number.isFinite(latitude) && Number.isFinite(longitude)) {
            place([latitude, longitude]);
            map.setView([latitude, longitude], 15);
        }

        map.on('click', (event) => {
            place(event.latlng);
            write(event.latlng);
        });

        return { remove: () => { if (marker) { map.removeLayer(marker); marker = null; } } };
    };

    // Lets people draw, edit, move and remove polygons, and keeps the input's value as a GeoJSON
    // FeatureCollection of them (empty when there are none). Changing the input reloads the shapes.
    const editShapes = (map, input) => {
        const group = L.featureGroup().addTo(map);
        let writing = false;

        const write = () => {
            const features = [];

            group.eachLayer((layer) => {
                if (typeof layer.toGeoJSON === 'function') {
                    features.push(layer.toGeoJSON());
                }
            });

            writing = true;
            input.value = features.length ? JSON.stringify({ type: 'FeatureCollection', features }) : '';
            input.dispatchEvent(new Event('change', { bubbles: true }));
            writing = false;
        };

        const track = (layer) => {
            layer.on('pm:edit pm:dragend pm:rotateend', write);
        };

        const load = () => {
            group.clearLayers();

            const geoJson = parse(input.value);

            if (!geoJson) {
                return;
            }

            L.geoJSON(geoJson, {
                onEachFeature: (_, layer) => {
                    // Multi-polygons become one layer per polygon, so each can be edited.
                    const parts = layer instanceof L.LayerGroup ? layer.getLayers() : [layer];

                    parts.forEach((part) => {
                        group.addLayer(part);
                        track(part);
                    });
                },
            });

            if (group.getLayers().length) {
                map.fitBounds(group.getBounds(), { padding: [16, 16] });
            }
        };

        load();

        if (map.pm) {
            map.pm.setGlobalOptions({ layerGroup: group, snappable: true });
            map.pm.addControls({
                position: 'topleft',
                drawMarker: false,
                drawCircleMarker: false,
                drawPolyline: false,
                drawCircle: false,
                drawText: false,
                cutPolygon: true,
                rotateMode: false,
            });

            map.on('pm:create', (event) => {
                group.addLayer(event.layer);
                track(event.layer);
                write();
            });

            map.on('pm:remove', (event) => {
                group.removeLayer(event.layer);
                write();
            });

            map.on('pm:cut', (event) => {
                group.removeLayer(event.originalLayer);
                group.addLayer(event.layer);
                track(event.layer);
                write();
            });
        }

        input.addEventListener('change', () => {
            if (!writing) {
                load();
            }
        });

        return group;
    };

    return { create, showShapes, addMarkers, pin, editShapes };
})();
