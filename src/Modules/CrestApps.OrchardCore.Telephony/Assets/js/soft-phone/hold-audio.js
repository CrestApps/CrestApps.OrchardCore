/*
 * Hold audio for an answered call the agent puts on hold.
 *
 * Telnyx delivers this call's audio to the browser, not to a server-side leg, so the browser is the media
 * endpoint. The provider's own call.hold() renegotiates the media to inactive, which is why a held caller hears
 * dead silence and starts wondering whether the line dropped. Instead of holding at the signalling layer, this
 * keeps the media flowing and swaps the microphone track on the outbound sender for a hold-audio track: the
 * caller keeps receiving RTP, now carrying music (or a gentle comfort tone), and unhold swaps the microphone
 * back. No renegotiation, no dropped media, no silence.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 * The pure decision logic (source selection, tone plan, sender selection) and the engage/release state machine
 * are the unit-tested parts; the WebAudio/HTMLAudio engine is a thin, injectable adapter so a mistake in the
 * orchestration cannot hide behind an un-testable browser API.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The comfort tone played when no hold-music URL is configured. A quiet, intermittent dual tone reads as
    // "you are still on hold" without the fatigue of a continuous beep. These numbers are pinned by tests: the
    // gain is deliberately far below unity because this audio is echoed into the agent's own ear on some setups
    // and sent down the line to the caller, and a wrong gain is a painful surprise in both places.
    var TONE_PLAN = {
        frequencies: [440, 480], // The North American call-progress pair, gentle and familiar.
        gain: 0.05,              // Quiet. Well under unity so it can never clip or startle.
        onSeconds: 1,            // Tone on...
        offSeconds: 3            // ...then a longer silence, the cadence people read as "still waiting".
    };

    // Decide where the hold audio comes from. A trimmed, non-empty URL wins; otherwise the synthesized tone.
    function selectHoldSource(config) {
        config = config || {};

        var url = typeof config.mediaUrl === 'string' ? config.mediaUrl.trim() : '';

        if (url) {
            return { kind: 'url', url: url };
        }

        return { kind: 'tone' };
    }

    // A defensive copy of the tone plan so a caller cannot mutate the shared constant.
    function buildTonePlan() {
        return {
            frequencies: TONE_PLAN.frequencies.slice(),
            gain: TONE_PLAN.gain,
            onSeconds: TONE_PLAN.onSeconds,
            offSeconds: TONE_PLAN.offSeconds
        };
    }

    // Pick the RTCRtpSender carrying audio to the far end. A sender whose track is null still counts: the mic
    // track may already have been replaced, and that sender is exactly the one we put a track back on. A video
    // sender is never a candidate.
    function findAudioSender(senders) {
        if (!senders || !senders.length) {
            return null;
        }

        var i;

        for (i = 0; i < senders.length; i++) {
            if (senders[i] && senders[i].track && senders[i].track.kind === 'audio') {
                return senders[i];
            }
        }

        for (i = 0; i < senders.length; i++) {
            if (senders[i] && (!senders[i].track || senders[i].track.kind === 'audio')) {
                return senders[i];
            }
        }

        return null;
    }

    // Orchestrates the swap. Browser-facing but thin: everything hard is delegated to an engine so the state
    // machine here — engage once, release once, always tear the engine down, never leak a half-applied swap — is
    // exercised by tests with a fake engine and fake senders.
    function createHoldAudioController(options) {
        options = options || {};

        var source = selectHoldSource(options);
        var createEngine = typeof options.createEngine === 'function' ? options.createEngine : defaultHoldAudioEngine;

        var engine = null;
        var savedSender = null;
        var savedTrack = null;
        var engaged = false;

        function engage(peerConnection, micTrack) {
            if (engaged) {
                return Promise.resolve(true);
            }

            var senders = peerConnection && typeof peerConnection.getSenders === 'function'
                ? peerConnection.getSenders()
                : [];
            var sender = findAudioSender(senders);

            if (!sender || typeof sender.replaceTrack !== 'function') {
                return Promise.reject(new Error('hold-audio: no audio sender to replace'));
            }

            engine = createEngine(source, buildTonePlan());

            return Promise.resolve(engine.start()).then(function (holdTrack) {
                if (!holdTrack) {
                    throw new Error('hold-audio: engine produced no track');
                }

                // Remember what to put back. Prefer the caller-supplied mic track; fall back to whatever the
                // sender currently carries so release is never a no-op that leaves music playing.
                savedSender = sender;
                savedTrack = micTrack || sender.track || null;

                return sender.replaceTrack(holdTrack);
            }).then(function () {
                engaged = true;

                return true;
            }).catch(function (error) {
                // A failure mid-swap must not leave the engine running or the flag half-set.
                return teardownEngine().then(function () {
                    throw error;
                });
            });
        }

        function release(peerConnection) {
            if (!engaged) {
                return Promise.resolve(false);
            }

            var sender = savedSender || findAudioSender(
                peerConnection && typeof peerConnection.getSenders === 'function' ? peerConnection.getSenders() : []);
            var restoreTrack = savedTrack;

            var restore = sender && typeof sender.replaceTrack === 'function'
                ? Promise.resolve(sender.replaceTrack(restoreTrack)).catch(function () { })
                : Promise.resolve();

            return restore.then(teardownEngine).then(function () {
                engaged = false;

                return true;
            });
        }

        function teardownEngine() {
            savedSender = null;
            savedTrack = null;

            var stopping = engine && typeof engine.stop === 'function' ? engine.stop() : null;

            engine = null;

            return Promise.resolve(stopping).catch(function () { });
        }

        return {
            engage: engage,
            release: release,
            isEngaged: function () { return engaged; },
            source: source
        };
    }

    // The real engine: builds a MediaStreamTrack from either an <audio> element (a configured URL) or a
    // WebAudio oscillator graph (the comfort tone). Kept out of the tested surface because it is nothing but
    // browser-API plumbing; the logic that decides which one to build and when to tear it down is above.
    function defaultHoldAudioEngine(source, tonePlan) {
        var element = null;
        var audioContext = null;
        var nodes = [];

        function startUrl() {
            var audio = root.document ? root.document.createElement('audio') : null;

            if (!audio || typeof audio.play !== 'function') {
                return Promise.reject(new Error('hold-audio: cannot create audio element'));
            }

            element = audio;
            audio.src = source.url;
            audio.loop = true;
            audio.crossOrigin = 'anonymous';
            audio.preload = 'auto';

            var capture = audio.captureStream || audio.mozCaptureStream;

            if (typeof capture !== 'function') {
                return Promise.reject(new Error('hold-audio: captureStream unavailable'));
            }

            return Promise.resolve(audio.play()).then(function () {
                var stream = capture.call(audio);
                var tracks = stream && stream.getAudioTracks ? stream.getAudioTracks() : [];

                return tracks[0] || null;
            });
        }

        function startTone() {
            var Ctx = root.AudioContext || root.webkitAudioContext;

            if (typeof Ctx !== 'function') {
                return Promise.reject(new Error('hold-audio: WebAudio unavailable'));
            }

            audioContext = new Ctx();

            var destination = audioContext.createMediaStreamDestination();
            var gain = audioContext.createGain();

            gain.gain.value = 0;
            gain.connect(destination);
            nodes.push(gain);

            tonePlan.frequencies.forEach(function (frequency) {
                var oscillator = audioContext.createOscillator();

                oscillator.type = 'sine';
                oscillator.frequency.value = frequency;
                oscillator.connect(gain);
                oscillator.start();
                nodes.push(oscillator);
            });

            // Pulse the gain on and off so the tone is intermittent rather than a solid drone.
            var cycle = tonePlan.onSeconds + tonePlan.offSeconds;
            var now = audioContext.currentTime;

            for (var i = 0; i < 600 / cycle; i++) {
                var base = now + i * cycle;

                gain.gain.setValueAtTime(tonePlan.gain, base);
                gain.gain.setValueAtTime(0, base + tonePlan.onSeconds);
            }

            var track = destination.stream.getAudioTracks()[0];

            // Hold is triggered by a click, so the context is usually allowed to start running; resume anyway in
            // case an autoplay policy created it suspended, or the tone would be silent on the first hold.
            if (typeof audioContext.resume === 'function') {
                try { audioContext.resume(); } catch (error) { /* best effort */ }
            }

            return Promise.resolve(track || null);
        }

        return {
            start: function () {
                return source.kind === 'url' ? startUrl() : startTone();
            },
            stop: function () {
                if (element) {
                    try { element.pause(); } catch (error) { /* best effort */ }
                    element.src = '';
                    element = null;
                }

                nodes.forEach(function (node) {
                    try { if (typeof node.stop === 'function') { node.stop(); } } catch (error) { /* best effort */ }
                    try { node.disconnect(); } catch (error) { /* best effort */ }
                });
                nodes = [];

                if (audioContext && typeof audioContext.close === 'function') {
                    try { audioContext.close(); } catch (error) { /* best effort */ }
                }

                audioContext = null;

                return Promise.resolve();
            }
        };
    }

    softPhone.HOLD_TONE_PLAN = TONE_PLAN;
    softPhone.selectHoldSource = selectHoldSource;
    softPhone.buildTonePlan = buildTonePlan;
    softPhone.findAudioSender = findAudioSender;
    softPhone.createHoldAudioController = createHoldAudioController;
}(typeof globalThis !== 'undefined' ? globalThis : window));
