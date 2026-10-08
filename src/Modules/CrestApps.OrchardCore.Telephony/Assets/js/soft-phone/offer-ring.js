/*
 * When the soft phone rings for a Contact Center offer, and when another open page silences it.
 *
 * Answering an offer waits on the server to accept it before the call connects; the ringtone carried on through that
 * round trip after the agent had clicked Answer, so a pending accept silences it.
 *
 * Every open agent page runs its own soft phone and rings for the same offer. The page the agent answers or declines
 * in tells the others through the browser ('offer-handled' on the 'crestapps-soft-phone-offers' channel): each settles
 * the offer's leg if it is the one holding it, and a page ringing for that same call marks it handled and falls silent
 * -- only for that call, so an unrelated call ringing in another page keeps ringing.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var OFFER_CHANNEL_NAME = 'crestapps-soft-phone-offers';
    var OFFER_HANDLED_MESSAGE = 'offer-handled';

    // Whether to ring: an offer is on screen and nobody has started accepting it.
    function shouldRingForOffer(offerVisible, acceptPending) {
        return !!offerVisible && !acceptPending;
    }

    // What an 'offer-handled' message from another page means here. Returns null for anything else, otherwise
    // { reservationId, answered, marksCurrentCallHandled }: the offer whose leg to settle (may be empty), whether it
    // was answered, and whether the call this page is showing is the one that was handled.
    function readOfferHandledMessage(message, currentCallId) {
        if (!message || message.type !== OFFER_HANDLED_MESSAGE) {
            return null;
        }

        return {
            reservationId: message.reservationId || '',
            answered: !!message.answered,
            marksCurrentCallHandled: !!currentCallId && currentCallId === message.callId
        };
    }

    softPhone.OFFER_CHANNEL_NAME = OFFER_CHANNEL_NAME;
    softPhone.OFFER_HANDLED_MESSAGE = OFFER_HANDLED_MESSAGE;
    softPhone.shouldRingForOffer = shouldRingForOffer;
    softPhone.readOfferHandledMessage = readOfferHandledMessage;
}(typeof globalThis !== 'undefined' ? globalThis : window));
