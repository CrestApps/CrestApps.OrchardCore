import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Invoicing/Assets/js/document-lines-editor.js';

const lines = globalThis.CrestAppsDocumentLines;

// The line editor works its amounts out in the browser for display only; the server's calculator works them out
// again on save and stays the source of truth.
describe('parseAmount', () => {
    it('reads a number input value, which is always written with a dot', () => {
        expect(lines.parseAmount('12.5')).toBe(12.5);
    });

    it('reads a decimal comma too, as typed in a decimal-comma culture', () => {
        expect(lines.parseAmount('12,5')).toBe(12.5);
    });

    it('reads nothing as no amount', () => {
        expect(lines.parseAmount('')).toBeNull();
        expect(lines.parseAmount('  ')).toBeNull();
        expect(lines.parseAmount('abc')).toBeNull();
    });
});

describe('lineAmount', () => {
    it('is the quantity times the price, rounded to the currency', () => {
        expect(lines.lineAmount({ quantity: 3, unitPrice: 33.333 }, 2)).toBe(100);
    });

    it('takes the line discount off, kept between nothing and the whole line', () => {
        expect(lines.lineAmount({ quantity: 2, unitPrice: 50, discountPercent: 10 }, 2)).toBe(90);
        expect(lines.lineAmount({ quantity: 1, unitPrice: 50, discountPercent: 150 }, 2)).toBe(0);
        expect(lines.lineAmount({ quantity: 1, unitPrice: 50, discountPercent: -5 }, 2)).toBe(50);
    });

    it('treats a missing quantity as one and a missing price as nothing', () => {
        expect(lines.lineAmount({ quantity: null, unitPrice: 40 }, 2)).toBe(40);
        expect(lines.lineAmount({ quantity: 2, unitPrice: null }, 2)).toBe(0);
    });

    it('rounds to whole units for a currency without decimals', () => {
        expect(lines.lineAmount({ quantity: 3, unitPrice: 333.4 }, 0)).toBe(1000);
    });
});

describe('estimateTax', () => {
    it('adds the rate on top of a net amount', () => {
        expect(lines.estimateTax(90, 20, false, 2)).toBe(18);
    });

    it('takes the tax out of a gross amount when prices include tax', () => {
        expect(lines.estimateTax(120, 20, true, 2)).toBe(20);
    });

    it('is unknown without a rate, for a line not saved yet', () => {
        expect(lines.estimateTax(90, null, false, 2)).toBeNull();
    });
});

describe('totals', () => {
    it('adds the lines, their estimated tax, and the total', () => {
        expect(lines.totals([{ amount: 100, rate: 20 }, { amount: 50, rate: 10 }], { pricesIncludeTax: false, decimals: 2 }))
            .toEqual({ subtotal: 150, tax: 25, total: 175, taxUnknown: false });
    });

    it('keeps the total at the gross when prices include tax', () => {
        expect(lines.totals([{ amount: 120, rate: 20 }], { pricesIncludeTax: true, decimals: 2 }))
            .toEqual({ subtotal: 120, tax: 20, total: 120, taxUnknown: false });
    });

    it('says the tax is unknown when a line with an amount has no rate yet', () => {
        expect(lines.totals([{ amount: 100, rate: 20 }, { amount: 30, rate: null }], { pricesIncludeTax: false, decimals: 2 }).taxUnknown).toBe(true);
    });
});

describe('renumber', () => {
    it('gives a posted field the index of its row', () => {
        expect(lines.renumber('Lines[7].UnitPrice', 'Lines', 2)).toBe('Lines[2].UnitPrice');
        expect(lines.renumber('Lines[__index__].Name', 'Lines', 0)).toBe('Lines[0].Name');
    });

    it('leaves other fields as they are', () => {
        expect(lines.renumber('Schedule[1].Amount', 'Lines', 0)).toBe('Schedule[1].Amount');
    });
});

describe('move', () => {
    it('moves a row up or down', () => {
        expect(lines.move(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'c', 'b']);
        expect(lines.move(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
    });

    it('leaves the rows as they are for a move past either end', () => {
        expect(lines.move(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
        expect(lines.move(['a', 'b'], 1, 2)).toEqual(['a', 'b']);
    });
});

describe('hasAmount', () => {
    it('is false for text and section heading lines only', () => {
        expect(lines.hasAmount('Item')).toBe(true);
        expect(lines.hasAmount('Fee')).toBe(true);
        expect(lines.hasAmount('Text')).toBe(false);
        expect(lines.hasAmount('Section')).toBe(false);
    });
});
