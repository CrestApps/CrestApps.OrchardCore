import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/auto-answer.js';

const {
    AUTO_ANSWER_WINDOW_MS,
    EXTENSION_CALL_KEY,
    autoAnswerOfferKey,
    createAutoAnswerArm,
    armAutoAnswer,
    consumeAutoAnswer,
    disarmAutoAnswer,
    disarmOtherOffers,
} = globalThis.CrestAppsSoftPhone;

const now = 5_000_000;

// An expectation armed for one offer's leg outlived the offer, so the next leg to reach the browser was answered
// without the agent touching anything.
describe('the inbound auto-answer arm', () => {
    it('answers the one leg it was armed for, then rings the next', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);

        expect(consumeAutoAnswer(arm, now + 1000)).toBe('offer:res-1');
        expect(consumeAutoAnswer(arm, now + 1001)).toBe('');
    });

    it('lapses after its window, and is spent by the lapsed leg too', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);

        expect(consumeAutoAnswer(arm, now + AUTO_ANSWER_WINDOW_MS)).toBe('');
        expect(arm.key).toBe('');
    });

    it('is dropped when its offer is withdrawn', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);

        disarmAutoAnswer(arm, autoAnswerOfferKey('res-1'));

        expect(consumeAutoAnswer(arm, now + 1)).toBe('');
    });

    it('is not dropped by the withdrawal of a different offer', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);

        disarmAutoAnswer(arm, autoAnswerOfferKey('res-2'));

        expect(consumeAutoAnswer(arm, now + 1)).toBe('offer:res-1');
    });

    it('is dropped when a different offer rings, but not by the offer it is for', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);

        disarmOtherOffers(arm, autoAnswerOfferKey('res-1'));
        expect(arm.key).toBe('offer:res-1');

        disarmOtherOffers(arm, autoAnswerOfferKey('res-2'));
        expect(consumeAutoAnswer(arm, now + 1)).toBe('');
    });

    it('keeps an extension call\'s arm when an offer rings', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, EXTENSION_CALL_KEY, now);

        disarmOtherOffers(arm, autoAnswerOfferKey('res-2'));

        expect(consumeAutoAnswer(arm, now + 1)).toBe(EXTENSION_CALL_KEY);
    });

    it('gives an offer of unknown reservation a key any other offer drops', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey(''), now);

        disarmOtherOffers(arm, autoAnswerOfferKey('res-2'));

        expect(consumeAutoAnswer(arm, now + 1)).toBe('');
    });

    it('refuses an arm that names nothing', () => {
        const arm = createAutoAnswerArm();

        expect(armAutoAnswer(arm, '', now)).toBe(false);
        expect(consumeAutoAnswer(arm, now)).toBe('');
    });

    it('replaces an earlier arm', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, autoAnswerOfferKey('res-1'), now);
        armAutoAnswer(arm, autoAnswerOfferKey('res-2'), now + 10);

        expect(consumeAutoAnswer(arm, now + 20)).toBe('offer:res-2');
    });
});
