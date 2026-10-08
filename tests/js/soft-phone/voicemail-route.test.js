import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/voicemail-route.js';

const { voicemailRoute, VOICEMAIL_ROUTES } = globalThis.CrestAppsSoftPhone;

// Bug: sending a ringing Contact Center offer to voicemail ran two paths at once. The soft phone declined the offer
// (which the Contact Center turned into voicemail) AND asked the telephony hub to send the same call to voicemail, so
// the caller was answered twice and heard the voicemail greeting twice. Exactly one path may send a call to voicemail.
describe('voicemailRoute', () => {
    const offer = {
        acceptUrl: '/offer/accept?reservationId=r1',
        declineUrl: '/offer/decline?reservationId=r1',
        voicemailUrl: '/offer/voicemail?reservationId=r1',
        reservationId: 'r1',
    };

    it('lets the Contact Center alone send an offered call to voicemail', () => {
        const decision = voicemailRoute({ browserInboundRinging: false, offer, hasCall: true });

        expect(decision.route).toBe(VOICEMAIL_ROUTES.contactCenter);
        expect(decision.lifecycleKey).toBe('voicemailUrl');
    });

    it('never also asks the telephony hub for an offered call', () => {
        const decision = voicemailRoute({ browserInboundRinging: false, offer, hasCall: true });

        expect(decision.route).not.toBe(VOICEMAIL_ROUTES.telephony);
    });

    it('declines through the Contact Center when the offer carries only a decline action', () => {
        // A decline of a direct-to-agent offer is itself what sends the caller to voicemail.
        const decision = voicemailRoute({
            browserInboundRinging: false,
            offer: { declineUrl: '/offer/decline?reservationId=r1', reservationId: 'r1' },
            hasCall: true,
        });

        expect(decision.route).toBe(VOICEMAIL_ROUTES.contactCenter);
        expect(decision.lifecycleKey).toBe('declineUrl');
    });

    it('sends a plain telephony call, with no Contact Center offer, through the telephony hub', () => {
        expect(voicemailRoute({ browserInboundRinging: false, offer: null, hasCall: true }).route).toBe(VOICEMAIL_ROUTES.telephony);
        expect(voicemailRoute({ browserInboundRinging: false, offer: { queue: 'Sales' }, hasCall: true }).route).toBe(VOICEMAIL_ROUTES.telephony);
    });

    it('declines the local leg of a direct extension call, whose hangup reaches voicemail on the server', () => {
        expect(voicemailRoute({ browserInboundRinging: true, offer, hasCall: true }).route).toBe(VOICEMAIL_ROUTES.browserLeg);
    });

    it('does nothing when there is no call and no offer', () => {
        expect(voicemailRoute({ browserInboundRinging: false, offer: null, hasCall: false }).route).toBe(VOICEMAIL_ROUTES.none);
        expect(voicemailRoute(null).route).toBe(VOICEMAIL_ROUTES.none);
    });
});
