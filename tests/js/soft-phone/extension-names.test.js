import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/extension-names.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-target.js';

const {
    EXTENSION_DIRECTORY_MAX_AGE_MS,
    createExtensionDirectory,
    storeExtensionEntries,
    shouldReloadExtensions,
    extensionName,
    describeExtension,
    transferToExtensionLabel,
    callExtensionNumber,
    extensionDirectoryEntries,
    filterTransferTargets,
    resolveTransferTarget,
} = globalThis.CrestAppsSoftPhone;

function directoryOf(entries) {
    const directory = createExtensionDirectory();
    storeExtensionEntries(directory, entries, 1000);

    return directory;
}

describe('extension directory', () => {
    it('names each extension by the name the server gave', () => {
        const directory = directoryOf([
            { extension: '2', displayName: 'Jane Doe' },
            { extension: ' 3 ', displayName: 'Sam Lee' },
        ]);

        expect(extensionName(directory, '2')).toBe('Jane Doe');
        expect(extensionName(directory, '3')).toBe('Sam Lee');
        expect(extensionName(directory, ' 2 ')).toBe('Jane Doe');
    });

    it('knows no name for an unknown extension, or one named only by its own number', () => {
        const directory = directoryOf([{ extension: '4', displayName: '4' }, { extension: '5', displayName: '' }]);

        expect(extensionName(directory, '4')).toBe('');
        expect(extensionName(directory, '5')).toBe('');
        expect(extensionName(directory, '9')).toBe('');
        expect(extensionName(null, '2')).toBe('');
    });

    it('is read again only once it has aged', () => {
        const directory = directoryOf([]);

        expect(shouldReloadExtensions(createExtensionDirectory(), 0)).toBe(true);
        expect(shouldReloadExtensions(directory, 1000 + EXTENSION_DIRECTORY_MAX_AGE_MS - 1)).toBe(false);
        expect(shouldReloadExtensions(directory, 1000 + EXTENSION_DIRECTORY_MAX_AGE_MS)).toBe(true);
    });
});

describe('describeExtension', () => {
    const strings = { extensionWithName: '{0} · ext {1}' };

    it('names the person and the extension', () => {
        expect(describeExtension(strings, 'Jane Doe', '2')).toBe('Jane Doe · ext 2');
    });

    it('uses the name the call carries when the directory has none', () => {
        expect(describeExtension(strings, '', '2', 'jdoe')).toBe('jdoe · ext 2');
    });

    it('is the bare extension when nobody is known to be behind it', () => {
        expect(describeExtension(strings, '', '2')).toBe('2');
        expect(describeExtension(strings, '', '2', '2')).toBe('2');
        expect(describeExtension(strings, '', '2', 'sip:gencred@sip.telnyx.com')).toBe('2');
        expect(describeExtension(strings, '', '2', '+17025550101')).toBe('2');
    });

    it('falls back to its own wording', () => {
        expect(describeExtension({}, 'Jane Doe', '2')).toBe('Jane Doe · ext 2');
    });
});

describe('transferToExtensionLabel', () => {
    it('offers the extension with the name of whoever it rings', () => {
        expect(transferToExtensionLabel({}, '2', 'Jane Doe')).toBe('Transfer to extension 2 · Jane Doe');
    });

    it('offers the bare extension when nobody is known to be behind it', () => {
        expect(transferToExtensionLabel({ transferToExtensionNumber: 'An extension: {0}' }, '9', '')).toBe('An extension: 9');
    });
});

describe('callExtensionNumber', () => {
    it('is the extension the server stamped on the call', () => {
        expect(callExtensionNumber({ metadata: { extensionNumber: ' 2 ' } })).toBe('2');
        expect(callExtensionNumber({ metadata: {} })).toBe('');
        expect(callExtensionNumber(null)).toBe('');
    });
});

describe('extensionDirectoryEntries', () => {
    it('lists the extensions by name, each sent as an extension when picked', () => {
        const directory = directoryOf([
            { extension: '3', displayName: 'Sam Lee' },
            { extension: '2', displayName: 'Jane Doe' },
        ]);

        const entries = extensionDirectoryEntries(directory, { transferExtension: 'Ext {0}' });

        expect(entries.map(entry => entry.name)).toEqual(['Jane Doe', 'Sam Lee']);
        expect(entries[0]).toMatchObject({ destination: '2', detail: 'Ext 2', isExtension: true });

        const shown = filterTransferTargets(entries, 'jane');
        expect(shown).toHaveLength(1);
        expect(resolveTransferTarget({ selected: shown[0] })).toMatchObject({ destination: '2', isExtension: true, label: 'Jane Doe' });
    });

    it('is nothing without a directory', () => {
        expect(extensionDirectoryEntries(null, {})).toEqual([]);
    });
});
