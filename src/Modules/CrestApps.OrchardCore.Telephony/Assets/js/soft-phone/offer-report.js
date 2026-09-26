/*
 * What a server report about the call of a Contact Center offer may change on the phone.
 *
 * An offer's call is live before any agent takes it: the platform answers the caller's leg to play hold music, or an
 * automated assistant talked to them first. Some providers can only say whether a call exists, and a server that
 * read "exists" as "connected" told a phone still ringing for the offer that the call was in progress. The phone
 * took that as the call connecting: the incoming-call prompt vanished and it showed a call in progress, with the
 * customer's number and the in-call buttons, although nobody had accepted anything. When the offer then expired the
 * server kept reporting the call as live, so the phone went on showing a call to nobody.
 *
 * So a report that an offer's call is live does not end the ringing while nobody has accepted the offer -- here, in
 * another page, or on another device -- and once an offer was withdrawn without being accepted here, reports about
 * its call are not this phone's until the call is offered to it again or ends.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long a call whose offer was withdrawn is kept off the phone. A caller can wait in a queue for a long time
    // after one agent's offer expired; the call's end, or a new offer of it, forgets it sooner.
    var UNACCEPTED_OFFER_CALL_TTL_MS = 60 * 60 * 1000;

    function isLiveStateName(stateName) {
        return stateName === 'Connecting' || stateName === 'Connected' || stateName === 'OnHold';
    }

    function isAccepted(accepted, reservationId, now) {
        return typeof softPhone.isOfferAccepted === 'function' &&
            softPhone.isOfferAccepted(accepted, reservationId, now);
    }

    function prune(unaccepted, now) {
        Object.keys(unaccepted).forEach(function (callId) {
            if (now - unaccepted[callId] > UNACCEPTED_OFFER_CALL_TTL_MS) {
                delete unaccepted[callId];
            }
        });
    }

    // Remembers the call of an offer that was withdrawn (declined, expired, taken by someone else) without this phone
    // accepting it.
    function rememberUnacceptedOfferCall(unaccepted, callId, now) {
        if (!unaccepted || !callId) {
            return;
        }

        prune(unaccepted, now);
        unaccepted[callId] = now;
    }

    // The call was offered to this phone again, or it ended: it is no longer kept off the phone.
    function forgetUnacceptedOfferCall(unaccepted, callId) {
        if (unaccepted && callId) {
            delete unaccepted[callId];
        }
    }

    function isUnacceptedOfferCall(unaccepted, callId, now) {
        return !!unaccepted && !!callId && typeof unaccepted[callId] === 'number' &&
            now - unaccepted[callId] <= UNACCEPTED_OFFER_CALL_TTL_MS;
    }

    // What to do with a server report of a call's state.
    //   report     - { callId, stateName } (stateName normalized: 'Ringing', 'Connected', 'OnHold', ...).
    //   offer      - the offer ringing on screen, { callId, reservationId, accepting }, or null. accepting is true
    //                while this page's own accept is in flight or has completed.
    //   accepted   - the offers known to have been accepted (rememberAcceptedOffer), from anywhere.
    //   unaccepted - the calls of offers withdrawn without an accept here (rememberUnacceptedOfferCall).
    // Returns 'apply' (the usual handling), 'keep-ringing' (the offer stays on screen as it is) or 'ignore' (the call
    // is not on this phone).
    function planOfferCallReport(report, offer, accepted, unaccepted, now) {
        if (!report || !report.callId || !isLiveStateName(report.stateName)) {
            return 'apply';
        }

        if (offer && offer.callId === report.callId) {
            return offer.accepting || isAccepted(accepted, offer.reservationId, now) ? 'apply' : 'keep-ringing';
        }

        return isUnacceptedOfferCall(unaccepted, report.callId, now) ? 'ignore' : 'apply';
    }

    softPhone.UNACCEPTED_OFFER_CALL_TTL_MS = UNACCEPTED_OFFER_CALL_TTL_MS;
    softPhone.rememberUnacceptedOfferCall = rememberUnacceptedOfferCall;
    softPhone.forgetUnacceptedOfferCall = forgetUnacceptedOfferCall;
    softPhone.isUnacceptedOfferCall = isUnacceptedOfferCall;
    softPhone.planOfferCallReport = planOfferCallReport;
}(typeof globalThis !== 'undefined' ? globalThis : window));
