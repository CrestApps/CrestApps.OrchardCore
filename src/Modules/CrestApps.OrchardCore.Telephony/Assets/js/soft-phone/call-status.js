/*
 * Call status text with elapsed time, and call duration for history rows.
 *
 * The soft phone said "In call" for as long as a call lasted and nothing more; an agent testing audio, or one
 * being asked how long they were on with a customer, had no way to tell twenty seconds from twenty minutes
 * without a clock of their own. The header now carries the elapsed time next to the state, ticking once a
 * second while the call is connected, and each entry in Recent carries the call's total duration.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 * Formatting delegates to the shared call timer when its bundle is present (so every surface agrees past the
 * hour mark) and carries a matching fallback so the soft phone never shows a bare number if it is not.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The separator between the state and the elapsed time: "In call · 1:23".
    var STATUS_SEPARATOR = ' · ';

    function pad(value) {
        return value < 10 ? '0' + value : String(value);
    }

    // m:ss, or h:mm:ss past the hour. Same shape as the shared call timer, which is preferred when loaded.
    function formatElapsed(totalSeconds) {
        var shared = root.CrestAppsTelephonyShared;

        if (shared && typeof shared.formatDuration === 'function') {
            return shared.formatDuration(totalSeconds);
        }

        if (!isFinite(totalSeconds) || totalSeconds < 0) {
            totalSeconds = 0;
        }

        var seconds = Math.floor(totalSeconds % 60);
        var minutes = Math.floor((totalSeconds / 60) % 60);
        var hours = Math.floor(totalSeconds / 3600);

        return (hours > 0 ? hours + ':' + pad(minutes) : minutes) + ':' + pad(seconds);
    }

    // The header text for a call: the state, and the elapsed time when one is known. An elapsed time that is
    // not a finite number (no connected moment recorded yet, or a call that is not connected) leaves the state
    // alone rather than showing "In call · 0:00" for a call that is still ringing.
    function formatCallStatus(statusText, elapsedSeconds) {
        var text = statusText || '';

        if (typeof elapsedSeconds !== 'number' || !isFinite(elapsedSeconds) || elapsedSeconds < 0) {
            return text;
        }

        return text + STATUS_SEPARATOR + formatElapsed(elapsedSeconds);
    }

    // When a call became connected, as the soft phone should remember it. The first moment the call is seen
    // connected is kept for as long as the call lasts; later state refreshes must not restart the clock, and a
    // call that is not (yet) connected has no connected moment at all.
    function connectedAtFor(isConnected, previousConnectedAt, nowMs) {
        if (!isConnected) {
            return null;
        }

        return typeof previousConnectedAt === 'number' && isFinite(previousConnectedAt) ? previousConnectedAt : nowMs;
    }

    // The duration to show on a history row: the recorded total, or nothing for a call that never connected or
    // is still going (its length is not known yet, and "0:00" next to a live call would read as a bug).
    function durationMeta(durationSeconds, inProgress) {
        if (inProgress || typeof durationSeconds !== 'number' || !isFinite(durationSeconds) || durationSeconds <= 0) {
            return '';
        }

        return formatElapsed(durationSeconds);
    }

    softPhone.STATUS_SEPARATOR = STATUS_SEPARATOR;
    softPhone.formatElapsed = formatElapsed;
    softPhone.formatCallStatus = formatCallStatus;
    softPhone.connectedAtFor = connectedAtFor;
    softPhone.durationMeta = durationMeta;
}(typeof globalThis !== 'undefined' ? globalThis : window));
