/*
 * Where a warm transfer's consult stands, as the transfer panel shows it.
 *
 * A warm transfer used to be a single request with nothing after it: the agent could not see whether the destination
 * had answered, and had no way to hand the call over or take it back. The consult is now a state the panel follows --
 * ringing, then connected, then completed or cancelled -- and these decide what the agent is told and which of
 * Complete and Cancel they may press at each point.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How often the panel asks where a live consult stands. The destination answering is what the agent waits on.
    var CONSULT_POLL_INTERVAL_MS = 1500;

    function label(strings, key, fallback) {
        return strings && typeof strings[key] === 'string' && strings[key] ? strings[key] : fallback;
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    function isLive(status) {
        return status === 'ringing' || status === 'connected';
    }

    // { status, live, message, canComplete, canCancel } for a consult ({ id, status, live }) with `targetName`.
    function consultView(consult, targetName, strings) {
        var status = consult && consult.status ? String(consult.status) : 'cancelled';
        var live = !!consult && consult.live !== false && isLive(status);
        var name = targetName || '';
        var message;

        switch (status) {
            case 'connected':
                message = format(label(strings, 'consultConnected', 'Talking to {0}. The caller is on hold.'), name);
                break;
            case 'completed':
                message = format(label(strings, 'consultCompleted', 'The call was handed to {0}.'), name);
                break;
            case 'ringing':
                message = format(label(strings, 'consultRinging', 'Calling {0}...'), name);
                break;
            default:
                message = label(strings, 'consultCancelled', 'The caller is back with you.');
                break;
        }

        return {
            status: status,
            live: live,
            message: message,

            // Only a destination who answered can take the caller; completing a ringing consult would hand them to
            // a phone nobody has picked up.
            canComplete: live && status === 'connected',
            canCancel: live
        };
    }

    // What to tell the agent when a consult they did not end has ended, or '' while it is still going.
    //   previousStatus - the status the panel last showed.
    //   next           - the consult as the server now reports it.
    //   context        - { callEnded } whether the customer's call is gone.
    function consultEndedMessage(previousStatus, next, targetName, strings, context) {
        var status = next && next.status ? String(next.status) : 'cancelled';

        if (next && next.live !== false && isLive(status)) {
            return '';
        }

        if (context && context.callEnded) {
            return label(strings, 'consultCallerLeft', 'The caller hung up.');
        }

        if (status === 'completed') {
            return format(label(strings, 'consultCompleted', 'The call was handed to {0}.'), targetName || '');
        }

        if (isLive(previousStatus)) {
            return format(label(strings, 'consultTargetLeft', '{0} did not take the call. The caller is back with you.'), targetName || '');
        }

        return label(strings, 'consultCancelled', 'The caller is back with you.');
    }

    softPhone.CONSULT_POLL_INTERVAL_MS = CONSULT_POLL_INTERVAL_MS;
    softPhone.consultView = consultView;
    softPhone.consultEndedMessage = consultEndedMessage;
}(typeof globalThis !== 'undefined' ? globalThis : window));
