import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/shared/call-timer.js';

const { formatDuration } = globalThis.CrestAppsTelephonyShared;

// The call timer runs on the soft phone, the contact-center soft-phone tab, the agent bar and the workspace. It
// had two implementations that disagreed the moment a call passed an hour: one showed "1:15:03", the other
// "75:03". These pin the one that survived.
describe('formatDuration', () => {
    it('formats under a minute', () => {
        expect(formatDuration(0)).toBe('0:00');
        expect(formatDuration(9)).toBe('0:09');
    });

    it('formats minutes and seconds', () => {
        expect(formatDuration(75)).toBe('1:15');
        expect(formatDuration(599)).toBe('9:59');
    });

    it('shows hours once a call passes one, rather than counting minutes upward forever', () => {
        expect(formatDuration(3600)).toBe('1:00:00');
        expect(formatDuration(4503)).toBe('1:15:03');
    });

    it('treats a negative or nonsense elapsed time as zero', () => {
        // The elapsed time is computed from a server clock offset, so it can briefly come out negative. Showing
        // "-1:-3" on a live call is worse than showing nothing having happened yet.
        expect(formatDuration(-5)).toBe('0:00');
        expect(formatDuration(Number.NaN)).toBe('0:00');
        expect(formatDuration(Number.POSITIVE_INFINITY)).toBe('0:00');
    });

    it('drops the fractional part rather than rendering it', () => {
        expect(formatDuration(75.9)).toBe('1:15');
    });
});
