/*
 * Call-quality measurement: turning an RTCStatsReport into the handful of numbers the in-call indicator and the
 * server-side telemetry both read.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Thresholds that classify a getStats sample as a poor connection. Kept in sync with the server-side
    // TelephonyCallQualityEvaluator so the in-call UX and the server logs agree on what "poor" means.
    var QUALITY_POOR_MOS = 3.5;
    var QUALITY_POOR_LOSS_PERCENT = 5;

    // Estimates a Mean Opinion Score (1.0-4.5) from round-trip time, jitter, and packet loss using the ITU-T
    // G.107 E-model approximation widely used for WebRTC quality monitoring. Latency and jitter are in
    // milliseconds, loss in percent. A higher score is better; ~4.0+ is good, below ~3.5 is poor.
    function estimateMos(rttMs, jitterMs, lossPercent) {
        var effectiveLatency = (rttMs || 0) + (jitterMs || 0) * 2 + 10;
        var r = effectiveLatency < 160
            ? 93.2 - effectiveLatency / 40
            : 93.2 - (effectiveLatency - 120) / 10;

        r = r - 2.5 * (lossPercent || 0);

        if (r < 0) {
            return 1;
        }

        if (r > 100) {
            return 4.5;
        }

        return 1 + 0.035 * r + r * (r - 60) * (100 - r) * 0.000007;
    }

    // Extracts the audio inbound-rtp, selected candidate pair, negotiated codec, and candidate types from an
    // RTCStatsReport. Written defensively because the exact shape and which candidate pair is flagged "selected"
    // varies across browsers (Chrome nominates a succeeded pair; Firefox marks one `selected`; the transport may
    // name the pair through selectedCandidatePairId).
    function parseWebRtcStats(report) {
        var inbound = null;
        var remoteInbound = null;
        var selectedPair = null;
        var nominatedPair = null;
        var transportPairId = null;
        var codecs = {};
        var localCandidates = {};
        var remoteCandidates = {};
        var pairs = {};

        report.forEach(function (stat) {
            switch (stat.type) {
                case 'inbound-rtp':
                    if (stat.kind === 'audio' || stat.mediaType === 'audio') {
                        inbound = stat;
                    }

                    break;
                case 'remote-inbound-rtp':
                    if (stat.kind === 'audio' || stat.mediaType === 'audio') {
                        remoteInbound = stat;
                    }

                    break;
                case 'candidate-pair':
                    pairs[stat.id] = stat;

                    if (stat.selected) {
                        selectedPair = stat;
                    }

                    if (stat.nominated && stat.state === 'succeeded' &&
                        (!nominatedPair || (stat.bytesReceived || 0) > (nominatedPair.bytesReceived || 0))) {
                        nominatedPair = stat;
                    }

                    break;
                case 'codec':
                    codecs[stat.id] = stat;

                    break;
                case 'local-candidate':
                    localCandidates[stat.id] = stat;

                    break;
                case 'remote-candidate':
                    remoteCandidates[stat.id] = stat;

                    break;
                case 'transport':
                    if (stat.selectedCandidatePairId) {
                        transportPairId = stat.selectedCandidatePairId;
                    }

                    break;
                default:
                    break;
            }
        });

        var pair = selectedPair ||
            (transportPairId && pairs[transportPairId]) ||
            nominatedPair ||
            null;
        var codec = inbound && inbound.codecId && codecs[inbound.codecId] ? codecs[inbound.codecId] : null;
        var localCandidate = pair && pair.localCandidateId ? localCandidates[pair.localCandidateId] : null;
        var remoteCandidate = pair && pair.remoteCandidateId ? remoteCandidates[pair.remoteCandidateId] : null;

        return {
            inbound: inbound,
            remoteInbound: remoteInbound,
            pair: pair,
            codec: codec ? codec.mimeType || '' : '',
            localCandidateType: localCandidate ? localCandidate.candidateType || '' : '',
            remoteCandidateType: remoteCandidate ? remoteCandidate.candidateType || '' : ''
        };
    }

    softPhone.QUALITY_POOR_MOS = QUALITY_POOR_MOS;
    softPhone.QUALITY_POOR_LOSS_PERCENT = QUALITY_POOR_LOSS_PERCENT;
    softPhone.estimateMos = estimateMos;
    softPhone.parseWebRtcStats = parseWebRtcStats;
}(typeof globalThis !== 'undefined' ? globalThis : window));
