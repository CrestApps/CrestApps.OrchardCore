import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/format.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-target.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/phone-input.js';

const { readPhoneInput, readTransferNumber, enhancePhoneInput } = globalThis.CrestAppsSoftPhone;

// A stand-in for an intl-tel-input instance with the United States selected.
function telInput({ valid, e164, dialCode = '1' }) {
    return {
        isValidNumber: () => valid,
        getNumber: () => e164,
        getSelectedCountryData: () => ({ dialCode })
    };
}

describe('readPhoneInput', () => {
    it('dials a number entered in international form as it is', () => {
        expect(readPhoneInput('+44 20 7946 0958', telInput({ valid: true, e164: '+442079460958' }))).toBe('+442079460958');
        expect(readPhoneInput('+44 20 7946 0958', null)).toBe('+442079460958');
    });

    it('takes the country-flag input\'s international number for a valid national number', () => {
        expect(readPhoneInput('(702) 555-0199', telInput({ valid: true, e164: '+17025550199' }))).toBe('+17025550199');
    });

    it('never dials a number other than the one on screen when the input has fallen out of step', () => {
        expect(readPhoneInput('(702) 555-0199', telInput({ valid: true, e164: '+17025550100' }))).toBe('+17025550199');
    });

    it('prefixes the selected country to a number the input does not consider valid', () => {
        expect(readPhoneInput('702499', telInput({ valid: false, e164: '' }))).toBe('+1702499');
        expect(readPhoneInput('17025550199', telInput({ valid: false, e164: '' }))).toBe('+17025550199');
    });

    it('dials the digits on screen when there is no country-flag input', () => {
        expect(readPhoneInput('(702) 555-0199', null)).toBe('7025550199');
    });
});

describe('readTransferNumber', () => {
    it('reads a complete number from the country-flag input', () => {
        expect(readTransferNumber('(702) 555-0199', telInput({ valid: true, e164: '+17025550199' })))
            .toEqual({ value: '+17025550199', valid: true });
    });

    // The keypad refuses an incomplete number with intl-tel-input's own check; a transfer uses the same one.
    it('reports an incomplete number as not valid, however short', () => {
        expect(readTransferNumber('2', telInput({ valid: false, e164: '' }))).toEqual({ value: '+12', valid: false });
    });

    it('reads a typed number itself when there is no country-flag input', () => {
        expect(readTransferNumber('(702) 555-0199', null)).toEqual({ value: '+17025550199', valid: true });
        expect(readTransferNumber('2', null)).toEqual({ value: '', valid: false });
        expect(readTransferNumber('', null)).toEqual({ value: '', valid: false });
    });
});

describe('enhancePhoneInput', () => {
    it('attaches intl-tel-input with the keypad\'s options and selected country', () => {
        const instance = {};
        const intlTelInput = vi.fn(() => instance);
        const input = {};

        expect(enhancePhoneInput(input, { intlTelInput, initialCountry: 'ca', containerClass: 'x-iti', dropdownParent: 'body' })).toBe(instance);
        expect(intlTelInput).toHaveBeenCalledWith(input, { containerClass: 'x-iti', dropdownParent: 'body', initialCountry: 'ca' });
    });

    // Bug: the transfer panel's field refused every letter, so a colleague could not be searched by name. intl-tel-input
    // is strict by default and drops any key that is not a digit; the panel's field asks for it not to be.
    it('turns off intl-tel-input\'s strict keys when asked, so a name can be typed into the field', () => {
        const intlTelInput = vi.fn(() => ({}));

        enhancePhoneInput({}, { intlTelInput, strictMode: false });
        expect(intlTelInput.mock.calls[0][1].strictMode).toBe(false);

        enhancePhoneInput({}, { intlTelInput });
        expect('strictMode' in intlTelInput.mock.calls[1][1]).toBe(false);
    });

    it('leaves the field plain when intl-tel-input is not loaded', () => {
        expect(enhancePhoneInput({}, { intlTelInput: undefined })).toBeNull();
        expect(enhancePhoneInput(null, { intlTelInput: vi.fn() })).toBeNull();
    });
});
