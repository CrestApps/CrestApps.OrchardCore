import { afterEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/answer-state.js';

const {
    answerButtonView,
    withTimeout,
    ANSWER_REGISTRATION_TIMEOUT_MS,
    rememberSettledOffer,
    isOfferSettled,
    SETTLED_OFFER_TTL_MS,
} = globalThis.CrestAppsSoftPhone;

// Bug: the first click on Answer changed nothing the agent could see for several seconds, so they clicked again and
// again. From the first click the button reads "Answering..." with a spinner and every offer button is disabled.
describe('answerButtonView', () => {
    const labels = { answering: 'Answering…' };

    it('shows the answer in progress from the first click', () => {
        expect(answerButtonView(true, 'Answer', labels)).toEqual({ disabled: true, spinner: true, label: 'Answering…' });
    });

    it('shows the button as it was before the click', () => {
        expect(answerButtonView(false, 'Answer & open', labels)).toEqual({ disabled: false, spinner: false, label: 'Answer & open' });
    });

    it('falls back to an English label when none is configured', () => {
        expect(answerButtonView(true, 'Answer', {}).label).toBe('Answering…');
        expect(answerButtonView(true, 'Answer', null).label).toBe('Answering…');
    });
});

// A registration that never completes (the provider never confirms the login) would leave the buttons disabled for
// good. Past the limit the answer is abandoned: the buttons come back with an error the agent can act on.
describe('withTimeout', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('settles with the registration when it completes in time', async () => {
        await expect(withTimeout(Promise.resolve('registered'), ANSWER_REGISTRATION_TIMEOUT_MS)).resolves.toBe('registered');
    });

    it('passes a failed registration through', async () => {
        await expect(withTimeout(Promise.reject(new Error('denied')), ANSWER_REGISTRATION_TIMEOUT_MS)).rejects.toThrow('denied');
    });

    it('gives up on a registration that never completes, and not a moment sooner', async () => {
        vi.useFakeTimers();
        let settled = false;
        const outcome = withTimeout(new Promise(() => { }), ANSWER_REGISTRATION_TIMEOUT_MS)
            .catch(error => error)
            .finally(() => { settled = true; });

        await vi.advanceTimersByTimeAsync(ANSWER_REGISTRATION_TIMEOUT_MS - 1);
        expect(settled).toBe(false);

        await vi.advanceTimersByTimeAsync(1);
        expect((await outcome).timedOut).toBe(true);
    });
});

// Bug: an offer revoked the moment it was made (the caller hung up as it was presented) flashed the incoming-call modal
// up. The offer can reach the phone twice -- pushed, and fetched by a request already in flight -- and either copy can
// land after the revoke, re-opening a call that is over. A settled offer is remembered so a late copy is dropped.
describe('settled offers', () => {
    it('recognises an offer settled by reservation', () => {
        const settled = {};
        rememberSettledOffer(settled, { reservationId: 'res-1' }, 1000);

        expect(isOfferSettled(settled, { callId: 'call-1', reservationId: 'res-1' }, 1001)).toBe(true);
    });

    it('recognises an offer whose call already ended', () => {
        const settled = {};
        rememberSettledOffer(settled, { callId: 'call-1' }, 1000);

        expect(isOfferSettled(settled, { callId: 'call-1', reservationId: 'res-1' }, 1001)).toBe(true);
    });

    it('does not mistake a different offer for a settled one', () => {
        const settled = {};
        rememberSettledOffer(settled, { callId: 'call-1', reservationId: 'res-1' }, 1000);

        expect(isOfferSettled(settled, { callId: 'call-2', reservationId: 'res-2' }, 1001)).toBe(false);
    });

    it('forgets a settled offer after a while', () => {
        const settled = {};
        rememberSettledOffer(settled, { reservationId: 'res-1' }, 1000);

        expect(isOfferSettled(settled, { reservationId: 'res-1' }, 1000 + SETTLED_OFFER_TTL_MS + 1)).toBe(false);
    });

    it('ignores empty keys', () => {
        const settled = {};
        rememberSettledOffer(settled, { callId: '', reservationId: null }, 1000);

        expect(Object.keys(settled)).toHaveLength(0);
        expect(isOfferSettled(settled, { callId: '', reservationId: '' }, 1001)).toBe(false);
        expect(isOfferSettled(settled, null, 1001)).toBe(false);
    });
});
