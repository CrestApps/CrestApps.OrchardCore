import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Products/Assets/js/product-listing-editor.js';

const editor = globalThis.CrestAppsProductListingEditor;

// Each filter row posts its Order number, and the listing shows its filters in that order across every group. A
// drag hands a group's own numbers out again, so rows of other groups keep their place between them.
describe('reassignOrders', () => {
    it('hands the group its own numbers again, smallest first, in the order the rows now stand', () => {
        // The rows held 2, 5 and 9; after the last was dragged to the top, the inputs read 9, 2, 5.
        expect(editor.reassignOrders(['9', '2', '5'])).toEqual([2, 5, 9]);
    });

    it('treats a blank number as zero', () => {
        expect(editor.reassignOrders(['', '3'])).toEqual([0, 3]);
    });
});

describe('move', () => {
    it('moves an item up', () => {
        expect(editor.move(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b']);
    });

    it('leaves the list as it is for a move past either end', () => {
        expect(editor.move(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
        expect(editor.move(['a', 'b'], 1, 2)).toEqual(['a', 'b']);
    });
});

describe('countUsed', () => {
    it('counts the ticked rows of the product types shown', () => {
        expect(editor.countUsed([
            { used: true, visible: true },
            { used: true, visible: false },
            { used: false, visible: true },
        ])).toBe(1);
    });
});
