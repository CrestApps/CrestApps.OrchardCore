/*
 * The reconnect schedule. Shared by the SignalR automatic-reconnect policy and the manual restart loop so both
 * back off identically and neither ever gives up: a soft phone that stops trying is a phone that stops ringing.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Reconnect backoff (ms) by attempt index: immediate, then 2s, 5s, 10s, 20s, capped at 30s.
    var RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 20000, 30000];

    function reconnectDelayMs(attempt) {
        var index = attempt > 0 ? attempt : 0;

        return RECONNECT_DELAYS_MS[Math.min(index, RECONNECT_DELAYS_MS.length - 1)];
    }

    softPhone.RECONNECT_DELAYS_MS = RECONNECT_DELAYS_MS;
    softPhone.reconnectDelayMs = reconnectDelayMs;
}(typeof globalThis !== 'undefined' ? globalThis : window));
