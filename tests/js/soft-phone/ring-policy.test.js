import { describe, expect, it } from 'vitest';

import {
    INBOUND_RING_ACTIONS,
    inboundRingAction,
    offerDeclinesRing,
} from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/ring-policy.js';

// Regression: an agent saw two ringing "Incoming call" prompts at once. A leg the phone did not expect took over the
// prompt while another call was ringing, and the earlier call was only hidden, never declined, so it kept ringing.
describe('inboundRingAction', () => {
    it('rings a leg when nothing else is ringing or on the line', () => {
        expect(inboundRingAction({ onCall: false, ringing: false, answering: false })).toBe(INBOUND_RING_ACTIONS.ring);
        expect(inboundRingAction()).toBe(INBOUND_RING_ACTIONS.ring);
    });

    it('declines a leg while another incoming call is ringing', () => {
        expect(inboundRingAction({ onCall: false, ringing: true, answering: false })).toBe(INBOUND_RING_ACTIONS.decline);
    });

    it('declines a leg while the agent is on a call', () => {
        expect(inboundRingAction({ onCall: true, ringing: false, answering: false })).toBe(INBOUND_RING_ACTIONS.decline);
        expect(inboundRingAction({ onCall: true, ringing: false, answering: true })).toBe(INBOUND_RING_ACTIONS.decline);
    });

    it('never declines a leg that arrives while the agent is answering the ringing call', () => {
        // Most likely that call's own leg, come late: declining it would drop the call the agent is taking.
        expect(inboundRingAction({ onCall: false, ringing: true, answering: true })).toBe(INBOUND_RING_ACTIONS.ring);
    });
});

describe('offerDeclinesRing', () => {
    it('declines a leg still ringing when a Contact Center offer arrives', () => {
        expect(offerDeclinesRing('browser-in-1', 'v3:offer')).toBe(true);
    });

    it('leaves things alone with no leg ringing', () => {
        expect(offerDeclinesRing('', 'v3:offer')).toBe(false);
        expect(offerDeclinesRing(null, 'v3:offer')).toBe(false);
    });

    it('does not decline the offer\'s own call', () => {
        expect(offerDeclinesRing('v3:offer', 'v3:offer')).toBe(false);
    });
});
