import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/extension-names.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-target.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/keypad-search.js';

const {
    createExtensionDirectory,
    storeExtensionEntries,
    keypadExtensionMatches,
    resolveKeypadExtension,
    buildKeypadExtensionResultsHtml
} = globalThis.CrestAppsSoftPhone;

const escapeHtml = value => String(value == null ? '' : value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

function directory() {
    const copy = createExtensionDirectory();

    storeExtensionEntries(copy, [
        { extension: '1', displayName: 'Mike Alhayek' },
        { extension: '2', displayName: 'Test 2' },
        { extension: '3', displayName: 'Jane Doe' },
        { extension: '4', displayName: 'Janet Lee' }
    ], 1, ['1']);

    return copy;
}

// The keypad's extension field took digits only: the country-flag input dropped every letter, so a colleague could only
// be called by knowing their extension. It now searches the directory by name, the way the transfer panel does.
describe('keypadExtensionMatches', () => {
    it('lists the extensions whose person matches a name, leaving the agent\'s own out', () => {
        expect(keypadExtensionMatches(directory(), 'jan').map(entry => entry.extension)).toEqual(['3', '4']);
        expect(keypadExtensionMatches(directory(), 'mike')).toEqual([]);
    });

    it('lists the extensions whose digits match', () => {
        expect(keypadExtensionMatches(directory(), '2').map(entry => entry.extension)).toEqual(['2']);
    });

    it('lists nothing for an empty field', () => {
        expect(keypadExtensionMatches(directory(), '  ')).toEqual([]);
    });
});

describe('resolveKeypadExtension', () => {
    it('dials typed digits as the extension they are', () => {
        expect(resolveKeypadExtension(directory(), '2')).toEqual({ extension: '2', refused: '' });
        expect(resolveKeypadExtension(directory(), '42')).toEqual({ extension: '42', refused: '' });
    });

    it('dials the one person a name narrows the list to', () => {
        expect(resolveKeypadExtension(directory(), 'test')).toEqual({ extension: '2', refused: '' });
    });

    it('refuses a name matching several people, or nobody', () => {
        expect(resolveKeypadExtension(directory(), 'jan')).toEqual({ extension: '', refused: 'several-match' });
        expect(resolveKeypadExtension(directory(), 'zed')).toEqual({ extension: '', refused: 'no-match' });
    });

    it('refuses the agent\'s own extension, typed or searched for', () => {
        expect(resolveKeypadExtension(directory(), '1')).toEqual({ extension: '', refused: 'own-extension' });
        expect(resolveKeypadExtension(directory(), 'mike')).toEqual({ extension: '', refused: 'no-match' });
    });

    it('refuses an empty field', () => {
        expect(resolveKeypadExtension(directory(), '')).toEqual({ extension: '', refused: 'empty' });
    });

    // The label the field shows for a call ("Test 2 · ext 2") is never an extension to dial.
    it('never reads a call\'s display label as an extension', () => {
        expect(resolveKeypadExtension(directory(), 'Test 2 · ext 2').extension).toBe('');
    });
});

describe('buildKeypadExtensionResultsHtml', () => {
    const strings = { transferExtension: 'Ext {0}', keypadExtensionResults: 'Matching extensions' };

    it('draws one button per match that dials its extension', () => {
        const html = buildKeypadExtensionResultsHtml(keypadExtensionMatches(directory(), 'jan'), strings, escapeHtml);

        expect(html).toContain('data-telephony-keypad-extension="3"');
        expect(html).toContain('data-telephony-keypad-extension="4"');
        expect(html).toContain('Jane Doe');
        expect(html).toContain('Ext 4');
    });

    it('draws nothing without matches', () => {
        expect(buildKeypadExtensionResultsHtml([], strings, escapeHtml)).toBe('');
    });

    it('escapes what it shows', () => {
        const html = buildKeypadExtensionResultsHtml([{ extension: '"><x', name: '<b>', detail: '<i>' }], strings, escapeHtml);

        expect(html).not.toContain('<b>');
        expect(html).not.toContain('"><x');
    });
});
