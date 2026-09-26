import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

const { classifyAssignment, formatText, transferTargetInputName } = globalThis.CrestAppsMessaging;

// A transfer reaches the recipient, the team whose pool it went to, and whoever held it before. Each of them is told
// something different, and the person who made the transfer is told nothing: they already know.
describe('classifyAssignment', () => {
    const view = { agentId: 'agent-b', conversationId: 'c-open' };

    it('tells the recipient a conversation was transferred to them', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-b', transferredByAgentId: 'agent-a', conversationId: 'c-9' }, view)).toBe('to-me');
    });

    it('stays quiet for the person who made the transfer', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-c', transferredByAgentId: 'agent-b', conversationId: 'c-open' }, view)).toBe('refresh');
    });

    it('tells the team a conversation was sent back to its shared pool', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: null, ownerQueueId: 'q-1', transferredByAgentId: 'agent-a', conversationId: 'c-9' }, view)).toBe('to-team');
    });

    it('tells someone looking at the conversation that it moved on without them', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-c', previousAgentId: 'agent-b', transferredByAgentId: 'agent-s', conversationId: 'c-open' }, view)).toBe('away');
    });

    it('prefers saying the open conversation moved over announcing it to the team', () => {
        expect(classifyAssignment({ isTransfer: true, ownerQueueId: 'q-1', transferredByAgentId: 'agent-s', conversationId: 'c-open' }, view)).toBe('away');
    });

    it('only refreshes the list for a claim or a routed assignment', () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('refresh');
        expect(classifyAssignment(null, view)).toBe('refresh');
    });

    it('does not mistake a transfer to a colleague for one to the viewer when the viewer has no agent profile', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-c', transferredByAgentId: 'agent-a', conversationId: 'c-9' }, {})).toBe('refresh');
    });
});

describe('formatText', () => {
    it('fills each numbered placeholder', () => {
        expect(formatText('{0} sent a conversation to {1}', ['Ann', 'Billing'])).toBe('Ann sent a conversation to Billing');
    });

    it('fills a placeholder used twice and leaves a missing one empty', () => {
        expect(formatText('{0} and {0} then {1}', ['x'])).toBe('x and x then ');
    });

    it('treats a missing template as empty', () => {
        expect(formatText(null, ['x'])).toBe('');
    });
});

// The transfer form carries a picker for a person and one for a team; only the one chosen must hold a selection.
describe('transferTargetInputName', () => {
    it('reads the team picker for a team transfer', () => {
        expect(transferTargetInputName('Queue')).toBe('targetQueueId');
    });

    it('reads the person picker otherwise', () => {
        expect(transferTargetInputName('Agent')).toBe('targetAgentId');
        expect(transferTargetInputName(undefined)).toBe('targetAgentId');
    });
});
