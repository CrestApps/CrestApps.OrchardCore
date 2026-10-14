// @ts-check
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
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */
import { extensionDirectoryEntries, isOwnExtension } from './extension-names.js';
import { filterTransferTargets, pickTypedMatch, readExtension } from './transfer-target.js';

// How many matches the keypad lists; the rest are found by typing more of the name.
var MAX_KEYPAD_MATCHES = 6;

function text(value) {
    return value == null ? '' : String(value).trim();
}

// The extensions matching what was typed, by name or digits, the agent's own left out: [{ extension, name, detail }].
export function keypadExtensionMatches(directory, query, strings) {
    var typed = text(query);

    if (!typed) {
        return [];
    }

    var entries = extensionDirectoryEntries(directory, strings || {});

    return filterTransferTargets(entries, typed).map(function (entry) {
        return { extension: String(entry.destination), name: entry.name, detail: entry.detail };
    });
}

// What dialing from the extension field calls: { extension, refused }, refused being 'empty', 'own-extension',
// 'several-match' or 'no-match'.
export function resolveKeypadExtension(directory, query, strings) {
    var typed = text(query);

    if (!typed) {
        return { extension: '', refused: 'empty' };
    }

    var digits = readExtension(typed);

    if (digits) {
        return isOwnExtension(directory, digits)
            ? { extension: '', refused: 'own-extension' }
            : { extension: digits, refused: '' };
    }

    var pick = pickTypedMatch(extensionDirectoryEntries(directory, strings || {}), typed);

    return pick.entry
        ? { extension: String(pick.entry.destination), refused: '' }
        : { extension: '', refused: pick.refused || 'no-match' };
}

// The list under the field: one button per match, each dialing its extension.
export function buildKeypadExtensionResultsHtml(matches, strings, escapeHtml) {
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
