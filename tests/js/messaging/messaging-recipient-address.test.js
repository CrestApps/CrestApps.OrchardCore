import { describe, expect, it } from 'vitest';

import {
    acceptsTypedAddress,
    isE164,
    looksLikePhoneNumber,
    shouldShowPhoneHint,
} from '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-recipient-address.js';

// A number typed for someone outside the contacts is sent as written, so the composer only offers it in E.164.
describe('isE164', () => {
    it.each(['+12015550123', '+442071838750'])('accepts %s', (number) => {
        expect(isE164(number)).toBe(true);
    });

    it.each([
        '2015550123',
        '201-555-0123',
        '+1 201 555 0123',
        '123-4567',
        '+1201555O123',
        '+12345',
        '+1201555012345678',
        '+0201555012',
        'Ann',
        '',
        null,
    ])('refuses %s', (number) => {
        expect(isE164(number)).toBe(false);
    });
});

describe('looksLikePhoneNumber', () => {
    it.each(['2015550123', '201-555-0123', '(201) 555-0123', '+1 201'])('reads %s as a number', (text) => {
        expect(looksLikePhoneNumber(text)).toBe(true);
    });

    it.each(['Ann', 'Ann 2', 'ann@example.com', '', '  '])('does not read %s as a number', (text) => {
        expect(looksLikePhoneNumber(text)).toBe(false);
    });
});

describe('acceptsTypedAddress', () => {
    const typed = (address) => ({ id: null, address });

    it('offers an E.164 number on a phone channel when it is what was typed', () => {
        expect(acceptsTypedAddress(typed('+12015550123'), '+12015550123 ', true)).toBe(true);
    });

    it('never offers a number the server reshaped from what was typed', () => {
        expect(acceptsTypedAddress(typed('+12015550123'), '2015550123', true)).toBe(false);
        expect(acceptsTypedAddress(typed('+12015550123'), '201-555-0123', true)).toBe(false);
    });

    it('never offers a typed number that is not E.164 on a phone channel', () => {
        expect(acceptsTypedAddress(typed('2015550123'), '2015550123', true)).toBe(false);
    });

    it('leaves other channels to their own rule, ignoring case', () => {
        expect(acceptsTypedAddress(typed('ann@example.com'), 'Ann@Example.com', false)).toBe(true);
    });

    it('is only about typed addresses, never contacts', () => {
        expect(acceptsTypedAddress({ id: 'contact-1', address: '+12015550123' }, '+12015550123', true)).toBe(false);
    });
});

describe('shouldShowPhoneHint', () => {
    it('shows when a number was typed on a phone channel and none could be offered', () => {
        expect(shouldShowPhoneHint('201-555-0123', true, false)).toBe(true);
    });

    it('hides once the number is offered, for a name, or on another channel', () => {
        expect(shouldShowPhoneHint('+12015550123', true, true)).toBe(false);
        expect(shouldShowPhoneHint('Ann', true, false)).toBe(false);
        expect(shouldShowPhoneHint('2015550123', false, false)).toBe(false);
    });
});
