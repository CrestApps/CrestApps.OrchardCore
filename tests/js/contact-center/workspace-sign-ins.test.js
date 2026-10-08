import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/workspace-panels.js';

const { signInsHtml } = globalThis.CrestAppsContactCenter;

const view = readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Views/AgentWorkspace/Index.cshtml', 'utf8');

const labels = {
    queues: 'Queues',
    campaigns: 'Campaigns',
    noSignIns: 'You are not signed in to any queue or campaign.',
    waiting: '{0} waiting',
};

// The workspace's top bar listed every signed-in queue beside the agent's name and status. An agent signed in to many
// queues and campaigns, or to ones with long names, crowded it. The sign-ins now have a card of their own.
describe('signInsHtml', () => {
    it('lists each queue with how many are waiting, and each campaign', () => {
        const html = signInsHtml(
            [{ name: 'Support', waitingCount: 2 }, { name: 'Billing', waitingCount: 0 }],
            [{ name: 'Spring renewals' }],
            labels);

        expect(html).toContain('Queues');
        expect(html).toContain('Support');
        expect(html).toContain('>2<');
        expect(html).toContain('title="2 waiting"');
        expect(html).toContain('Billing');
        expect(html).toContain('is-empty');
        expect(html).toContain('Campaigns');
        expect(html).toContain('Spring renewals');
    });

    it('leaves out a group the agent has nothing in', () => {
        const html = signInsHtml([{ name: 'Support', waitingCount: 0 }], [], labels);

        expect(html).toContain('Queues');
        expect(html).not.toContain('Campaigns');
    });

    it('says so when the agent is signed in to nothing', () => {
        expect(signInsHtml([], [], labels)).toContain('You are not signed in to any queue or campaign.');
        expect(signInsHtml(null, undefined, labels)).toContain('You are not signed in to any queue or campaign.');
    });

    // A long name is cut short on screen, so the whole name is kept where it can still be read.
    it('keeps the whole name of every chip in its title', () => {
        const name = 'Outbound follow-up for customers who asked for a call back after hours';
        const html = signInsHtml([], [{ name }], labels);

        expect(html).toContain(`title="${name}"`);
    });

    it('escapes the names it shows', () => {
        const html = signInsHtml([{ name: '<b>Support</b>', waitingCount: 0 }], [{ name: 'A "quoted" one' }], labels);

        expect(html).not.toContain('<b>');
        expect(html).toContain('&lt;b&gt;Support&lt;/b&gt;');
        expect(html).toContain('A &quot;quoted&quot; one');
    });
});

describe('the workspace layout', () => {
    it('no longer lists the sign-ins in the top bar, and gives them a card', () => {
        const topBar = view.slice(view.indexOf('<div class="cc-topbar">'), view.indexOf('<div class="cc-error"'));

        expect(topBar).not.toContain('data-cc-queues');
        expect(view).toContain('data-cc-sign-ins');
    });

    it('puts the Live dashboard link last in the top bar', () => {
        const topBar = view.slice(view.indexOf('<div class="cc-topbar">'), view.indexOf('<div class="cc-error"'));

        expect(topBar.lastIndexOf('Live dashboard')).toBeGreaterThan(topBar.lastIndexOf('data-cc-connection'));
        expect(topBar).toContain('cc-topbar__end');
    });
});
