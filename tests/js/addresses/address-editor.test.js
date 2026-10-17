import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Addresses/Assets/js/address-editor.js';

const editor = globalThis.CrestAppsAddressEditor;

const offered = ['Name', 'Company', 'StreetAddress', 'DependentLocality', 'City', 'Region', 'PostalCode', 'SortingCode'];

// The United States writes "%N%n%O%n%A%n%C, %S %Z"; Japan, in its own script, "〒%Z%n%S%n%A%n%O%n%N".
const unitedStates = { lines: [['Name'], ['Company'], ['StreetAddress'], ['City', 'Region', 'PostalCode']] };
const japan = { lines: [['PostalCode'], ['Region'], ['StreetAddress'], ['Company'], ['Name']] };

describe('planRows', () => {
    it('follows the country\'s lines', () => {
        expect(editor.planRows(unitedStates, offered)).toEqual([['Name'], ['Company'], ['StreetAddress'], ['City', 'Region', 'PostalCode']]);
        expect(editor.planRows(japan, offered)).toEqual([['PostalCode'], ['Region'], ['StreetAddress'], ['Company'], ['Name']]);
    });

    it('drops what the page does not ask for, and the lines left empty', () => {
        expect(editor.planRows(unitedStates, ['StreetAddress', 'City', 'Region', 'PostalCode'])).toEqual([['StreetAddress'], ['City', 'Region', 'PostalCode']]);
    });

    it('lays out nothing without a layout', () => {
        expect(editor.planRows(null, offered)).toEqual([]);
    });
});

describe('unusedComponents', () => {
    it('sets aside what the country does not use', () => {
        expect(editor.unusedComponents(editor.planRows(japan, offered), offered)).toEqual(['DependentLocality', 'City', 'SortingCode']);
        expect(editor.unusedComponents(editor.planRows(unitedStates, offered), offered)).toEqual(['DependentLocality', 'SortingCode']);
    });
});

describe('columnClass', () => {
    it('gives the street the whole width, a part alone half, and shares a line between its parts', () => {
        expect(editor.columnClass('StreetAddress', 1)).toBe('col-12');
        expect(editor.columnClass('Region', 1)).toBe('col-md-6');
        expect(editor.columnClass('City', 3)).toBe('col-md');
    });
});
