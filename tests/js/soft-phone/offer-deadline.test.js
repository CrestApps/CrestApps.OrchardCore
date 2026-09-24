import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-deadline.js';

const {
    OFFER_RING_STOP_GRACE_MS,
    computeServerOffsetMs,
    computeOfferRingStopDelayMs,
    offerRingStopDelayMs,
} = globalThis.CrestAppsSoftPhone;

const serverNow = Date.parse('2026-09-24T21:12:20.000Z');
const deadline = '2026-09-24T21:12:50.000Z';
const serverTime = '2026-09-24T21:12:20.000Z';

// Regression: the soft phone stopped ringing on its own thirty seconds in, while the server only moved the caller to
// voicemail forty seconds later; the agent saw a silent phone and then, much later, their status change. The server
// owns the deadline, so the phone stops ringing when the server says the offer is gone, and its own fallback waits
// until after the server's deadline -- measured on the server's clock -- never before it.
describe('computeOfferRingStopDelayMs', () => {
    it('stops a grace period after the server deadline when the clocks agree', () => {
        expect(computeOfferRingStopDelayMs(deadline, 0, serverNow)).toBe(30000 + OFFER_RING_STOP_GRACE_MS);
    });

    it('never stops before the server deadline when this device clock runs ahead', () => {
        // The device reads 21:12:30 while the server reads 21:12:20.
        const offset = 10000;
        const deviceNow = serverNow + offset;
        const delay = computeOfferRingStopDelayMs(deadline, offset, deviceNow);

        expect(deviceNow + delay - offset).toBeGreaterThanOrEqual(Date.parse(deadline));
        expect(delay).toBe(30000 + OFFER_RING_STOP_GRACE_MS);
    });

    it('does not ring on long past the server deadline when this device clock runs behind', () => {
        const offset = -10000;
        const deviceNow = serverNow + offset;

        expect(computeOfferRingStopDelayMs(deadline, offset, deviceNow)).toBe(30000 + OFFER_RING_STOP_GRACE_MS);
    });

    it('stops at once for an offer seen after its deadline and grace', () => {
        expect(computeOfferRingStopDelayMs(deadline, 0, Date.parse(deadline) + OFFER_RING_STOP_GRACE_MS + 1)).toBe(0);
    });

    it('leaves the ring to the server when the offer has no deadline', () => {
        expect(computeOfferRingStopDelayMs(undefined, 0, serverNow)).toBeNull();
        expect(computeOfferRingStopDelayMs('not a date', 0, serverNow)).toBeNull();
    });

    it('honours an explicit grace', () => {
        expect(computeOfferRingStopDelayMs(deadline, 0, serverNow, 0)).toBe(30000);
    });
});

describe('computeServerOffsetMs', () => {
    it('is how far this device runs ahead of the server', () => {
        expect(computeServerOffsetMs(serverTime, serverNow + 4000)).toBe(4000);
        expect(computeServerOffsetMs(serverTime, serverNow - 4000)).toBe(-4000);
    });

    it('is zero when the message carries no server time', () => {
        expect(computeServerOffsetMs(undefined, serverNow)).toBe(0);
    });
});

describe('offerRingStopDelayMs', () => {
    it('measures the clock offset once, when the offer is first seen', () => {
        // Arrives on a device five seconds ahead of the server.
        const properties = { expiresUtc: deadline, serverTimeUtc: serverTime };
        const arrivedAt = serverNow + 5000;
        const first = offerRingStopDelayMs(properties, arrivedAt);

        // Re-rendered ten seconds later: the time that passed is not clock drift.
        const later = offerRingStopDelayMs(properties, arrivedAt + 10000);

        expect(first).toBe(30000 + OFFER_RING_STOP_GRACE_MS);
        expect(later).toBe(20000 + OFFER_RING_STOP_GRACE_MS);
        expect(Object.keys(properties)).toEqual(['expiresUtc', 'serverTimeUtc']);
    });

    it('is null with no offer', () => {
        expect(offerRingStopDelayMs(null, serverNow)).toBeNull();
    });
});

describe('the soft phone', () => {
    const source = readFileSync('src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js', 'utf8');

    it('schedules its own ring stop from the server deadline helper', () => {
        expect(source).toContain('offerRingStopDelayMs(');
    });

    it('keeps no ring timeout of its own that could fire before the server deadline', () => {
        expect(source).not.toMatch(/Date\.parse\(incomingContext\.properties\.expiresUtc\)/);
    });
});
