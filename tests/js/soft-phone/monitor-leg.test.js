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
    monitorLegReplaces,
    monitorLegTalks,
    anyMonitorLegTalks,
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

// Live, the phone kept its microphone off on a monitor leg whatever the mode: nobody heard a supervisor who joined a
// call or took it over. The microphone follows the mode instead.
describe("the supervisor's microphone on a monitor leg", () => {
    it('is off while listening', () => {
        expect(monitorLegTalks('Monitor')).toBe(false);
    });

    it('is on while coaching the agent and while joining the call', () => {
        expect(monitorLegTalks('Whisper')).toBe(true);
        expect(monitorLegTalks('Barge')).toBe(true);
    });

    it('is off for a mode it does not know', () => {
        expect(monitorLegTalks(undefined)).toBe(false);
        expect(monitorLegTalks('barge')).toBe(false);
    });

    it('is on while any leg the phone holds has the supervisor talking', () => {
        expect(anyMonitorLegTalks({ a: { info: { mode: 'Monitor' } }, b: { info: { mode: 'Barge' } } })).toBe(true);
    });

    it('is off when every leg is listening, or there is none', () => {
        expect(anyMonitorLegTalks({ a: { info: { mode: 'Monitor' } }, b: { info: null }, c: null })).toBe(false);
        expect(anyMonitorLegTalks({})).toBe(false);
        expect(anyMonitorLegTalks(null)).toBe(false);
    });
});

// Live: a takeover failed because the provider takes no command on a supervising leg, so the call cannot be bridged to
// it. The supervisor is rung again with the engagement's token on an ordinary leg, and the phone answers that in place
// of the leg it holds.
describe('the leg a takeover moves the engagement to', () => {
    it('replaces the leg the phone holds for the same engagement', () => {
        expect(monitorLegReplaces({ 'tok-1': { legId: 'sv-1' } }, { token: 'tok-1', legId: 'take-1' })).toBe(true);
    });

    it('is not the leg the phone already holds', () => {
        expect(monitorLegReplaces({ 'tok-1': { legId: 'sv-1' } }, { token: 'tok-1', legId: 'sv-1' })).toBe(false);
    });

    it('is nothing the phone holds no engagement for', () => {
        expect(monitorLegReplaces({ 'tok-1': { legId: 'sv-1' } }, { token: 'tok-2', legId: 'take-1' })).toBe(false);
        expect(monitorLegReplaces({}, { token: 'tok-1', legId: 'take-1' })).toBe(false);
        expect(monitorLegReplaces(null, { token: 'tok-1', legId: 'take-1' })).toBe(false);
        expect(monitorLegReplaces({ 'tok-1': { legId: 'sv-1' } }, { token: '', legId: 'take-1' })).toBe(false);
    });
});
