// @ts-check
/*
 * The reconnect schedule. Shared by the SignalR automatic-reconnect policy and the manual restart loop so both
 * back off identically and neither ever gives up: a soft phone that stops trying is a phone that stops ringing.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */

// Reconnect backoff (ms) by attempt index: immediate, then 2s, 5s, 10s, 20s, capped at 30s.
export var RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 20000, 30000];

export function reconnectDelayMs(attempt) {
    var index = attempt > 0 ? attempt : 0;

    return RECONNECT_DELAYS_MS[Math.min(index, RECONNECT_DELAYS_MS.length - 1)];
}
