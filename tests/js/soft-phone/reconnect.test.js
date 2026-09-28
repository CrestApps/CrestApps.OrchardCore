import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/reconnect.js';

const { reconnectDelayMs, RECONNECT_DELAYS_MS } = globalThis.CrestAppsSoftPhone;

// A soft phone that stops trying to reconnect is a phone that stops ringing, with nothing on screen saying so.
// The same schedule is used by the automatic reconnect and by the manual restart loop, so they cannot drift.
describe('reconnectDelayMs', () => {
    it('retries the first attempt immediately', () => {
        expect(reconnectDelayMs(0)).toBe(0);
    });

    it('backs off over the following attempts', () => {
        expect(reconnectDelayMs(1)).toBe(2000);
        expect(reconnectDelayMs(2)).toBe(5000);
        expect(reconnectDelayMs(3)).toBe(10000);
        expect(reconnectDelayMs(4)).toBe(20000);
    });

    it('caps the delay instead of running off the end of the schedule', () => {
        // Reading past the end would give undefined, and a timer scheduled for undefined fires immediately —
        // turning a backoff into a tight retry loop against a hub that is already struggling.
        expect(reconnectDelayMs(5)).toBe(30000);
        expect(reconnectDelayMs(50)).toBe(30000);
    });

    it('never gives up: every attempt has a delay', () => {
        for (let attempt = 0; attempt < 100; attempt++) {
            expect(typeof reconnectDelayMs(attempt)).toBe('number');
        }
    });

    it('treats a negative attempt count as the first attempt', () => {
        expect(reconnectDelayMs(-1)).toBe(0);
    });

    it('exposes the schedule the two reconnect paths share', () => {
        expect(RECONNECT_DELAYS_MS).toEqual([0, 2000, 5000, 10000, 20000, 30000]);
    });
});
