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
