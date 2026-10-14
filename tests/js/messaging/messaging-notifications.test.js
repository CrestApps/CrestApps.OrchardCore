import { describe, expect, it } from 'vitest';

import {
    createRecentNotifications,
    notificationKey,
} from '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

// A transfer goes to the recipient's own group and to the group of the queue they serve, so one connection hears the
// same event twice. Live, that was two toasts for one transfer.
describe('createRecentNotifications', () => {
    function clockAt(start) {
        const clock = { now: start };
        clock.read = () => clock.now;

        return clock;
    }

    it('announces a notification the first time, and not again within the window', () => {
        const clock = clockAt(1000);
        const recent = createRecentNotifications(5000, clock.read);

        expect(recent.isNew('transfer:c1')).toBe(true);

        clock.now += 4999;
        expect(recent.isNew('transfer:c1')).toBe(false);
    });

    it('announces the same notification again once the window has passed', () => {
        const clock = clockAt(1000);
        const recent = createRecentNotifications(5000, clock.read);

        recent.isNew('transfer:c1');

        clock.now += 5000;
        expect(recent.isNew('transfer:c1')).toBe(true);
    });

    it('announces different notifications independently', () => {
        const recent = createRecentNotifications(5000, () => 1000);

        expect(recent.isNew('transfer:c1')).toBe(true);
        expect(recent.isNew('transfer:c2')).toBe(true);
        expect(recent.isNew('message:c1')).toBe(true);
    });
});

describe('notificationKey', () => {
    it('is what happened and to which conversation', () => {
        expect(notificationKey('transfer', { conversationId: 'c1' })).toBe('transfer:c1');
    });

    it('folds copies of the same event into one key', () => {
        expect(notificationKey('transfer', { conversationId: 'c1', toName: 'Ann' }))
            .toBe(notificationKey('transfer', { conversationId: 'c1', toName: 'Ann' }));
    });

    it('keeps a notification with no conversation', () => {
        expect(notificationKey('transfer', null)).toBe('transfer:');
    });
});
