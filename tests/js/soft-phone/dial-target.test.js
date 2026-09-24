import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/dial-target.js';

const { resolveDialTarget, shouldOfferDial, isSameNumber } = globalThis.CrestAppsSoftPhone;

const ownNumber = '+15550100200';

// Bug: an agent on hold pressed the green button where Hold had been, and the phone dialed the tenant's own number.
// While a call is held the keypad is open for adding a call, and the number field shows the held call's number as a
// display; for a call the platform bridged to the agent that display was the tenant's own caller id. The dial button
// took whatever the field held, so it called the tenant from the tenant, which came back in as a new inbound call.
describe('resolveDialTarget', () => {
    it('refuses to dial the number the field is only showing for the current call', () => {
        expect(resolveDialTarget({ number: ownNumber, isCallDisplay: true, liveCall: true, ownNumbers: [ownNumber] }))
            .toEqual({ number: '', refused: 'call-display' });
        expect(resolveDialTarget({ number: '+15550100300', isCallDisplay: true, liveCall: true, ownNumbers: [] }))
            .toEqual({ number: '', refused: 'call-display' });
    });

    it('refuses to add a call to the tenant own outbound caller id, however it is written', () => {
        expect(resolveDialTarget({ number: '+1 (555) 010-0200', isCallDisplay: false, liveCall: true, ownNumbers: [ownNumber] }))
            .toEqual({ number: '', refused: 'own-number' });
        expect(resolveDialTarget({ number: ownNumber, isCallDisplay: false, liveCall: true, ownNumbers: [ownNumber] }))
            .toEqual({ number: '', refused: 'own-number' });
    });

    it('dials a number the agent typed while a call is held', () => {
        expect(resolveDialTarget({ number: '+15550100300', isCallDisplay: false, liveCall: true, ownNumbers: [ownNumber] }))
            .toEqual({ number: '+15550100300', refused: '' });
    });

    it('leaves an ordinary dial with no live call alone', () => {
        expect(resolveDialTarget({ number: ownNumber, isCallDisplay: false, liveCall: false, ownNumbers: [ownNumber] }))
            .toEqual({ number: ownNumber, refused: '' });
    });

    it('has nothing to dial without a number', () => {
        expect(resolveDialTarget({ number: '', isCallDisplay: false, liveCall: false, ownNumbers: [] }))
            .toEqual({ number: '', refused: '' });
    });
});

describe('isSameNumber', () => {
    it('compares the digits, not the formatting', () => {
        expect(isSameNumber('+1 (555) 010-0200', '15550100200')).toBe(true);
        expect(isSameNumber('+15550100200', '+15550100201')).toBe(false);
        expect(isSameNumber('', '')).toBe(false);
    });
});

// The same bug from the other side: the dial button sits first in the row, so on hold it appeared exactly where the
// Hold button had been a moment before, over a number the agent never entered.
describe('shouldOfferDial', () => {
    it('offers the dial button with no call', () => {
        expect(shouldOfferDial({ callActive: false, stateName: 'Idle', numberIsCallDisplay: false })).toBe(true);
    });

    it('hides it while a held call shows its own number in the field', () => {
        expect(shouldOfferDial({ callActive: true, stateName: 'OnHold', numberIsCallDisplay: true })).toBe(false);
    });

    it('offers it on hold once the agent has typed a number to add', () => {
        expect(shouldOfferDial({ callActive: true, stateName: 'OnHold', numberIsCallDisplay: false })).toBe(true);
    });

    it('never offers it on a call that is not held', () => {
        expect(shouldOfferDial({ callActive: true, stateName: 'Connected', numberIsCallDisplay: false })).toBe(false);
        expect(shouldOfferDial({ callActive: true, stateName: 'Ringing', numberIsCallDisplay: false })).toBe(false);
    });
});
