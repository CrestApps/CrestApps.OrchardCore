import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';

const {
    readOfferLegTag,
    classifyOfferLeg,
    rememberAcceptedOffer,
    forgetAcceptedOffer,
    isOfferAccepted,
} = globalThis.CrestAppsSoftPhone;

const clientState = state => Buffer.from(JSON.stringify(state), 'utf8').toString('base64');

// The platform rings the browser with the offer's leg while the offer is still on screen. Reading the leg wrong
// either rings the agent twice for one call, or answers a call they never accepted.
describe('readOfferLegTag', () => {
    it('reads the offer from the client state the platform attached', () => {
        const tag = readOfferLegTag({
            clientState: clientState({ i: 'cc-predial', p: 'caller-1', r: 'res-1' }),
            telnyxCallControlId: 'leg-1',
        });

        expect(tag).toEqual({ reservationId: 'res-1', legId: 'leg-1' });
    });

    it('reads the offer from the SIP header when the client state is not surfaced', () => {
        const tag = readOfferLegTag({
            customHeaders: [{ name: 'X-Offer-Id', value: 'res-2' }],
            telnyxCallControlId: 'leg-2',
        });

        expect(tag.reservationId).toBe('res-2');
    });

    it('matches the header name regardless of case, and accepts a plain map', () => {
        expect(readOfferLegTag({ customHeaders: [{ name: 'x-offer-id', value: 'res-3' }] }).reservationId).toBe('res-3');
        expect(readOfferLegTag({ customHeaders: { 'X-OFFER-ID': 'res-4' } }).reservationId).toBe('res-4');
    });

    it('accepts client state that is already decoded', () => {
        const tag = readOfferLegTag({ clientState: JSON.stringify({ i: 'cc-predial', r: 'res-5' }) });

        expect(tag.reservationId).toBe('res-5');
    });

    it('does not take another kind of leg for an offer leg', () => {
        // The accept-time agent leg and an extension call carry client state too; neither names an offer.
        const tag = readOfferLegTag({ clientState: clientState({ i: 'cc-agent', p: 'caller-1', r: 'res-1' }) });

        expect(tag.reservationId).toBe('');
    });

    it('survives options with nothing in them', () => {
        expect(readOfferLegTag(undefined)).toEqual({ reservationId: '', legId: '' });
        expect(readOfferLegTag({ clientState: 'not base64 and not json' })).toEqual({ reservationId: '', legId: '' });
    });
});

describe('classifyOfferLeg', () => {
    const now = 1_000_000;

    it('holds the leg of an offer nobody has accepted', () => {
        const decision = classifyOfferLeg(
            { reservationId: 'res-1', legId: 'leg-1' },
            { reservationId: 'res-1', agentLegId: '', accepting: false },
            {},
            now);

        expect(decision).toEqual({ action: 'hold', reservationId: 'res-1' });
    });

    it('holds an offer leg that arrives before the offer is on screen', () => {
        // The ring and the leg travel separately; the leg often wins.
        expect(classifyOfferLeg({ reservationId: 'res-1', legId: 'leg-1' }, null, {}, now))
            .toEqual({ action: 'hold', reservationId: 'res-1' });
    });

    it('answers the leg when this page is already accepting its offer', () => {
        expect(classifyOfferLeg(
            { reservationId: 'res-1', legId: 'leg-1' },
            { reservationId: 'res-1', accepting: true },
            {},
            now)).toEqual({ action: 'answer', reservationId: 'res-1' });
    });

    it('answers the leg when the offer was accepted somewhere else before it arrived', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-1', now);

        expect(classifyOfferLeg({ reservationId: 'res-1', legId: 'leg-1' }, null, accepted, now + 500))
            .toEqual({ action: 'answer', reservationId: 'res-1' });
    });

    it('does not answer a leg because a different offer is being accepted', () => {
        expect(classifyOfferLeg(
            { reservationId: 'res-2', legId: 'leg-2' },
            { reservationId: 'res-1', accepting: true },
            {},
            now)).toEqual({ action: 'hold', reservationId: 'res-2' });
    });

    it('recognizes a leg by the provider id the offer carries when the leg names no offer', () => {
        expect(classifyOfferLeg(
            { reservationId: '', legId: 'leg-9' },
            { reservationId: 'res-9', agentLegId: 'leg-9', accepting: false },
            {},
            now)).toEqual({ action: 'hold', reservationId: 'res-9' });
    });

    it('leaves an ordinary incoming call to the usual rules', () => {
        // A colleague dialing this agent's extension, or the accept-time agent leg.
        expect(classifyOfferLeg(
            { reservationId: '', legId: 'leg-3' },
            { reservationId: 'res-1', agentLegId: 'leg-1', accepting: true },
            {},
            now)).toBeNull();
        expect(classifyOfferLeg(null, null, {}, now)).toBeNull();
    });
});

describe('accepted offer memory', () => {
    it('forgets an accept after a minute, so a stale one cannot answer a later leg', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-1', 0);

        expect(isOfferAccepted(accepted, 'res-1', 59_000)).toBe(true);
        expect(isOfferAccepted(accepted, 'res-1', 61_000)).toBe(false);
    });

    it('prunes old entries as new ones are remembered', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-1', 0);
        rememberAcceptedOffer(accepted, 'res-2', 120_000);

        expect(Object.keys(accepted)).toEqual(['res-2']);
    });

    it('forgets an accept that failed', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-1', 0);
        forgetAcceptedOffer(accepted, 'res-1');

        expect(isOfferAccepted(accepted, 'res-1', 1)).toBe(false);
    });
});
