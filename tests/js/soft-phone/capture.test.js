import { readFileSync } from 'node:fs';
import { beforeAll, describe, expect, it } from 'vitest';

const moduleSources = [
    'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/quality.js',
    'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/diagnostics.js',
    'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/level-probe.js'
];

let softPhone;

beforeAll(() => {
    moduleSources.forEach(source => {
        // The helpers attach to a namespace on the global rather than exporting, because in the browser they are
        // concatenated into one file. Evaluating them here runs the same code the browser runs.
        new Function(readFileSync(source, 'utf8'))();
    });

    softPhone = globalThis.CrestAppsSoftPhone;
});

/**
 * Builds an RTCStatsReport-alike from a list of stats: a Map already satisfies the forEach the parser uses.
 */
function statsReport(stats) {
    return new Map(stats.map((stat, index) => [stat.id || `s${index}`, stat]));
}

// Every measurement the soft phone took described the direction the agent was listening to: inbound loss,
// inbound jitter, the candidate pair's round trip. So a call where the caller could not hear the agent reported
// a clean bill of health -- no loss, 4ms jitter, a good MOS -- and there was no number anywhere that described
// the microphone. These cover the capture side, which is the half that was missing.
describe('parsing the capture side of a stats report', () => {
    it('reads the audio media source', () => {
        const parsed = softPhone.parseWebRtcStats(statsReport([
            { type: 'media-source', kind: 'audio', audioLevel: 0.12, totalAudioEnergy: 3.5 },
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 1000 }
        ]));

        expect(parsed.mediaSource).toBeTruthy();
        expect(parsed.mediaSource.audioLevel).toBe(0.12);
        expect(parsed.mediaSource.totalAudioEnergy).toBe(3.5);
    });

    it('reads the outbound audio rtp', () => {
        const parsed = softPhone.parseWebRtcStats(statsReport([
            { type: 'outbound-rtp', kind: 'audio', bytesSent: 4096, packetsSent: 64 },
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 1000 }
        ]));

        expect(parsed.outbound.bytesSent).toBe(4096);
        expect(parsed.outbound.packetsSent).toBe(64);
    });

    it('ignores a video media source', () => {
        const parsed = softPhone.parseWebRtcStats(statsReport([
            { type: 'media-source', kind: 'video', framesPerSecond: 30 }
        ]));

        expect(parsed.mediaSource).toBeNull();
    });

    it('reports nothing rather than throwing when the capture stats are absent', () => {
        // Browsers differ on which stats they expose, and a sample is best-effort: a missing media-source must
        // never break the sampler that also carries the inbound numbers.
        const parsed = softPhone.parseWebRtcStats(statsReport([
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 1000 }
        ]));

        expect(parsed.mediaSource).toBeNull();
        expect(parsed.outbound).toBeNull();
    });
});

describe('telling a dead microphone from a quiet one', () => {
    it('calls a capture with no level and no new energy silent', () => {
        expect(softPhone.isCaptureSilent(0, 0)).toBe(true);
    });

    it('does not call an agent who is listening silent', () => {
        // A level at the floor is normal while the caller is talking. What separates the two cases is whether
        // any new audio energy accumulated: a live microphone in a quiet room still gathers some.
        expect(softPhone.isCaptureSilent(0.001, 0.02)).toBe(false);
    });

    it('does not call a speaking agent silent', () => {
        expect(softPhone.isCaptureSilent(0.14, 0.9)).toBe(false);
    });

    it('treats missing numbers as silence rather than as health', () => {
        // Called only for a capture the browser actually reported (the sampler gates on that), so absent
        // numbers within a reported capture mean silence rather than a working microphone.
        expect(softPhone.isCaptureSilent(undefined, undefined)).toBe(true);
    });

    it('keeps the threshold below ordinary speech and above a silent floor', () => {
        expect(softPhone.QUALITY_SILENT_MIC_LEVEL).toBeGreaterThan(0);
        expect(softPhone.QUALITY_SILENT_MIC_LEVEL).toBeLessThan(0.05);
    });

    it('needs more than one sample before calling a microphone dead', () => {
        expect(softPhone.QUALITY_SILENT_MIC_SAMPLES).toBeGreaterThan(1);
    });
});

// Four provider warnings were logged during a call the caller could barely hear, and each one reached the
// server as "Provider warning" with no code and no context. Telnyx raises "Low local microphone audio detected"
// through this exact channel, so the one signal that would have named the problem was dropped on the floor --
// silently, and in a way that reads like a warning somebody had already looked at.
describe('describing a provider warning', () => {
    it('keeps a code and message when the SDK provides them', () => {
        const text = softPhone.describeProviderWarning({ code: 'LOW_AUDIO', message: 'Low local microphone audio detected' });

        expect(text).toBe('[LOW_AUDIO] Low local microphone audio detected');
    });

    it('reads the alternative names an SDK might use', () => {
        expect(softPhone.describeProviderWarning({ type: 'media', detail: 'no audio' })).toBe('[media] no audio');
        expect(softPhone.describeProviderWarning({ name: 'Warning', reason: 'device lost' })).toBe('[Warning] device lost');
    });

    it('passes a plain string through', () => {
        expect(softPhone.describeProviderWarning('Low local microphone audio detected'))
            .toBe('Low local microphone audio detected');
    });

    it('serializes a shape it does not recognize instead of discarding it', () => {
        const text = softPhone.describeProviderWarning({ warningCode: 42, warningText: 'something happened' });

        expect(text).toContain('warningCode');
        expect(text).toContain('something happened');
    });

    it('names the keys when the payload has no enumerable properties', () => {
        // An Error-like object serializes to "{}" — reporting that is no better than reporting nothing, but the
        // key names are enough to fix the extraction properly next time.
        const warning = Object.defineProperty({}, 'hidden', { value: 'x', enumerable: false });
        const text = softPhone.describeProviderWarning(warning);

        expect(text).toContain('keys=');
    });

    it('survives a payload that cannot be serialized', () => {
        const warning = {};
        warning.self = warning;

        expect(() => softPhone.describeProviderWarning(warning)).not.toThrow();
        expect(softPhone.describeProviderWarning(warning)).toContain('unserializable');
    });

    it('never reports an empty description for a warning that fired', () => {
        expect(softPhone.describeProviderWarning({})).toBe('keys=[]');
    });
});

// LOW_INBOUND_AUDIO (31006) is the SDK noticing the far end has been quiet for three seconds -- an ordinary pause
// in any two-party call, re-raised every fifteen seconds while it holds. Reported as a warning it produced twenty
// identical lines per call and buried the ones that mattered. It is kept, but as information; every other
// provider warning keeps its severity.
describe('classifyProviderWarning', () => {
    it('reports the far-end-quiet warning as information, in the shape the SDK event delivers it', () => {
        expect(softPhone.classifyProviderWarning({ warning: { code: 31006, name: 'LOW_INBOUND_AUDIO' } })).toBe('info');
    });

    it('recognises the same code unwrapped', () => {
        expect(softPhone.classifyProviderWarning({ code: 31006, name: 'LOW_INBOUND_AUDIO' })).toBe('warning'.replace('warning', 'info'));
    });

    it('keeps a quiet local microphone as a warning -- that one is about what the far end hears', () => {
        expect(softPhone.classifyProviderWarning({ warning: { code: 31005, name: 'LOW_LOCAL_AUDIO' } })).toBe('warning');
    });

    it('keeps quality, latency and no-bytes warnings as warnings', () => {
        expect(softPhone.classifyProviderWarning({ warning: { code: 31004, name: 'LOW_MOS' } })).toBe('warning');
        expect(softPhone.classifyProviderWarning({ warning: { code: 31001, name: 'HIGH_RTT' } })).toBe('warning');
        expect(softPhone.classifyProviderWarning({ warning: { code: 32002, name: 'LOW_BYTES_SENT' } })).toBe('warning');
    });

    it('treats an unrecognised shape as a warning rather than guessing', () => {
        expect(softPhone.classifyProviderWarning('some string')).toBe('warning');
        expect(softPhone.classifyProviderWarning(null)).toBe('warning');
        expect(softPhone.classifyProviderWarning({})).toBe('warning');
    });
});
