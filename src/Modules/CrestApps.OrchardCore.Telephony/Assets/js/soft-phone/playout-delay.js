/*
 * Playout delay: asking the browser to hold less incoming audio before playing it.
 *
 * Half of the pause an agent feels between finishing a sentence and hearing the reply is spent inside their own
 * browser. Chrome buffers arriving audio before playing it, sized for the worst jitter it expects rather than the
 * jitter it is seeing: on these calls the network jitter measured 3-13 ms while the buffer held 60-105 ms, and it
 * opened at 310-628 ms for the first half-minute of a call -- which is why the beginning of a call feels worst.
 * Every millisecond of that sits in the return leg of a conversation.
 *
 * RTCRtpReceiver.playoutDelayHint asks for a shorter hold. It is a hint: the browser still grows the buffer when
 * packets actually arrive late, so this trades a smaller safety margin for less delay rather than forcing
 * anything. The cost of asking for too little is concealment -- the browser inventing audio for packets that
 * arrived after their moment -- which is why the concealment rate is reported alongside it. Judge a setting by
 * both numbers, never by the delay alone.
 *
 * Automatic (no hint) is the default, so nothing changes until an agent or an operator chooses otherwise.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The choices offered, in seconds. -1 means "no hint": leave the browser to its own judgement, which is what
    // every call did before this existed. The floor is deliberately not zero -- a request for no buffer at all
    // conceals on the first packet that is a millisecond late, and sounds worse than the delay it removes.
    var PLAYOUT_DELAY_OPTIONS = [-1, 0.12, 0.08, 0.04, 0.02];

    // Concealment above this share of received audio means the buffer is too small for the network it is on:
    // the browser is filling gaps often enough to be heard as roughness rather than as a shorter delay.
    var CONCEALMENT_WARNING_PERCENT = 5;

    function clampPlayoutDelay(value) {
        var seconds = typeof value === 'number' ? value : parseFloat(value);

        if (!isFinite(seconds)) {
            return -1;
        }

        // Match to an offered value rather than trusting an arbitrary number from storage or configuration.
        for (var i = 0; i < PLAYOUT_DELAY_OPTIONS.length; i++) {
            if (Math.abs(PLAYOUT_DELAY_OPTIONS[i] - seconds) < 0.0005) {
                return PLAYOUT_DELAY_OPTIONS[i];
            }
        }

        return -1;
    }

    // A short label for the capture/quality readout: "playout 40ms", or empty when the browser is left to decide.
    function describePlayoutDelay(seconds) {
        var clamped = clampPlayoutDelay(seconds);

        return clamped < 0 ? '' : 'playout ' + Math.round(clamped * 1000) + 'ms';
    }

    /*
     * Applies the hint to every audio receiver on a peer connection.
     *
     * Returns the number of receivers the hint was set on; zero means the browser does not support the hint
     * (Firefox does not) or there is no audio receiver yet, in which case the call simply keeps the browser's own
     * buffering and nothing is lost.
     */
    function applyPlayoutDelay(peerConnection, seconds) {
        if (!peerConnection || typeof peerConnection.getReceivers !== 'function') {
            return 0;
        }

        var clamped = clampPlayoutDelay(seconds);
        var applied = 0;

        peerConnection.getReceivers().forEach(function (receiver) {
            if (!receiver || !receiver.track || receiver.track.kind !== 'audio') {
                return;
            }

            // Absent in browsers that do not implement it; assigning would silently do nothing, so the caller is
            // told instead by way of the count.
            if (!('playoutDelayHint' in receiver)) {
                return;
            }

            try {
                // Clearing the hint is undefined, not a number: a receiver told "0" would hold nothing at all.
                receiver.playoutDelayHint = clamped < 0 ? undefined : clamped;
                applied++;
            } catch (error) { /* a browser that exposes the property but refuses the value */ }
        });

        return applied;
    }

    // Concealment as a percentage of the audio received in a window: the share the browser had to invent.
    // Returns -1 when the browser does not report the counters, so an absent measurement stays distinguishable
    // from a measured zero.
    function concealmentPercent(inbound, previous) {
        if (!inbound ||
            typeof inbound.concealedSamples !== 'number' ||
            typeof inbound.totalSamplesReceived !== 'number') {
            return -1;
        }

        var concealed = inbound.concealedSamples - ((previous && previous.concealedSamples) || 0);
        var received = inbound.totalSamplesReceived - ((previous && previous.totalSamplesReceived) || 0);

        if (received > 0) {
            return Math.max(0, (concealed / received) * 100);
        }

        return inbound.totalSamplesReceived > 0
            ? Math.max(0, (inbound.concealedSamples / inbound.totalSamplesReceived) * 100)
            : -1;
    }

    softPhone.PLAYOUT_DELAY_OPTIONS = PLAYOUT_DELAY_OPTIONS;
    softPhone.CONCEALMENT_WARNING_PERCENT = CONCEALMENT_WARNING_PERCENT;
    softPhone.clampPlayoutDelay = clampPlayoutDelay;
    softPhone.describePlayoutDelay = describePlayoutDelay;
    softPhone.applyPlayoutDelay = applyPlayoutDelay;
    softPhone.concealmentPercent = concealmentPercent;
}(typeof globalThis !== 'undefined' ? globalThis : window));
