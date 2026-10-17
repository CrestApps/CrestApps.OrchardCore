// @ts-check
/*
 * The call timer. One implementation, shared by every surface that shows how long a call has been running: the
 * soft phone, the contact-center soft-phone tab, the agent bar and the agent workspace. It had two, which
 * disagreed the moment a call passed an hour.
 *
 * An ES module: telephony-client.js and the soft phone import it, the telephony client hands it to the other
 * bundles as window.telephonyClient.formatDuration, and the unit tests import it directly.
 */

export function pad(value) {
    return value < 10 ? '0' + value : String(value);
}

export function formatDuration(totalSeconds) {
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
