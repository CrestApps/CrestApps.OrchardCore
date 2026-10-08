/*
 * Phone-number formatting for display. Deliberately independent of intl-tel-input, which only enhances the
 * editable keypad input: the display formatter has to work on screens where the phone-field library is not
 * loaded at all.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function normalizeDialNumber(value) {
        var input = String(value || '').trim();
        var hasInternationalPrefix = input.charAt(0) === '+';
        var digits = input.replace(/\D/g, '');

        return (hasInternationalPrefix ? '+' : '') + digits;
    }

    function formatNanpNumber(digits, international) {
        var national = international ? digits.substring(1) : digits;
        var formatted = '';

        if (international) {
            formatted = '+1';
        }

        if (national.length > 0) {
            formatted += (international ? ' ' : '') + '(' + national.substring(0, 3);
        }

        if (national.length >= 3) {
            formatted += ')';
        }

        if (national.length > 3) {
            formatted += ' ' + national.substring(3, 6);
        }

        if (national.length > 6) {
            formatted += '-' + national.substring(6, 10);
        }

        return formatted;
    }

    function formatInternationalNumber(digits) {
        if (!digits) {
            return '+';
        }

        var countryCodeLength = digits.length > 10 ? Math.min(3, digits.length - 10) : Math.min(2, digits.length);
        var countryCode = digits.substring(0, countryCodeLength);
        var national = digits.substring(countryCodeLength);
        var groups = [];

        while (national.length > 4) {
            groups.push(national.substring(0, 3));
            national = national.substring(3);
        }

        if (national) {
            groups.push(national);
        }

        return '+' + countryCode + (groups.length ? ' ' + groups.join(' ') : '');
    }

    // Formats a number for display only (call history and active-call rows).
    function formatPhoneNumber(value) {
        var normalized = normalizeDialNumber(value);
        var international = normalized.charAt(0) === '+';
        var digits = normalized.replace(/\D/g, '');

        if (!international && digits.length < 7) {
            return digits;
        }

        if ((!international && digits.length <= 10) ||
            (international && digits.charAt(0) === '1' && digits.length <= 11)) {
            return formatNanpNumber(digits, international);
        }

        return international ? formatInternationalNumber(digits) : digits;
    }

    softPhone.normalizeDialNumber = normalizeDialNumber;
    softPhone.formatNanpNumber = formatNanpNumber;
    softPhone.formatInternationalNumber = formatInternationalNumber;
    softPhone.formatPhoneNumber = formatPhoneNumber;
}(typeof globalThis !== 'undefined' ? globalThis : window));
