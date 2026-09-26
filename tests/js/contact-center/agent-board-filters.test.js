import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/agent-board-filters.js';

const {
    agentStatusGroup,
    filterSupervisorAgents,
    normalizeAgentBoardFilters,
    agentBoardFiltersActive,
} = globalThis.CrestAppsContactCenter;

const agent = overrides => ({
    agentId: 'agent-1',
    userId: 'user-1',
    displayName: 'Ann Agent',
    presenceStatus: 'Available',
    queueIds: ['support'],
    campaignIds: [],
    activeInteractions: 0,
    ...overrides,
});

const board = [
    agent({ agentId: 'ann', displayName: 'Ann Agent', presenceStatus: 'Available', queueIds: ['support'], campaignIds: ['renewals'] }),
    agent({ agentId: 'bob', displayName: 'Bob Builder', presenceStatus: 'Busy', queueIds: ['sales'], activeInteractionId: 'int-1', activeInteractions: 1 }),
    agent({ agentId: 'cat', displayName: 'Cátia Souza', presenceStatus: 'Break', queueIds: ['support', 'sales'] }),
    agent({ agentId: 'dan', displayName: 'Dan Dialer', presenceStatus: 'Offline', queueIds: ['support'], campaignIds: ['renewals', 'winback'] }),
    agent({ agentId: 'eve', displayName: 'Eve Phone', presenceStatus: 'Available', queueIds: ['sales'], phoneCall: { party: '+17025550100' } }),
    agent({ agentId: 'fay', displayName: 'Fay Wrap', presenceStatus: 'WrapUp', queueIds: ['support'] }),
];

const ids = agents => agents.map(item => item.agentId);

// The statuses group the way the queue tiles count them, so a filter and a tile agree on who is busy or not ready.
describe('the status groups of the agent board', () => {
    it.each([
        ['Available', 'available'],
        ['Reserved', 'busy'],
        ['Busy', 'busy'],
        ['WrapUp', 'busy'],
        ['Break', 'notReady'],
        ['RequestBreak', 'notReady'],
        ['Away', 'notReady'],
        ['DoNotDisturb', 'notReady'],
        ['Meeting', 'notReady'],
        ['Training', 'notReady'],
        ['AfterHoursUnavailable', 'notReady'],
        ['Offline', 'offline'],
        [undefined, 'offline'],
    ])('files %s under %s', (status, group) => {
        expect(agentStatusGroup(status)).toBe(group);
    });
});

describe('filtering the agent board', () => {
    it('shows everybody when nothing is filtered', () => {
        expect(ids(filterSupervisorAgents(board, {}))).toEqual(['ann', 'bob', 'cat', 'dan', 'eve', 'fay']);
        expect(ids(filterSupervisorAgents(board, null))).toHaveLength(6);
    });

    it('keeps the agents signed in to the chosen queue', () => {
        expect(ids(filterSupervisorAgents(board, { queueId: 'sales' }))).toEqual(['bob', 'cat', 'eve']);
    });

    it('keeps the agents signed in to the chosen campaign', () => {
        expect(ids(filterSupervisorAgents(board, { campaignId: 'renewals' }))).toEqual(['ann', 'dan']);
    });

    it('keeps the agents in the chosen status group', () => {
        expect(ids(filterSupervisorAgents(board, { status: 'available' }))).toEqual(['ann', 'eve']);
        expect(ids(filterSupervisorAgents(board, { status: 'busy' }))).toEqual(['bob', 'fay']);
        expect(ids(filterSupervisorAgents(board, { status: 'notReady' }))).toEqual(['cat']);
        expect(ids(filterSupervisorAgents(board, { status: 'offline' }))).toEqual(['dan']);
    });

    // A number dialed from the keypad is a call too, whatever the agent's presence says.
    it('keeps the agents on a call, a Contact Center interaction or a phone call of their own', () => {
        expect(ids(filterSupervisorAgents(board, { status: 'onCall' }))).toEqual(['bob', 'eve']);
    });

    it('finds an agent by any part of their name, ignoring case and accents', () => {
        expect(ids(filterSupervisorAgents(board, { search: 'ANN' }))).toEqual(['ann']);
        expect(ids(filterSupervisorAgents(board, { search: 'catia' }))).toEqual(['cat']);
        expect(ids(filterSupervisorAgents(board, { search: '  souza ' }))).toEqual(['cat']);
    });

    it('applies every filter together', () => {
        expect(ids(filterSupervisorAgents(board, { queueId: 'support', status: 'available' }))).toEqual(['ann']);
        expect(ids(filterSupervisorAgents(board, { queueId: 'support', campaignId: 'renewals', search: 'dan' }))).toEqual(['dan']);
        expect(filterSupervisorAgents(board, { queueId: 'sales', campaignId: 'winback' })).toEqual([]);
    });

    it('tolerates agents the server sent without their queues or campaigns', () => {
        const bare = [{ agentId: 'x', displayName: 'X', presenceStatus: 'Available' }];

        expect(filterSupervisorAgents(bare, { queueId: 'support' })).toEqual([]);
        expect(ids(filterSupervisorAgents(bare, {}))).toEqual(['x']);
    });
});

describe('the filters a supervisor left on', () => {
    const state = { queues: [{ id: 'support' }, { id: 'sales' }], campaigns: [{ id: 'renewals' }] };

    it('keeps a queue, campaign and status the board still has', () => {
        expect(normalizeAgentBoardFilters({ queueId: 'sales', campaignId: 'renewals', status: 'busy', search: 'bo' }, state))
            .toEqual({ queueId: 'sales', campaignId: 'renewals', status: 'busy', search: 'bo' });
    });

    // Remembered from an earlier visit, a queue the supervisor no longer oversees would hide every agent.
    it('drops a queue or campaign that is gone, and a status it does not know', () => {
        expect(normalizeAgentBoardFilters({ queueId: 'old', campaignId: 'gone', status: 'sleeping', search: 42 }, state))
            .toEqual({ queueId: '', campaignId: '', status: '', search: '' });
        expect(normalizeAgentBoardFilters(null, state)).toEqual({ queueId: '', campaignId: '', status: '', search: '' });
    });

    it('says whether any filter is on', () => {
        expect(agentBoardFiltersActive({ queueId: '', campaignId: '', status: '', search: '  ' })).toBe(false);
        expect(agentBoardFiltersActive({ status: 'onCall' })).toBe(true);
    });
});
