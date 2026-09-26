import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/auto-answer.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/monitor-leg.js';

const {
    MONITOR_ARM_WAIT_MS,
    MONITOR_ARM_WINDOW_MS,
    createMonitorLegArms,
    armMonitorLeg,
    disarmMonitorLeg,
    readMonitorLegTag,
    claimMonitorLegArm,
    monitorLegAction,
} = globalThis.CrestAppsSoftPhone;

const now = 9_000_000;
const encode = state => Buffer.from(JSON.stringify(state)).toString('base64');

// The platform rings a supervisor's own phone to play them a call. The phone must answer that leg by itself -- and
// only that leg: a colleague's call, an offer's leg, anything else, rings or is handled exactly as before.
describe('reading a monitor leg', () => {
    it('reads the token from the client state the platform stamps on the leg', () => {
        expect(readMonitorLegTag({ telnyxCallControlId: 'sv-1', clientState: encode({ i: 'cc-sv', l: 'tok-1' }) }))
            .toEqual({ token: 'tok-1', legId: 'sv-1' });
    });

    it('reads the token from the SIP header when the SDK hands over no client state', () => {
        expect(readMonitorLegTag({ telnyxCallControlId: 'sv-1', customHeaders: [{ name: 'X-Monitor-Leg', value: 'tok-2' }] }))
            .toEqual({ token: 'tok-2', legId: 'sv-1' });
    });

    it('is not a monitor leg without the intent or the header', () => {
        expect(readMonitorLegTag({ telnyxCallControlId: 'leg-1', clientState: encode({ i: 'ob-dest', p: 'x' }) })).toBeNull();
        expect(readMonitorLegTag({ telnyxCallControlId: 'leg-1', customHeaders: [{ name: 'X-Transfer-Leg', value: '1' }] })).toBeNull();
        expect(readMonitorLegTag({})).toBeNull();
        expect(readMonitorLegTag(null)).toBeNull();
    });
});

describe('the monitor-leg arm', () => {
    it('answers the leg carrying the armed token, once', () => {
        const arms = createMonitorLegArms();
        armMonitorLeg(arms, 'tok-1', now, { agentName: 'Ann' });
        const tag = { token: 'tok-1', legId: 'sv-1' };

        expect(monitorLegAction(arms, tag, now + 10, now)).toBe('answer');
        expect(claimMonitorLegArm(arms, tag, now + 10)).toEqual({ until: now + MONITOR_ARM_WINDOW_MS, info: { agentName: 'Ann' } });
        expect(claimMonitorLegArm(arms, tag, now + 20)).toBeNull();
    });

    it('never answers a leg carrying a different token', () => {
        const arms = createMonitorLegArms();
        armMonitorLeg(arms, 'tok-1', now);

        expect(claimMonitorLegArm(arms, { token: 'tok-other', legId: 'sv-2' }, now)).toBeNull();
        expect(monitorLegAction(arms, { token: 'tok-other' }, now + MONITOR_ARM_WAIT_MS, now)).toBe('hangup');
        expect(monitorLegAction(arms, { token: '' }, now + MONITOR_ARM_WAIT_MS, now)).toBe('hangup');
    });

    it('waits a moment for an arm still on its way, then hangs the leg up', () => {
        const arms = createMonitorLegArms();
        const tag = { token: 'tok-late', legId: 'sv-3' };

        expect(monitorLegAction(arms, tag, now + 100, now)).toBe('wait');
        expect(monitorLegAction(arms, tag, now + MONITOR_ARM_WAIT_MS, now)).toBe('hangup');

        armMonitorLeg(arms, 'tok-late', now + 200);
        expect(monitorLegAction(arms, tag, now + 300, now)).toBe('answer');
    });

    it('lapses', () => {
        const arms = createMonitorLegArms();
        armMonitorLeg(arms, 'tok-1', now);
        const tag = { token: 'tok-1' };

        expect(claimMonitorLegArm(arms, tag, now + MONITOR_ARM_WINDOW_MS)).toBeNull();
    });

    it('is dropped when the engagement is over before the leg arrives', () => {
        const arms = createMonitorLegArms();
        armMonitorLeg(arms, 'tok-1', now);
        disarmMonitorLeg(arms, 'tok-1');

        expect(claimMonitorLegArm(arms, { token: 'tok-1' }, now + 1)).toBeNull();
    });

    it('refuses an arm without a token', () => {
        expect(armMonitorLeg(createMonitorLegArms(), '', now)).toBe(false);
    });
});

// A monitor leg is never one the ordinary auto-answer takes: an arm left for an offer must not answer it.
describe('the soft phone bundle', () => {
    const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.Telephony/Assets.json', 'utf8'));
    const group = assets.find(candidate => candidate.output === 'wwwroot/scripts/soft-phone.js');

    it('carries the monitor-leg helper after the client-state reader it uses and ahead of the soft phone', () => {
        expect(group.inputs).toContain('Assets/js/soft-phone/monitor-leg.js');
        expect(group.inputs.indexOf('Assets/js/soft-phone/offer-leg.js')).toBeLessThan(group.inputs.indexOf('Assets/js/soft-phone/monitor-leg.js'));
        expect(group.inputs.indexOf('Assets/js/soft-phone/monitor-leg.js')).toBeLessThan(group.inputs.indexOf('Assets/js/soft-phone.js'));
    });
});
