/*
 * Which path sends a ringing call to voicemail when the agent clicks Voicemail.
 *
 * Exactly one path may do it. The soft phone used to decline a Contact Center offer (which the Contact Center turns
 * into voicemail, recording the decline and releasing the agent) and also ask the telephony hub to send the same call
 * to voicemail. Both answered the caller and both played the greeting, so the caller heard it twice before the beep.
 *
 * A call the Contact Center offered belongs to the Contact Center: it sends the caller to voicemail through the offer's
 * own action. The telephony hub is only for a plain telephony call that no Contact Center offer owns. A direct
 * extension call rung in the browser is declined locally; the server routes its caller to voicemail from that hangup.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var VOICEMAIL_ROUTES = Object.freeze({
        browserLeg: 'browser-leg',
        contactCenter: 'contact-center',
        telephony: 'telephony',
        none: 'none'
    });

    // state.browserInboundRinging: a direct extension call is ringing in the browser.
    // state.offer: the ringing offer's properties (its lifecycle actions), or nothing for a plain telephony call.
    // state.hasCall: the phone knows the ringing call, so the telephony hub can act on it.
    //
    // Returns the route, and for the Contact Center the offer property holding the action to post. An offer that
    // carries its voicemail action uses it; one that only carries a decline is declined, which for a direct-to-agent
    // line is itself what sends the caller to voicemail.
    function voicemailRoute(state) {
        var current = state || {};

        if (current.browserInboundRinging) {
            return { route: VOICEMAIL_ROUTES.browserLeg };
        }

        var offer = current.offer || {};
        var lifecycleKey = offer.voicemailUrl ? 'voicemailUrl' : (offer.declineUrl ? 'declineUrl' : null);

        if (lifecycleKey) {
            return { route: VOICEMAIL_ROUTES.contactCenter, lifecycleKey: lifecycleKey };
        }

        return { route: current.hasCall ? VOICEMAIL_ROUTES.telephony : VOICEMAIL_ROUTES.none };
    }

    softPhone.VOICEMAIL_ROUTES = VOICEMAIL_ROUTES;
    softPhone.voicemailRoute = voicemailRoute;
}(typeof globalThis !== 'undefined' ? globalThis : window));
