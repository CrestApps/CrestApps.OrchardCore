/*
 * The call timer. One implementation, shared by every surface that shows how long a call has been running: the
 * soft phone, the contact-center soft-phone tab, the agent bar and the agent workspace. It had two, which
 * disagreed the moment a call passed an hour.
 *
 * Concatenated ahead of telephony-client.js by the module asset pipeline and attached to a shared namespace, so
 * the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var shared = root.CrestAppsTelephonyShared = root.CrestAppsTelephonyShared || {};

    function pad(value) {
        return value < 10 ? '0' + value : String(value);
    }

    function formatDuration(totalSeconds) {
        // The elapsed time is derived from a server clock offset, so it can come out negative or non-finite for
        // a moment. A live call showing "-1:-3" reads as a broken screen; zero reads as "just started".
        if (!isFinite(totalSeconds) || totalSeconds < 0) {
            totalSeconds = 0;
        }

        var seconds = Math.floor(totalSeconds % 60);
        var minutes = Math.floor((totalSeconds / 60) % 60);
        var hours = Math.floor(totalSeconds / 3600);

        return (hours > 0 ? hours + ':' + pad(minutes) : minutes) + ':' + pad(seconds);
    }

    shared.pad = pad;
    shared.formatDuration = formatDuration;
}(typeof globalThis !== 'undefined' ? globalThis : window));
