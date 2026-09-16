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

    // The capture level (RTCAudioSourceStats.audioLevel, 0..1 where 1.0 is full scale) at or below which the
    // microphone is delivering nothing the far end could hear. Room noise on a live microphone sits above this.
    var QUALITY_SILENT_MIC_LEVEL = 0.005;

    // Consecutive silent samples before a capture is called dead. One sample is just a pause in the
    // conversation; several in a row while the call is up is a microphone that is not working.
    var QUALITY_SILENT_MIC_SAMPLES = 3;

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

    // The average jitter-buffer delay above which a conversation starts to feel like a walkie-talkie: the two
    // parties begin talking over each other because each hears the other late. Well under the point where any
    // other metric reacts.
    var QUALITY_HIGH_JITTER_BUFFER_MS = 200;

    // Average time received audio waited in the jitter buffer before it was played out, over the window since
    // the previous sample.
    //
    // This is the missing half of perceived delay. Round-trip time measures the network between the browser and
    // the provider edge; the jitter buffer is what the browser itself adds on top, adapting to conditions and
    // routinely holding hundreds of milliseconds. A call can therefore have a healthy round-trip time, no
    // packet loss, low jitter and a good score while the people on it are audibly talking over each other --
    // which is exactly what a clean-looking call that the agent described as delayed turned out to be. Nothing
    // transmitted before this could see it.
    //
    // Measured over the window rather than the call, because these counters are cumulative and a call-lifetime
    // average hides a buffer that grew late. Returns -1 when the browser does not report the counters.
    function readJitterBufferMs(inbound, previous) {
        if (!inbound ||
            typeof inbound.jitterBufferDelay !== 'number' ||
            typeof inbound.jitterBufferEmittedCount !== 'number') {
            return -1;
        }

        var delayDelta = inbound.jitterBufferDelay - ((previous && previous.jitterBufferDelay) || 0);
        var emittedDelta = inbound.jitterBufferEmittedCount - ((previous && previous.jitterBufferEmittedCount) || 0);

        if (emittedDelta > 0) {
            return (delayDelta / emittedDelta) * 1000;
        }

        // No audio was emitted in this window (the call just started, or playout stalled). The call-lifetime
        // average is less sharp but still true; only when there is nothing at all is the answer unknown.
        return inbound.jitterBufferEmittedCount > 0
            ? (inbound.jitterBufferDelay / inbound.jitterBufferEmittedCount) * 1000
            : -1;
    }

    // Distinguishes a microphone that is dead from one that is merely quiet. The capture level alone cannot:
    // an agent who is listening rather than talking also reads near zero. totalAudioEnergy, by contrast, only
    // accumulates while the track actually delivers samples, so a level at the floor AND no new energy since
    // the previous sample means nothing is being captured at all. That is the case the far end experiences as
    // "I cannot hear you" while every other measurement on the call looks healthy.
    function isCaptureSilent(level, energyDelta) {
        return (level || 0) <= QUALITY_SILENT_MIC_LEVEL && (energyDelta || 0) <= 0;
    }

    // Extracts the audio inbound-rtp, the capture (media-source) and outbound-rtp, the selected candidate pair,
    // negotiated codec, and candidate types from an RTCStatsReport. Written defensively because the exact shape
    // and which candidate pair is flagged "selected" varies across browsers (Chrome nominates a succeeded pair;
    // Firefox marks one `selected`; the transport may name the pair through selectedCandidatePairId).
    function parseWebRtcStats(report) {
        var inbound = null;
        var remoteInbound = null;
        var mediaSource = null;
        var outbound = null;
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
                case 'media-source':
                    // The capture itself: audioLevel and totalAudioEnergy describe what the microphone is
                    // feeding the encoder, which is the only measurement that says whether the agent can be
                    // heard. Everything else on this report describes the direction they are listening to.
                    if (stat.kind === 'audio' || stat.mediaType === 'audio') {
                        mediaSource = stat;
                    }

                    break;
                case 'outbound-rtp':
                    if (stat.kind === 'audio' || stat.mediaType === 'audio') {
                        outbound = stat;
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
        // The codec the browser SENDS. Only the receive codec was reported until now, so the direction the far
        // end hears -- the one every complaint has been about -- had no codec on record at all.
        var sendCodec = outbound && outbound.codecId && codecs[outbound.codecId] ? codecs[outbound.codecId] : null;
        var localCandidate = pair && pair.localCandidateId ? localCandidates[pair.localCandidateId] : null;
        var remoteCandidate = pair && pair.remoteCandidateId ? remoteCandidates[pair.remoteCandidateId] : null;

        return {
            inbound: inbound,
            remoteInbound: remoteInbound,
            mediaSource: mediaSource,
            outbound: outbound,
            pair: pair,
            codec: codec ? codec.mimeType || '' : '',
            sendCodec: sendCodec ? sendCodec.mimeType || '' : '',
            localCandidateType: localCandidate ? localCandidate.candidateType || '' : '',
            remoteCandidateType: remoteCandidate ? remoteCandidate.candidateType || '' : ''
        };
    }

    softPhone.QUALITY_POOR_MOS = QUALITY_POOR_MOS;
    softPhone.QUALITY_POOR_LOSS_PERCENT = QUALITY_POOR_LOSS_PERCENT;
    softPhone.QUALITY_SILENT_MIC_LEVEL = QUALITY_SILENT_MIC_LEVEL;
    softPhone.QUALITY_SILENT_MIC_SAMPLES = QUALITY_SILENT_MIC_SAMPLES;
    softPhone.QUALITY_HIGH_JITTER_BUFFER_MS = QUALITY_HIGH_JITTER_BUFFER_MS;
    softPhone.estimateMos = estimateMos;
    softPhone.readJitterBufferMs = readJitterBufferMs;
    softPhone.isCaptureSilent = isCaptureSilent;
    softPhone.parseWebRtcStats = parseWebRtcStats;
}(typeof globalThis !== 'undefined' ? globalThis : window));
