import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import {
    assignmentToastBody,
    assignmentToastTitle,
    classifyAssignment,
    formatText,
    transferTargetInputName,
} from '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

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

    it("only refreshes the list for the viewer's own claim or reply", () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', assignedByAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('refresh');
        expect(classifyAssignment(null, view)).toBe('refresh');
    });

    it('tells the agent routing pushed a conversation to', () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('assigned-to-me');
    });

    it('tells the agent someone else assigned a conversation to', () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', assignedByAgentId: 'agent-s', conversationId: 'c-9' }, view)).toBe('assigned-to-me');
    });

    it("stays quiet when the conversation was already the viewer's", () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', previousAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('refresh');
    });

    it('only refreshes the list for an assignment to a colleague or a claim from the pool', () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-c', assignedByAgentId: 'agent-c', ownerQueueId: 'q-1', conversationId: 'c-9' }, view)).toBe('refresh');
        expect(classifyAssignment({ assignedAgentId: 'agent-c', previousAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('refresh');
    });

    it('stays quiet for the person who made a transfer even when it is credited as an assignment', () => {
        expect(classifyAssignment({ isTransfer: true, assignedAgentId: 'agent-b', assignedByAgentId: 'agent-b', conversationId: 'c-9' }, view)).toBe('refresh');
    });

    it('tells nobody about a routed assignment when the viewer has no agent profile', () => {
        expect(classifyAssignment({ assignedAgentId: 'agent-b', conversationId: 'c-9' }, {})).toBe('refresh');
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
    assigned: 'A conversation was assigned to you',
    assignedFrom: 'Assigned to you: {0}',
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

    it("raises no toast for the viewer's own claim", () => {
        expect(assignmentToastTitle('refresh', { transferredByName: 'Ann' }, texts)).toBeNull();
    });

    it('names who an assigned conversation is with', () => {
        expect(assignmentToastTitle('assigned-to-me', { contactAddress: '+15550100' }, texts)).toBe('Assigned to you: +15550100');
    });

    it('still tells the agent when the contact is unknown', () => {
        expect(assignmentToastTitle('assigned-to-me', {}, texts)).toBe('A conversation was assigned to you');
    });

    // Both pages raise these toasts, so both must take their titles from the one tested choice.
    it.each([
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-notifications.js',
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-workspace.js',
    ])('is what %s titles its transfer toasts with', path => {
        expect(readFileSync(path, 'utf8')).toContain('assignmentToastTitle(kind, notification, transferTexts)');
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

describe('assignmentToastBody', () => {
    it('shows the latest message under a title that already names the contact', () => {
        expect(assignmentToastBody('assigned-to-me', { contactAddress: '+15550100', preview: 'Hi there' })).toBe('Hi there');
    });

    it('names the contact of a transferred conversation with its latest message', () => {
        expect(assignmentToastBody('to-me', { contactAddress: 'ann@example.com', preview: 'Hi there' })).toBe('ann@example.com: Hi there');
        expect(assignmentToastBody('to-me', { contactAddress: 'ann@example.com' })).toBe('ann@example.com');
    });

    it('is empty when there is nothing to show', () => {
        expect(assignmentToastBody('to-me', {})).toBe('');
        expect(assignmentToastBody('to-me', null)).toBe('');
    });

    // A routed push sends the assignment and then the message; both pages announce it once, as an assignment.
    it.each([
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-notifications.js',
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/messaging-workspace.js',
    ])('is what %s fills its assignment toasts with, and it swallows the message that follows', path => {
        const source = readFileSync(path, 'utf8');

        expect(source).toContain('assignmentToastBody(kind, notification)');
        expect(source).toContain("recent.isNew(notificationKey('inbound', notification));");
        expect(source).toContain("'assigned-to-me'");
    });

    it.each([
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Views/MessagingNotifications.cshtml',
        'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Views/Admin/Workspace.cshtml',
    ])('has %s carry the assignment sentences', path => {
        const view = readFileSync(path, 'utf8');

        expect(view).toContain('data-assigned-to-you-text=');
        expect(view).toContain('data-assigned-from-to-you-text=');
    });
});
