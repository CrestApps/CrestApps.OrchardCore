import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/playout-delay.js';

const softPhone = globalThis.CrestAppsSoftPhone;

const audioReceiver = (extra = {}) => ({ track: { kind: 'audio' }, playoutDelayHint: undefined, ...extra });
const videoReceiver = () => ({ track: { kind: 'video' }, playoutDelayHint: undefined });
const peer = (receivers) => ({ getReceivers: () => receivers });

describe('clampPlayoutDelay', () => {
    it('accepts the offered values', () => {
        expect(softPhone.clampPlayoutDelay(0.04)).toBe(0.04);
        expect(softPhone.clampPlayoutDelay('0.08')).toBe(0.08);
    });

    it('falls back to no hint for anything else, rather than inventing a buffer size', () => {
        expect(softPhone.clampPlayoutDelay(0.5)).toBe(-1);
        expect(softPhone.clampPlayoutDelay('soon')).toBe(-1);
        expect(softPhone.clampPlayoutDelay(undefined)).toBe(-1);
        expect(softPhone.clampPlayoutDelay(null)).toBe(-1);
    });

    it('does not offer zero -- a buffer of nothing conceals on the first late packet', () => {
        expect(softPhone.PLAYOUT_DELAY_OPTIONS).not.toContain(0);
        expect(Math.min(...softPhone.PLAYOUT_DELAY_OPTIONS.filter(v => v > 0))).toBeGreaterThan(0);
    });
});

describe('describePlayoutDelay', () => {
    it('names the requested hold in milliseconds', () => {
        expect(softPhone.describePlayoutDelay(0.04)).toBe('playout 40ms');
    });

    it('says nothing when the browser is left to decide', () => {
        expect(softPhone.describePlayoutDelay(-1)).toBe('');
        expect(softPhone.describePlayoutDelay(undefined)).toBe('');
    });
});

describe('applyPlayoutDelay', () => {
    it('sets the hint on audio receivers and reports how many took it', () => {
        const a = audioReceiver();
        const count = softPhone.applyPlayoutDelay(peer([a, videoReceiver()]), 0.04);

        expect(count).toBe(1);
        expect(a.playoutDelayHint).toBe(0.04);
    });

    it('clears the hint with undefined, not zero -- zero would ask for no buffer at all', () => {
        const a = audioReceiver({ playoutDelayHint: 0.04 });
        softPhone.applyPlayoutDelay(peer([a]), -1);

        expect(a.playoutDelayHint).toBeUndefined();
    });

    it('leaves video receivers alone', () => {
        const v = videoReceiver();
        softPhone.applyPlayoutDelay(peer([v]), 0.04);

        expect(v.playoutDelayHint).toBeUndefined();
    });

    it('reports zero where the browser does not implement the hint, so the caller knows nothing changed', () => {
        const unsupported = { track: { kind: 'audio' } };

        expect(softPhone.applyPlayoutDelay(peer([unsupported]), 0.04)).toBe(0);
    });

    it('survives a receiver that refuses the value', () => {
        const refusing = { track: { kind: 'audio' }, set playoutDelayHint(v) { throw new Error('no'); }, get playoutDelayHint() { return undefined; } };

        expect(() => softPhone.applyPlayoutDelay(peer([refusing]), 0.04)).not.toThrow();
        expect(softPhone.applyPlayoutDelay(peer([refusing]), 0.04)).toBe(0);
    });

    it('does nothing without a peer connection', () => {
        expect(softPhone.applyPlayoutDelay(null, 0.04)).toBe(0);
        expect(softPhone.applyPlayoutDelay({}, 0.04)).toBe(0);
    });
});

// The cost of asking for a shorter buffer: audio the browser had to invent because a packet arrived after its
// moment. A delay setting is only safe to keep if this stays low, so it is reported beside it.
describe('concealmentPercent', () => {
    it('is the share concealed over the window since the previous sample', () => {
        const previous = { concealedSamples: 1000, totalSamplesReceived: 100000 };
        const current = { concealedSamples: 1500, totalSamplesReceived: 150000 };

        expect(softPhone.concealmentPercent(current, previous)).toBeCloseTo(1, 6);
    });

    it('falls back to the call-lifetime share when the window received nothing', () => {
        const current = { concealedSamples: 2000, totalSamplesReceived: 100000 };

        expect(softPhone.concealmentPercent(current, current)).toBeCloseTo(2, 6);
    });

    it('is unknown when the browser does not report the counters', () => {
        expect(softPhone.concealmentPercent({ jitter: 0.01 }, null)).toBe(-1);
        expect(softPhone.concealmentPercent(null, null)).toBe(-1);
    });

    it('is unknown before any audio has been received at all', () => {
        expect(softPhone.concealmentPercent({ concealedSamples: 0, totalSamplesReceived: 0 }, null)).toBe(-1);
    });

    it('never reports a negative share when counters reset', () => {
        const previous = { concealedSamples: 5000, totalSamplesReceived: 500000 };
        const current = { concealedSamples: 10, totalSamplesReceived: 1000 };

        expect(softPhone.concealmentPercent(current, previous)).toBeGreaterThanOrEqual(0);
    });
});
