import { describe, expect, it } from 'vitest';
import { withDraftStamp } from '../../../src/Modules/CrestApps.OrchardCore.Surveys/Assets/js/designer/draft-stamp.js';

// Review V-L14: after the page's own panel saves, Discard draft carries the draft version the last save returned.
describe('withDraftStamp', () => {
    it('replaces the draft value the page was shown with', () => {
        const href = withDraftStamp('https://site/admin/surveys/s1/draft/discard?draft=638000000000000000', '638000000010000000', 'https://site/admin/surveys/s1/design');

        expect(new URL(href).searchParams.get('draft')).toBe('638000000010000000');
        expect(new URL(href).pathname).toBe('/admin/surveys/s1/draft/discard');
    });

    it('adds the draft value to a link without one, and resolves a relative link', () => {
        const href = withDraftStamp('/admin/surveys/s1/draft/discard', '42', 'https://site/admin/surveys/s1/design');

        expect(href).toBe('https://site/admin/surveys/s1/draft/discard?draft=42');
    });

    it('leaves the link as it is without a version', () => {
        expect(withDraftStamp('/admin/x?draft=1', '', 'https://site/')).toBe('/admin/x?draft=1');
    });
});
