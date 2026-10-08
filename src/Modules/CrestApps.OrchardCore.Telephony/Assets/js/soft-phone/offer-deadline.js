/*
 * When the soft phone gives up ringing for a Contact Center offer on its own.
 *
 * The server owns an offer's deadline. When it passes, the server expires the offer, moves the caller on (voicemail,
 * back into the queue, the next agent) and tells every agent screen the offer is gone -- that revocation is what
 * stops the ringing. The phone keeps a fallback only for a revocation that never arrives, and that fallback must never
 * fire before the server's deadline: a phone that stopped ringing on its own clock while the server still held the
 * offer left the agent watching a silent phone while the caller rang on, and their status changed long after.
 *
 * So the fallback is measured on the server's clock, not the device's (the offer carries the server's time alongside
 * its deadline), and it waits a grace period past the deadline for the revocation to land first.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How long past the server's deadline the phone waits for the server's revocation before it stops ringing anyway.
    var OFFER_RING_STOP_GRACE_MS = 2000;

    // Where the offset measured for an offer is remembered, so re-rendering the same offer later does not mistake the
    // time that has passed since it arrived for clock drift.
    var OFFSET_KEY = '__crestAppsServerOffsetMs';

    function parseUtcMs(value) {
        if (!value) {
            return null;
        }

        var parsed = Date.parse(value);

        return isFinite(parsed) ? parsed : null;
    }

    // How far this device's clock runs ahead of the server's (negative when behind), from the server's time stamped on
    // a message and when this device received it. Zero when the message carried no server time.
    function computeServerOffsetMs(serverTimeUtc, receivedAtMs) {
        var serverMs = parseUtcMs(serverTimeUtc);

        return serverMs === null || !isFinite(receivedAtMs) ? 0 : receivedAtMs - serverMs;
    }

    // How long from now until the phone stops ringing on its own: the server's deadline, moved onto this device's clock
    // by the offset, plus the grace period. Never negative; null when the offer carries no deadline, in which case only
    // the server's revocation ends the ring.
    function computeOfferRingStopDelayMs(expiresUtc, serverOffsetMs, nowMs, graceMs) {
        var expiresMs = parseUtcMs(expiresUtc);

        if (expiresMs === null) {
            return null;
        }

        var grace = typeof graceMs === 'number' && graceMs >= 0 ? graceMs : OFFER_RING_STOP_GRACE_MS;
        var offset = isFinite(serverOffsetMs) ? serverOffsetMs : 0;

        return Math.max(0, expiresMs + offset + grace - nowMs);
    }

    // The same, read off an offer's properties ({ expiresUtc, serverTimeUtc }). The clock offset is measured the first
    // time the offer is seen and remembered on it.
    function offerRingStopDelayMs(properties, nowMs) {
        if (!properties) {
            return null;
        }

        if (typeof properties[OFFSET_KEY] !== 'number') {
            try {
                Object.defineProperty(properties, OFFSET_KEY, {
                    value: computeServerOffsetMs(properties.serverTimeUtc, nowMs),
                    enumerable: false,
                    configurable: true,
                    writable: true
                });
            } catch (e) {
                return computeOfferRingStopDelayMs(properties.expiresUtc, computeServerOffsetMs(properties.serverTimeUtc, nowMs), nowMs);
            }
        }

        return computeOfferRingStopDelayMs(properties.expiresUtc, properties[OFFSET_KEY], nowMs);
    }

    softPhone.OFFER_RING_STOP_GRACE_MS = OFFER_RING_STOP_GRACE_MS;
    softPhone.computeServerOffsetMs = computeServerOffsetMs;
    softPhone.computeOfferRingStopDelayMs = computeOfferRingStopDelayMs;
    softPhone.offerRingStopDelayMs = offerRingStopDelayMs;
}(typeof globalThis !== 'undefined' ? globalThis : window));
