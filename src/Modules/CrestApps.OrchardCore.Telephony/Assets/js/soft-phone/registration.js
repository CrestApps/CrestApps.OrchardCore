/*
 * When the soft phone registers with the provider, and what a click on Answer does while it is not registered yet.
 *
 * The platform rings the agent's leg for an offer the moment the offer is made, at the credential this browser last
 * registered on. A phone that is not registered then has nothing to ring (the provider answers SIP 480), and a phone
 * that only registers once the agent clicks Answer makes that click pay for the whole registration -- microphone,
 * credential, provider login -- several seconds in which nothing on screen changed, so the agent clicked again and
 * again. These are the pure decisions: when to start registering, and whether a click registers, accepts or is a
 * repeat of one already under way.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Whether to start registering now: browser audio is in use, nothing is registered or being registered, the hub
    // that hands out the credentials is up, and the microphone is not known to be blocked -- a blocked microphone fails
    // every attempt the same way, and the agent's Retry is the way back from it.
    function shouldStartRegistration(state) {
        state = state || {};

        return !!state.browserAudioEnabled &&
            !state.registered &&
            !state.registering &&
            !state.microphoneBlocked &&
            // A microphone request an earlier attempt made is still open (a permission prompt nobody has answered, a
            // device that does not respond): another would only stack up behind it. Live, a phone asked again every
            // 35 seconds all night. The phone registers the moment that request is answered.
            !state.captureInFlight &&
            state.hubConnected !== false;
    }

    // How long to wait before registering again after `failures` failed or stalled attempts in a row: soon at first, then
    // backing off to a couple of minutes, so a phone that cannot register neither gives up nor floods the server.
    var REGISTRATION_RETRY_DELAYS_MS = [5000, 15000, 30000, 60000, 120000];

    function registrationRetryDelay(failures) {
        var index = Math.max(0, Math.min(REGISTRATION_RETRY_DELAYS_MS.length - 1, (failures || 1) - 1));

        return REGISTRATION_RETRY_DELAYS_MS[index];
    }

    // Whether the `failures`-th failure in a row is worth a line in the server log: the first, one that fails
    // differently from the one before, and every tenth of a run, so a phone failing all night leaves a trail rather
    // than a flood.
    function shouldReportRegistrationFailure(failures, message, previousMessage) {
        return failures <= 1 || message !== previousMessage || failures % 10 === 0;
    }

    // What a click on Answer does: 'ignore' while an earlier click is still being carried out (registering for it or
    // accepting it), 'register' when the phone has to register before it can take the call, and 'accept' otherwise.
    function answerClickAction(state) {
        state = state || {};

        if (state.acceptPending || state.registeringForAnswer) {
            return 'ignore';
        }

        return state.browserAudioEnabled && !state.registered ? 'register' : 'accept';
    }

    // Whether the agent's answer is under way -- from the first click, including the registration it waits on -- so
    // the phone shows it at once: the offer's buttons disabled, the ringtone silenced, the status "Connecting".
    function isAnswerInProgress(state) {
        state = state || {};

        return !!state.acceptPending || !!state.registeringForAnswer;
    }

    // What to do with the registration before it is used: 'keep' it, 'replace-capture' (a fresh microphone under the same
    // registration) or 'reregister'. Live, a Bluetooth microphone that kept dropping registered the phone again each time:
    // a new SIP credential every few minutes, one of them while a supervisor was listening, which hung their monitor leg
    // up and left the next engagement ringing the address being replaced (refused, 480). Nothing is rebuilt while a call
    // or a monitor leg is up, and a dead capture alone never needs a new credential.
    //   state - { expiring, captureDead, reregisterRequested, liveMedia }
    function planRegistrationSelfHeal(state) {
        state = state || {};

        if (state.liveMedia) {
            return 'keep';
        }

        if (state.expiring || state.reregisterRequested) {
            return 'reregister';
        }

        return state.captureDead ? 'replace-capture' : 'keep';
    }

    // How a microphone that stopped delivering is recovered: a fresh capture swapped in where it is, first, whether or
    // not anything is up; if that fails, an idle phone registers again and a phone with media up tells its user.
    //   state - { liveMedia }
    function planMicrophoneLossRecovery(state) {
        return {
            first: 'replace-capture',
            onFailure: state && state.liveMedia ? 'warn' : 'reregister'
        };
    }

    // How long a registration may take end to end -- the credential, the microphone, the registration config and the
    // provider's login -- before it is given up on and tried again. Live (2026-09-26), a supervisor's phone asked for a
    // credential at the moment the tunnel in front of the server was replaced; the reply never arrived, nothing timed
    // out, and the phone stayed "registering" for good: every later attempt joined the stalled one, so it was never
    // registered again, and every supervisor leg rang the credential it had registered before (refused, SIP 480).
    var REGISTRATION_TIMEOUT_MS = 30000;

    // How soon a registration that failed or stalled is tried again, besides the minute-long heartbeat.
    var REGISTRATION_RETRY_MS = 5000;

    // Gives a registration attempt `timeoutMs` to settle. It settles like `attempt`, or rejects with an error flagged
    // `registrationTimedOut` whose `stage` is what `stageOf()` said the attempt was doing. A session the attempt produces
    // after that is handed to `onLate`, so a stalled attempt that wakes up never leaves a registration nobody holds.
    function guardRegistration(attempt, timeoutMs, stageOf, onLate) {
        return new Promise(function (resolve, reject) {
            var timedOut = false;
            var timer = root.setTimeout(function () {
                var stage = typeof stageOf === 'function' ? (stageOf() || '') : '';
                var error = new Error('The phone did not finish registering within ' + Math.round(timeoutMs / 1000) + ' seconds' +
                    (stage ? ' (it was waiting for the ' + stage + ')' : '') + '.');

                timedOut = true;
                error.registrationTimedOut = true;
                error.stage = stage;
                reject(error);
            }, timeoutMs);

            Promise.resolve(attempt).then(function (session) {
                if (timedOut) {
                    if (typeof onLate === 'function') {
                        onLate(session);
                    }

                    return;
                }

                root.clearTimeout(timer);
                resolve(session);
            }, function (error) {
                if (timedOut) {
                    return;
                }

                root.clearTimeout(timer);
                reject(error);
            });
        });
    }

    softPhone.REGISTRATION_TIMEOUT_MS = REGISTRATION_TIMEOUT_MS;
    softPhone.REGISTRATION_RETRY_MS = REGISTRATION_RETRY_MS;
    softPhone.guardRegistration = guardRegistration;
    softPhone.registrationRetryDelay = registrationRetryDelay;
    softPhone.shouldReportRegistrationFailure = shouldReportRegistrationFailure;
    softPhone.shouldStartRegistration = shouldStartRegistration;
    softPhone.planRegistrationSelfHeal = planRegistrationSelfHeal;
    softPhone.planMicrophoneLossRecovery = planMicrophoneLossRecovery;
    softPhone.answerClickAction = answerClickAction;
    softPhone.isAnswerInProgress = isAnswerInProgress;
}(typeof globalThis !== 'undefined' ? globalThis : window));
