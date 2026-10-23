import { describe, expect, it } from 'vitest';

import {
    activityToAutoOpen,
    autoOpenWorkKey,
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
