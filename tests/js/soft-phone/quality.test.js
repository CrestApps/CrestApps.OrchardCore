import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/quality.js';

const { estimateMos, parseWebRtcStats, QUALITY_POOR_MOS, QUALITY_POOR_LOSS_PERCENT } = globalThis.CrestAppsSoftPhone;

/**
 * Builds an RTCStatsReport-shaped object: the real one is a Map, and the parser only ever iterates it.
 */
function statsReport(stats) {
    return {
        forEach(callback) {
            stats.forEach(callback);
        },
    };
}

// The call-quality score is what tells an agent their line is bad and what the server logs as a poor call. A
// change here changes both, silently, so the arithmetic is pinned.
describe('estimateMos', () => {
    it('scores a clean connection near the top of the scale', () => {
        expect(estimateMos(0, 0, 0)).toBeGreaterThan(4.3);
        expect(estimateMos(0, 0, 0)).toBeLessThanOrEqual(4.5);
    });

    it('falls as latency rises', () => {
        expect(estimateMos(300, 0, 0)).toBeLessThan(estimateMos(50, 0, 0));
    });

    it('counts jitter about twice as heavily as plain latency', () => {
        // Jitter is worse than latency for a conversation: a steady delay is tolerable, a varying one is not.
        expect(estimateMos(0, 100, 0)).toBeLessThan(estimateMos(100, 0, 0));
    });

    it('bottoms out at 1 rather than going negative', () => {
        expect(estimateMos(1000, 500, 90)).toBe(1);
    });

    it('treats missing samples as zero rather than producing NaN', () => {
        // A browser that reports no round-trip time must not turn the whole score into NaN, which renders as
        // an empty quality indicator and looks like the feature is broken.
        expect(Number.isNaN(estimateMos(undefined, undefined, undefined))).toBe(false);
    });

    it('agrees with the poor-call thresholds the server also uses', () => {
        expect(QUALITY_POOR_MOS).toBe(3.5);
        expect(QUALITY_POOR_LOSS_PERCENT).toBe(5);
    });
});

describe('parseWebRtcStats', () => {
    it('picks the audio inbound stream, its codec, and the candidate types in use', () => {
        // Arrange
        const report = statsReport([
            { type: 'inbound-rtp', kind: 'video', id: 'v' },
            { type: 'inbound-rtp', kind: 'audio', id: 'a', codecId: 'c1' },
            { type: 'codec', id: 'c1', mimeType: 'audio/opus' },
            { type: 'candidate-pair', id: 'p1', selected: true, localCandidateId: 'l1', remoteCandidateId: 'r1' },
            { type: 'local-candidate', id: 'l1', candidateType: 'srflx' },
            { type: 'remote-candidate', id: 'r1', candidateType: 'relay' },
        ]);

        // Act
        const parsed = parseWebRtcStats(report);

        // Assert
        expect(parsed.inbound.id).toBe('a');
        expect(parsed.codec).toBe('audio/opus');
        expect(parsed.localCandidateType).toBe('srflx');
        expect(parsed.remoteCandidateType).toBe('relay');
    });

    it('finds the pair the transport names when no pair is flagged selected', () => {
        // Chrome does not set `selected`; it nominates a succeeded pair and names it from the transport. Reading
        // only `selected` reports no candidate types at all on Chrome, which is most agents.
        const report = statsReport([
            { type: 'transport', id: 't', selectedCandidatePairId: 'p2' },
            { type: 'candidate-pair', id: 'p1' },
            { type: 'candidate-pair', id: 'p2', localCandidateId: 'l1' },
            { type: 'local-candidate', id: 'l1', candidateType: 'relay' },
        ]);

        expect(parseWebRtcStats(report).localCandidateType).toBe('relay');
    });

    it('falls back to the busiest nominated pair when nothing else identifies one', () => {
        const report = statsReport([
            { type: 'candidate-pair', id: 'p1', nominated: true, state: 'succeeded', bytesReceived: 10, localCandidateId: 'l1' },
            { type: 'candidate-pair', id: 'p2', nominated: true, state: 'succeeded', bytesReceived: 900, localCandidateId: 'l2' },
            { type: 'local-candidate', id: 'l1', candidateType: 'host' },
            { type: 'local-candidate', id: 'l2', candidateType: 'relay' },
        ]);

        expect(parseWebRtcStats(report).localCandidateType).toBe('relay');
    });

    it('reports empty strings rather than throwing on a report with nothing useful in it', () => {
        // A call that has not negotiated yet reports almost nothing, and the quality poll runs anyway.
        const parsed = parseWebRtcStats(statsReport([]));

        expect(parsed.inbound).toBeNull();
        expect(parsed.pair).toBeNull();
        expect(parsed.codec).toBe('');
        expect(parsed.localCandidateType).toBe('');
    });

    it('reads the media type from either property name browsers use', () => {
        const parsed = parseWebRtcStats(statsReport([{ type: 'inbound-rtp', mediaType: 'audio', id: 'a' }]));

        expect(parsed.inbound.id).toBe('a');
    });
});

// Perceived delay is round-trip time plus whatever the browser holds in its jitter buffer, and only the first
// half was ever measured. A call with a healthy round trip, no loss and low jitter can still have the two
// parties talking over each other, which is exactly what a "Good"-rated call that the agent described as
// delayed turned out to be.
describe('readJitterBufferMs', () => {
    const { readJitterBufferMs, QUALITY_HIGH_JITTER_BUFFER_MS } = globalThis.CrestAppsSoftPhone;

    it('is unknown when the browser does not report the counters', () => {
        expect(readJitterBufferMs({ jitter: 0.01 }, null)).toBe(-1);
        expect(readJitterBufferMs({ jitterBufferDelay: 5 }, null)).toBe(-1);
        expect(readJitterBufferMs(null, null)).toBe(-1);
    });

    it('is unknown when nothing has been emitted at all', () => {
        expect(readJitterBufferMs({ jitterBufferDelay: 0, jitterBufferEmittedCount: 0 }, null)).toBe(-1);
    });

    // The counters are cumulative and in the spec's own units: jitterBufferDelay is the total seconds every
    // emitted sample spent waiting, and jitterBufferEmittedCount counts audio SAMPLES, so 48000 of them is one
    // second of 48 kHz audio. The average delay is therefore the ratio, in seconds.
    it('averages over the window since the previous sample, not over the whole call', () => {
        // Earlier: 48000 samples that waited 20ms each. This window: 4800 samples that waited 500ms each --
        // the buffer the agent is listening through right now.
        const previous = { jitterBufferDelay: 960, jitterBufferEmittedCount: 48000 };
        const current = { jitterBufferDelay: 960 + 2400, jitterBufferEmittedCount: 52800 };

        expect(readJitterBufferMs(current, previous)).toBeCloseTo(500, 6);
    });

    it('does not let a good start hide a buffer that grew late', () => {
        const previous = { jitterBufferDelay: 960, jitterBufferEmittedCount: 48000 };
        const current = { jitterBufferDelay: 960 + 2400, jitterBufferEmittedCount: 52800 };
        const lifetimeAverage = (current.jitterBufferDelay / current.jitterBufferEmittedCount) * 1000;

        expect(lifetimeAverage).toBeLessThan(QUALITY_HIGH_JITTER_BUFFER_MS);
        expect(readJitterBufferMs(current, previous)).toBeGreaterThan(QUALITY_HIGH_JITTER_BUFFER_MS);
    });

    it('uses the call-lifetime average when the window emitted nothing', () => {
        const current = { jitterBufferDelay: 4800, jitterBufferEmittedCount: 48000 };

        expect(readJitterBufferMs(current, current)).toBeCloseTo(100, 6);
    });

    it('treats a first sample as the whole call so far', () => {
        expect(readJitterBufferMs({ jitterBufferDelay: 1200, jitterBufferEmittedCount: 48000 }, null)).toBeCloseTo(25, 6);
    });
});

// The capture side of the stats report, which exists in Chrome and not in Firefox. The parser has to surface
// its absence rather than paper over it, because "no capture statistics" and "a microphone delivering silence"
// call for opposite responses.
describe('parseWebRtcStats capture and outbound', () => {
    it('finds the audio media source and outbound stream', () => {
        const parsed = parseWebRtcStats(statsReport([
            { type: 'inbound-rtp', kind: 'audio', packetsReceived: 10 },
            { type: 'media-source', kind: 'audio', audioLevel: 0.12, totalAudioEnergy: 3.4 },
            { type: 'outbound-rtp', kind: 'audio', bytesSent: 4096, packetsSent: 128 },
        ]));

        expect(parsed.mediaSource.audioLevel).toBe(0.12);
        expect(parsed.outbound.bytesSent).toBe(4096);
    });

    it('leaves the media source null where the browser reports none, as Firefox does', () => {
        const parsed = parseWebRtcStats(statsReport([
            { type: 'inbound-rtp', kind: 'audio', packetsReceived: 10 },
            { type: 'outbound-rtp', kind: 'audio', bytesSent: 4096 },
        ]));

        expect(parsed.mediaSource).toBeNull();
        expect(parsed.outbound).not.toBeNull();
    });

    it('ignores a video media source', () => {
        const parsed = parseWebRtcStats(statsReport([
            { type: 'media-source', kind: 'video', framesPerSecond: 30 },
        ]));

        expect(parsed.mediaSource).toBeNull();
    });
});
