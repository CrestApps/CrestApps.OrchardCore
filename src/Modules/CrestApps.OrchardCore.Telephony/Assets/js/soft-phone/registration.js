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
            state.hubConnected !== false;
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

    softPhone.shouldStartRegistration = shouldStartRegistration;
    softPhone.answerClickAction = answerClickAction;
    softPhone.isAnswerInProgress = isAnswerInProgress;
}(typeof globalThis !== 'undefined' ? globalThis : window));
