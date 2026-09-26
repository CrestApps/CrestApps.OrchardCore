/*
 * Keeping the agent audible: the microphone every call sends, and a watch for a call on which nothing is sent.
 *
 * The soft phone captures the microphone once per registration and hands that one stream to every call. The provider
 * SDK treats the stream it is handed as its own: when a call ends it stops every live track of it. A stopped track
 * raises no event, so nothing noticed, and every call after the first on a registration went out on a dead track --
 * the agent heard the caller, the caller heard nothing, and the provider reported receiving no packets from the agent.
 * The SDK tells its listeners a call is being torn down just before it does so, which is the moment to take the shared
 * stream back.
 *
 * However the send track dies, a call must not carry on silently one-way. RTP carries silence as well as speech, so a
 * healthy call never stops sending for more than a packet or two: a connected call that has sent nothing for a few
 * seconds while the agent is neither muted nor holding is one the caller cannot hear. The watch says so, so the agent
 * is told at once instead of by the caller, and the call's quality record says so instead of rating it good.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long a connected call may send nothing before the agent is warned: long enough to ride out the media settling
    // as the call connects, short enough that the agent hears about it before the caller gives up on them.
    var OUTBOUND_SILENCE_MS = 5000;

    // How often the watch reads the sent-bytes counter. A stats read is cheap; the SDK itself reads them every second.
    var OUTBOUND_CHECK_INTERVAL_MS = 1000;

    // The call states in which the SDK tears a call down, stopping the tracks of options.localStream right after it
    // has told its listeners.
    var TEARDOWN_STATES = ['destroy', 'recovering'];

    // Whether a stream holds an audio track that can still carry the agent's voice.
    function hasLiveAudioTrack(stream) {
        if (!stream || typeof stream.getAudioTracks !== 'function') {
            return false;
        }

        return stream.getAudioTracks().some(function (track) {
            return !!track && track.readyState === 'live';
        });
    }

    // Takes the shared capture back from a call the SDK is about to tear down, so the SDK stops nothing of it. Returns
    // whether it did. A call still in progress keeps it, and a stream the SDK captured for itself is left to the SDK.
    function releaseSharedCapture(call, sharedStream) {
        if (!call || !sharedStream || !call.options || call.options.localStream !== sharedStream) {
            return false;
        }

        if (TEARDOWN_STATES.indexOf(call.state) === -1) {
            return false;
        }

        call.options.localStream = null;

        return true;
    }

    /*
     * Decides, one reading at a time, whether audio is leaving the call.
     *
     * observe({ now, bytesSent, trackLive, suppressed }) returns 'stalled' the first time nothing has left for the
     * threshold, 'resumed' when audio leaves again after that, and null otherwise. While suppressed (muted or on hold)
     * nothing is concluded and the window starts again afterwards.
     */
    function createOutboundAudioWatch(options) {
        var thresholdMs = options && options.thresholdMs > 0 ? options.thresholdMs : OUTBOUND_SILENCE_MS;
        var windowStart = null;
        var lastBytes = 0;
        var stalled = false;
        var everStalled = false;

        function observe(sample) {
            var now = sample && typeof sample.now === 'number' ? sample.now : 0;
            var bytes = sample && typeof sample.bytesSent === 'number' ? sample.bytesSent : 0;

            if (sample && sample.suppressed) {
                windowStart = null;

                return null;
            }

            if (windowStart === null) {
                windowStart = now;
                lastBytes = bytes;

                return null;
            }

            // A counter that went backwards is a new sender, not lost audio: take it as the new baseline.
            var progressed = sample.trackLive !== false && bytes > lastBytes;

            lastBytes = bytes;

            if (progressed) {
                windowStart = now;

                if (stalled) {
                    stalled = false;

                    return 'resumed';
                }

                return null;
            }

            if (!stalled && now - windowStart >= thresholdMs) {
                stalled = true;
                everStalled = true;

                return 'stalled';
            }

            return null;
        }

        return {
            observe: observe,
            isStalled: function () { return stalled; },
            hasStalled: function () { return everStalled; }
        };
    }

    // The audio track the call is sending, or null when no sender carries one: a call that sends nothing at all.
    function readSendTrack(peer) {
        var senders = peer && typeof peer.getSenders === 'function' ? peer.getSenders() : [];
        var i;

        for (i = 0; i < senders.length; i++) {
            if (senders[i] && senders[i].track && senders[i].track.kind === 'audio') {
                return senders[i].track;
            }
        }

        return null;
    }

    function readBytesSent(report) {
        var total = 0;

        if (report && typeof report.forEach === 'function') {
            report.forEach(function (stat) {
                if (stat && stat.type === 'outbound-rtp' && (stat.kind || stat.mediaType) === 'audio' &&
                    typeof stat.bytesSent === 'number') {
                    total += stat.bytesSent;
                }
            });
        }

        return total;
    }

    /*
     * Runs the watch over a live call's peer connection.
     *
     * options.readPeer()        - the call's RTCPeerConnection, or null until it has one.
     * options.isSuppressed()    - true while the agent is muted or holding.
     * options.onStalled(info)   - nothing left the call for the threshold; info is { trackState, bytesSent }.
     * options.onResumed()       - audio is leaving again.
     * options.thresholdMs, options.intervalMs, options.now, options.setInterval, options.clearInterval - injectable.
     *
     * Returns { stop, hasStalled }.
     */
    function startOutboundAudioMonitor(options) {
        options = options || {};

        var watch = createOutboundAudioWatch(options);
        var now = typeof options.now === 'function' ? options.now : function () { return Date.now(); };
        var schedule = typeof options.setInterval === 'function' ? options.setInterval : function (fn, ms) { return root.setInterval(fn, ms); };
        var unschedule = typeof options.clearInterval === 'function' ? options.clearInterval : function (id) { root.clearInterval(id); };
        var stopped = false;
        var reading = false;

        function tick() {
            var peer = stopped || reading || typeof options.readPeer !== 'function' ? null : options.readPeer();

            if (!peer || typeof peer.getStats !== 'function') {
                return;
            }

            var suppressed = typeof options.isSuppressed === 'function' && !!options.isSuppressed();
            var track = readSendTrack(peer);

            reading = true;

            Promise.resolve(peer.getStats()).then(function (report) {
                reading = false;

                if (stopped) {
                    return;
                }

                var bytesSent = readBytesSent(report);
                var result = watch.observe({
                    now: now(),
                    bytesSent: bytesSent,
                    trackLive: !!track && track.readyState === 'live',
                    suppressed: suppressed
                });

                if (result === 'stalled' && typeof options.onStalled === 'function') {
                    options.onStalled({ trackState: track ? track.readyState : 'none', bytesSent: bytesSent });
                } else if (result === 'resumed' && typeof options.onResumed === 'function') {
                    options.onResumed();
                }
            }, function () {
                reading = false;
            });
        }

        var timer = schedule(tick, options.intervalMs > 0 ? options.intervalMs : OUTBOUND_CHECK_INTERVAL_MS);

        return {
            stop: function () {
                if (!stopped) {
                    stopped = true;
                    unschedule(timer);
                }
            },
            isStalled: watch.isStalled,
            hasStalled: watch.hasStalled
        };
    }

    softPhone.OUTBOUND_SILENCE_MS = OUTBOUND_SILENCE_MS;
    softPhone.hasLiveAudioTrack = hasLiveAudioTrack;
    softPhone.releaseSharedCapture = releaseSharedCapture;
    softPhone.createOutboundAudioWatch = createOutboundAudioWatch;
    softPhone.startOutboundAudioMonitor = startOutboundAudioMonitor;
}(typeof globalThis !== 'undefined' ? globalThis : window));
