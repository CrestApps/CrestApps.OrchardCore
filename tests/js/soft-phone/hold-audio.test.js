import { beforeEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/hold-audio.js';

const { selectHoldSource, buildTonePlan, findAudioSender, createHoldAudioController, HOLD_TONE_PLAN } =
    globalThis.CrestAppsSoftPhone;

// A fake RTCRtpSender that records the track it currently carries and every replaceTrack it is asked to do.
function fakeSender(track) {
    return {
        track,
        replaced: [],
        replaceTrack(next) {
            this.replaced.push(next);
            this.track = next;

            return Promise.resolve();
        },
    };
}

function fakePeerConnection(senders) {
    return { getSenders: () => senders };
}

// A fake engine standing in for the WebAudio/HTMLAudio adapter: it hands back a known track and records that it
// was started and stopped, so the controller's orchestration is observable without a browser.
function fakeEngine(holdTrack) {
    const engine = {
        started: 0,
        stopped: 0,
        track: holdTrack,
        start() {
            this.started++;

            return Promise.resolve(this.track);
        },
        stop() {
            this.stopped++;

            return Promise.resolve();
        },
    };

    return engine;
}

describe('selectHoldSource', () => {
    it('uses a configured URL when one is present', () => {
        expect(selectHoldSource({ mediaUrl: 'https://cdn.example/hold.mp3' }))
            .toEqual({ kind: 'url', url: 'https://cdn.example/hold.mp3' });
    });

    it('trims the URL and treats whitespace-only as absent', () => {
        expect(selectHoldSource({ mediaUrl: '  https://cdn.example/hold.mp3  ' }).url)
            .toBe('https://cdn.example/hold.mp3');
        expect(selectHoldSource({ mediaUrl: '   ' })).toEqual({ kind: 'tone' });
    });

    it('falls back to the comfort tone when no URL is given', () => {
        expect(selectHoldSource()).toEqual({ kind: 'tone' });
        expect(selectHoldSource({})).toEqual({ kind: 'tone' });
        expect(selectHoldSource({ mediaUrl: 42 })).toEqual({ kind: 'tone' });
    });
});

describe('buildTonePlan', () => {
    it('returns the pinned, deliberately quiet tone plan', () => {
        // The gain reaches both the caller's line and, on some setups, the agent's own ear. A wrong value is a
        // painful surprise, so it is pinned well under unity here.
        const plan = buildTonePlan();

        expect(plan.gain).toBe(0.05);
        expect(plan.gain).toBeLessThan(0.2);
        expect(plan.frequencies).toEqual([440, 480]);
        expect(plan.onSeconds).toBeGreaterThan(0);
        expect(plan.offSeconds).toBeGreaterThan(plan.onSeconds);
    });

    it('hands back a copy so callers cannot mutate the shared constant', () => {
        const plan = buildTonePlan();
        plan.frequencies.push(1000);
        plan.gain = 1;

        expect(buildTonePlan().frequencies).toEqual([440, 480]);
        expect(buildTonePlan().gain).toBe(0.05);
        expect(HOLD_TONE_PLAN.gain).toBe(0.05);
    });
});

describe('findAudioSender', () => {
    it('returns the sender carrying an audio track', () => {
        const audio = fakeSender({ kind: 'audio' });

        expect(findAudioSender([fakeSender({ kind: 'video' }), audio])).toBe(audio);
    });

    it('skips a video sender and falls back to a track-less sender', () => {
        const empty = fakeSender(null);

        expect(findAudioSender([fakeSender({ kind: 'video' }), empty])).toBe(empty);
    });

    it('returns null when there is nothing usable', () => {
        expect(findAudioSender([])).toBeNull();
        expect(findAudioSender(null)).toBeNull();
        expect(findAudioSender([fakeSender({ kind: 'video' })])).toBeNull();
    });
});

describe('createHoldAudioController', () => {
    let holdTrack;
    let engine;
    let controller;

    beforeEach(() => {
        holdTrack = { kind: 'audio', id: 'hold' };
        engine = fakeEngine(holdTrack);
        controller = createHoldAudioController({ createEngine: () => engine });
    });

    it('swaps the mic track for the hold track on engage and back on release', async () => {
        const micTrack = { kind: 'audio', id: 'mic' };
        const sender = fakeSender(micTrack);
        const pc = fakePeerConnection([sender]);

        await controller.engage(pc, micTrack);

        expect(controller.isEngaged()).toBe(true);
        expect(engine.started).toBe(1);
        expect(sender.track).toBe(holdTrack);

        await controller.release(pc);

        expect(controller.isEngaged()).toBe(false);
        expect(engine.stopped).toBe(1);
        // Restores the microphone the agent was on, not the hold track.
        expect(sender.track).toBe(micTrack);
    });

    it('is idempotent: engaging twice starts one engine and one swap', async () => {
        const sender = fakeSender({ kind: 'audio', id: 'mic' });
        const pc = fakePeerConnection([sender]);

        await controller.engage(pc, sender.track);
        await controller.engage(pc, sender.track);

        expect(engine.started).toBe(1);
        expect(sender.replaced).toEqual([holdTrack]);
    });

    it('releasing before engaging is a harmless no-op', async () => {
        await expect(controller.release(fakePeerConnection([]))).resolves.toBe(false);
        expect(engine.stopped).toBe(0);
    });

    it('rejects and stays disengaged when there is no audio sender to replace', async () => {
        const pc = fakePeerConnection([fakeSender({ kind: 'video' })]);

        await expect(controller.engage(pc, null)).rejects.toThrow(/no audio sender/);
        expect(controller.isEngaged()).toBe(false);
    });

    it('tears the engine down and stays disengaged when the engine fails to start', async () => {
        const failing = fakeEngine(holdTrack);
        failing.start = vi.fn(() => Promise.reject(new Error('boom')));
        failing.stop = vi.fn(() => Promise.resolve());
        const failController = createHoldAudioController({ createEngine: () => failing });

        const sender = fakeSender({ kind: 'audio', id: 'mic' });
        const pc = fakePeerConnection([sender]);

        await expect(failController.engage(pc, sender.track)).rejects.toThrow('boom');
        expect(failController.isEngaged()).toBe(false);
        expect(failing.stop).toHaveBeenCalledTimes(1);
        // The outbound track must not have been left swapped when the swap never completed.
        expect(sender.track).not.toBe(holdTrack);
    });

    it('exposes the resolved source so the caller can log which path ran', () => {
        const url = createHoldAudioController({ mediaUrl: 'https://cdn.example/hold.mp3', createEngine: () => engine });

        expect(url.source).toEqual({ kind: 'url', url: 'https://cdn.example/hold.mp3' });
        expect(controller.source).toEqual({ kind: 'tone' });
    });
});
