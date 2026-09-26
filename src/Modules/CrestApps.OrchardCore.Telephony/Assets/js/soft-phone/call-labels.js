/*
 * What the phone calls each line: a name or a number, never an id.
 *
 * Live, a conference row read "v3:UyLVvJ3o7qQklQFQnZpvgJylmV..." -- the provider's id for the agent's own leg -- where
 * the dialed number belonged, and the phone sometimes showed the conference's id for a moment. The phone reads its calls
 * again every few seconds, and a provider's report of a call may say nothing of whom it is with; the row then fell back
 * to the call's id. The numbers a call was first reported with are now remembered and stamped back onto a report that
 * has none, and a label that is still only an id is replaced by a plain word ("Participant", "Caller").
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // A provider's call control id ("v3:..."), a conference name the platform makes ("conf-...", "ext-...",
    // "consult-...", "cc-park-..."), a UUID, or a test harness's call id.
    var ID_PATTERNS = [
        /^v\d+:/i,
        /^(conf|ext|consult|cc-[a-z]+)-/i,
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
        /^call-[0-9a-f-]{8,}$/i
    ];

    function looksLikeId(value) {
        return ID_PATTERNS.some(function (pattern) { return pattern.test(value); });
    }

    // The label to show, or `fallback` when it is empty or only an id.
    function friendlyCallLabel(label, fallback) {
        var text = label == null ? '' : String(label).trim();

        return text && !looksLikeId(text) ? text : (fallback || '');
    }

    function createCallPartyMemory() {
        return {};
    }

    var PARTY_KEYS = ['from', 'to', 'direction'];

    // Remembers whom a call is with from a report that says, and stamps it onto one that does not.
    function carryCallParty(memory, call) {
        if (!call || !call.callId || !memory) {
            return call;
        }

        var remembered = Object.prototype.hasOwnProperty.call(memory, call.callId) ? memory[call.callId] : {};

        PARTY_KEYS.forEach(function (key) {
            var value = call[key];

            if (value != null && value !== '') {
                remembered[key] = value;
            } else if (remembered[key] != null) {
                call[key] = remembered[key];
            }
        });

        if (Object.keys(remembered).length) {
            memory[call.callId] = remembered;
        }

        return call;
    }

    function forgetCallParty(memory, callId) {
        if (memory && callId) {
            delete memory[callId];
        }
    }

    softPhone.friendlyCallLabel = friendlyCallLabel;
    softPhone.createCallPartyMemory = createCallPartyMemory;
    softPhone.carryCallParty = carryCallParty;
    softPhone.forgetCallParty = forgetCallParty;
}(typeof globalThis !== 'undefined' ? globalThis : window));
