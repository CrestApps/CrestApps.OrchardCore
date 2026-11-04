// @ts-check
/*
 * What the soft phone tells an agent when the provider's WebRTC SDK raises an error.
 *
 * The Telnyx SDK names each error (SDP_SEND_FAILED, WEBSOCKET_ERROR, ...) and words it for a developer: "SDP_SEND_FAILED:
 * Failed to send call data to server" reads as if this site failed, when it means the SDK could not deliver the call's
 * Invite or Answer over its own signaling socket to the provider, nearly always because that connection dropped. The
 * agent is told what happened and what to do, and the SDK's own words are kept for the diagnostics sent to the server,
 * which never saw these errors before.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */

var ERROR_NAME = /\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\b/;

// The errors that mean the provider's signaling connection was lost while a call was being set up or answered.
var SIGNALING_LOST_ERRORS = ['SDP_SEND_FAILED', 'WEBSOCKET_CONNECTION_FAILED', 'WEBSOCKET_ERROR', 'RECONNECTION_EXHAUSTED'];

var DEFAULT_SIGNALING_LOST = 'The call could not be connected because the connection to the phone service dropped. ' +
    'Check your network connection and try again.';

/*
 * The SDK's own text for an error event, whichever shape it arrives in.
 */
export function getProviderErrorText(error) {
    if (!error) {
        return '';
    }

    if (typeof error === 'string') {
        return error;
    }

    var inner = error.error;

    if (typeof inner === 'string') {
        return inner;
    }

    if (inner && typeof inner.message === 'string') {
        return inner.name ? inner.name + ': ' + inner.message : inner.message;
    }

    return typeof error.message === 'string' ? error.message : '';
}

/*
 * The SDK's name for the error, such as 'SDP_SEND_FAILED', or '' when it gives none.
 */
export function getProviderErrorName(error) {
    if (!error || typeof error === 'string') {
        var fromText = ERROR_NAME.exec(String(error || ''));

        return fromText ? fromText[1] : '';
    }

    var inner = error.error && typeof error.error === 'object' ? error.error : null;
    var named = (inner && inner.name) || error.name;

    if (typeof named === 'string' && ERROR_NAME.test(named)) {
        return named;
    }

    var match = ERROR_NAME.exec(getProviderErrorText(error));

    return match ? match[1] : '';
}

/*
 * The message to show the agent. strings is the soft phone's localized strings; a missing entry falls back to English.
 */
export function describeProviderError(error, strings) {
    var name = getProviderErrorName(error);

    if (SIGNALING_LOST_ERRORS.indexOf(name) !== -1) {
        return (strings && strings.providerSignalingLost) || DEFAULT_SIGNALING_LOST;
    }

    return getProviderErrorText(error) || 'Telnyx WebRTC error.';
}
