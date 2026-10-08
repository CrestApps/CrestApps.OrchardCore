import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-report.js';

const {
    planOfferCallReport,
    rememberUnacceptedOfferCall,
    forgetUnacceptedOfferCall,
    isUnacceptedOfferCall,
    rememberAcceptedOffer,
    UNACCEPTED_OFFER_CALL_TTL_MS,
} = globalThis.CrestAppsSoftPhone;

const now = 1_000_000;
const ringing = { callId: 'call-2', reservationId: 'res-2', accepting: false };

// A caller handed to a queue is on a live leg the whole time an agent's phone rings for them. The server reported that
// leg as connected, and the phone took it as the call connecting: the prompt vanished and it showed a call in progress
// that nobody had accepted.
describe('planOfferCallReport', () => {
    it('keeps a ringing offer ringing when its call is reported live and nobody accepted it', () => {
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, ringing, {}, {}, now)).toBe('keep-ringing');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'OnHold' }, ringing, {}, {}, now)).toBe('keep-ringing');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connecting' }, ringing, {}, {}, now)).toBe('keep-ringing');
    });

    it('applies the report once this page is accepting the offer', () => {
        const accepting = { ...ringing, accepting: true };

        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, accepting, {}, {}, now)).toBe('apply');
    });

    it('applies the report once the offer is known to be accepted elsewhere', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-2', now);

        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, ringing, accepted, {}, now)).toBe('apply');
    });

    it('does not read an accept of another offer as an accept of this one', () => {
        const accepted = {};
        rememberAcceptedOffer(accepted, 'res-1', now);

        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, ringing, accepted, {}, now)).toBe('keep-ringing');
    });

    it('always applies the end of a call, and a ringing report', () => {
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Disconnected' }, ringing, {}, {}, now)).toBe('apply');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Failed' }, ringing, {}, {}, now)).toBe('apply');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Ringing' }, ringing, {}, {}, now)).toBe('apply');
    });

    it('applies reports of other calls', () => {
        expect(planOfferCallReport({ callId: 'call-9', stateName: 'Connected' }, ringing, {}, {}, now)).toBe('apply');
        expect(planOfferCallReport({ callId: 'call-9', stateName: 'Connected' }, null, {}, {}, now)).toBe('apply');
    });

    it('ignores live reports of a call whose offer was withdrawn without an accept', () => {
        const unaccepted = {};
        rememberUnacceptedOfferCall(unaccepted, 'call-2', now);

        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, null, {}, unaccepted, now + 1000)).toBe('ignore');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Disconnected' }, null, {}, unaccepted, now + 1000)).toBe('apply');
    });

    it('reads the call as the phone\'s again when it is offered again', () => {
        const unaccepted = {};
        rememberUnacceptedOfferCall(unaccepted, 'call-2', now);

        // The same call, ringing on screen as a new offer, is judged as that offer.
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, { ...ringing, accepting: true }, {}, unaccepted, now)).toBe('apply');

        forgetUnacceptedOfferCall(unaccepted, 'call-2');
        expect(planOfferCallReport({ callId: 'call-2', stateName: 'Connected' }, null, {}, unaccepted, now)).toBe('apply');
    });

    it('tolerates a missing report', () => {
        expect(planOfferCallReport(null, ringing, {}, {}, now)).toBe('apply');
        expect(planOfferCallReport({ stateName: 'Connected' }, ringing, {}, {}, now)).toBe('apply');
    });
});

describe('the calls of withdrawn offers', () => {
    it('are forgotten after a while', () => {
        const unaccepted = {};
        rememberUnacceptedOfferCall(unaccepted, 'call-old', now);

        expect(isUnacceptedOfferCall(unaccepted, 'call-old', now + UNACCEPTED_OFFER_CALL_TTL_MS)).toBe(true);
        expect(isUnacceptedOfferCall(unaccepted, 'call-old', now + UNACCEPTED_OFFER_CALL_TTL_MS + 1)).toBe(false);

        rememberUnacceptedOfferCall(unaccepted, 'call-new', now + UNACCEPTED_OFFER_CALL_TTL_MS + 1);
        expect(Object.keys(unaccepted)).toEqual(['call-new']);
    });

    it('ignores empty call ids', () => {
        const unaccepted = {};
        rememberUnacceptedOfferCall(unaccepted, '', now);
        forgetUnacceptedOfferCall(unaccepted, '');

        expect(unaccepted).toEqual({});
        expect(isUnacceptedOfferCall(unaccepted, '', now)).toBe(false);
    });
});
