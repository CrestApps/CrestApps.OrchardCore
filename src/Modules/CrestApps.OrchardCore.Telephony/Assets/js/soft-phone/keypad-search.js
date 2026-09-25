/*
 * Finding a colleague's extension by name on the keypad.
 *
 * The keypad's extension field took digits only: it was enhanced with the country-flag input, which drops every key
 * that is not a digit, so a colleague could only be called by knowing their extension. In extension mode the field is
 * now a plain search box, as the transfer panel's is: a name lists the extensions of the people it matches, picking one
 * dials it, Enter dials the one person a name narrows the list to, and digits still dial the extension they are. The
 * agent's own extensions, which only ring the phone doing the dialing, are never listed and are refused when typed.
 *
 * The search is the transfer panel's own (soft-phone/transfer-target.js) over the phone system's extensions
 * (soft-phone/extension-names.js).
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How many matches the keypad lists; the rest are found by typing more of the name.
    var MAX_KEYPAD_MATCHES = 6;

    function text(value) {
        return value == null ? '' : String(value).trim();
    }

    // The extensions matching what was typed, by name or digits, the agent's own left out: [{ extension, name, detail }].
    function keypadExtensionMatches(directory, query, strings) {
        var typed = text(query);

        if (!typed) {
            return [];
        }

        var entries = softPhone.extensionDirectoryEntries(directory, strings || {});

        return softPhone.filterTransferTargets(entries, typed).map(function (entry) {
            return { extension: String(entry.destination), name: entry.name, detail: entry.detail };
        });
    }

    // What dialing from the extension field calls: { extension, refused }, refused being 'empty', 'own-extension',
    // 'several-match' or 'no-match'.
    function resolveKeypadExtension(directory, query, strings) {
        var typed = text(query);

        if (!typed) {
            return { extension: '', refused: 'empty' };
        }

        var digits = softPhone.readExtension(typed);

        if (digits) {
            return softPhone.isOwnExtension(directory, digits)
                ? { extension: '', refused: 'own-extension' }
                : { extension: digits, refused: '' };
        }

        var pick = softPhone.pickTypedMatch(softPhone.extensionDirectoryEntries(directory, strings || {}), typed);

        return pick.entry
            ? { extension: String(pick.entry.destination), refused: '' }
            : { extension: '', refused: pick.refused || 'no-match' };
    }

    // The list under the field: one button per match, each dialing its extension.
    function buildKeypadExtensionResultsHtml(matches, strings, escapeHtml) {
        if (!matches || !matches.length) {
            return '';
        }

        strings = strings || {};

        return '<div class="telephony-soft-phone__keypad-results-list" role="list" aria-label="' +
            escapeHtml(strings.keypadExtensionResults || 'Matching extensions') + '">' +
            matches.slice(0, MAX_KEYPAD_MATCHES).map(function (match) {
                return '<button type="button" role="listitem" class="telephony-soft-phone__keypad-result" data-telephony-keypad-extension="' +
                    escapeHtml(match.extension) + '">' +
                    '<span class="telephony-soft-phone__keypad-result-name">' + escapeHtml(match.name) + '</span>' +
                    '<span class="telephony-soft-phone__keypad-result-extension">' + escapeHtml(match.detail) + '</span>' +
                    '</button>';
            }).join('') +
            '</div>';
    }

    softPhone.keypadExtensionMatches = keypadExtensionMatches;
    softPhone.resolveKeypadExtension = resolveKeypadExtension;
    softPhone.buildKeypadExtensionResultsHtml = buildKeypadExtensionResultsHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
