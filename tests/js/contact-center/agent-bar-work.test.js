import { describe, expect, it } from 'vitest';

import {
    acceptedOfferActivityToOpen,
    activityToAutoOpen,
    autoOpenWorkKey,
    isOfferForUser,
    readPoppedActivities,
    rememberPoppedActivity,
} from '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/agent-bar-work.js';

function memoryStorage() {
    const values = {};

    return {
        getItem: (key) => (key in values ? values[key] : null),
        setItem: (key, value) => { values[key] = String(value); },
    };
}

// Live: agents on a Power campaign were connected to customers with the bar tucked away and no record on screen. A
// dialed call has no offer, and the bar opened itself only for an offer.
describe('autoOpenWorkKey', () => {
    it('opens for a ringing offer', () => {
        expect(autoOpenWorkKey({ offer: { reservationId: 'r1' } })).toBe('offer:r1');
    });

    it('opens for a connected call that has a record, and again when it ends so the activity can be completed', () => {
        const live = { activeInteraction: { interactionId: 'i1', activityItemId: 'a1', status: 'Active' } };
        const ended = { activeInteraction: { interactionId: 'i1', activityItemId: 'a1', status: 'Ended' } };

        expect(autoOpenWorkKey(live)).toBe('call:i1');
        expect(autoOpenWorkKey(ended)).toBe('call:i1:ended');
        expect(autoOpenWorkKey({ activeInteraction: { interactionId: 'i1', activityItemId: 'a1', status: 'Failed' } })).toBe('call:i1:ended');
    });

    it('prefers the offer while one rings', () => {
        expect(autoOpenWorkKey({
            offer: { reservationId: 'r2' },
            activeInteraction: { interactionId: 'i1', activityItemId: 'a1', status: 'Active' },
        })).toBe('offer:r2');
    });

    it('does not open for a call without a record, a dial still reaching the customer, or nothing', () => {
        expect(autoOpenWorkKey({ activeInteraction: { interactionId: 'i1', status: 'Active' } })).toBe('');
        expect(autoOpenWorkKey({ pendingDial: { interactionId: 'i1' } })).toBe('');
        expect(autoOpenWorkKey({})).toBe('');
        expect(autoOpenWorkKey(null)).toBe('');
    });
});

describe('activityToAutoOpen', () => {
    const connected = { activeInteraction: { activityItemId: 'a1', autoOpenActivity: true } };

    it('opens the record of an automatic dial the agent is connected to', () => {
        expect(activityToAutoOpen(connected, {})).toBe('a1');
    });

    it('opens it once per tab, so an agent who moved on is never sent back', () => {
        expect(activityToAutoOpen(connected, { a1: true })).toBe('');
    });

    it('leaves every other call alone', () => {
        expect(activityToAutoOpen({ activeInteraction: { activityItemId: 'a1', autoOpenActivity: false } }, {})).toBe('');
        expect(activityToAutoOpen({ activeInteraction: { activityItemId: 'a1' } }, {})).toBe('');
        expect(activityToAutoOpen({ activeInteraction: { autoOpenActivity: true } }, {})).toBe('');
        expect(activityToAutoOpen({}, {})).toBe('');
    });
});

describe('popped activities', () => {
    it('are kept across page loads in the tab\'s storage', () => {
        const storage = memoryStorage();
        const popped = readPoppedActivities(storage);

        rememberPoppedActivity(storage, popped, 'a1');

        expect(readPoppedActivities(storage)).toEqual({ a1: true });
    });

    it('keep only the most recent ones', () => {
        const storage = memoryStorage();
        const popped = {};

        for (let i = 0; i < 60; i++) {
            rememberPoppedActivity(storage, popped, 'a' + i);
        }

        const kept = Object.keys(readPoppedActivities(storage));

        expect(kept).toHaveLength(50);
        expect(kept).not.toContain('a0');
        expect(kept).toContain('a59');
    });

    it('work for the page alone when storage is missing, refused or holds garbage', () => {
        const refusing = {
            getItem: () => { throw new Error('denied'); },
            setItem: () => { throw new Error('denied'); },
        };
        const popped = readPoppedActivities(refusing);

        rememberPoppedActivity(refusing, popped, 'a1');
        rememberPoppedActivity(null, popped, 'a2');

        expect(popped).toEqual({ a1: true, a2: true });
        expect(readPoppedActivities({ getItem: () => '{not json' })).toEqual({});
        expect(readPoppedActivities(null)).toEqual({});
    });
});

// Live: administrators who were not signed in to any queue or campaign were taken off the page they were on, to the
// record of a call another agent had just answered, and two of them landed on the same record at once. The acceptance
// is also sent to the offer's queue and to that queue's supervisors, and the bar opened the record for every screen
// that heard it.
describe('acceptedOfferActivityToOpen', () => {
    const accepted = { userId: 'u1', reason: 'Accepted', kind: 'InboundCall', activityItemId: 'a1', reservationId: 'r1' };

    it('opens the record for the agent who accepted the offer', () => {
        expect(acceptedOfferActivityToOpen(accepted, 'u1')).toBe('a1');
        expect(acceptedOfferActivityToOpen({ ...accepted, kind: 'PreviewDial', activityItemId: 42 }, 'u1')).toBe('42');
    });

    it('opens nothing on the screens of anyone else who hears of it: the queue, its supervisors', () => {
        expect(acceptedOfferActivityToOpen(accepted, 'u2')).toBe('');
        expect(acceptedOfferActivityToOpen(accepted, 'supervisor')).toBe('');
    });

    it('never takes an unknown owner as this agent: a page changes under its user only for their own offer', () => {
        expect(acceptedOfferActivityToOpen(accepted, '')).toBe('');
        expect(acceptedOfferActivityToOpen(accepted, null)).toBe('');
        expect(acceptedOfferActivityToOpen({ ...accepted, userId: null }, 'u1')).toBe('');
        expect(acceptedOfferActivityToOpen(null, 'u1')).toBe('');
    });

    it('opens nothing for an offer that expired or was released, for an automatic dial, or without a record', () => {
        expect(acceptedOfferActivityToOpen({ ...accepted, reason: 'Expired' }, 'u1')).toBe('');
        expect(acceptedOfferActivityToOpen({ ...accepted, reason: 'Released' }, 'u1')).toBe('');
        expect(acceptedOfferActivityToOpen({ ...accepted, kind: 'AutoDial' }, 'u1')).toBe('');
        expect(acceptedOfferActivityToOpen({ ...accepted, activityItemId: null }, 'u1')).toBe('');
    });
});

describe('isOfferForUser', () => {
    it('is true only when the offer names the signed-in user', () => {
        expect(isOfferForUser({ userId: 'u1' }, 'u1')).toBe(true);
        expect(isOfferForUser({ userId: 'u2' }, 'u1')).toBe(false);
        expect(isOfferForUser({ userId: 'u1' }, '')).toBe(false);
        expect(isOfferForUser({}, 'u1')).toBe(false);
        expect(isOfferForUser(null, 'u1')).toBe(false);
    });
});
