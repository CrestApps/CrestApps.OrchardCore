/*
 * Who a call may be transferred to, and how.
 *
 * The transfer button used to open the browser's own "Transfer to number" prompt. Inside the desktop app's narrow
 * window that prompt was cut off, it named the site rather than the phone, and it took whatever was typed -- including
 * the tenant's own number, which only rings the tenant back. The soft phone now picks the target in its own panel: a
 * directory entry, or a number checked the way the keypad checks one. These are the decisions that panel makes.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Must match the Transfer and AttendedTransfer flags of CrestApps.OrchardCore.Telephony.Models.TelephonyCapabilities.
    var TRANSFER_CAPABILITY = 1 << 5;
    var ATTENDED_TRANSFER_CAPABILITY = 1 << 11;

    // Must match CrestApps.OrchardCore.Telephony.Models.TransferMode.
    var TRANSFER_MODE_VALUES = { blind: 0, warm: 1 };

    function digitsOf(value) {
        return value == null ? '' : String(value).replace(/\D+/g, '');
    }

    // Whether two numbers are the same line. A number typed without its country code is the same North American line as
    // the tenant's +1 number, so "7025550100" and "+17025550100" match.
    function isSameLine(left, right) {
        var a = digitsOf(left);
        var b = digitsOf(right);

        if (!a || !b) {
            return false;
        }

        if (a === b) {
            return true;
        }

        return (a.length === 11 && a.charAt(0) === '1' && a.substring(1) === b) ||
            (b.length === 11 && b.charAt(0) === '1' && b.substring(1) === a);
    }

    // The transfer modes the provider supports, blind first: ['blind'], ['warm'], ['blind', 'warm'] or [].
    function transferModes(capabilities) {
        var value = Number(capabilities) || 0;
        var modes = [];

        if ((value & TRANSFER_CAPABILITY) === TRANSFER_CAPABILITY) {
            modes.push('blind');
        }

        if ((value & ATTENDED_TRANSFER_CAPABILITY) === ATTENDED_TRANSFER_CAPABILITY) {
            modes.push('warm');
        }

        return modes;
    }

    // The TransferMode value the hub expects for a mode name; blind for anything it does not know.
    function transferModeValue(mode) {
        return Object.prototype.hasOwnProperty.call(TRANSFER_MODE_VALUES, mode) ? TRANSFER_MODE_VALUES[mode] : 0;
    }

    // Whether what was typed reads as a phone number or extension rather than a name or an address.
    function isNumberLike(value) {
        var text = value == null ? '' : String(value).trim();

        return !!text && /^[+\d\s().\-]+$/.test(text) && /\d/.test(text);
    }

    // The fields a transfer service's entries carry beyond a provider entry's, kept as they are.
    var SERVICE_ENTRY_FIELDS = ['kind', 'targetType', 'targetId', 'group', 'presence', 'status', 'disabled', 'isExtension'];

    // A directory entry as the panel shows it: { id, name, destination, detail }, plus what a transfer service's entry
    // says about what it is (see soft-phone/transfer-service.js).
    function normalizeDirectoryEntry(entry) {
        entry = entry || {};

        var destination = String(entry.destination || entry.extension || entry.phoneNumber || '');

        // A service entry has already said what to show under its name; a provider entry shows its number.
        var detail = entry.kind
            ? String(entry.detail == null ? '' : entry.detail)
            : String(entry.extension || entry.phoneNumber || entry.detail || destination);
        var normalized = {
            id: String(entry.id || destination),
            name: String(entry.displayName || entry.name || destination),
            destination: destination,
            detail: detail
        };

        SERVICE_ENTRY_FIELDS.forEach(function (field) {
            if (entry[field] !== undefined) {
                normalized[field] = entry[field];
            }
        });

        return normalized;
    }

    // The directory entries matching what the agent typed, by name, extension or number. Entries with nowhere to send
    // the call are dropped.
    function filterTransferTargets(entries, query) {
        var text = query == null ? '' : String(query).trim().toLowerCase();
        var digits = digitsOf(text);

        return (entries || []).map(normalizeDirectoryEntry).filter(function (entry) {
            if (!entry.destination) {
                return false;
            }

            if (!text) {
                return true;
            }

            if (entry.name.toLowerCase().indexOf(text) !== -1 || entry.detail.toLowerCase().indexOf(text) !== -1) {
                return true;
            }

            return !!digits && isNumberLike(text) &&
                (digitsOf(entry.destination).indexOf(digits) !== -1 || digitsOf(entry.detail).indexOf(digits) !== -1);
        });
    }

    // A typed number in international format, or '' when it cannot be read as one. A number without a country code
    // is read as North American only when it has exactly the ten digits one has; anything else must start with +.
    function toInternationalNumber(value) {
        var text = value == null ? '' : String(value).trim();
        var digits = digitsOf(text);

        if (text.charAt(0) === '+') {
            return digits.length >= 8 && digits.length <= 15 ? '+' + digits : '';
        }

        if (digits.length === 10) {
            return '+1' + digits;
        }

        if (digits.length === 11 && digits.charAt(0) === '1') {
            return '+' + digits;
        }

        return '';
    }

    // The digits of a typed extension, or '' when what was typed is not one. An extension is only digits, which may
    // be spaced or dashed the way people write them; a leading + makes it a phone number, not an extension.
    function readExtension(value) {
        var text = value == null ? '' : String(value).trim();
        var digits = digitsOf(text);

        return /^[\d\s-]+$/.test(text) && digits.length >= 1 && digits.length <= 15 ? digits : '';
    }

    // What to transfer the call to.
    //   query      - what the agent typed.
    //   dialMode   - 'number' (the default) or 'extension', as the panel's Number / Extension toggle is set.
    //   number     - in number mode, what the country-flag input read from the query: { value, valid }. Without one
    //                the query is read as an international or ten-digit North American number.
    //   selected   - the directory entry the agent picked, if any ({ destination, name }); it wins in either mode.
    //   ownNumbers - the tenant's own outbound caller ids.
    // Returns { destination, isExtension, label, refused }: where to send the call and whether it is an internal
    // extension, or '' with why it was refused ('empty' | 'invalid-number' | 'invalid-extension' | 'own-number').
    function resolveTransferTarget(options) {
        options = options || {};

        var selected = options.selected;
        var refuse = function (reason) {
            return { destination: '', isExtension: false, label: '', refused: reason };
        };

        if (selected && selected.destination) {
            // A picked extension is sent as one, so the provider rings the colleague rather than dialing the digits.
            return { destination: String(selected.destination), isExtension: !!selected.isExtension, label: String(selected.name || selected.destination), refused: '' };
        }

        var text = options.query == null ? '' : String(options.query).trim();

        if (!text) {
            return refuse('empty');
        }

        if (options.dialMode === 'extension') {
            var extension = readExtension(text);

            return extension
                ? { destination: extension, isExtension: true, label: text, refused: '' }
                : refuse('invalid-extension');
        }

        if (!isNumberLike(text)) {
            // A name nobody in the directory was picked for.
            return refuse('empty');
        }

        var reading = options.number || { value: toInternationalNumber(text), valid: !!toInternationalNumber(text) };
        var number = reading.value ? String(reading.value) : '';

        if ((options.ownNumbers || []).some(function (own) { return isSameLine(text, own) || isSameLine(number, own); })) {
            return refuse('own-number');
        }

        if (!reading.valid || !number) {
            return refuse('invalid-number');
        }

        return { destination: number, isExtension: false, label: text, refused: '' };
    }

    // Why the phone cannot transfer this call itself, or ''. A call dialed from this browser runs in the provider SDK
    // alone: the server holds no handle on it, so a transfer command for it can only fail. A transfer service that
    // carries the call's transfer (see soft-phone/transfer-service.js) is not bound by that.
    //   call           - the call to transfer.
    //   serviceApplies - whether a transfer service carries this call's transfer.
    function transferBlockedReason(options) {
        options = options || {};

        return options.call && options.call.browserOriginated && !options.serviceApplies ? 'browser-call' : '';
    }

    softPhone.TRANSFER_MODE_VALUES = TRANSFER_MODE_VALUES;
    softPhone.isSameLine = isSameLine;
    softPhone.transferModes = transferModes;
    softPhone.transferModeValue = transferModeValue;
    softPhone.isNumberLike = isNumberLike;
    softPhone.normalizeDirectoryEntry = normalizeDirectoryEntry;
    softPhone.filterTransferTargets = filterTransferTargets;
    softPhone.toInternationalNumber = toInternationalNumber;
    softPhone.readExtension = readExtension;
    softPhone.resolveTransferTarget = resolveTransferTarget;
    softPhone.transferBlockedReason = transferBlockedReason;
}(typeof globalThis !== 'undefined' ? globalThis : window));
