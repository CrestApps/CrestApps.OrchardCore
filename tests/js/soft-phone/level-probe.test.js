import { beforeEach, describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/level-probe.js';

const softPhone = globalThis.CrestAppsSoftPhone;

// A fake AnalyserNode graph: the test drives the waveform, so the accumulate/peak/reset behaviour can be
// checked without a browser or a real microphone.
function createFakeAudio(waveform) {
    const state = { closed: false, disconnected: false, resumed: 0, state: 'running' };

    class FakeAudioContext {
        constructor() {
            this.state = state.state;
            state.instance = this;
        }

        createMediaStreamSource() {
            return {
                connect: node => { state.sourceConnectedTo = node; },
                disconnect: () => { state.disconnected = true; }
            };
        }

        createAnalyser() {
            return {
                fftSize: 0,
                connect: node => { state.analyserConnectedTo = node; },
                disconnect: () => { state.analyserDisconnected = true; },
                getFloatTimeDomainData(target) {
                    for (let i = 0; i < target.length; i++) {
                        target[i] = waveform.value;
                    }
                }
            };
        }

        createGain() {
            const gain = {
                isGain: true,
                gain: { value: null },
                connect: node => { state.gainConnectedTo = node; },
                disconnect: () => { state.gainDisconnected = true; }
            };

            state.gain = gain;

            return gain;
        }

        get destination() {
            return { isDestination: true };
        }

        resume() {
            state.resumed++;

            return Promise.resolve();
        }

        close() {
            state.closed = true;
        }
    }

    return { FakeAudioContext, state };
}

function createFakeTimers() {
    const ticks = [];

    return {
        timers: {
            setInterval: fn => {
                ticks.push(fn);

                return ticks.length;
            },
            clearInterval: () => { ticks.length = 0; }
        },
        tick: (times = 1) => {
            for (let i = 0; i < times; i++) {
                ticks.forEach(fn => fn());
            }
        },
        get running() {
            return ticks.length > 0;
        }
    };
}

function audioStream() {
    return { getAudioTracks: () => [{ id: 'track' }] };
}

describe('frameRms', () => {
    it('is zero for silence', () => {
        expect(softPhone.frameRms(new Float32Array(8), 8)).toBe(0);
    });

    it('is the amplitude for a constant signal', () => {
        const samples = new Float32Array(8).fill(0.5);

        expect(softPhone.frameRms(samples, 8)).toBeCloseTo(0.5, 6);
    });

    it('ignores sign, so a negative half-cycle counts as loudly as a positive one', () => {
        const samples = new Float32Array([-0.4, 0.4, -0.4, 0.4]);

        expect(softPhone.frameRms(samples, 4)).toBeCloseTo(0.4, 6);
    });
});

describe('createLevelProbe', () => {
    let waveform;
    let fake;
    let timers;

    beforeEach(() => {
        waveform = { value: 0 };
        fake = createFakeAudio(waveform);
        timers = createFakeTimers();
    });

    function probe(stream = audioStream()) {
        return softPhone.createLevelProbe(stream, {
            audioContext: fake.FakeAudioContext,
            timers: timers.timers
        });
    }

    it('returns null for a stream with no audio, so the level reads as unknown rather than silent', () => {
        expect(probe({ getAudioTracks: () => [] })).toBeNull();
    });

    it('returns null when there is no stream at all', () => {
        expect(probe(null)).toBeNull();
    });

    it('returns null when the browser has no AudioContext', () => {
        expect(softPhone.createLevelProbe(audioStream(), { audioContext: null, timers: timers.timers })).toBeNull();
    });

    it('reports the loudest frame in the window, not the last one', () => {
        const p = probe();

        waveform.value = 0.02;
        timers.tick();
        waveform.value = 0.25;
        timers.tick();
        waveform.value = 0;
        timers.tick();

        expect(p.read().peak).toBeCloseTo(0.25, 6);
    });

    it('averages across the window, which silence between words drags down', () => {
        const p = probe();

        waveform.value = 0.4;
        timers.tick();
        waveform.value = 0;
        timers.tick(3);

        const window = p.read();

        expect(window.average).toBeCloseTo(0.1, 6);
        expect(window.peak).toBeCloseTo(0.4, 6);
    });

    it('starts a fresh window on each read, so one loud moment is not reported forever', () => {
        const p = probe();

        waveform.value = 0.3;
        timers.tick();
        p.read();

        waveform.value = 0.01;
        timers.tick();

        expect(p.read().peak).toBeCloseTo(0.01, 6);
    });

    it('reports no reads for a window nothing was sampled in', () => {
        const p = probe();

        expect(p.read().reads).toBe(0);
    });

    it('asks a suspended context to resume, because a suspended graph reads as pure silence', () => {
        fake.state.state = 'suspended';
        probe();

        expect(fake.state.resumed).toBeGreaterThan(0);
    });

    // Firefox only processes an audio graph that reaches the destination. An analyser left hanging reads pure
    // silence there forever, which looks exactly like a dead microphone on a call that is working -- and did,
    // on a Firefox call where both directions reported 0.000 while the two people could hear each other.
    it('routes the analyser to the destination, or Firefox never processes it', () => {
        probe();

        expect(fake.state.analyserConnectedTo).toBe(fake.state.gain);
        expect(fake.state.gainConnectedTo.isDestination).toBe(true);
    });

    // The far end's audio is already being played by the remote audio element. Routing it to the destination a
    // second time at any audible gain would play every caller twice.
    it('routes it silently, so measuring the far end does not play it twice', () => {
        probe();

        expect(fake.state.gain.gain.value).toBe(0);
    });

    it('releases the audio graph on dispose, so a probe does not outlive its call', () => {
        const p = probe();

        p.dispose();

        expect(fake.state.closed).toBe(true);
        expect(fake.state.disconnected).toBe(true);
        expect(fake.state.analyserDisconnected).toBe(true);
        expect(fake.state.gainDisconnected).toBe(true);
        expect(timers.running).toBe(false);
    });

    it('tolerates being disposed twice', () => {
        const p = probe();

        p.dispose();

        expect(() => p.dispose()).not.toThrow();
    });
});

describe('probeLevel', () => {
    it('is unknown without a probe, which is what a browser that could not measure reports', () => {
        expect(softPhone.probeLevel(null)).toBe(softPhone.LEVEL_UNKNOWN);
    });

    it('is unknown when the window contained no reads at all', () => {
        expect(softPhone.probeLevel({ read: () => ({ peak: 0, average: 0, reads: 0 }) })).toBe(softPhone.LEVEL_UNKNOWN);
    });

    it('is zero -- a finding, not an absence -- when the window was measured and silent', () => {
        expect(softPhone.probeLevel({ read: () => ({ peak: 0, average: 0, reads: 10 }) })).toBe(0);
    });

    it('is the peak of a window that carried speech', () => {
        expect(softPhone.probeLevel({ read: () => ({ peak: 0.18, average: 0.04, reads: 10 }) })).toBeCloseTo(0.18, 6);
    });

    it('treats a measured silence as below the silence threshold and speech as above it', () => {
        expect(softPhone.probeLevel({ read: () => ({ peak: 0.001, reads: 10 }) })).toBeLessThanOrEqual(softPhone.LEVEL_SILENT);
        expect(softPhone.probeLevel({ read: () => ({ peak: 0.12, reads: 10 }) })).toBeGreaterThan(softPhone.LEVEL_SILENT);
    });
});

// A track is 'live' until stopped, but a source that stopped feeding it leaves it live and MUTED -- and a muted
// track produces no frames, so nothing is sent. A readyState-only check passed such a track onto a call that ran
// thirty seconds with zero bytes sent while the caller heard silence.
describe('isTrackDeliverable', () => {
    it('accepts a live, unmuted track', () => {
        expect(softPhone.isTrackDeliverable({ readyState: 'live', muted: false })).toBe(true);
    });

    it('rejects a muted track even though it is live', () => {
        expect(softPhone.isTrackDeliverable({ readyState: 'live', muted: true })).toBe(false);
    });

    it('rejects an ended track', () => {
        expect(softPhone.isTrackDeliverable({ readyState: 'ended', muted: false })).toBe(false);
    });

    it('rejects no track at all', () => {
        expect(softPhone.isTrackDeliverable(null)).toBe(false);
        expect(softPhone.isTrackDeliverable(undefined)).toBe(false);
    });
});
