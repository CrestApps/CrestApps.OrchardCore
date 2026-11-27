import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Products/Assets/js/product-price-editor.js';

const editor = globalThis.CrestAppsProductPriceEditor;

// "Add a price" clones the last row. MVC writes a hidden "false" after every checkbox; blanking it made the first
// save of a product with an added row fail with "Invalid Boolean value" for each checkbox.
describe('resetAction', () => {
    it('keeps the hidden false that MVC writes after a checkbox', () => {
        expect(editor.resetAction({ tagName: 'INPUT', type: 'hidden', name: 'ProductPricePart.Prices[1].IsDefault', value: 'false' })).toBe('keep');
        expect(editor.resetAction({ tagName: 'INPUT', type: 'hidden', name: 'ProductPricePart.Prices[1].Remove', value: 'false' })).toBe('keep');
    });

    it('renumbers the invariant marker ASP.NET Core writes for number and date inputs', () => {
        expect(editor.resetAction({ tagName: 'INPUT', type: 'hidden', name: '__Invariant', value: 'ProductPricePart.Prices[0].Amount' })).toBe('renumber');
    });

    it('keeps other hidden inputs', () => {
        expect(editor.resetAction({ tagName: 'INPUT', type: 'hidden', name: 'ProductPricePart.Prices[1].Token', value: 'abc' })).toBe('keep');
    });

    it('clears the row id so the server adds a new price', () => {
        expect(editor.resetAction({ tagName: 'INPUT', type: 'hidden', name: 'ProductPricePart.Prices[1].PriceId' })).toBe('clear');
    });

    it('unchecks checkboxes', () => {
        expect(editor.resetAction({ tagName: 'INPUT', type: 'checkbox', name: 'ProductPricePart.Prices[1].IsActive' })).toBe('uncheck');
    });

    it('clears what a person types into', () => {
        ['text', 'number', 'date', 'datetime-local'].forEach(type => {
            expect(editor.resetAction({ tagName: 'INPUT', type, name: 'ProductPricePart.Prices[1].Amount' })).toBe('clear');
        });
    });

    it('keeps a select on its choice', () => {
        expect(editor.resetAction({ tagName: 'SELECT', type: 'select-one', name: 'ProductPricePart.Prices[1].Kind' })).toBe('keep');
    });
});

describe('renumber', () => {
    it('renumbers the index in a name and an id', () => {
        expect(editor.renumber('ProductPricePart.Prices[0].Amount', 3)).toBe('ProductPricePart.Prices[3].Amount');
        expect(editor.renumber('ProductPricePart_Prices_0__Amount', 3)).toBe('ProductPricePart_Prices_3__Amount');
    });

    it('leaves a value without an index alone', () => {
        expect(editor.renumber('__Invariant', 3)).toBe('__Invariant');
    });
});
