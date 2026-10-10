import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-designer/designer-ui.js';

const ui = globalThis.CrestAppsReportDesigner.ui;

describe('local URLs', () => {
    it('keeps the root-relative paths the server gives', () => {
        expect(ui.localUrl('/Admin/reports/designs/abc')).toBe('/Admin/reports/designs/abc');
        expect(ui.localUrl('/tenant/Admin/reports/builder/drafts?x=1#top')).toBe('/tenant/Admin/reports/builder/drafts?x=1#top');
    });

    it('turns other sites and script URLs into paths on this site', () => {
        expect(ui.localUrl('javascript:alert(1)')).toBe('/javascript:alert(1)');
        expect(ui.localUrl('//evil.example/path')).toBe('/evil.example/path');
        expect(ui.localUrl('\\evil.example')).toBe('/evil.example');
        expect(ui.localUrl('https://evil.example')).toBe('/https://evil.example');
        expect(ui.localUrl('  /Admin')).toBe('/Admin');
    });

    it('leaves a missing URL empty', () => {
        expect(ui.localUrl('')).toBe('');
        expect(ui.localUrl(null)).toBe('');
    });
});

describe('appending children', () => {
    // A minimal DOM: the helper must turn text into text nodes and append only nodes.
    function fakeDocument() {
        class Node {
            constructor(nodeType, text) {
                this.nodeType = nodeType;
                this.text = text;
                this.children = [];
            }

            appendChild(child) {
                this.children.push(child);

                return child;
            }
        }

        globalThis.Node = Node;

        return {
            createTextNode: (text) => new Node(3, text),
            element: () => new Node(1),
        };
    }

    it('appends text as text nodes and skips empty values', () => {
        const dom = fakeDocument();
        const previous = globalThis.document;

        globalThis.document = dom;

        try {
            const element = dom.element();
            const child = dom.element();

            ui.append(element, ['<b>not markup</b>', 3, null, false, [child]]);

            expect(element.children.map((node) => node.nodeType)).toEqual([3, 3, 1]);
            expect(element.children[0].text).toBe('<b>not markup</b>');
        } finally {
            globalThis.document = previous;
            delete globalThis.Node;
        }
    });
});
