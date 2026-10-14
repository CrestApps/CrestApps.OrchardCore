import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Carts/Assets/js/variant-picker.js';

const picker = globalThis.CrestAppsVariantPicker;

// The variants on sale of a cap: S / Gloss is switched off, so it is not among them.
const variants = [
    { id: 's-matte', options: { Size: 'S', Finish: 'Matte' } },
    { id: 'm-matte', options: { Size: 'M', Finish: 'Matte' } },
    { id: 'm-gloss', options: { Size: 'M', Finish: 'Gloss' } },
];

describe('findVariant', () => {
    it('finds the variant of the chosen options', () => {
        expect(picker.findVariant(variants, { Size: 'M', Finish: 'Gloss' }).id).toBe('m-gloss');
    });

    it('finds none for a combination that is not on sale', () => {
        expect(picker.findVariant(variants, { Size: 'S', Finish: 'Gloss' })).toBeNull();
    });
});

describe('availableValues', () => {
    it('offers only the values that make a variant on sale with the options already chosen', () => {
        expect(picker.availableValues(variants, { Size: 'S', Finish: 'Matte' }, 'Finish')).toEqual(['Matte']);
        expect(picker.availableValues(variants, { Size: 'M', Finish: 'Matte' }, 'Finish')).toEqual(['Matte', 'Gloss']);
    });

    it('does not let an option rule out its own values', () => {
        expect(picker.availableValues(variants, { Size: 'S', Finish: 'Matte' }, 'Size')).toEqual(['S', 'M']);
        expect(picker.availableValues(variants, { Size: 'S', Finish: 'Gloss' }, 'Size')).toEqual(['M']);
    });
});
