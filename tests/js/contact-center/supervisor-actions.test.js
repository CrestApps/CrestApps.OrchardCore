import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/supervisor-actions.js';

const {
    supervisorAgentActions,
    supervisorAgentActionsHtml,
    supervisorAgentMenuHtml,
    nextMonitorEngagement,
    monitorBannerHtml,
} = globalThis.CrestAppsContactCenter;

const onCall = overrides => ({
    agentId: 'agent-1',
    displayName: 'Ann',
    activeInteractionId: 'int-1',
    availableMonitoringModes: ['Monitor', 'Whisper', 'Barge'],
    availableInterventions: ['TakeOver', 'EndCall', 'Transfer', 'Record'],
    recordingState: 'Recording',
    ...overrides,
});

const supervisor = { canIntervene: true, canMessage: true };

// The dashboard shows what the server said is possible, and nothing else: a call whose provider cannot be monitored gets
// no mode buttons, an agent on no call gets only the agent actions, and a supervisor without the intervention permission
// cannot take over, end, transfer, record or set state.
describe('the dashboard actions for an agent', () => {
    it('offers Listen, Whisper and Barge that ring the supervisor while they are not on the call', () => {
        const actions = supervisorAgentActions(onCall(), supervisor, {});

        expect(actions.modes.map(mode => [mode.mode, mode.label, mode.action, mode.pressed])).toEqual([
            ['Monitor', 'Listen', 'engage', false],
            ['Whisper', 'Whisper', 'engage', false],
            ['Barge', 'Barge', 'engage', false],
        ]);
        expect(actions.stop).toBe(false);
        expect(actions.takeOver).toEqual(expect.objectContaining({ disabled: false, joinFirst: true }));
    });

    it('switches mode on the same leg once engaged, shows the active one pressed, and offers Stop', () => {
        const actions = supervisorAgentActions(onCall({ monitorMode: 'Whisper', monitorConnected: true }), supervisor, { engagedAs: 'You: {0}' });

        expect(actions.modes.map(mode => [mode.mode, mode.action, mode.pressed])).toEqual([
            ['Monitor', 'switch', false],
            ['Whisper', 'switch', true],
            ['Barge', 'switch', false],
        ]);
        expect(actions.stop).toBe(true);
        expect(actions.engagedLabel).toBe('You: Whisper');
        expect(actions.takeOver).toEqual(expect.objectContaining({ disabled: false, joinFirst: false }));
    });

    it('offers only the modes the server listed for the call', () => {
        const actions = supervisorAgentActions(onCall({ availableMonitoringModes: ['Monitor'] }), supervisor, {});

        expect(actions.modes.map(mode => mode.mode)).toEqual(['Monitor']);
        expect(supervisorAgentActions(onCall({ availableMonitoringModes: [] }), supervisor, {}).modes).toEqual([]);
    });

    it('holds Take over while the supervisor\'s phone is still connecting', () => {
        const actions = supervisorAgentActions(onCall({ monitorMode: 'Barge', monitorConnected: false }), supervisor, {});

        expect(actions.takeOver.disabled).toBe(true);
        expect(actions.engagedLabel).toBe('Connecting your phone…');
    });

    it('lists the call and agent interventions in the More menu', () => {
        const actions = supervisorAgentActions(onCall(), supervisor, {});

        expect(actions.menu.map(item => item.action)).toEqual([
            'end-call', 'transfer', 'record-off', 'state:Available', 'state:Away', 'state:Break', 'state:SignOut', 'message',
        ]);
    });

    it('offers to start recording a call that is not recording', () => {
        const actions = supervisorAgentActions(onCall({ recordingState: 'Stopped' }), supervisor, {});

        expect(actions.menu.map(item => item.action)).toContain('record-on');
    });

    it('gives an agent on no call only the agent actions', () => {
        const actions = supervisorAgentActions({ agentId: 'agent-2' }, supervisor, {});

        expect(actions.modes).toEqual([]);
        expect(actions.takeOver).toBeNull();
        expect(actions.menu.map(item => item.action)).toEqual(['state:Available', 'state:Away', 'state:Break', 'state:SignOut', 'message']);
    });

    it('gives a supervisor without the intervention permission only messaging', () => {
        const actions = supervisorAgentActions(onCall({ availableInterventions: [] }), { canIntervene: false, canMessage: true }, {});

        expect(actions.takeOver).toBeNull();
        expect(actions.menu.map(item => item.action)).toEqual(['message']);
    });

    it('says why a call cannot be monitored', () => {
        const html = supervisorAgentActionsHtml({ agentId: 'agent-3', monitoringUnavailableReason: 'Not a Contact Center call' }, { canMessage: false }, {});

        expect(html).toContain('Not a Contact Center call');
        expect(html).toContain('role="note"');
    });

    it('marks the pressed mode for assistive technology and escapes what it shows', () => {
        const agent = onCall({ agentId: '<a>', monitorMode: 'Monitor', monitorConnected: true });
        const html = supervisorAgentActionsHtml(agent, supervisor, {});
        const menu = supervisorAgentMenuHtml(agent, supervisor, {}, true);

        expect(html).toContain('data-cc-switch="int-1" data-cc-mode="Monitor" aria-pressed="true"');
        expect(menu).toContain('aria-expanded="true"');
        expect(html + menu).not.toContain('<a>');
    });
});

// The supervisor's own phone follows the engagement from their click to its end.
describe('the supervisor phone engagement', () => {
    const requested = nextMonitorEngagement(null, {
        source: 'hub',
        notification: { state: 'Requested', interactionId: 'int-1', monitorToken: 'tok', agentName: 'Ann', mode: 'Monitor' },
    });

    it('is requested, then connecting as the phone answers, then connected', () => {
        expect(requested.phase).toBe('requested');

        const answering = nextMonitorEngagement(requested, { source: 'phone', type: 'answering', token: 'tok' });
        expect(answering.phase).toBe('connecting');

        const connected = nextMonitorEngagement(answering, { source: 'hub', notification: { state: 'Connected', interactionId: 'int-1' } });
        expect(connected.phase).toBe('connected');
    });

    it('ignores the events of another engagement\'s leg', () => {
        expect(nextMonitorEngagement(requested, { source: 'phone', type: 'ended', token: 'other' })).toBe(requested);
        expect(nextMonitorEngagement(requested, { source: 'hub', notification: { state: 'Ended', interactionId: 'int-2' } })).toBe(requested);
    });

    it('changes mode, takes over, and ends', () => {
        const whisper = nextMonitorEngagement(requested, { source: 'hub', notification: { state: 'ModeChanged', interactionId: 'int-1', mode: 'Whisper' } });
        expect(whisper.mode).toBe('Whisper');

        const tookOver = nextMonitorEngagement(whisper, { source: 'hub', notification: { state: 'TookOver', interactionId: 'int-1' } });
        expect(tookOver.phase).toBe('tookOver');

        const ended = nextMonitorEngagement(tookOver, { source: 'phone', type: 'ended', token: 'tok' });
        expect(ended.phase).toBe('ended');
    });

    it('shows who is being monitored, a mode switcher and Stop', () => {
        const connected = { ...requested, phase: 'connected', mode: 'Whisper' };
        const html = monitorBannerHtml(connected, {});

        expect(html).toContain('Monitoring Ann');
        expect(html).toContain('Only the agent hears you.');
        expect(html).toContain('data-cc-monitor-mode="Whisper"');
        expect(html).toMatch(/aria-checked="true" data-cc-monitor-mode="Whisper"/);
        expect(html).toContain('data-cc-monitor-stop');
        expect(html).not.toMatch(/data-cc-monitor-mode="[A-Za-z]+" disabled/);
    });

    it('offers Hang up, and no mode switcher, once the supervisor took the call over', () => {
        const html = monitorBannerHtml({ ...requested, phase: 'tookOver' }, {});

        expect(html).toContain('You took over Ann&#39;s call');
        expect(html).toContain('Hang up');
        expect(html).not.toContain('data-cc-monitor-mode');
    });

    it('shows nothing once the engagement ended', () => {
        expect(monitorBannerHtml({ ...requested, phase: 'ended' }, {})).toBe('');
        expect(monitorBannerHtml(null, {})).toBe('');
    });
});

describe('a supervisor\'s message to an agent', () => {
    const { supervisorMessageHtml } = globalThis.CrestAppsContactCenter;

    it('shows who it is from and what it says, escaped, with a way to dismiss it', () => {
        const html = supervisorMessageHtml({ messageId: 'm1', fromName: 'Sam', text: 'Offer <b>10%</b>' }, {});

        expect(html).toContain('Message from Sam');
        expect(html).toContain('Offer &lt;b&gt;10%&lt;/b&gt;');
        expect(html).toContain('data-cc-dismiss-message');
        expect(html).toContain('role="alert"');
    });

    it('shows nothing for an empty message', () => {
        expect(supervisorMessageHtml({ text: '' }, {})).toBe('');
    });
});

describe('the bundles', () => {
    const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets.json', 'utf8'));
    const helper = 'Assets/js/shared/supervisor-actions.js';

    it.each([
        ['wwwroot/scripts/supervisor-dashboard.js', 'Assets/js/supervisor-dashboard.js'],
        ['wwwroot/scripts/contact-center-supervisor-phone.js', 'Assets/js/contact-center-supervisor-phone.js'],
    ])('%s carries the helper ahead of the script that uses it', (output, script) => {
        const group = assets.find(candidate => candidate.output === output);

        expect(group).toBeDefined();
        expect(group.inputs.indexOf(helper)).toBeGreaterThanOrEqual(0);
        expect(group.inputs.indexOf(helper)).toBeLessThan(group.inputs.indexOf(script));
    });
});

// The More menu opened to the left of its button and slid under the admin sidebar, so its items were cut off. It is a
// compact kebab button now, and the menu is placed in the viewport, never partly off it.
describe('the More actions button', () => {
    it('is a labelled kebab icon with a tooltip, not a text button', () => {
        const html = supervisorAgentMenuHtml(onCall(), supervisor, {}, false);

        expect(html).toContain('fa-ellipsis-vertical');
        expect(html).toContain('aria-label="More actions for Ann"');
        expect(html).toContain('title="More actions for Ann"');
        expect(html).toContain('aria-expanded="false"');
        expect(html).toContain(' hidden>');
        expect(html).not.toContain('&#9662;');
    });

    it('is apart from the call actions, so it can sit at the far right of the name row', () => {
        const actions = supervisorAgentActionsHtml(onCall(), supervisor, {});

        expect(actions).not.toContain('data-cc-more');
        expect(actions).toContain('data-cc-takeover');
        expect(supervisorAgentMenuHtml(onCall(), supervisor, {}, false)).not.toContain('data-cc-takeover');
    });

    it('is not shown when the supervisor has nothing to do there', () => {
        expect(supervisorAgentMenuHtml({ agentId: 'agent-3' }, { canIntervene: false, canMessage: false }, {}, false)).toBe('');
    });
});

describe('placing the More menu', () => {
    const { supervisorMenuPlacement } = globalThis.CrestAppsContactCenter;
    const viewport = { width: 1400, height: 900 };
    const menu = { width: 200, height: 300 };

    it('opens under the button with its right edge on the button\'s', () => {
        expect(supervisorMenuPlacement({ left: 600, right: 632, top: 100, bottom: 130 }, menu, viewport))
            .toEqual({ left: 432, top: 134, flipped: false });
    });

    it('moves right rather than off the left edge of the screen', () => {
        expect(supervisorMenuPlacement({ left: 40, right: 72, top: 100, bottom: 130 }, menu, viewport).left).toBe(8);
    });

    it('moves left rather than off the right edge', () => {
        expect(supervisorMenuPlacement({ left: 1390, right: 1450, top: 100, bottom: 130 }, menu, viewport).left).toBe(1192);
    });

    it('flips above the button when there is no room below', () => {
        expect(supervisorMenuPlacement({ left: 600, right: 632, top: 800, bottom: 830 }, menu, viewport))
            .toEqual({ left: 432, top: 496, flipped: true });
    });

    it('stays on screen when there is room neither above nor below', () => {
        const placed = supervisorMenuPlacement({ left: 100, right: 132, top: 200, bottom: 230 }, menu, { width: 430, height: 400 });

        expect(placed.flipped).toBe(false);
        expect(placed.top).toBe(92);
        expect(placed.top + menu.height).toBeLessThanOrEqual(400 - 8);
    });

    it('fits a phone-width window', () => {
        const placed = supervisorMenuPlacement({ left: 300, right: 332, top: 100, bottom: 130 }, menu, { width: 430, height: 740 });

        expect(placed.left).toBe(132);
        expect(placed.left + menu.width).toBeLessThanOrEqual(430 - 8);
    });
});
