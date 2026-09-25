import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

const { classifyInbound, maxTicks, rowMatchesFilter, selectNewBubbles, tabBadgeCount, unseenInboundCount } = globalThis.CrestAppsMessaging;

// A customer's message that lands in the open thread while the agent is scrolled up, or on another browser tab, is still
// waiting for them, so the open channel's tab must count it rather than treat the thread on screen as read.
describe('unseenInboundCount', () => {
    const bubbles = [{ inbound: true }, { inbound: false }, { inbound: true }];

    it('counts the customer messages the agent has not seen', () => {
        expect(unseenInboundCount(bubbles, false)).toBe(2);
    });

    it('counts nothing when the agent is at the bottom of the visible thread', () => {
        expect(unseenInboundCount(bubbles, true)).toBe(0);
    });

    it('never counts the agent\'s own messages', () => {
        expect(unseenInboundCount([{ inbound: false }], false)).toBe(0);
    });
});

// The workspace shows one customer's conversation with a tab per channel. A message for that customer on another
// channel must badge that channel's tab, never the thread on screen, and a message for anyone else must not be
// appended to the open thread.
describe('classifyInbound', () => {
    const view = { conversationId: 'c-sms', customerKey: 'contact:1' };

    it('appends a message for the conversation on screen', () => {
        expect(classifyInbound({ conversationId: 'c-sms', customerKey: 'contact:1', channel: 'SMS' }, view)).toBe('thread');
    });

    it('badges the tab of the same customer on another channel', () => {
        expect(classifyInbound({ conversationId: 'c-email', customerKey: 'contact:1', channel: 'Email' }, view)).toBe('tab');
    });

    it('treats another customer as elsewhere', () => {
        expect(classifyInbound({ conversationId: 'c-9', customerKey: 'contact:9', channel: 'SMS' }, view)).toBe('elsewhere');
    });

    it('treats everything as elsewhere when no conversation is open', () => {
        expect(classifyInbound({ conversationId: 'c-sms', customerKey: 'contact:1', channel: 'SMS' }, {})).toBe('elsewhere');
    });

    it('does not badge a tab when the notification names no channel', () => {
        expect(classifyInbound({ conversationId: 'c-x', customerKey: 'contact:1' }, view)).toBe('elsewhere');
    });
});

describe('tabBadgeCount', () => {
    it('shows the unread count the server reported for the conversation', () => {
        expect(tabBadgeCount({ unreadCount: 3 })).toBe(3);
    });

    it('shows at least one, since a message just arrived', () => {
        expect(tabBadgeCount({ unreadCount: 0 })).toBe(1);
        expect(tabBadgeCount({})).toBe(1);
    });
});

// .NET ticks exceed JavaScript's safe-integer range; rounding them made the server re-send the newest message on
// every poll, which piled up as duplicates in the thread.
describe('maxTicks', () => {
    it('compares ticks exactly, beyond the safe-integer range', () => {
        expect(maxTicks(['638900000000000001', '638900000000000002', '638900000000000000'])).toBe('638900000000000002');
    });

    it('ignores empty and malformed values', () => {
        expect(maxTicks([null, '', 'abc', '5'])).toBe('5');
        expect(maxTicks([])).toBe('0');
    });
});

describe('selectNewBubbles', () => {
    it('keeps only bubbles not already on screen, by id or by ticks when there is no id', () => {
        const existing = [{ id: 'm1', ticks: '1' }, { id: null, ticks: '2' }];
        const incoming = [{ id: 'm1', ticks: '1' }, { id: null, ticks: '2' }, { id: 'm3', ticks: '3' }];

        expect(selectNewBubbles(existing, incoming).map(bubble => bubble.id)).toEqual(['m3']);
    });

    it('does not add the same bubble twice from one batch', () => {
        const incoming = [{ id: 'm4', ticks: '4' }, { id: 'm4', ticks: '4' }];

        expect(selectNewBubbles([], incoming)).toHaveLength(1);
    });
});

describe('rowMatchesFilter', () => {
    it('matches every term in any order and case', () => {
        expect(rowMatchesFilter('Ann Smith +15551112222 order', 'smith ann')).toBe(true);
        expect(rowMatchesFilter('Ann Smith', 'bob')).toBe(false);
    });

    it('matches everything for an empty search', () => {
        expect(rowMatchesFilter('anything', '   ')).toBe(true);
    });
});

// The bundles are concatenations, not a module graph: a helper not listed ahead of the script that calls it is not in
// the browser at all.
describe('the messaging workspace bundle', () => {
    const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets.json', 'utf8'));
    const group = assets.find(candidate => candidate.output === 'wwwroot/scripts/messaging-workspace.js');

    it('carries the state helpers ahead of the workspace script', () => {
        expect(group).toBeDefined();
        expect(group.inputs.indexOf('Assets/js/shared/messaging-state.js'))
            .toBeLessThan(group.inputs.indexOf('Assets/js/messaging-workspace.js'));
    });
});
