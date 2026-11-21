import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Leaflet sets a string popup or tooltip with innerHTML, so a label carrying markup (a work-order summary built from a
// contact's name, a territory name) ran as HTML in the dispatcher's session. The map helper now hands Leaflet an element
// whose text is the label, or the element the caller built itself.
function fakeLeaflet() {
    const bound = [];
    const layer = kind => ({
        bindTooltip(content, options) { bound.push({ kind, method: 'bindTooltip', content, options }); return this; },
        bindPopup(content, options) { bound.push({ kind, method: 'bindPopup', content, options }); return this; },
    });
    const group = () => {
        const layers = [];

        return {
            addTo() { return this; },
            addLayer(item) { layers.push(item); return this; },
            getLayers: () => layers,
            getBounds: () => ({}),
        };
    };

    return {
        bound,
        L: {
            featureGroup: group,
            geoJSON: () => layer('shape'),
            marker: () => layer('marker'),
        },
    };
}

function fakeDocument() {
    return {
        createElement: tag => {
            const node = { nodeType: 1, tagName: tag.toUpperCase(), children: [], text: '' };

            Object.defineProperty(node, 'textContent', {
                get: () => node.text,
                set: value => { node.text = String(value); },
            });
            Object.defineProperty(node, 'innerHTML', {
                get: () => { throw new Error('the label is read as text, never as markup'); },
                set: () => { throw new Error('the label is set as text, never as markup'); },
            });

            return node;
        },
    };
}

const map = { fitBounds() {} };
const markup = '<img src=x onerror="alert(1)">Ann & co';

describe('the CrestApps map helper', () => {
    let leaflet;

    beforeEach(async () => {
        vi.resetModules();
        leaflet = fakeLeaflet();
        globalThis.window = {};
        globalThis.document = fakeDocument();
        globalThis.L = leaflet.L;

        await import('../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/crestapps-map.js');
    });

    afterEach(() => {
        delete globalThis.window;
        delete globalThis.document;
        delete globalThis.L;
    });

    it('shows a marker label with markup as text', () => {
        globalThis.window.crestappsMap.addMarkers(map, [{ latitude: 1, longitude: 2, label: markup }]);

        const [popup] = leaflet.bound;

        expect(popup.method).toBe('bindPopup');
        expect(typeof popup.content).not.toBe('string');
        expect(popup.content.textContent).toBe(markup);
    });

    it('shows a shape label with markup as text', () => {
        globalThis.window.crestappsMap.showShapes(map, [{ geoJson: { type: 'Polygon' }, label: markup }]);

        const [tooltip] = leaflet.bound;

        expect(tooltip.method).toBe('bindTooltip');
        expect(tooltip.options).toEqual({ sticky: true });
        expect(typeof tooltip.content).not.toBe('string');
        expect(tooltip.content.textContent).toBe(markup);
    });

    it('passes an element the caller built as it is', () => {
        const link = globalThis.document.createElement('a');

        globalThis.window.crestappsMap.addMarkers(map, [{ latitude: 1, longitude: 2, label: link }]);
        globalThis.window.crestappsMap.showShapes(map, [{ geoJson: { type: 'Polygon' }, label: link }]);

        expect(leaflet.bound.map(entry => entry.content)).toEqual([link, link]);
    });

    it('binds nothing when there is no label', () => {
        globalThis.window.crestappsMap.addMarkers(map, [{ latitude: 1, longitude: 2 }]);
        globalThis.window.crestappsMap.showShapes(map, [{ geoJson: { type: 'Polygon' }, label: '' }]);

        expect(leaflet.bound).toEqual([]);
    });
});

// The dispatch board's marker shows a link to the visit. It built that as an HTML string with the summary escaped by
// hand (only < > &); it now builds the link and the text as elements and hands the map the element.
describe('the dispatch board map markers', () => {
    it('build their label as elements, not as an HTML string', async () => {
        const { readFileSync } = await import('node:fs');
        const code = readFileSync('src/Modules/CrestApps.OrchardCore.Dispatch/Assets/js/dispatch-board.js', 'utf8');

        expect(code).not.toMatch(/label:\s*`[^`]*</);
        expect(code).not.toMatch(/\.replace\(\/\[<>&\]\/g/);
        // The technician dots are Leaflet tooltips too: a technician's name is text there as well.
        expect(code).not.toMatch(/bindTooltip\(format\(/);
    });
});
