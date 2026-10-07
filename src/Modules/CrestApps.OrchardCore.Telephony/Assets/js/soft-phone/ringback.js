/*
 * The ringback tone the agent hears while a number they dialed is ringing.
 *
 * A number dialed from the keypad (and an extension call) is connected on the server: the platform rings this phone's
 * own leg, the phone answers it at once, and only then is the number dialed -- and joined to the agent once it answers.
 * The carrier's ringback plays on the number's leg, which is not joined to the agent until then, so the agent heard
 * nothing at all and could not tell whether the number was being dialed. The server now says where the other party
 * stands ('RemotePartyChanged': Ringing, Answered, Ended) and the phone plays a ringback of its own in between.
 *
 * Once a call's party answered or went, the call never rings again: a late or redelivered 'Ringing' is ignored. A
 * ringback that is never stopped by the server stops by itself after RINGBACK_MAX_MS.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Longer than any carrier lets a number ring unanswered; the server's own end arrives well before.
    var RINGBACK_MAX_MS = 120000;

    // The North American ringback: 440 Hz and 480 Hz together, two seconds on and four off.
    var RINGBACK_CADENCE = { frequencies: [440, 480], onSeconds: 2, offSeconds: 4 };

    var RINGING = 'ringing';
    var ANSWERED = 'answered';
    var ENDED = 'ended';

    // Must match CrestApps.OrchardCore.Telephony.Models.RemotePartyState, sent as its name or, failing that, its value.
    var STATES_BY_NAME = { ringing: RINGING, answered: ANSWERED, ended: ENDED };
    var STATES_BY_VALUE = [RINGING, ANSWERED, ENDED];

    function normalizeRemotePartyState(value) {
        if (typeof value === 'number') {
            return STATES_BY_VALUE[value] || null;
        }

        return typeof value === 'string' ? STATES_BY_NAME[value.trim().toLowerCase()] || null : null;
    }

    function createRingbackMemory() {
        return { calls: {} };
    }

    // Records where a call's other party stands. Returns the state the call is in afterwards ('ringing', 'answered' or
    // 'ended'), or null for an update that names no call or no state.
    function noteRemoteParty(memory, update, nowMs) {
        if (!memory || !update || !update.callId) {
            return null;
        }

        var state = normalizeRemotePartyState(update.state);

        if (!state) {
            return null;
        }

        var entry = memory.calls[update.callId];

        // Answered or gone is final.
        if (entry && entry.state !== RINGING) {
            return entry.state;
        }

        if (state === RINGING) {
            if (!entry) {
                memory.calls[update.callId] = { state: RINGING, since: nowMs };
            }

            return RINGING;
        }

        memory.calls[update.callId] = { state: state, since: entry ? entry.since : nowMs };

        return state;
    }

    // Forgets a call that ended.
    function forgetRemoteParty(memory, callId) {
        if (memory && callId) {
            delete memory.calls[callId];
        }
    }

    // Forgets every call the phone no longer shows.
    function pruneRemoteParties(memory, activeCallIds) {
        if (!memory) {
            return;
        }

        var keep = {};

        (activeCallIds || []).forEach(function (callId) {
            keep[callId] = true;
        });

        Object.keys(memory.calls).forEach(function (callId) {
            if (!keep[callId]) {
                delete memory.calls[callId];
            }
        });
    }

    // Whether the call's other party is ringing right now: told so, not told otherwise since, and not for too long.
    function isRemotePartyRinging(memory, callId, nowMs) {
        var entry = memory && callId ? memory.calls[callId] : null;

        return !!entry && entry.state === RINGING && nowMs - entry.since < RINGBACK_MAX_MS;
    }

    // How long until a ringing call's ringback stops by itself, or null when it is not ringing.
    function ringbackTimeLeftMs(memory, callId, nowMs) {
        return isRemotePartyRinging(memory, callId, nowMs)
            ? Math.max(0, RINGBACK_MAX_MS - (nowMs - memory.calls[callId].since))
            : null;
    }

    function isLive(call) {
        var state = call && call.state ? String(call.state) : '';

        return !!state && state !== 'Disconnected' && state !== 'Failed' && state !== 'Idle';
    }

    // Whether to play the ringback, and if not, why. Plays for the call the agent is on while its party rings; a call the
    // browser dialed itself carries the carrier's own ringback, and a held call is not in the agent's ear.
    //   memory  - the remote-party memory
    //   call    - the call the phone shows: { callId, state, isOnHold, browserOriginated }
    //   options - { nowMs, isHeld }
    // Returns { play: true } or { play: false, reason } with reason one of: 'no-call', 'browser-call', 'call-ended',
    // 'held', 'answered', 'ended', 'timeout', 'not-ringing'.
    function planRingback(memory, call, options) {
        options = options || {};

        var nowMs = typeof options.nowMs === 'number' ? options.nowMs : Date.now();

        if (!call || !call.callId) {
            return { play: false, reason: 'no-call' };
        }

        if (call.browserOriginated) {
            return { play: false, reason: 'browser-call' };
        }

        if (!isLive(call)) {
            return { play: false, reason: 'call-ended' };
        }

        if (call.isOnHold || options.isHeld) {
            return { play: false, reason: 'held' };
        }

        var entry = memory ? memory.calls[call.callId] : null;

        if (!entry) {
            return { play: false, reason: 'not-ringing' };
        }

        if (entry.state === ANSWERED || entry.state === ENDED) {
            return { play: false, reason: entry.state };
        }

        if (!isRemotePartyRinging(memory, call.callId, nowMs)) {
            return { play: false, reason: 'timeout' };
        }

        return { play: true };
    }

    softPhone.RINGBACK_MAX_MS = RINGBACK_MAX_MS;
    softPhone.RINGBACK_CADENCE = RINGBACK_CADENCE;
    softPhone.normalizeRemotePartyState = normalizeRemotePartyState;
    softPhone.createRingbackMemory = createRingbackMemory;
    softPhone.noteRemoteParty = noteRemoteParty;
    softPhone.forgetRemoteParty = forgetRemoteParty;
    softPhone.pruneRemoteParties = pruneRemoteParties;
    softPhone.isRemotePartyRinging = isRemotePartyRinging;
    softPhone.ringbackTimeLeftMs = ringbackTimeLeftMs;
    softPhone.planRingback = planRingback;
}(typeof globalThis !== 'undefined' ? globalThis : window));
