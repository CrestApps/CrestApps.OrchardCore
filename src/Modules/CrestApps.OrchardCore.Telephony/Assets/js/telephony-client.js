/*
 * Shared browser helpers for the CrestApps telephony and contact-center clients.
 *
 * Exposes a small set of pure UI helpers on `window.telephonyClient` so the soft phone, the
 * contact-center soft-phone tab, the agent workspace, and the supervisor dashboard no longer each
 * carry their own copy. `callStateNames` mirrors the server-side
 * CrestApps.OrchardCore.Telephony.Models.CallState enum in ordinal order; a build-time guard test
 * fails when the two drift, so the C# enum stays authoritative for the wire ordinals.
 */
import { formatDuration } from './shared/call-timer.js';
import { formatPhoneNumber } from './soft-phone/format.js';
import { escapeHtml } from '../../../CrestApps.OrchardCore.Resources/Assets/js/browser/html.js';

(function (window) {
    'use strict';

    // The ordinal order of this array must match CrestApps.OrchardCore.Telephony.Models.CallState.
    // A guard test (CallStateNamesJsSyncTests) fails the build when it drifts from the enum.
    var CALL_STATE_NAMES = ['Idle', 'Connecting', 'Ringing', 'Connected', 'OnHold', 'Disconnected', 'Failed'];

    function normalizeCallState(state) {
        if (typeof state === 'number') {
            return CALL_STATE_NAMES[state] || 'Idle';
        }

        if (typeof state === 'string' && state.length) {
            return state;
        }

        return 'Idle';
    }

    window.telephonyClient = {
        callStateNames: CALL_STATE_NAMES.slice(),
        // The shared helper of the crestapps-browser toolkit, bundled in and kept here as an alias because the soft phone
        // and Contact Center read it off the telephony client. It escapes all five of & < > " ', so what it returns is
        // safe inside a quoted attribute (a caller number in data-telephony-history-number) as well as in text.
        escapeHtml: escapeHtml,
        // The call timer (shared/call-timer.js) and the phone number formatter (soft-phone/format.js), one implementation
        // each, so every surface that shows a duration or a number agrees on what it says.
        formatDuration: formatDuration,
        formatPhoneNumber: formatPhoneNumber,
        normalizeCallState: normalizeCallState
    };
})(window);
