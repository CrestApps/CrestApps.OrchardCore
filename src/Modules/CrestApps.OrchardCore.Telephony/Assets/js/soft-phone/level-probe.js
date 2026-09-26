/*
 * Audio level probe: how loud a stream actually is, measured identically in every browser.
 *
 * getStats already reports a capture level, but only where the browser implements RTCAudioSourceStats --
 * Chrome does, Firefox does not -- and it reports nothing at all about how loud the audio arriving from the
 * far end is. Both gaps showed up on the same call: an agent reported the caller sounding distant while every
 * transmitted metric said the connection was healthy, and the capture measurement that was supposed to cover
 * the other direction came back unavailable because the call was answered in Firefox.
 *
 * So loudness is measured here instead of asked for: an AnalyserNode over the stream, sampled continuously
 * and reduced to a peak per reporting window. Peak rather than average, because the question is "was there
 * ever speech in this window", and an average over eight seconds of a normal conversation is mostly silence.
 *
 * The value is an RMS amplitude in 0..1, which is NOT the same scale as RTCAudioSourceStats.audioLevel --
 * conversational speech peaks around 0.1-0.3 here. The two are reported as separate fields for that reason,
 * rather than merged into one number that would mean different things depending on the browser.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit
 * tests. The WebAudio pieces are injectable so the accumulate/peak/reset logic can be tested without a
 * browser.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How often the waveform is read. Fast enough that a brief utterance inside a long reporting window is not
    // missed, slow enough to cost nothing next to the call itself.
    var LEVEL_PROBE_INTERVAL_MS = 100;

    // Reported when no measurement could be taken, matching the rest of the quality contract: an absent
    // measurement must stay distinguishable from a measured silence, because the two call for opposite
    // responses.
    var LEVEL_UNKNOWN = -1;

    // The peak RMS below which a window contained nothing a person would call speech. Room tone on a live
    // microphone, and comfort noise on a live call, both sit above it.
    var LEVEL_SILENT = 0.005;

    // Root-mean-square amplitude of one waveform frame.
    function frameRms(samples, length) {
        var total = 0;

        for (var i = 0; i < length; i++) {
            total += samples[i] * samples[i];
        }

        return Math.sqrt(total / length);
    }

    /*
     * Watches one MediaStream and accumulates its level until read.
     *
     * options.audioContext  - AudioContext constructor (defaults to the browser's).
     * options.timers        - object with setInterval/clearInterval (defaults to the global).
     *
     * Returns null when the stream carries no audio or the browser has no usable AudioContext -- a caller that
     * gets null reports the level as unknown rather than as zero.
     */
    function createLevelProbe(stream, options) {
        var settings = options || {};
        var AudioCtx = settings.audioContext || root.AudioContext || root.webkitAudioContext;
        var timers = settings.timers || root;

        if (!AudioCtx || !stream || typeof stream.getAudioTracks !== 'function' || !stream.getAudioTracks().length) {
            return null;
        }

        var context;
        var analyser;
        var source;
        var sink;
        var data;

        try {
            context = new AudioCtx();
            source = context.createMediaStreamSource(stream);
            analyser = context.createAnalyser();
            analyser.fftSize = 512;
            source.connect(analyser);

            // Firefox only pulls an audio graph that reaches the context destination. Left unconnected, the
            // analyser is never processed and every read comes back as digital silence -- which is far worse
            // than no measurement, because it looks exactly like a dead microphone on a call that is working.
            // Chrome pulls an analyser regardless, which is precisely why this hid until a call was answered
            // in Firefox and both directions reported 0.000 while the two people could hear each other.
            //
            // The route to the destination is through a silent gain stage: the graph runs, and nothing is
            // added to what the agent hears. That matters most for the far end's audio, which the remote
            // element is already playing -- routing it here at any audible gain would play it twice.
            sink = context.createGain();
            sink.gain.value = 0;
            analyser.connect(sink);
            sink.connect(context.destination);

            data = new Float32Array(analyser.fftSize);
        } catch (error) {
            // A stream with no usable audio, or a browser that refuses the graph. Unknown, not silent.
            return null;
        }

        // An AudioContext created outside a user gesture starts suspended, and a suspended graph reads as pure
        // silence -- which would be indistinguishable from a dead microphone. Ask for it to run; if the request
        // is refused the reads stay at zero and the caller sees a silent window, so also re-check on read.
        function resume() {
            if (context.state === 'suspended' && typeof context.resume === 'function') {
                try {
                    Promise.resolve(context.resume()).catch(function () { });
                } catch (error) { /* best effort */ }
            }
        }

        resume();

        var peak = 0;
        var sum = 0;
        var reads = 0;
        var disposed = false;

        var timer = timers.setInterval(function () {
            if (disposed) {
                return;
            }

            try {
                analyser.getFloatTimeDomainData(data);
            } catch (error) {
                return;
            }

            var rms = frameRms(data, data.length);

            peak = Math.max(peak, rms);
            sum += rms;
            reads++;
        }, LEVEL_PROBE_INTERVAL_MS);

        return {
            /*
             * Returns the window since the previous read and starts a new one:
             *   peak    - loudest frame in the window (the one to judge "was anything heard" on)
             *   average - mean across the window, dominated by the silence between words
             *   reads   - how many frames went into it; zero means the window produced no measurement
             */
            read: function () {
                resume();

                if (!reads) {
                    return { peak: 0, average: 0, reads: 0 };
                }

                var window = { peak: peak, average: sum / reads, reads: reads };

                peak = 0;
                sum = 0;
                reads = 0;

                return window;
            },
            // The audio engine's state: a probe whose context never left "suspended" reads silence however loud
            // the track is, which is indistinguishable from a dead microphone without it.
            state: function () {
                return disposed ? 'disposed' : context.state;
            },
            dispose: function () {
                if (disposed) {
                    return;
                }

                disposed = true;
                timers.clearInterval(timer);

                try {
                    source.disconnect();
                    analyser.disconnect();
                    sink.disconnect();
                } catch (error) { /* best effort */ }

                try {
                    if (typeof context.close === 'function') {
                        context.close();
                    }
                } catch (error) { /* best effort */ }
            }
        };
    }

    // Whether a captured track can deliver audio at all. A track is 'live' until it is stopped, but a source
    // that has stopped feeding it -- a device asleep, unplugged, or switched away by the OS -- leaves it live and
    // MUTED, and a muted track produces no frames: the encoder sends nothing, and the far end hears nothing. A
    // check on readyState alone waved exactly such a track through onto a call that then ran thirty seconds with
    // zero bytes sent.
    function isTrackDeliverable(track) {
        return !!track && track.readyState === 'live' && !track.muted;
    }

    // Whether the capture probe has to be rebuilt to measure the track that is being sent now.
    //
    // A probe binds to the track it was built on and never follows a swap: createMediaStreamSource takes the
    // stream's track once, and a track later removed from that stream, stopped, or replaced on the sender leaves
    // the probe reading the old one. A stopped track feeds the graph zero-filled frames, so the probe does not
    // go quiet -- it reports a confident 0.000 for the rest of the call. That is what every mid-call microphone
    // change did (device picked, boost or processing changed, dead microphone recovered): OutLevel read 0.000
    // while the far end measured the agent at around 0.3.
    //
    // Rebuild when there is a live track to measure and it is not the one the probe was built on. An ended
    // current track is not worth rebuilding onto -- it would measure the same silence -- and no track at all
    // leaves nothing to bind to.
    function captureProbeNeedsRebuild(probeTrackId, currentTrackId, currentTrackState) {
        if (!currentTrackId || currentTrackState === 'ended') {
            return false;
        }

        return probeTrackId !== currentTrackId;
    }

    // The audio track the soft phone is receiving: whatever the live receiver carries, else the remote element's
    // stream. Like the sender, the receiver is the authority: the provider SDK can replace the stream on the remote
    // element during a call, and a probe left on the first one read 0.000 for the rest of the call while the agent
    // could hear the caller.
    function selectReceiveTrack(receivers, remoteStream) {
        var receiver = (receivers || []).filter(function (candidate) {
            return candidate && candidate.track && candidate.track.kind === 'audio';
        })[0];

        if (receiver) {
            return receiver.track;
        }

        return remoteStream && typeof remoteStream.getAudioTracks === 'function'
            ? remoteStream.getAudioTracks()[0] || null
            : null;
    }

    // Whether the incoming level probe has to be rebuilt to measure the track being received now: the same rule as
    // the capture probe, applied to the receiver's track.
    function inboundProbeNeedsRebuild(probeTrackId, receiveTrack) {
        return captureProbeNeedsRebuild(probeTrackId,
            receiveTrack ? receiveTrack.id : null,
            receiveTrack ? receiveTrack.readyState : null);
    }

    // The level to report for a probe window: the peak, or unknown when the window produced no frames at all.
    // A window that was measured and found silent reports 0, which is a finding; a window that could not be
    // measured reports -1, which is not.
    function probeLevel(probe) {
        if (!probe) {
            return LEVEL_UNKNOWN;
        }

        var window = probe.read();

        return window && window.reads ? window.peak : LEVEL_UNKNOWN;
    }

    softPhone.LEVEL_PROBE_INTERVAL_MS = LEVEL_PROBE_INTERVAL_MS;
    softPhone.LEVEL_UNKNOWN = LEVEL_UNKNOWN;
    softPhone.LEVEL_SILENT = LEVEL_SILENT;
    softPhone.frameRms = frameRms;
    softPhone.createLevelProbe = createLevelProbe;
    softPhone.probeLevel = probeLevel;
    softPhone.isTrackDeliverable = isTrackDeliverable;
    softPhone.captureProbeNeedsRebuild = captureProbeNeedsRebuild;
    softPhone.selectReceiveTrack = selectReceiveTrack;
    softPhone.inboundProbeNeedsRebuild = inboundProbeNeedsRebuild;
}(typeof globalThis !== 'undefined' ? globalThis : window));
