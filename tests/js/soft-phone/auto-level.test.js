import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/auto-level.js';

const softPhone = globalThis.CrestAppsSoftPhone;
const next = softPhone.nextAutoLevelGainDb;
const defaults = softPhone.AUTO_LEVEL_DEFAULTS;

// Runs the level for a stretch of audio at one measured (pre-gain) level, deciding every `stepMs`.
function run(gainDb, measuredDb, seconds, options = {}, stepMs = 50) {
    let gain = gainDb;

    for (let t = 0; t < seconds * 1000; t += stepMs) {
        gain = next(gain, measuredDb, options, stepMs);
    }

    return gain;
}

describe('nextAutoLevelGainDb', () => {
    it('raises a quiet voice toward the target, no faster than the rise rate', () => {
        // -35 dBFS speech wants +17 dB to reach -18.
        const afterOneSecond = run(0, -35, 1);

        expect(afterOneSecond).toBeCloseTo(defaults.riseDbPerSec, 5);
        expect(next(0, -35, {}, 50)).toBeCloseTo(defaults.riseDbPerSec * 0.05, 5);
    });

    it('settles on the gain that puts the voice at the target', () => {
        const settled = run(0, -35, 10);

        expect(Math.abs((-35 + settled) - defaults.targetDb)).toBeLessThanOrEqual(defaults.toleranceDb);
    });

    it('holds the gain through silence and anything under the speech threshold, so the room is never raised', () => {
        expect(next(9, -60, { speechThresholdDb: -40 }, 50)).toBe(9);
        expect(next(9, -40.5, { speechThresholdDb: -40 }, 50)).toBe(9);
        expect(next(9, -Infinity, {}, 50)).toBe(9);
        expect(run(9, -70, 30)).toBe(9);
    });

    it('moves once the window reaches the speech threshold', () => {
        expect(next(0, -40, { speechThresholdDb: -40 }, 50)).toBeGreaterThan(0);
    });

    it('falls faster than it rises on loud speech', () => {
        // At +15 dB a -25 dBFS voice comes out at -10: far over the target.
        const fall = 15 - next(15, -25, {}, 100);
        const rise = next(0, -40, { speechThresholdDb: -50 }, 100);

        expect(fall).toBeCloseTo(defaults.fallDbPerSec * 0.1, 5);
        expect(rise).toBeCloseTo(defaults.riseDbPerSec * 0.1, 5);
        expect(fall).toBeGreaterThan(rise);
    });

    it('never lifts past the maximum gain', () => {
        expect(run(0, -55, 60, { speechThresholdDb: -60 })).toBe(defaults.maxGainDb);
        expect(next(40, -55, { speechThresholdDb: -60 }, 50)).toBeLessThanOrEqual(defaults.maxGainDb);
    });

    it('does not go below unity for a voice that is merely above the target', () => {
        // -14 dBFS speech wants -4 dB, but it is not hot enough to need turning down.
        expect(run(3, -14, 5)).toBe(0);
        expect(run(0, -14, 5)).toBe(0);
    });

    it('turns a hot voice down below unity, but no further than the minimum gain', () => {
        const hot = run(0, -6, 5);

        expect(hot).toBe(defaults.minGainDb);
    });

    it('lets a gain left below unity recover gradually instead of jumping back', () => {
        const recovered = next(-5, -20, {}, 50);

        expect(recovered).toBeGreaterThan(-5);
        expect(recovered).toBeLessThan(0);
    });

    it('does not hunt within the tolerance', () => {
        expect(next(10, -28.5, {}, 50)).toBe(10);
    });

    it('bounds one decision by the longest step, so a delayed timer cannot jump the gain', () => {
        const step = next(0, -35, {}, 10000);

        expect(step).toBeCloseTo(defaults.riseDbPerSec * defaults.maxStepMs / 1000, 5);
    });

    it('holds when no time has passed or the inputs are not numbers', () => {
        expect(next(6, -30, {}, 0)).toBe(6);
        expect(next(6, -30, {}, -10)).toBe(6);
        expect(next(6, NaN, {}, 50)).toBe(6);
        expect(next(6, -30, {}, undefined)).toBe(6);
    });

    it('treats an unusable current gain as unity, never a surprise lift', () => {
        expect(next(undefined, -70, {}, 50)).toBe(0);
        expect(next(NaN, -70, {}, 50)).toBe(0);
        expect(next(99, -70, {}, 50)).toBe(defaults.maxGainDb);
    });

    it('takes its settings from the options, defaulting each one it is not given', () => {
        expect(next(0, -30, { targetDb: -29, toleranceDb: 0.5 }, 50)).toBeCloseTo(0.3, 5);
        expect(next(0, -30, { riseDbPerSec: 2 }, 1000 / 10)).toBeCloseTo(0.2, 5);
        expect(next(0, -30, { riseDbPerSec: 'fast' }, 100)).toBeCloseTo(defaults.riseDbPerSec * 0.1, 5);
    });
});

describe('clampAutoLevelGainDb', () => {
    it('keeps a gain inside the allowed range', () => {
        expect(softPhone.clampAutoLevelGainDb(9)).toBe(9);
        expect(softPhone.clampAutoLevelGainDb(50)).toBe(defaults.maxGainDb);
        expect(softPhone.clampAutoLevelGainDb(-50)).toBe(defaults.minGainDb);
        expect(softPhone.clampAutoLevelGainDb('9')).toBe(0);
        expect(softPhone.clampAutoLevelGainDb(null)).toBe(0);
    });
});

describe('level conversions', () => {
    it('converts an RMS amplitude to dBFS, with silence at minus infinity', () => {
        expect(softPhone.amplitudeToDb(1)).toBe(0);
        expect(softPhone.amplitudeToDb(0.1)).toBeCloseTo(-20, 5);
        expect(softPhone.amplitudeToDb(0)).toBe(-Infinity);
    });

    it('converts a gain in dB to a linear factor', () => {
        expect(softPhone.dbToGain(0)).toBe(1);
        expect(softPhone.dbToGain(20)).toBeCloseTo(10, 5);
        expect(softPhone.dbToGain(-6)).toBeCloseTo(0.501, 3);
    });
});
