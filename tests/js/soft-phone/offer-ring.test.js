import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-ring.js';

const { shouldRingForOffer, readOfferHandledMessage } = globalThis.CrestAppsSoftPhone;

// Regression: answering an offer waits on the server to accept it before the call connects, and the agent heard the
// ringtone carry on through that whole round trip after clicking Answer.
describe('shouldRingForOffer', () => {
    it('rings while an offer is on screen and nobody has answered it', () => {
        expect(shouldRingForOffer(true, false)).toBe(true);
    });

    it('stops ringing the moment an accept is pending', () => {
        expect(shouldRingForOffer(true, true)).toBe(false);
    });

    it('does not ring with no offer on screen', () => {
        expect(shouldRingForOffer(false, false)).toBe(false);
        expect(shouldRingForOffer(false, true)).toBe(false);
    });
});

// Regression: every open agent page rings for the same offer. Answering in one page kept the others ringing until the
// server's own update reached them. The answering page now tells the others through the browser, and each one that is
// ringing for that same call falls silent -- and only for that call: an unrelated call ringing in another page keeps
// ringing.
describe('readOfferHandledMessage', () => {
    const answered = { type: 'offer-handled', callId: 'v3:caller', reservationId: 'res-1', answered: true };

    it('marks the matching ringing call handled', () => {
        expect(readOfferHandledMessage(answered, 'v3:caller'))
            .toEqual({ reservationId: 'res-1', answered: true, marksCurrentCallHandled: true });
    });

    it('leaves a different ringing call alone, but still settles the offer leg it names', () => {
        expect(readOfferHandledMessage(answered, 'v3:someone-else'))
            .toEqual({ reservationId: 'res-1', answered: true, marksCurrentCallHandled: false });
    });

    it('marks nothing when this page has no call', () => {
        expect(readOfferHandledMessage(answered, null).marksCurrentCallHandled).toBe(false);
    });

    it('carries a decline through as not answered', () => {
        expect(readOfferHandledMessage({ type: 'offer-handled', callId: 'v3:caller', reservationId: 'res-1', answered: false }, 'v3:caller'))
            .toEqual({ reservationId: 'res-1', answered: false, marksCurrentCallHandled: true });
    });

    it('ignores anything that is not an offer-handled message', () => {
        expect(readOfferHandledMessage(null, 'v3:caller')).toBe(null);
        expect(readOfferHandledMessage({ type: 'something-else', callId: 'v3:caller' }, 'v3:caller')).toBe(null);
    });
});
