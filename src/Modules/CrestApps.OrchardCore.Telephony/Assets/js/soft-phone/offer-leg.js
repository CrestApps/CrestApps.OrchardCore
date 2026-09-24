/*
 * Recognizing the provider leg the platform rang for a Contact Center offer that is still ringing on screen.
 *
 * Accepting an offer used to make the platform ring this browser only after the click, so the agent waited the whole
 * invite-and-answer round trip of their own phone before hearing the caller. The platform now rings the browser
 * while the offer is still ringing. That leg must not ring as a second call on top of the offer, and must not be
 * answered on arrival (the agent has not accepted anything yet): the phone holds it, answers it the moment the agent
 * accepts -- here, in another page, or on another device -- and hangs it up if they decline.
 *
 * The leg names its offer in the provider's client state (intent "cc-predial", offer "r") and in a SIP header; the
 * offer's context may also name the provider's own id for the leg. These are the pure decisions: what an incoming
 * leg says about its offer, and what to do with it given the offer on screen and the offers known to be accepted.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var PRE_DIALED_LEG_INTENT = 'cc-predial';
    var OFFER_ID_HEADER = 'x-offer-id';

    // How long an accept is remembered for a leg that has not arrived yet. The platform rings the leg within a second
    // or two of the offer; anything later is not the leg for that accept.
    var ACCEPTED_OFFER_MEMORY_MS = 60000;

    function decodeBase64(value) {
        try {
            if (typeof root.atob === 'function') {
                return root.atob(value);
            }
        } catch (error) {
            return null;
        }

        return null;
    }

    function parseObject(text) {
        try {
            var parsed = JSON.parse(text);

            return parsed && typeof parsed === 'object' ? parsed : null;
        } catch (error) {
            return null;
        }
    }

    // The provider hands client state over base64-encoded; tolerate it already decoded too.
    function readClientState(value) {
        if (!value || typeof value !== 'string') {
            return null;
        }

        var decoded = decodeBase64(value);

        return (decoded && parseObject(decoded)) || parseObject(value);
    }

    // Custom SIP headers arrive as a list of { name, value } pairs, or occasionally as a plain map.
    function readHeader(headers, name) {
        if (!headers) {
            return '';
        }

        if (Array.isArray(headers)) {
            for (var i = 0; i < headers.length; i++) {
                var header = headers[i];

                if (header && typeof header.name === 'string' && header.name.toLowerCase() === name) {
                    return header.value == null ? '' : String(header.value);
                }
            }

            return '';
        }

        if (typeof headers === 'object') {
            var keys = Object.keys(headers);

            for (var j = 0; j < keys.length; j++) {
                if (keys[j].toLowerCase() === name) {
                    return headers[keys[j]] == null ? '' : String(headers[keys[j]]);
                }
            }
        }

        return '';
    }

    // What an incoming provider leg says about the offer it was rung for: { reservationId, legId }. Either may be
    // empty. A leg that names no offer is an ordinary incoming call as far as this is concerned.
    function readOfferLegTag(options) {
        options = options || {};

        var reservationId = '';
        var state = readClientState(options.clientState || options.client_state);

        if (state && state.i === PRE_DIALED_LEG_INTENT && state.r) {
            reservationId = String(state.r);
        }

        if (!reservationId) {
            reservationId = readHeader(options.customHeaders || options.custom_headers, OFFER_ID_HEADER);
        }

        var legId = options.telnyxCallControlId || options.callControlId || '';

        return {
            reservationId: reservationId,
            legId: legId ? String(legId) : ''
        };
    }

    function rememberAcceptedOffer(accepted, reservationId, now) {
        if (!accepted || !reservationId) {
            return;
        }

        accepted[reservationId] = now;

        // Forget the old ones so the memory cannot grow for as long as the page stays open.
        Object.keys(accepted).forEach(function (id) {
            if (now - accepted[id] > ACCEPTED_OFFER_MEMORY_MS) {
                delete accepted[id];
            }
        });
    }

    function forgetAcceptedOffer(accepted, reservationId) {
        if (accepted && reservationId) {
            delete accepted[reservationId];
        }
    }

    function isOfferAccepted(accepted, reservationId, now) {
        if (!accepted || !reservationId || typeof accepted[reservationId] !== 'number') {
            return false;
        }

        return now - accepted[reservationId] <= ACCEPTED_OFFER_MEMORY_MS;
    }

    // What to do with an incoming provider leg.
    //   tag      - readOfferLegTag() of the leg.
    //   offer    - the offer on screen, { reservationId, agentLegId, accepting }, or null. accepting is true while
    //              this page's own accept is in flight or has completed.
    //   accepted - the offers known to have been accepted (rememberAcceptedOffer), from anywhere.
    // Returns null for a leg that is not an offer's (the phone's usual rules apply), otherwise
    // { action: 'hold' | 'answer', reservationId }.
    function classifyOfferLeg(tag, offer, accepted, now) {
        if (!tag) {
            return null;
        }

        var matchesOffer = !!(offer && offer.reservationId &&
            ((tag.reservationId && tag.reservationId === offer.reservationId) ||
                (tag.legId && offer.agentLegId && tag.legId === offer.agentLegId)));

        var reservationId = tag.reservationId || (matchesOffer ? offer.reservationId : '');

        if (!reservationId) {
            return null;
        }

        // The agent already said yes -- here, in another page, or on another device -- before the leg arrived.
        if (isOfferAccepted(accepted, reservationId, now) || (matchesOffer && offer.accepting)) {
            return { action: 'answer', reservationId: reservationId };
        }

        // An offer's leg, and nobody has accepted it: hold it, without ringing it and without answering it. That is
        // also what happens to a leg whose offer this page has not shown yet; the offer usually follows it.
        return { action: 'hold', reservationId: reservationId };
    }

    softPhone.readOfferLegTag = readOfferLegTag;
    softPhone.classifyOfferLeg = classifyOfferLeg;
    softPhone.rememberAcceptedOffer = rememberAcceptedOffer;
    softPhone.forgetAcceptedOffer = forgetAcceptedOffer;
    softPhone.isOfferAccepted = isOfferAccepted;
    softPhone.OFFER_LEG_CAPABILITY = 'held-offer-leg';
}(typeof globalThis !== 'undefined' ? globalThis : window));
