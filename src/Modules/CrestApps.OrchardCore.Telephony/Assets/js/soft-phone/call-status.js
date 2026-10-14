// @ts-check
/*
 * Call status text with elapsed time, and call duration for history rows.
 *
 * The soft phone said "In call" for as long as a call lasted and nothing more; an agent testing audio, or one
 * being asked how long they were on with a customer, had no way to tell twenty seconds from twenty minutes
 * without a clock of their own. The header now carries the elapsed time next to the state, ticking once a
 * second while the call is connected, and each entry in Recent carries the call's total duration.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 * Formatting is the shared call timer's (shared/call-timer.js), so every surface agrees past the hour mark.
 */
import { formatDuration } from '../shared/call-timer.js';

// The separator between the state and the elapsed time: "In call · 1:23".
export var STATUS_SEPARATOR = ' · ';

// m:ss, or h:mm:ss past the hour: the shared call timer, so the phone reads the same as every other surface.
export function formatElapsed(totalSeconds) {
    return formatDuration(totalSeconds);
}

// The header text for a call: the state, and the elapsed time when one is known. An elapsed time that is
// not a finite number (no connected moment recorded yet, or a call that is not connected) leaves the state
// alone rather than showing "In call · 0:00" for a call that is still ringing.
export function formatCallStatus(statusText, elapsedSeconds) {
    var text = statusText || '';

    if (typeof elapsedSeconds !== 'number' || !isFinite(elapsedSeconds) || elapsedSeconds < 0) {
        return text;
    }

    return text + STATUS_SEPARATOR + formatElapsed(elapsedSeconds);
}

// When a call became connected, as the soft phone should remember it. The first moment the call is seen
// connected is kept for as long as the call lasts; later state refreshes must not restart the clock, and a
// call that is not (yet) connected has no connected moment at all.
export function connectedAtFor(isConnected, previousConnectedAt, nowMs) {
    if (!isConnected) {
        return null;
    }

    return typeof previousConnectedAt === 'number' && isFinite(previousConnectedAt) ? previousConnectedAt : nowMs;
}

// The duration to show on a history row: the recorded total, or nothing for a call that never connected or
// is still going (its length is not known yet, and "0:00" next to a live call would read as a bug).
export function durationMeta(durationSeconds, inProgress) {
    if (inProgress || typeof durationSeconds !== 'number' || !isFinite(durationSeconds) || durationSeconds <= 0) {
        return '';
    }

    return formatElapsed(durationSeconds);
}
