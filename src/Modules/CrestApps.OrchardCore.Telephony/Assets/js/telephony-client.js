/*
 * Shared browser helpers for the CrestApps telephony and contact-center clients.
 *
 * Exposes a small set of pure UI helpers on `window.telephonyClient` so the soft phone, the
 * contact-center soft-phone tab, the agent workspace, and the supervisor dashboard no longer each
 * carry their own copy. `callStateNames` mirrors the server-side
 * CrestApps.OrchardCore.Telephony.Models.CallState enum in ordinal order; a build-time guard test
 * fails when the two drift, so the C# enum stays authoritative for the wire ordinals.
 */
(function (window, document) {
    'use strict';

    // The ordinal order of this array must match CrestApps.OrchardCore.Telephony.Models.CallState.
    // A guard test (CallStateNamesJsSyncTests) fails the build when it drifts from the enum.
    var CALL_STATE_NAMES = ['Idle', 'Connecting', 'Ringing', 'Connected', 'OnHold', 'Disconnected', 'Failed'];

    function escapeHtml(value) {
        var node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);

        return node.innerHTML;
    }

    function pad(value) {
        return value < 10 ? '0' + value : String(value);
    }

    function formatDuration(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds < 0) {
            totalSeconds = 0;
        }

        var seconds = Math.floor(totalSeconds % 60);
        var minutes = Math.floor((totalSeconds / 60) % 60);
        var hours = Math.floor(totalSeconds / 3600);

        return (hours > 0 ? hours + ':' + pad(minutes) : minutes) + ':' + pad(seconds);
    }

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
        escapeHtml: escapeHtml,
        formatDuration: formatDuration,
        normalizeCallState: normalizeCallState
    };
})(window, document);
