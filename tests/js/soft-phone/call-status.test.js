import { afterEach, describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-status.js';

const softPhone = globalThis.CrestAppsSoftPhone;

afterEach(() => {
    delete globalThis.CrestAppsTelephonyShared;
});

// The header said "In call" for the whole call and nothing more. An agent testing audio quality, or one asked
// how long they were on with a customer, had no clock but their own.
describe('formatCallStatus', () => {
    it('appends the elapsed time to the state', () => {
        expect(softPhone.formatCallStatus('In call', 83)).toBe('In call · 1:23');
    });

    it('rolls into hours past sixty minutes', () => {
        expect(softPhone.formatCallStatus('In call', 3725)).toBe('In call · 1:02:05');
    });

    it('leaves the state alone while no connected moment is known -- a ringing call is not "0:00"', () => {
        expect(softPhone.formatCallStatus('Ringing', null)).toBe('Ringing');
        expect(softPhone.formatCallStatus('Ringing', undefined)).toBe('Ringing');
        expect(softPhone.formatCallStatus('Ringing', NaN)).toBe('Ringing');
    });

    it('treats a negative elapsed time (clock skew) as unknown rather than showing a negative timer', () => {
        expect(softPhone.formatCallStatus('In call', -4)).toBe('In call');
    });

    it('prefers the shared call timer when its bundle is loaded, so every surface agrees', () => {
        globalThis.CrestAppsTelephonyShared = { formatDuration: () => 'SHARED' };

        expect(softPhone.formatCallStatus('In call', 5)).toBe('In call · SHARED');
    });
});

describe('connectedAtFor', () => {
    it('records the first moment a call is seen connected', () => {
        expect(softPhone.connectedAtFor(true, null, 1000)).toBe(1000);
    });

    it('keeps the original moment across later refreshes, so the clock never restarts mid-call', () => {
        expect(softPhone.connectedAtFor(true, 1000, 9000)).toBe(1000);
    });

    it('has no connected moment for a call that is not connected', () => {
        expect(softPhone.connectedAtFor(false, 1000, 9000)).toBeNull();
        expect(softPhone.connectedAtFor(false, null, 9000)).toBeNull();
    });
});

describe('durationMeta', () => {
    it('formats a recorded total', () => {
        expect(softPhone.durationMeta(247, false)).toBe('4:07');
    });

    it('shows nothing for a call still in progress -- its length is not known yet', () => {
        expect(softPhone.durationMeta(30, true)).toBe('');
    });

    it('shows nothing for a call that never connected', () => {
        expect(softPhone.durationMeta(0, false)).toBe('');
        expect(softPhone.durationMeta(undefined, false)).toBe('');
        expect(softPhone.durationMeta(NaN, false)).toBe('');
    });
});
