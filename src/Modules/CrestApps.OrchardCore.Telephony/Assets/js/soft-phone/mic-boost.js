/*
 * Microphone boost: a gain stage with a limiter in front of the encoder.
 *
 * The far end of a call compared the agent's live voice with the platform's text-to-speech and found the voice
 * low and dull -- the speech was leaving the browser at about -21 dBFS active level, against the -12 to -16 that
 * a synthesized prompt carries, after Chrome's automatic gain control had already done what it could. Nothing
 * downstream changes level, so the gap arrives at the caller intact. This closes it in the one place the soft
 * phone controls: the capture is routed through a gain node and a hard limiter and the limiter's output is what
 * the call sends. The limiter is what makes the boost safe to leave on: a loud word hits the ceiling and is held
 * there for a few milliseconds instead of clipping.
 *
 * Off by default, and off means no graph at all -- the capture is sent as it is, exactly as before this existed.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 * The AudioContext is injectable so the graph wiring can be tested without a browser.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The boosts an agent can choose. Bounded above because the limiter can hold peaks but cannot make a
    // twelve-decibel lift of a quiet room sound like anything but a quiet room amplified.
    var MIC_BOOST_OPTIONS_DB = [0, 3, 6, 9, 12];

    // Limiter settings: a hard knee just under full scale, fast attack, short release. Speech peaks that the gain
    // pushes past the ceiling are caught within a few milliseconds and let go quickly enough not to pump.
    var LIMITER_THRESHOLD_DB = -3;
    var LIMITER_KNEE_DB = 0;
    var LIMITER_RATIO = 20;
    var LIMITER_ATTACK_S = 0.003;
    var LIMITER_RELEASE_S = 0.05;

    // Where the soft clipper after the limiter starts to round peaks off (linear, about -0.9 dBFS). The limiter's
    // own output sits under it except for the milliseconds before it reacts.
    var CLIPPER_KNEE = 0.9;

    // Normalizes a stored or chosen value to one of the allowed boosts. Anything unrecognized is off, never a
    // surprise lift.
    function clampBoostDb(value) {
        var db = typeof value === 'number' ? value : parseInt(value, 10);

        return MIC_BOOST_OPTIONS_DB.indexOf(db) === -1 ? 0 : db;
    }

    // The linear gain for a boost in decibels.
    function boostGainFor(db) {
        return Math.pow(10, clampBoostDb(db) / 20);
    }

    // A short label for the capture-format readout: "boost+6".
    function describeBoost(db) {
        var clamped = clampBoostDb(db);

        return clamped > 0 ? 'boost+' + clamped : '';
    }

    /*
     * Adds the boost stage -- gain, then the limiter -- to a graph that is being built, after `input`. Returns the
     * node the rest of the graph continues from and the nodes it created (for disposal), or null when the boost is
     * off and nothing was added. Shared by the plain boost below and the voice isolation chain
     * (voice-isolation.js), so there is one gain/limiter definition whichever path the capture takes.
     *
     * options.alwaysLimit - add the limiter even when the boost is off. The voice isolation chain asks for it
     *                       while its automatic voice level is on: that gain can lift a voice by up to 20 dB and
     *                       has to be exactly as safe as a boost the agent chose.
     */
    function connectBoostStage(context, input, boostDb, options) {
        var db = clampBoostDb(boostDb);
        var alwaysLimit = !!(options && options.alwaysLimit);

        if (db === 0 && !alwaysLimit) {
            return null;
        }

        var nodes = [];
        var tail = input;

        if (db !== 0) {
            var gain = context.createGain();
            gain.gain.value = boostGainFor(db);
            tail.connect(gain);
            tail = gain;
            nodes.push(gain);
        }

        var limiter = context.createDynamicsCompressor();
        limiter.threshold.value = LIMITER_THRESHOLD_DB;
        limiter.knee.value = LIMITER_KNEE_DB;
        limiter.ratio.value = LIMITER_RATIO;
        limiter.attack.value = LIMITER_ATTACK_S;
        limiter.release.value = LIMITER_RELEASE_S;
        tail.connect(limiter);
        nodes.push(limiter);
        tail = limiter;

        // The compressor needs a few milliseconds to react, and a word that starts far louder than the gain was set
        // for (an agent who suddenly raises their voice while the gain is lifting a quiet one) overshot full scale
        // by 6 dB in a measured run before it caught up -- clipped at the encoder. A soft clipper after it takes
        // those first milliseconds: transparent below the knee, it rounds anything above it off under full scale.
        if (typeof context.createWaveShaper === 'function') {
            var clipper = context.createWaveShaper();
            clipper.curve = softClipCurve();
            clipper.oversample = '2x';
            tail.connect(clipper);
            nodes.push(clipper);
            tail = clipper;
        }

        return { output: tail, nodes: nodes };
    }

    // The soft clipper's transfer curve: the identity up to CLIPPER_KNEE, then a tanh shoulder that reaches
    // just under full scale at an input of 1. A wave shaper holds anything beyond +/-1 at the curve's ends.
    var clipCurve = null;

    function softClipCurve() {
        if (clipCurve) {
            return clipCurve;
        }

        var points = 4097;
        var curve = new Float32Array(points);

        for (var i = 0; i < points; i++) {
            var x = (i / (points - 1)) * 2 - 1;
            var magnitude = Math.abs(x);
            var y = magnitude <= CLIPPER_KNEE
                ? magnitude
                : CLIPPER_KNEE + (1 - CLIPPER_KNEE) * Math.tanh((magnitude - CLIPPER_KNEE) / (1 - CLIPPER_KNEE));

            curve[i] = x < 0 ? -y : y;
        }

        clipCurve = curve;

        return curve;
    }

    /*
     * Builds the send stream for a captured microphone stream.
     *
     * With no boost, the source stream is returned as the send stream and there is nothing to dispose. With a
     * boost, the source is routed through gain -> limiter -> a MediaStreamAudioDestinationNode, and that node's
     * stream is returned; its track is what the call should send.
     *
     * options.audioContext - AudioContext constructor (defaults to the browser's).
     *
     * Returns { stream, boosted, dispose } -- dispose releases the graph (and is a no-op when nothing was built).
     */
    function createBoostPipeline(sourceStream, boostDb, options) {
        var db = clampBoostDb(boostDb);
        var settings = options || {};
        var AudioCtx = settings.audioContext || root.AudioContext || root.webkitAudioContext;

        if (!sourceStream || db === 0 || !AudioCtx) {
            return { stream: sourceStream, boosted: false, dispose: function () { } };
        }

        var context;
        var source;
        var stage;
        var destination;

        try {
            context = new AudioCtx();
            source = context.createMediaStreamSource(sourceStream);
            stage = connectBoostStage(context, source, db);
            destination = context.createMediaStreamDestination();
            stage.output.connect(destination);

            // A context created outside a user gesture starts suspended and a suspended graph is silence; the
            // switch that builds this runs from a settings change or a call, so the request is normally granted.
            if (context.state === 'suspended' && typeof context.resume === 'function') {
                try {
                    Promise.resolve(context.resume()).catch(function () { });
                } catch (error) { /* best effort */ }
            }
        } catch (error) {
            // A browser that refuses any part of the graph: send the capture as it is rather than nothing.
            try {
                if (context && typeof context.close === 'function') {
                    context.close();
                }
            } catch (closeError) { /* best effort */ }

            return { stream: sourceStream, boosted: false, dispose: function () { } };
        }

        var disposed = false;

        return {
            stream: destination.stream,
            boosted: true,
            dispose: function () {
                if (disposed) {
                    return;
                }

                disposed = true;

                try {
                    source.disconnect();
                    stage.nodes.forEach(function (node) {
                        node.disconnect();
                    });
                } catch (error) { /* best effort */ }

                try {
                    if (typeof context.close === 'function') {
                        context.close();
                    }
                } catch (error) { /* best effort */ }
            }
        };
    }

    softPhone.MIC_BOOST_OPTIONS_DB = MIC_BOOST_OPTIONS_DB;
    softPhone.clampBoostDb = clampBoostDb;
    softPhone.boostGainFor = boostGainFor;
    softPhone.describeBoost = describeBoost;
    softPhone.connectBoostStage = connectBoostStage;
    softPhone.createBoostPipeline = createBoostPipeline;
}(typeof globalThis !== 'undefined' ? globalThis : window));
