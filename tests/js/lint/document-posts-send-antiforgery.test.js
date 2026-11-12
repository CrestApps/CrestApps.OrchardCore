import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// SEC-FE4 f: the chat interaction document editor posted its uploads and removals with a bare fetch, without the
// antiforgery token, so the endpoints could never start checking it. The posts go through crestapps.http.request,
// which sends RequestVerificationToken on anything but a read.
const view = 'src/Modules/CrestApps.OrchardCore.AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml';

describe('chat interaction document posts', () => {
    const source = readFileSync(view, 'utf8');

    it('use the shared request helper, which sends the antiforgery token', () => {
        expect(source).toMatch(/crestapps\.http\.request\(uploadUrl,/);
        expect(source).toMatch(/crestapps\.http\.request\(removeUrl,/);
    });

    it('never post with a bare fetch', () => {
        expect(source).not.toMatch(/fetch\((uploadUrl|removeUrl),/);
    });
});
