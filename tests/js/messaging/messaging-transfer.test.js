import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

const { assignmentToastTitle, classifyAssignment, formatText, transferTargetInputName } = globalThis.CrestAppsMessaging;

// A transfer reaches the recipient, the queue whose pool it went to, and whoever held it before. Each of them is told
// something different, and the person who made the transfer is told nothing: they already know.
describe('classifyAssignment', () => {
    const view = { agentId: 'agent-b', conversationId: 'c-open' };

    it('tells the recipient a conversation was transferred to them', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-b', transferredByAgentId: 'agent-a', conversationId: 'c-9' }, view)).toBe('to-me');
    });

    it('stays quiet for the person who made the transfer', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-c', transferredByAgentId: 'agent-b', conversationId: 'c-open' }, view)).toBe('refresh');
    });

    it('tells the queue a conversation was sent back to its shared pool', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: null, ownerQueueId: 'q-1', transferredByAgentId: 'agent-a', conversationId: 'c-9' }, view)).toBe('to-queue');
    });

    it('tells someone looking at the conversation that it moved on without them', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-c', previousAgentId: 'agent-b', transferredByAgentId: 'agent-s', conversationId: 'c-open' }, view)).toBe('away');
    });

    it('prefers saying the open conversation moved over announcing it to the queue', () => {
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

// The sentences the pages carry for script to complete, as MessagingNotifications.cshtml and Workspace.cshtml word them.
const texts = {
    someone: 'Someone',
    toYou: '{0} transferred a conversation to you',
    toQueue: '{0} sent a conversation to the {1} queue',
    toYourQueue: '{0} sent a conversation to one of your queues',
    away: 'This conversation was transferred to {0}',
};

describe('assignmentToastTitle', () => {
    it('tells the recipient who transferred the conversation to them', () => {
        expect(assignmentToastTitle('to-me', { transferredByName: 'Ann' }, texts)).toBe('Ann transferred a conversation to you');
    });

    it('says someone when the sender has no name', () => {
        expect(assignmentToastTitle('to-me', { transferredByName: '' }, texts)).toBe('Someone transferred a conversation to you');
    });

    it('names the queue a conversation was sent back to', () => {
        expect(assignmentToastTitle('to-queue', { transferredByName: 'Ann', transferredToName: 'Billing' }, texts))
            .toBe('Ann sent a conversation to the Billing queue');
    });

    it('says one of your queues when the queue has no name', () => {
        expect(assignmentToastTitle('to-queue', { transferredByName: 'Ann' }, texts)).toBe('Ann sent a conversation to one of your queues');
    });

    it('tells someone looking at the conversation where it went', () => {
        expect(assignmentToastTitle('away', { transferredToName: 'Bea' }, texts)).toBe('This conversation was transferred to Bea');
        expect(assignmentToastTitle('away', {}, texts)).toBe('This conversation was transferred to Someone');
    });

    it('raises no toast for a claim or a routed assignment', () => {
        expect(assignmentToastTitle('refresh', { transferredByName: 'Ann' }, texts)).toBeNull();
    });

    // Both pages raise these toasts, so both must take their titles from the one tested choice.
    it.each([
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-notifications.js',
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-workspace.js',
    ])('is what %s titles its transfer toasts with', path => {
        expect(readFileSync(path, 'utf8')).toContain('messaging.assignmentToastTitle(kind, notification, transferTexts)');
    });
});

// The thread shows each transfer with the note the sender left for the next agent.
describe('the transfer row in the thread', () => {
    const view = readFileSync('src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Views/Admin/_ThreadEvent.cshtml', 'utf8');

    it('is worded by the tested ThreadTimeline.DescribeTransfer', () => {
        expect(view).toContain('ThreadTimeline.DescribeTransfer(Model, T)');
    });

    it('still shows the note', () => {
        expect(view).toContain('@Model.Note');
    });
});

// The transfer picker searches as the agent types. The shared selector sends the text as "query", and the actions it
// calls must bind a parameter of that name, or every search quietly returns the unfiltered list.
describe('the transfer picker search', () => {
    it('sends the typed text as a query parameter', () => {
        const selector = readFileSync('src/Modules/CrestApps.OrchardCore.Resources/Assets/js/item-selector.js', 'utf8');

        expect(selector).toContain("url.searchParams.set('query', query)");
    });

    it.each(['TransferAgents', 'TransferQueues'])('reaches %s as its query parameter', action => {
        const controller = readFileSync('src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Controllers/AdminController.cs', 'utf8');

        expect(controller).toContain(`public async Task<IActionResult> ${action}(string id, string query)`);
    });
});
