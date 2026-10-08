import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/supervisor-actions.js';

// The supervisor's engagement banner, wired to a stand-in soft phone and Contact Center hub. Live, a supervisor who took
// a call over had only the banner: nothing to mute, hold or hang up. The banner hands the call to the phone as the
// engagement becomes taken over, once, and keeps its own Hang up when the phone could not take it. Confirmed live: the
// supervisor's phone got full control of the call.

const script = '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/contact-center-supervisor-phone.js';

let hub;
let banner;
let phone;

function createBanner() {
    return {
        innerHTML: '',
        hidden: true,
        getAttribute: name => ({ 'data-cc-monitor-strings': '{}', 'data-cc-monitor-hub-url': '/hub' })[name] ?? null,
        setAttribute() { },
        closest: () => null,
        querySelectorAll: () => [],
        addEventListener() { },
    };
}

function createPhone(promoted) {
    return {
        armMonitorLeg: vi.fn(),
        disarmMonitorLeg: vi.fn(),
        hangupMonitorLeg: vi.fn(),
        setMonitorLegMode: vi.fn(),
        promoteMonitorLeg: vi.fn(() => promoted),
        onMonitorLeg() { },
        showError: vi.fn(),
    };
}

// Loads the banner script afresh against the stand-ins; it wires the banner as the page loads.
async function load(promoted) {
    hub = {};
    banner = createBanner();
    phone = createPhone(promoted);

    globalThis.window = globalThis;
    globalThis.document = {
        readyState: 'complete',
        querySelector: () => null,
        querySelectorAll: selector => (selector === '[data-cc-monitor-banner]' ? [banner] : []),
    };
    globalThis.contactCenterRealTime = {
        connect: () => ({
            started: Promise.resolve(),
            connection: { on: (name, handler) => { hub[name] = handler; }, invoke: vi.fn() },
        }),
    };
    globalThis.telephonySoftPhone = { getInstance: () => phone };

    vi.resetModules();
    await import(script);
}

const engagementChanged = state => hub.SupervisorEngagementChanged({
    state,
    interactionId: 'int-1',
    monitorToken: 'tok',
    agentName: 'Ann',
    mode: 'Monitor',
});

describe('the supervisor\'s banner when they take the call over', () => {
    it('hands the call to the phone once, and goes', async () => {
        await load(true);
        engagementChanged('Requested');

        engagementChanged('TookOver');
        engagementChanged('TookOver');

        expect(phone.armMonitorLeg).toHaveBeenCalledWith('tok', expect.objectContaining({ interactionId: 'int-1' }));
        expect(phone.promoteMonitorLeg).toHaveBeenCalledTimes(1);
        expect(phone.promoteMonitorLeg).toHaveBeenCalledWith('tok');
        expect(banner.hidden).toBe(true);
        expect(banner.innerHTML).toBe('');
    });

    it('keeps Hang up when the phone could not take the call, and does not ask again', async () => {
        await load(false);
        engagementChanged('Requested');

        engagementChanged('TookOver');
        engagementChanged('TookOver');

        expect(phone.promoteMonitorLeg).toHaveBeenCalledTimes(1);
        expect(banner.hidden).toBe(false);
        expect(banner.innerHTML).toContain('Hang up');
        expect(banner.innerHTML).toContain('You took over Ann&#39;s call');
    });
});
