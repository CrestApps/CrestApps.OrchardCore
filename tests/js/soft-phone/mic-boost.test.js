import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/mic-boost.js';

const softPhone = globalThis.CrestAppsSoftPhone;

// A fake Web Audio graph that records how it was wired, so the pipeline can be checked without a browser.
function createFakeAudio() {
    const state = { nodes: [], connections: [], closed: false, resumed: 0, state: 'suspended' };
    const node = (kind, extra = {}) => {
        const n = { kind, connect: (to) => { state.connections.push([kind, to.kind]); }, disconnect: () => { n.disconnected = true; }, ...extra };
        state.nodes.push(n);

        return n;
    };

    class FakeAudioContext {
        constructor() { this.state = state.state; state.instance = this; }
        createMediaStreamSource() { return node('source'); }
        createGain() { return node('gain', { gain: { value: 1 } }); }
        createDynamicsCompressor() { return node('limiter', { threshold: { value: 0 }, knee: { value: 0 }, ratio: { value: 1 }, attack: { value: 0 }, release: { value: 0 } }); }
        createMediaStreamDestination() { return node('destination', { stream: { id: 'boosted-stream', getAudioTracks: () => [{ id: 'boosted-track' }] } }); }
        resume() { state.resumed++; return Promise.resolve(); }
        close() { state.closed = true; }
    }

    return { FakeAudioContext, state };
}

const sourceStream = { id: 'mic-stream', getAudioTracks: () => [{ id: 'mic-track' }] };

describe('clampBoostDb', () => {
    it('accepts the offered boosts', () => {
        expect(softPhone.clampBoostDb(6)).toBe(6);
        expect(softPhone.clampBoostDb('9')).toBe(9);
    });

    it('treats anything unrecognized as off -- never a surprise lift', () => {
        expect(softPhone.clampBoostDb(5)).toBe(0);
        expect(softPhone.clampBoostDb(-3)).toBe(0);
        expect(softPhone.clampBoostDb('loud')).toBe(0);
        expect(softPhone.clampBoostDb(undefined)).toBe(0);
    });
});

describe('boostGainFor', () => {
    it('is unity when off', () => {
        expect(softPhone.boostGainFor(0)).toBe(1);
    });

    it('doubles the amplitude at +6 dB', () => {
        expect(softPhone.boostGainFor(6)).toBeCloseTo(1.995, 2);
    });

    it('quadruples it at +12 dB', () => {
        expect(softPhone.boostGainFor(12)).toBeCloseTo(3.981, 2);
    });
});

describe('describeBoost', () => {
    it('names an active boost for the capture readout', () => {
        expect(softPhone.describeBoost(6)).toBe('boost+6');
    });

    it('is empty when off, so the readout does not grow a meaningless token', () => {
        expect(softPhone.describeBoost(0)).toBe('');
    });
});

describe('createBoostPipeline', () => {
    it('sends the capture untouched when the boost is off -- no graph, nothing to dispose', () => {
        const fake = createFakeAudio();
        const pipeline = softPhone.createBoostPipeline(sourceStream, 0, { audioContext: fake.FakeAudioContext });

        expect(pipeline.stream).toBe(sourceStream);
        expect(pipeline.boosted).toBe(false);
        expect(fake.state.nodes).toHaveLength(0);
        expect(() => pipeline.dispose()).not.toThrow();
    });

    it('routes source -> gain -> limiter -> destination and sends the destination stream', () => {
        const fake = createFakeAudio();
        const pipeline = softPhone.createBoostPipeline(sourceStream, 6, { audioContext: fake.FakeAudioContext });

        expect(pipeline.boosted).toBe(true);
        expect(pipeline.stream.id).toBe('boosted-stream');
        expect(fake.state.connections).toEqual([['source', 'gain'], ['gain', 'limiter'], ['limiter', 'destination']]);
    });

    it('applies the chosen gain and a hard limiter just under full scale', () => {
        const fake = createFakeAudio();
        softPhone.createBoostPipeline(sourceStream, 6, { audioContext: fake.FakeAudioContext });
        const gain = fake.state.nodes.find(n => n.kind === 'gain');
        const limiter = fake.state.nodes.find(n => n.kind === 'limiter');

        expect(gain.gain.value).toBeCloseTo(1.995, 2);
        expect(limiter.threshold.value).toBe(-3);
        expect(limiter.knee.value).toBe(0);
        expect(limiter.ratio.value).toBe(20);
    });

    it('asks a suspended context to resume, because a suspended graph is silence', () => {
        const fake = createFakeAudio();
        softPhone.createBoostPipeline(sourceStream, 3, { audioContext: fake.FakeAudioContext });

        expect(fake.state.resumed).toBe(1);
    });

    it('releases the graph on dispose', () => {
        const fake = createFakeAudio();
        const pipeline = softPhone.createBoostPipeline(sourceStream, 9, { audioContext: fake.FakeAudioContext });

        pipeline.dispose();

        expect(fake.state.closed).toBe(true);
        expect(fake.state.nodes.filter(n => n.kind !== 'destination').every(n => n.disconnected)).toBe(true);
    });

    it('falls back to the raw capture when the browser refuses the graph, rather than sending nothing', () => {
        class Refusing { createMediaStreamSource() { throw new Error('no'); } close() { } }
        const pipeline = softPhone.createBoostPipeline(sourceStream, 6, { audioContext: Refusing });

        expect(pipeline.stream).toBe(sourceStream);
        expect(pipeline.boosted).toBe(false);
    });

    it('sends the capture untouched when no AudioContext exists', () => {
        const pipeline = softPhone.createBoostPipeline(sourceStream, 6, { audioContext: null });

        expect(pipeline.stream).toBe(sourceStream);
        expect(pipeline.boosted).toBe(false);
    });
});
