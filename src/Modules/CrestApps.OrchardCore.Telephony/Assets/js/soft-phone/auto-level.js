/*
 * Automatic voice level: the decision math for the gain stage voice isolation puts after its noise gate.
 *
 * Voice isolation turns the browser's automatic gain control off, because that control raises the room every
 * time the agent pauses. Nothing replaced it, so a headset microphone delivering speech at -35 to -25 dBFS went
 * out at that level -- 10 dB and more under what a caller expects -- and callers said the agent was very quiet
 * while every report showed the outgoing level at a twentieth of full scale or less. This is the replacement:
 * a gain that steers the agent's voice toward a target level, and that is only allowed to move while the agent
 * is speaking. In a pause it holds, so the room behind the agent is never raised the way the browser's control
 * raised it.
 *
 * The voice isolation chain measures the denoised signal (before this gain) every few tens of milliseconds and
 * asks nextAutoLevelGainDb for the new gain. Everything here is a pure function of its arguments so it can be
 * reasoned about, and tested, without an audio engine.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    /*
     * The defaults, in dBFS for levels and dB for gains.
     *
     * targetDb -18: the RMS level, over a ~40 ms window, that the agent's speech is steered toward. Windows inside
     *   speech read a few dB above the long-term active speech level, and the gain falls faster than it rises, so
     *   it settles where the louder words sit near the target: an active level of roughly -20 dBFS. That is where
     *   a headset with the browser's gain control on leaves speech, 3 dB above the -21 dBFS a caller already
     *   called low and dull next to a synthesized prompt (-12 to -16), and 15 dB under the limiter's ceiling,
     *   which leaves room for the crest of ordinary speech without the limiter working on every word.
     * maxGainDb +20: enough to bring a -38 dBFS voice -- quieter than any working close-talk headset -- to the
     *   target, and no more: past that, what is being raised is a microphone in the wrong place.
     * minGainDb -6 with hotDb -10: the gain never goes below unity for ordinary speech (this stage exists to lift
     *   a quiet voice, not to second-guess a normal one). Only a voice already within a few dB of full scale
     *   (window RMS above -10 dBFS) may be turned down, by up to 6 dB, so the limiter is not flattening every word.
     * riseDbPerSec 6 / fallDbPerSec 15: rises slowly, so a raised voice or a cough cannot pump the level, and
     *   falls two and a half times faster, so a loud stretch is brought down within a word or two.
     * speechThresholdDb: the level below which a window is not speech and the gain holds. The voice isolation
     *   chain passes its gate's open threshold, so the gain moves exactly when the gate lets the agent through.
     * toleranceDb 1: within this of the target nothing moves, so steady speech does not make the gain hunt.
     * maxStepMs 200: a timer the browser delayed (a background tab) must not turn into one large jump.
     */
    var AUTO_LEVEL_DEFAULTS = {
        targetDb: -18,
        maxGainDb: 20,
        minGainDb: -6,
        hotDb: -10,
        riseDbPerSec: 6,
        fallDbPerSec: 15,
        speechThresholdDb: -40,
        toleranceDb: 1,
        maxStepMs: 200
    };

    function option(options, name) {
        var value = options ? options[name] : undefined;

        return typeof value === 'number' && isFinite(value) ? value : AUTO_LEVEL_DEFAULTS[name];
    }

    function clamp(value, low, high) {
        return Math.min(high, Math.max(low, value));
    }

    // An RMS amplitude (0..1) in dBFS. Silence is -Infinity.
    function amplitudeToDb(amplitude) {
        return typeof amplitude === 'number' && amplitude > 0 ? 20 * Math.log10(amplitude) : -Infinity;
    }

    // A gain in dB as a linear factor.
    function dbToGain(db) {
        return Math.pow(10, db / 20);
    }

    // A gain clamped to the allowed range. Anything that is not a finite number is unity, never a surprise lift.
    function clampAutoLevelGainDb(gainDb, options) {
        if (typeof gainDb !== 'number' || !isFinite(gainDb)) {
            return 0;
        }

        return clamp(gainDb, option(options, 'minGainDb'), option(options, 'maxGainDb'));
    }

    /*
     * The gain, in dB, for the next stretch of audio.
     *
     * currentGainDb - the gain applied now.
     * measuredDb    - the RMS level, in dBFS, of the signal BEFORE this gain over the latest window.
     * options       - any of AUTO_LEVEL_DEFAULTS' keys; missing ones take the default.
     * elapsedMs     - how long since the previous decision; bounds how far the gain may move.
     *
     * Holds (returns the current gain) when the window is not speech, when nothing has elapsed, or when the
     * measurement is not a number. Otherwise moves toward the gain that would put the window at the target,
     * by at most the rise or fall rate for the elapsed time.
     */
    function nextAutoLevelGainDb(currentGainDb, measuredDb, options, elapsedMs) {
        var current = clampAutoLevelGainDb(currentGainDb, options);

        if (typeof measuredDb !== 'number' || !isFinite(measuredDb) || measuredDb < option(options, 'speechThresholdDb')) {
            return current;
        }

        var elapsed = typeof elapsedMs === 'number' && isFinite(elapsedMs) ? Math.min(elapsedMs, option(options, 'maxStepMs')) : 0;

        if (elapsed <= 0) {
            return current;
        }

        var error = (option(options, 'targetDb') - measuredDb) - current;

        if (Math.abs(error) <= option(options, 'toleranceDb')) {
            return current;
        }

        var seconds = elapsed / 1000;
        var next = current + clamp(error, -option(options, 'fallDbPerSec') * seconds, option(options, 'riseDbPerSec') * seconds);

        // Below unity only for a voice that is already hot. Otherwise the gain may not fall further below unity
        // than it already is -- it is never pulled up abruptly, only stopped.
        if (measuredDb <= option(options, 'hotDb')) {
            next = Math.max(next, Math.min(current, 0));
        }

        return clampAutoLevelGainDb(next, options);
    }

    softPhone.AUTO_LEVEL_DEFAULTS = AUTO_LEVEL_DEFAULTS;
    softPhone.amplitudeToDb = amplitudeToDb;
    softPhone.dbToGain = dbToGain;
    softPhone.clampAutoLevelGainDb = clampAutoLevelGainDb;
    softPhone.nextAutoLevelGainDb = nextAutoLevelGainDb;
}(typeof globalThis !== 'undefined' ? globalThis : window));
