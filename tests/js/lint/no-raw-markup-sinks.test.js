import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Values that someone other than the reader wrote (a prompt template's description, a file name, an error page, a
// column's valid values) were written into the page as markup. Each is text now: encoded by Razor, set with
// textContent, or parsed into an inert document. These are the places the November 2026 review found (SEC-FE4 b, c, g).
const modules = join('src', 'Modules');
const read = path => readFileSync(join(modules, path), 'utf8');

describe.each([
    'CrestApps.OrchardCore.AI.Prompting/Views/AIProfilePromptSelection.Edit.cshtml',
    'CrestApps.OrchardCore.AI.Prompting/Views/ChatInteractionPromptSelection.Edit.cshtml',
])('the prompt parameter list in %s', path => {
    it('shows a template parameter name and description as text', () => {
        const view = read(path);

        expect(view).not.toMatch(/innerHTML\s*=\s*'<strong>'/);
        expect(view).toMatch(/\.textContent\s*=\s*p\.Name/);
    });
});

describe('the classifieds search map', () => {
    const view = read('CrestApps.OrchardCore.Classifieds/Views/ClassifiedsSearch.cshtml');

    it('loads Leaflet through the resources that carry integrity hashes', () => {
        expect(view).not.toMatch(/asp-src="https?:/);
        expect(view).toMatch(/<style asp-name="leaflet"/);
        expect(view).toMatch(/<script asp-name="crestapps-map"/);
    });

    it('builds the popup as elements instead of an HTML string escaped by hand', () => {
        expect(view).not.toMatch(/href="\$\{/);
        expect(view).not.toMatch(/\.replace\(\/</);
        expect(view).not.toMatch(/bindPopup\(/);
    });
});

describe('the content import column list', () => {
    it('encodes a column\'s valid values', () => {
        expect(read('CrestApps.OrchardCore.ContentTransfer/Views/Admin/Import.cshtml')).not.toMatch(/Html\.Raw\("<code>/);
    });
});

describe('the product facet', () => {
    it('writes no element by hand with the facet key in it', () => {
        expect(read('CrestApps.OrchardCore.Products/Views/ProductFacet.cshtml')).not.toMatch(/Html\.Raw\(\$"<div.*\{id\}/);
    });
});

describe('the import file picker', () => {
    it('shows the chosen file name as text', () => {
        expect(read('CrestApps.OrchardCore.ContentTransfer/Views/ImportContentFile.Edit.cshtml')).not.toMatch(/\.innerHTML\s*=/);
    });
});

describe('the chat interaction documents editor', () => {
    const view = read('CrestApps.OrchardCore.AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml');

    it('reads an error page through an inert document, never by setting innerHTML on a live element', () => {
        expect(view).not.toMatch(/div\.innerHTML\s*=\s*trimmed/);
        expect(view).toMatch(/new DOMParser\(\)\.parseFromString\(trimmed, 'text\/html'\)/);
    });
});
