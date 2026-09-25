/*
 * Who an extension rings, by name.
 *
 * An extension on its own -- "2" -- tells the agent nothing about who they are about to call or transfer to. The server
 * names the person behind each extension as the site names its users (their display name, else their username), and
 * the phone reads that list once and keeps it, so a name can be shown beside an extension wherever one appears: the
 * transfer panel's "Transfer to extension 2 · Jane Doe", the keypad while an extension is typed, the call on screen
 * ("Jane Doe · ext 2") and the Recent list. Nothing is looked up per keystroke.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long the list is trusted before the phone reads it again, when something asks for a name.
    var EXTENSION_DIRECTORY_MAX_AGE_MS = 5 * 60 * 1000;

    function text(value) {
        return value == null ? '' : String(value).trim();
    }

    function label(strings, key, fallback) {
        return strings && typeof strings[key] === 'string' && strings[key] ? strings[key] : fallback;
    }

    function format(template, values) {
        return String(template).replace(/\{(\d+)\}/g, function (match, index) {
            var value = values[Number(index)];

            return value === undefined ? match : String(value);
        });
    }

    // The phone's copy of the server's extensions: { names: { number: name }, loadedAt }.
    function createExtensionDirectory() {
        return { names: {}, loadedAt: 0 };
    }

    // Replaces the copy with the entries the server sent ({ extension, displayName }).
    function storeExtensionEntries(directory, entries, now) {
        if (!directory) {
            return;
        }

        var names = {};

        (entries || []).forEach(function (entry) {
            var number = text(entry && entry.extension);
            var name = text(entry && entry.displayName);

            if (number && name && name !== number) {
                names[number] = name;
            }
        });

        directory.names = names;
        directory.loadedAt = now || Date.now();
    }

    // Whether the copy should be read again: never read, or older than the maximum age.
    function shouldReloadExtensions(directory, now) {
        return !directory || !directory.loadedAt || (now - directory.loadedAt) >= EXTENSION_DIRECTORY_MAX_AGE_MS;
    }

    // The name of whoever the extension rings, or '' when the phone does not know it.
    function extensionName(directory, number) {
        var key = text(number);

        return directory && directory.names && key && Object.prototype.hasOwnProperty.call(directory.names, key)
            ? directory.names[key]
            : '';
    }

    // Whether a value a call or history entry carries is a name worth showing: not empty, not the extension itself, not a
    // SIP address and not a number.
    function isPersonName(value, number) {
        var candidate = text(value);

        return !!candidate && candidate !== text(number) && !/^sips?:/i.test(candidate) && !/^[+\d\s().\-]+$/.test(candidate);
    }

    // "Jane Doe · ext 2", or the bare extension when nobody is known to be behind it.
    //   name   - the name the directory gives, or '' (see extensionName).
    //   number - the extension.
    //   fallbackName - a name the call itself carries (the provider's display name for it), used when the directory
    //                  has none.
    function describeExtension(strings, name, number, fallbackName) {
        var extension = text(number);
        var person = text(name) || (isPersonName(fallbackName, extension) ? text(fallbackName) : '');

        if (!extension) {
            return person;
        }

        return person
            ? format(label(strings, 'extensionWithName', '{0} · ext {1}'), [person, extension])
            : extension;
    }

    // The transfer panel's offer for a typed extension: "Transfer to extension 2 · Jane Doe", or without the name when
    // the phone does not know who it rings.
    function transferToExtensionLabel(strings, number, name) {
        var person = text(name);

        return person
            ? format(label(strings, 'transferToExtensionNamed', 'Transfer to extension {0} · {1}'), [number, person])
            : format(label(strings, 'transferToExtensionNumber', 'Transfer to extension {0}'), [number]);
    }

    // The extension a call was placed to, as the server stamped it on the call.
    function callExtensionNumber(call) {
        var metadata = call && call.metadata;

        return metadata ? text(metadata.extensionNumber) : '';
    }

    // The transfer panel's rows for the phone system's extensions, when the provider has no directory of its own: each
    // is sent as an extension, never dialed as a phone number.
    function extensionDirectoryEntries(directory, strings) {
        var names = (directory && directory.names) || {};

        return Object.keys(names).sort(function (left, right) {
            return names[left].localeCompare(names[right]) || left.localeCompare(right);
        }).map(function (number) {
            return {
                id: 'extension:' + number,
                name: names[number],
                destination: number,
                detail: format(label(strings, 'transferExtension', 'Ext {0}'), [number]),
                extension: number,
                isExtension: true,
                kind: 'agent',
                group: label(strings, 'transferGroupExtensions', 'Extensions')
            };
        });
    }

    softPhone.EXTENSION_DIRECTORY_MAX_AGE_MS = EXTENSION_DIRECTORY_MAX_AGE_MS;
    softPhone.createExtensionDirectory = createExtensionDirectory;
    softPhone.storeExtensionEntries = storeExtensionEntries;
    softPhone.shouldReloadExtensions = shouldReloadExtensions;
    softPhone.extensionName = extensionName;
    softPhone.describeExtension = describeExtension;
    softPhone.transferToExtensionLabel = transferToExtensionLabel;
    softPhone.callExtensionNumber = callExtensionNumber;
    softPhone.extensionDirectoryEntries = extensionDirectoryEntries;
}(typeof globalThis !== 'undefined' ? globalThis : window));
