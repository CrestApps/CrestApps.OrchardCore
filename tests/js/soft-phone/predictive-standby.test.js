import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/auto-answer.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/predictive-standby.js';

const {
    STANDBY_GRACE_MS,
    STANDBY_LEG_MEMORY_MS,
    createPredictiveStandby,
    isStandbyEligible,
    setPredictiveStandby,
    isStandingBy,
    readStandbyLegTag,
    noteStandbyLeg,
    wasStandbyLegSeen,
    shouldStandbyAnswerLeg,
    createAutoAnswerArm,
    shouldAutoAnswerInboundLeg,
} = globalThis.CrestAppsSoftPhone;

const NOW = 1_000_000;

// The leg the platform rings for a call the agent was claimed for, tagged with the claim and the user.
function claimedLeg(reservationId = 'res-1', userId = 'user-1', extra = {}) {
    return {
        customHeaders: [
            { name: 'X-CC-Reservation', value: reservationId },
            { name: 'X-CC-Agent-User', value: userId },
        ],
        ...extra,
    };
}

function standingBy(userId = 'user-1') {
    const standby = createPredictiveStandby();
    setPredictiveStandby(standby, true, userId, NOW);

    return standby;
}

// An agent stands by only while Available and signed in to a campaign: only a campaign claims an agent for a call
// without offering it. A queue offers its calls, and those keep their accept.
describe('isStandbyEligible', () => {
    it('stands by while Available in a campaign', () => {
        expect(isStandbyEligible('Available', ['campaign-1'])).toBe(true);
    });

    it('does not stand by in any other state, or without a campaign', () => {
        expect(isStandbyEligible('Busy', ['campaign-1'])).toBe(false);
        expect(isStandbyEligible('Break', ['campaign-1'])).toBe(false);
        expect(isStandbyEligible('Available', [])).toBe(false);
        expect(isStandbyEligible('Available', [''])).toBe(false);
        expect(isStandbyEligible('Available', null)).toBe(false);
    });
});

describe('readStandbyLegTag', () => {
    it('reads the claim and the user from the SIP headers, whatever their case or shape', () => {
        expect(readStandbyLegTag(claimedLeg())).toEqual({ reservationId: 'res-1', agentUserId: 'user-1' });
        expect(readStandbyLegTag({ custom_headers: { 'x-cc-reservation': 'res-2', 'X-CC-AGENT-USER': 'user-2' } }))
            .toEqual({ reservationId: 'res-2', agentUserId: 'user-2' });
    });

    it('reads nothing from an untagged leg', () => {
        expect(readStandbyLegTag({})).toEqual({ reservationId: '', agentUserId: '' });
        expect(readStandbyLegTag(null)).toEqual({ reservationId: '', agentUserId: '' });
    });
});

// The point of the standby: the claimed leg is answered at once, even when it arrives before the push about the claim.
describe('shouldStandbyAnswerLeg', () => {
    it('answers the tagged leg of a claim for this user while standing by', () => {
        expect(shouldStandbyAnswerLeg(standingBy(), claimedLeg(), NOW, {})).toBe('res-1');
    });

    it('prefers the user the page knows over the one the standby was set with', () => {
        expect(shouldStandbyAnswerLeg(standingBy('stale'), claimedLeg(), NOW, { ownUserId: 'user-1' })).toBe('res-1');
    });

    it('never answers a claim made for another user', () => {
        expect(shouldStandbyAnswerLeg(standingBy(), claimedLeg('res-1', 'user-2'), NOW, {})).toBe('');
    });

    it('never answers a leg without both tags', () => {
        const standby = standingBy();

        expect(shouldStandbyAnswerLeg(standby, {}, NOW, {})).toBe('');
        expect(shouldStandbyAnswerLeg(standby, { customHeaders: [{ name: 'X-CC-Reservation', value: 'res-1' }] }, NOW, {})).toBe('');
        expect(shouldStandbyAnswerLeg(standby, { customHeaders: [{ name: 'X-CC-Agent-User', value: 'user-1' }] }, NOW, {})).toBe('');
    });

    it('never answers an inbound offer leg, which keeps its accept', () => {
        const offerLeg = { customHeaders: [{ name: 'X-Offer-Id', value: 'res-1' }] };

        expect(shouldStandbyAnswerLeg(standingBy(), offerLeg, NOW, {})).toBe('');
    });

    it('lets a colleague\'s call to this phone and a call handed over ring, whatever they carry', () => {
        const standby = standingBy();
        const destination = claimedLeg('res-1', 'user-1', {
            customHeaders: [
                { name: 'X-CC-Reservation', value: 'res-1' },
                { name: 'X-CC-Agent-User', value: 'user-1' },
                { name: 'X-Destination-Leg', value: '1' },
            ],
        });

        expect(shouldStandbyAnswerLeg(standby, destination, NOW, {})).toBe('');
        expect(shouldStandbyAnswerLeg(standby, claimedLeg('res-1', 'user-1', { transferLeg: true }), NOW, {})).toBe('');
    });

    it('does not answer while the phone is already on a call', () => {
        expect(shouldStandbyAnswerLeg(standingBy(), claimedLeg(), NOW, { onCall: true })).toBe('');
    });

    it('does not answer when the phone never stood by', () => {
        expect(shouldStandbyAnswerLeg(createPredictiveStandby(), claimedLeg(), NOW, { ownUserId: 'user-1' })).toBe('');
        expect(shouldStandbyAnswerLeg(null, claimedLeg(), NOW, { ownUserId: 'user-1' })).toBe('');
    });

    it('does not consume the ordinary arm: an untagged leg still answers only on an arm', () => {
        const arm = createAutoAnswerArm();

        expect(shouldStandbyAnswerLeg(standingBy(), {}, NOW, {})).toBe('');
        expect(shouldAutoAnswerInboundLeg(arm, NOW, {})).toBe(false);
    });
});

// The claim takes the agent off Available, and that push can beat the invite here. A standby just dropped still answers
// a tagged leg for a few seconds, and only that.
describe('setPredictiveStandby', () => {
    it('keeps answering for the grace period after the standby is dropped, then stops', () => {
        const standby = standingBy();

        expect(setPredictiveStandby(standby, false, 'user-1', NOW)).toBe(false);
        expect(isStandingBy(standby, NOW + STANDBY_GRACE_MS - 1)).toBe(true);
        expect(shouldStandbyAnswerLeg(standby, claimedLeg(), NOW + STANDBY_GRACE_MS - 1, {})).toBe('res-1');
        expect(isStandingBy(standby, NOW + STANDBY_GRACE_MS)).toBe(false);
        expect(shouldStandbyAnswerLeg(standby, claimedLeg(), NOW + STANDBY_GRACE_MS, {})).toBe('');
    });

    it('does not restart the grace period while already not standing by', () => {
        const standby = standingBy();

        setPredictiveStandby(standby, false, '', NOW);
        setPredictiveStandby(standby, false, '', NOW + STANDBY_GRACE_MS - 1);

        expect(isStandingBy(standby, NOW + STANDBY_GRACE_MS)).toBe(false);
    });

    it('never grants a grace period to a phone that was not standing by', () => {
        const standby = createPredictiveStandby();

        setPredictiveStandby(standby, false, 'user-1', NOW);

        expect(isStandingBy(standby, NOW)).toBe(false);
    });

    it('standing by again ends any grace period', () => {
        const standby = standingBy();

        setPredictiveStandby(standby, false, '', NOW);
        setPredictiveStandby(standby, true, '', NOW + 1);

        expect(isStandingBy(standby, NOW + STANDBY_GRACE_MS * 10)).toBe(true);
        expect(standby.userId).toBe('user-1');
    });
});

// The push about a claim whose leg the phone already took must not arm it for the next leg, whoever's that is.
describe('noteStandbyLeg / wasStandbyLegSeen', () => {
    it('remembers a claim\'s leg for a minute', () => {
        const standby = createPredictiveStandby();

        noteStandbyLeg(standby, 'res-1', NOW);

        expect(wasStandbyLegSeen(standby, 'res-1', NOW + STANDBY_LEG_MEMORY_MS)).toBe(true);
        expect(wasStandbyLegSeen(standby, 'res-1', NOW + STANDBY_LEG_MEMORY_MS + 1)).toBe(false);
        expect(wasStandbyLegSeen(standby, 'res-2', NOW)).toBe(false);
    });

    it('forgets old legs as new ones arrive, so the memory does not grow', () => {
        const standby = createPredictiveStandby();

        noteStandbyLeg(standby, 'res-1', NOW);
        noteStandbyLeg(standby, 'res-2', NOW + STANDBY_LEG_MEMORY_MS + 1);

        expect(Object.keys(standby.legs)).toEqual(['res-2']);
    });

    it('ignores a leg without a claim', () => {
        const standby = createPredictiveStandby();

        noteStandbyLeg(standby, '', NOW);
        noteStandbyLeg(null, 'res-1', NOW);

        expect(Object.keys(standby.legs)).toEqual([]);
        expect(wasStandbyLegSeen(standby, '', NOW)).toBe(false);
    });
});
