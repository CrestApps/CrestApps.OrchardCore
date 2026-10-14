import { describe, expect, it } from 'vitest';

import {
    connectedAtFor,
    durationMeta,
    formatCallStatus,
} from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-status.js';
import { formatDuration } from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/shared/call-timer.js';

// The header said "In call" for the whole call and nothing more. An agent testing audio quality, or one asked
// how long they were on with a customer, had no clock but their own.
describe('formatCallStatus', () => {
    it('appends the elapsed time to the state', () => {
        expect(formatCallStatus('In call', 83)).toBe('In call · 1:23');
    });

    it('rolls into hours past sixty minutes', () => {
        expect(formatCallStatus('In call', 3725)).toBe('In call · 1:02:05');
    });

    it('leaves the state alone while no connected moment is known -- a ringing call is not "0:00"', () => {
        expect(formatCallStatus('Ringing', null)).toBe('Ringing');
        expect(formatCallStatus('Ringing', undefined)).toBe('Ringing');
        expect(formatCallStatus('Ringing', NaN)).toBe('Ringing');
    });

    it('treats a negative elapsed time (clock skew) as unknown rather than showing a negative timer', () => {
        expect(formatCallStatus('In call', -4)).toBe('In call');
    });

    it('formats the elapsed time with the shared call timer, so every surface agrees', () => {
        [0, 5, 83, 3725, -3, Number.NaN].forEach(seconds => {
            if (seconds >= 0) {
                expect(formatCallStatus('In call', seconds)).toBe('In call · ' + formatDuration(seconds));
            }
        });
    });
});

describe('connectedAtFor', () => {
    it('records the first moment a call is seen connected', () => {
        expect(connectedAtFor(true, null, 1000)).toBe(1000);
    });

    it('keeps the original moment across later refreshes, so the clock never restarts mid-call', () => {
        expect(connectedAtFor(true, 1000, 9000)).toBe(1000);
    });

    it('has no connected moment for a call that is not connected', () => {
        expect(connectedAtFor(false, 1000, 9000)).toBeNull();
        expect(connectedAtFor(false, null, 9000)).toBeNull();
    });
});

describe('durationMeta', () => {
    it('formats a recorded total', () => {
        expect(durationMeta(247, false)).toBe('4:07');
    });

    it('shows nothing for a call still in progress -- its length is not known yet', () => {
        expect(durationMeta(30, true)).toBe('');
    });

    it('shows nothing for a call that never connected', () => {
        expect(durationMeta(0, false)).toBe('');
        expect(durationMeta(undefined, false)).toBe('');
        expect(durationMeta(NaN, false)).toBe('');
    });
});
