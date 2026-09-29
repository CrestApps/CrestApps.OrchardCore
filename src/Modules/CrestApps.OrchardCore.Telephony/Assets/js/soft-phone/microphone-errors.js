/*
 * Why the browser would not give the soft phone a microphone.
 *
 * getUserMedia rejects with a handful of error names, and one of them, NotAllowedError, covers several unrelated
 * causes: the agent blocked the site, the agent dismissed the prompt, the operating system blocks the browser from
 * every microphone (Windows "Let apps access your microphone" is off), or the page is embedded where microphone
 * access is not delegated. Each has a different fix, and telling an agent to "allow microphone access in your
 * browser" when the browser already allows it sends them looking in the wrong place. Observed live: a dial failed
 * with that message, and the only thing logged was "NotAllowedError".
 *
 * So the soft phone gathers what it can see (the error's own message, the site's permission state, whether the page
 * is a secure context, whether the permissions policy allows the microphone, how many inputs are present), names the
 * cause from those facts, and reports all of them, so the agent is told the real reason and support can see it too.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var PERMISSION_ERRORS = ['NotAllowedError', 'PermissionDeniedError', 'SecurityError'];
    var DEVICE_MISSING_ERRORS = ['NotFoundError', 'DevicesNotFoundError', 'OverconstrainedError'];
    var DEVICE_BUSY_ERRORS = ['NotReadableError', 'TrackStartError', 'AbortError'];

    // Chrome says "Permission denied by system" when the operating system refuses; Firefox says the request "is not
    // allowed by the user agent or the platform". Safari gives no such hint.
    var SYSTEM_REFUSAL = /(by system|by the platform|or the platform|operating system)/i;

    /*
     * Names the cause of a capture failure.
     *
     * facts: { name, message, permissionState ('granted' | 'denied' | 'prompt' | null), isSecureContext,
     *          policyAllowed (true | false | null when unknown), audioInputCount (number | null when unknown) }
     *
     * Returns one of:
     *   'insecure'       the page is not a secure context, so no browser offers a microphone at all;
     *   'policy'         the page is embedded where the microphone is not delegated, or the browser refused it as a
     *                    security matter;
     *   'system-denied'  the operating system blocks the browser from the microphone;
     *   'site-denied'    the agent (or an administrator policy) blocked this site;
     *   'dismissed'      the browser asked and the prompt was dismissed or closed;
     *   'not-found'      there is no microphone, or the chosen one is gone;
     *   'in-use'         a microphone is there but could not be started, usually because another application has it;
     *   'unknown'        anything else.
     */
    function classifyMicrophoneError(facts) {
        var details = facts || {};
        var name = details.name || '';
        var message = details.message || '';

        if (details.isSecureContext === false) {
            return 'insecure';
        }

        if (PERMISSION_ERRORS.indexOf(name) >= 0) {
            if (details.policyAllowed === false || name === 'SecurityError') {
                return 'policy';
            }

            if (SYSTEM_REFUSAL.test(message)) {
                return 'system-denied';
            }

            // The site holds the permission and capture was still refused: something below the browser said no.
            if (details.permissionState === 'granted') {
                return 'system-denied';
            }

            if (details.permissionState === 'prompt') {
                return 'dismissed';
            }

            return 'site-denied';
        }

        if (DEVICE_MISSING_ERRORS.indexOf(name) >= 0 || details.audioInputCount === 0) {
            return 'not-found';
        }

        if (DEVICE_BUSY_ERRORS.indexOf(name) >= 0) {
            return 'in-use';
        }

        return 'unknown';
    }

    // The browser's own words for the failure, as shown to the agent after the explanation.
    function describeBrowserError(facts) {
        var details = facts || {};
        var name = details.name || 'Error';

        return details.message ? name + ': ' + details.message : name;
    }

    // Every fact the classification used, on one line, for the server log.
    function describeMicrophoneFacts(facts) {
        var details = facts || {};

        function known(value) {
            return value === null || value === undefined ? 'unknown' : String(value);
        }

        return [
            'error=' + describeBrowserError(details),
            'permission=' + known(details.permissionState),
            'secureContext=' + known(details.isSecureContext),
            'policy=' + (details.policyAllowed === true ? 'allowed' : details.policyAllowed === false ? 'blocked' : 'unknown'),
            'audioInputs=' + known(details.audioInputCount),
            'origin=' + known(details.origin),
            'browser=' + known(details.userAgent),
        ].join('; ');
    }

    softPhone.classifyMicrophoneError = classifyMicrophoneError;
    softPhone.describeBrowserError = describeBrowserError;
    softPhone.describeMicrophoneFacts = describeMicrophoneFacts;
}(typeof globalThis !== 'undefined' ? globalThis : window));
