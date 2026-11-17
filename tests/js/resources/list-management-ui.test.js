import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const source = readFileSync(new URL('../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/list-management-ui.js', import.meta.url), 'utf8');

describe('bulk actions', () => {
    it('run from the same number of checked rows that shows them', () => {
        // A list that sets data-minimum-selection="1" shows its actions for one row; a click must then run them too.
        expect(source).toContain('checkedCheckboxes.length < options.minimumSelection');
        expect(source).not.toContain('checkedCheckboxes.length <= 1');
    });
});
