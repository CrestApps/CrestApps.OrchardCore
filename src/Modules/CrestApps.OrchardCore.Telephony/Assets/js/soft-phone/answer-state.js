/*
 * What the offer's buttons show while the agent's answer is under way, and which offers are already over.
 *
 * The first click on Answer used to change nothing the agent could see for several seconds, so they clicked again and
 * again. From that click the button reads "Answering..." with a spinner and every offer button is disabled, until the
 * call connects -- or, if the answer fails or the registration it waits on never completes, until the buttons come back
 * with an error.
 *
 * An offer can reach the phone more than once -- pushed by the hub, and fetched by a request already in flight -- and
 * a copy can land after the offer was revoked or its call ended, re-opening the incoming-call modal for a call that is
 * over. Settled offers are remembered for a while so a late copy is dropped instead of flashing the modal up.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long an answer may wait on the registration before it is given up and the buttons come back.
    var ANSWER_REGISTRATION_TIMEOUT_MS = 15000;

    // How long a revoked offer, or an ended call, is remembered: far longer than any copy of it can be in flight, and
    // short enough that a call id is never held against a later, genuine offer.
    var SETTLED_OFFER_TTL_MS = 30 * 1000;

    // The offer button's look: disabled with a spinner and "Answering..." while the answer is in progress, and as it
    // was (its own label) otherwise.
    function answerButtonView(inProgress, restingLabel, labels) {
        if (!inProgress) {
            return { disabled: false, spinner: false, label: restingLabel };
        }

        return { disabled: true, spinner: true, label: (labels && labels.answering) || 'Answering…' };
    }

    // Settles like `promise`, or rejects with an error flagged `timedOut` once `ms` have passed without it settling.
    // A later settlement of `promise` is then ignored.
    function withTimeout(promise, ms) {
        return new Promise(function (resolve, reject) {
            var timer = root.setTimeout(function () {
                var error = new Error('Timed out.');
                error.timedOut = true;
                reject(error);
            }, ms);

            Promise.resolve(promise).then(function (value) {
                root.clearTimeout(timer);
                resolve(value);
            }, function (error) {
                root.clearTimeout(timer);
                reject(error);
            });
        });
    }

    function settledKeys(offer) {
        var keys = [];

        if (offer && offer.reservationId) {
            keys.push('r:' + offer.reservationId);
        }

        if (offer && offer.callId) {
            keys.push('c:' + offer.callId);
        }

        return keys;
    }

    // Remembers an offer that is over -- revoked (by reservation) or whose call ended (by call id) -- and forgets the
    // ones remembered long enough.
    function rememberSettledOffer(settled, offer, now) {
        Object.keys(settled).forEach(function (key) {
            if (now - settled[key] > SETTLED_OFFER_TTL_MS) {
                delete settled[key];
            }
        });

        settledKeys(offer).forEach(function (key) {
            settled[key] = now;
        });
    }

    function isOfferSettled(settled, offer, now) {
        return settledKeys(offer).some(function (key) {
            return typeof settled[key] === 'number' && now - settled[key] <= SETTLED_OFFER_TTL_MS;
        });
    }

    // Ignore and Voicemail post to the Contact Center and wait about a second for its answer. Live, the buttons stayed
    // clickable meanwhile, and the second click of a double-click declined the next reservation, offered the moment the
    // first was declined. A click on an offer's button is taken only once per offer, and a click within this long of the
    // last one is a double-click, whichever offer it lands on.
    var OFFER_CLICK_GUARD_MS = 800;

    // Whether a click on an offer's Ignore, Voicemail or Answer is the agent acting on `offer`.
    //   pending - the offer action under way, { reservationId, at }, or null.
    //   offer   - the offer on screen, { reservationId }.
    // Returns 'act', or 'ignore' for a repeat click on the same offer or one landing within the guard of the last.
    function offerActionClick(pending, offer, now) {
        if (!pending) {
            return 'act';
        }

        if (isOfferActionPending(pending, offer)) {
            return 'ignore';
        }

        return typeof pending.at === 'number' && now - pending.at < OFFER_CLICK_GUARD_MS ? 'ignore' : 'act';
    }

    // Whether the action under way is for this offer, so its buttons stay disabled until the request fails.
    function isOfferActionPending(pending, offer) {
        return !!(pending && offer && pending.reservationId && pending.reservationId === offer.reservationId);
    }

    softPhone.OFFER_CLICK_GUARD_MS = OFFER_CLICK_GUARD_MS;
    softPhone.offerActionClick = offerActionClick;
    softPhone.isOfferActionPending = isOfferActionPending;
    softPhone.ANSWER_REGISTRATION_TIMEOUT_MS = ANSWER_REGISTRATION_TIMEOUT_MS;
    softPhone.SETTLED_OFFER_TTL_MS = SETTLED_OFFER_TTL_MS;
    softPhone.answerButtonView = answerButtonView;
    softPhone.withTimeout = withTimeout;
    softPhone.rememberSettledOffer = rememberSettledOffer;
    softPhone.isOfferSettled = isOfferSettled;
}(typeof globalThis !== 'undefined' ? globalThis : window));
