/*
 * The one-shot expectation that the next inbound provider leg is one this browser is waiting for, so the media
 * adapter answers it instead of ringing it as an unsolicited call.
 *
 * Two legs are expected that way: the browser's own leg of an extension call it just placed, and the leg the platform
 * rings at the accept of an offer taken somewhere else (the docked agent bar, another page) when this page held no leg
 * for it. The expectation used to be a bare deadline shared by both, so one armed for an offer outlived it: declined,
 * expired or taken by another agent, the offer was over, yet whatever leg reached the browser next within the window
 * -- a colleague's call, the next caller's -- was answered without the agent touching anything.
 *
 * An arm now names what it is for (an offer's reservation, or the extension call), is consumed by the first leg that
 * uses it, lapses after a short window, and is dropped the moment what it was armed for is over -- an offer withdrawn
 * without an accept here, or a different offer ringing on screen.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long an arm waits for its leg. The platform rings within a second or two of the accept or the dial.
    var AUTO_ANSWER_WINDOW_MS = 20000;

    // What an arm is for.
    var EXTENSION_CALL_KEY = 'extension';

    // An offer whose reservation is not known still gets a key of its own: any other offer on screen drops it.
    function offerKey(reservationId) {
        return 'offer:' + (reservationId || '');
    }

    function createAutoAnswerArm() {
        return { key: '', until: 0 };
    }

    // Arms for `key` (an offerKey() or EXTENSION_CALL_KEY), replacing any earlier arm. An arm with no key is refused.
    function armAutoAnswer(arm, key, now, windowMs) {
        if (!arm || !key) {
            return false;
        }

        arm.key = key;
        arm.until = now + (typeof windowMs === 'number' && windowMs > 0 ? windowMs : AUTO_ANSWER_WINDOW_MS);

        return true;
    }

    // Whether the leg arriving now is expected. Consumes the arm: the next leg rings unless something arms again.
    // Returns the key the leg was expected for, or '' when it was not.
    function consumeAutoAnswer(arm, now) {
        if (!arm || !arm.key) {
            return '';
        }

        var key = arm.key;
        var live = now < arm.until;

        arm.key = '';
        arm.until = 0;

        return live ? key : '';
    }

    // Drops the arm if it is for `key`.
    function disarmAutoAnswer(arm, key) {
        if (arm && arm.key && arm.key === key) {
            arm.key = '';
            arm.until = 0;
        }
    }

    // A different offer is now the one on screen: an arm left for any other offer is for one that is over.
    function disarmOtherOffers(arm, key) {
        if (arm && arm.key && arm.key !== key && arm.key !== EXTENSION_CALL_KEY) {
            arm.key = '';
            arm.until = 0;
        }
    }

    // The intent the platform stamps on a leg it rings at a call's destination: this phone is the one being called.
    var DESTINATION_LEG_INTENT = 'ob-dest';

    // The SIP header the platform adds to that leg, for an SDK that hands over no client state (live, Telnyx's did not).
    var DESTINATION_LEG_HEADER = 'x-destination-leg';

    // Whether the leg is one the platform rang at this phone as somebody's destination -- a colleague calling this
    // agent's extension -- rather than this phone's own leg of a call it placed. Read from the leg's client state, or
    // from its SIP header.
    function isDestinationLeg(options) {
        if (!options) {
            return false;
        }

        var read = softPhone.readProviderClientState;
        var state = typeof read === 'function' ? read(options.clientState || options.client_state) : null;

        if (state && state.i === DESTINATION_LEG_INTENT) {
            return true;
        }

        var readHeader = softPhone.readProviderHeader;

        return typeof readHeader === 'function' &&
            !!readHeader(options.customHeaders || options.custom_headers, DESTINATION_LEG_HEADER);
    }

    // Whether an accept made elsewhere may arm this phone for its offer's leg: only for an offer this phone was offered
    // ({ reservationId: callId }). An offer it never had is another agent's -- the server tells the offer's queue and
    // every supervisor of each accept -- and arming for it answered the next leg to arrive, whoever's it was.
    function canArmForOffer(reservationId, offeredReservations) {
        return !!reservationId && !!offeredReservations &&
            Object.prototype.hasOwnProperty.call(offeredReservations, reservationId);
    }

    // Whether an inbound leg arriving now is answered without ringing. Only an arm decides, and only once: nothing the
    // phone remembers about an earlier call does. A leg that is somebody's call to this phone -- a colleague's extension
    // call, a call handed over -- always rings, and leaves the arm for the phone's own leg.
    //   leg - { clientState | client_state, transferLeg }.
    function shouldAutoAnswerInboundLeg(arm, now, leg) {
        if (leg && (leg.transferLeg || isDestinationLeg(leg))) {
            return false;
        }

        return consumeAutoAnswer(arm, now) !== '';
    }

    softPhone.AUTO_ANSWER_WINDOW_MS = AUTO_ANSWER_WINDOW_MS;
    softPhone.isDestinationLeg = isDestinationLeg;
    softPhone.canArmForOffer = canArmForOffer;
    softPhone.shouldAutoAnswerInboundLeg = shouldAutoAnswerInboundLeg;
    softPhone.EXTENSION_CALL_KEY = EXTENSION_CALL_KEY;
    softPhone.autoAnswerOfferKey = offerKey;
    softPhone.createAutoAnswerArm = createAutoAnswerArm;
    softPhone.armAutoAnswer = armAutoAnswer;
    softPhone.consumeAutoAnswer = consumeAutoAnswer;
    softPhone.disarmAutoAnswer = disarmAutoAnswer;
    softPhone.disarmOtherOffers = disarmOtherOffers;
}(typeof globalThis !== 'undefined' ? globalThis : window));
