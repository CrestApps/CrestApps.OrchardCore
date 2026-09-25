/*
 * Reading a phone number from a field the country-flag input (intl-tel-input) enhances.
 *
 * The keypad and the transfer panel both take a phone number this way, and a transfer used to read it its own way: a
 * plain text box that accepted anything of two digits or more as a number, so an extension typed into it was checked
 * as a phone number it could never be, and a real number was sent without a country code. The two now share the
 * keypad's field -- the same flag, the same country, the same completeness check -- and these are the decisions that
 * field makes.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function normalize(value) {
        return typeof softPhone.normalizeDialNumber === 'function'
            ? softPhone.normalizeDialNumber(value)
            : String(value || '').trim();
    }

    // Enhances a number field with intl-tel-input, the way the keypad's field is. A country is always selected,
    // otherwise intl-tel-input cannot read a national number as an international one.
    //   input   - the field.
    //   options - { intlTelInput, initialCountry, containerClass, dropdownParent }.
    // Returns the intl-tel-input instance, or null when intl-tel-input is not loaded.
    function enhancePhoneInput(input, options) {
        options = options || {};

        if (!input || typeof options.intlTelInput !== 'function') {
            return null;
        }

        var settings = {
            containerClass: options.containerClass,
            dropdownParent: options.dropdownParent
        };

        if (options.initialCountry) {
            settings.initialCountry = options.initialCountry;
        }

        return options.intlTelInput(input, settings);
    }

    // The number a field holds, as it would be dialed: in international form whenever it can be.
    //   raw      - what the field shows.
    //   telInput - its intl-tel-input instance, if any.
    function readPhoneInput(raw, telInput) {
        var value = normalize(raw);

        // A number the user already entered in international form is dialed as-is.
        if (value.charAt(0) === '+') {
            return value;
        }

        var visibleDigits = value.replace(/\D/g, '');

        if (telInput && typeof telInput.getNumber === 'function') {
            // Only trust the intl-tel-input E.164 output for real, valid phone numbers. Short strings are not valid
            // numbers, so they are not turned into a bogus "+1101" style destination here.
            var isValid = typeof telInput.isValidNumber !== 'function' || telInput.isValidNumber();

            if (isValid) {
                var e164 = telInput.getNumber();

                // Guard against intl-tel-input desyncing (e.g. a keypad edit that did not update its internal state):
                // only accept its E.164 when its digits actually contain what the user sees, so the dialed number can
                // never differ from the visible number.
                if (e164 && e164.charAt(0) === '+' &&
                    (visibleDigits === '' || e164.replace(/\D/g, '').indexOf(visibleDigits) !== -1)) {
                    return e164;
                }
            }
        }

        // Fall back to the visible number, prefixed with the selected country's dial code so it stays routable. This
        // path guarantees the dialed number matches what is on screen.
        if (visibleDigits && telInput && typeof telInput.getSelectedCountryData === 'function') {
            var dialCode = (telInput.getSelectedCountryData() || {}).dialCode;

            if (dialCode) {
                return visibleDigits.indexOf(dialCode) === 0
                    ? '+' + visibleDigits
                    : '+' + dialCode + visibleDigits;
            }
        }

        return value;
    }

    // The number a transfer would be sent to: { value, valid }. With intl-tel-input the keypad's own completeness
    // check decides; without it the field is read as an international or ten-digit North American number.
    function readTransferNumber(raw, telInput) {
        if (telInput && typeof telInput.isValidNumber === 'function') {
            var value = readPhoneInput(raw, telInput);

            return { value: value, valid: !!telInput.isValidNumber() && value.charAt(0) === '+' };
        }

        var number = typeof softPhone.toInternationalNumber === 'function' ? softPhone.toInternationalNumber(raw) : '';

        return { value: number, valid: !!number };
    }

    softPhone.enhancePhoneInput = enhancePhoneInput;
    softPhone.readPhoneInput = readPhoneInput;
    softPhone.readTransferNumber = readTransferNumber;
}(typeof globalThis !== 'undefined' ? globalThis : window));
