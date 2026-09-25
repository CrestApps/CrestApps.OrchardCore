import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
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
    shouldAutoAnswerInboundLeg,
    isDestinationLeg,
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

// Live: once a colleague's extension call had been answered, every later one was answered on arrival -- no ring, no
// Answer, nothing on screen -- because "the agent answered a ring" was kept for good and read as "the next leg is
// expected". Only an arm decides now, and a leg the platform rang at this phone as somebody's destination never uses one.
describe('shouldAutoAnswerInboundLeg', () => {
    const encode = state => btoa(JSON.stringify(state));

    it('rings a leg nothing was armed for', () => {
        const arm = createAutoAnswerArm();

        expect(shouldAutoAnswerInboundLeg(arm, now, {})).toBe(false);
    });

    it('answers the one leg an arm is for, and rings the next', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, EXTENSION_CALL_KEY, now);

        expect(shouldAutoAnswerInboundLeg(arm, now + 500, {})).toBe(true);
        expect(shouldAutoAnswerInboundLeg(arm, now + 600, {})).toBe(false);
    });

    it('rings a colleague\'s call to this phone even while an arm is live, and keeps the arm for the phone\'s own leg', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, EXTENSION_CALL_KEY, now);
        const colleague = { clientState: encode({ i: 'ob-dest', p: 'v3:caller-leg', v: 'user-2' }) };

        expect(shouldAutoAnswerInboundLeg(arm, now + 100, colleague)).toBe(false);
        expect(arm.key).toBe(EXTENSION_CALL_KEY);
        expect(shouldAutoAnswerInboundLeg(arm, now + 200, { clientState: encode({ i: 'ob-agent', d: 'sip:x@y' }) })).toBe(true);
    });

    it('rings a call a colleague hands over, and keeps the arm', () => {
        const arm = createAutoAnswerArm();
        armAutoAnswer(arm, EXTENSION_CALL_KEY, now);

        expect(shouldAutoAnswerInboundLeg(arm, now + 100, { transferLeg: true })).toBe(false);
        expect(arm.key).toBe(EXTENSION_CALL_KEY);
    });

    it('reads the destination tag however the provider hands client state over', () => {
        expect(isDestinationLeg({ clientState: JSON.stringify({ i: 'ob-dest' }) })).toBe(true);
        expect(isDestinationLeg({ client_state: encode({ i: 'ob-dest' }) })).toBe(true);
        expect(isDestinationLeg({ clientState: encode({ i: 'ob-agent' }) })).toBe(false);
        expect(isDestinationLeg({})).toBe(false);
        expect(isDestinationLeg(null)).toBe(false);
    });
});
