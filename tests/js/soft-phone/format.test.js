import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/format.js';

const { normalizeDialNumber, formatNanpNumber, formatInternationalNumber, formatPhoneNumber } = globalThis.CrestAppsSoftPhone;

// Every number an agent sees in the soft phone goes through here: call history, the active-call row, the
// keypad. These are characterization tests written against the behaviour as it shipped, so the split that
// moved this code out of the 6,251-line script cannot quietly change what an agent reads off the screen.
describe('normalizeDialNumber', () => {
    it('keeps the digits and the international prefix, and nothing else', () => {
        expect(normalizeDialNumber(' (555) 123-4567 ')).toBe('5551234567');
        expect(normalizeDialNumber('+1 (555) 123-4567')).toBe('+15551234567');
    });

    it('treats nothing as an empty number rather than as the string "null"', () => {
        expect(normalizeDialNumber(null)).toBe('');
        expect(normalizeDialNumber(undefined)).toBe('');
    });
});

describe('formatNanpNumber', () => {
    it('formats a full national number', () => {
        expect(formatNanpNumber('5551234567', false)).toBe('(555) 123-4567');
    });

    it('formats a full international number', () => {
        expect(formatNanpNumber('15551234567', true)).toBe('+1 (555) 123-4567');
    });

    it('formats a partial number as it is typed', () => {
        // The keypad reformats on every keystroke, so a half-typed number has to stay readable.
        expect(formatNanpNumber('5', false)).toBe('(5');
        expect(formatNanpNumber('555', false)).toBe('(555)');
        expect(formatNanpNumber('555123', false)).toBe('(555) 123');
    });
});

describe('formatInternationalNumber', () => {
    it('groups the national part after the country code', () => {
        expect(formatInternationalNumber('442071838750')).toBe('+44 207 183 8750');
    });

    it('returns just the prefix when there are no digits yet', () => {
        expect(formatInternationalNumber('')).toBe('+');
    });
});

describe('formatPhoneNumber', () => {
    it('formats a North American number', () => {
        expect(formatPhoneNumber('5551234567')).toBe('(555) 123-4567');
    });

    it('leaves a short number alone rather than bracketing a fragment', () => {
        expect(formatPhoneNumber('12345')).toBe('12345');
    });

    it('formats a number outside the North American plan', () => {
        expect(formatPhoneNumber('+442071838750')).toBe('+44 207 183 8750');
    });

    it('formats an extension-length string without inventing punctuation', () => {
        expect(formatPhoneNumber('101')).toBe('101');
    });
});
