import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/auto-answer.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/quality.js';
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
    planMonitorLegPromotion,
    planMonitorCallPromotion,
    monitorLegTalks,
    anyMonitorLegTalks,
    holdsMonitorLeg,
    readMonitorLegMedia,
    describeMonitorLegMedia,
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

// Live, a supervisor who took a call over had only a banner for it: nothing to mute, hold or hang up. The leg they were
// on leaves the monitor legs and becomes a call of their phone's own. Confirmed live: the supervisor's phone got full
// control of the call.
describe('the leg a supervisor took the call over on', () => {
    const promotable = { legId: 'take-1', controller: { promote: () => true } };

    it('is taken out of the monitor legs', () => {
        const legs = { 'tok-1': promotable, 'tok-2': { legId: 'sv-2', controller: { promote: () => true } } };

        expect(planMonitorLegPromotion(legs, 'tok-1')).toBe(promotable);
        expect(Object.keys(legs)).toEqual(['tok-2']);
    });

    it('is missed when the phone holds no leg for the engagement, and nothing is taken out', () => {
        const legs = { 'tok-1': promotable };

        expect(planMonitorLegPromotion(legs, 'tok-other')).toBeNull();
        expect(planMonitorLegPromotion(legs, '')).toBeNull();
        expect(planMonitorLegPromotion(legs, undefined)).toBeNull();
        expect(planMonitorLegPromotion(null, 'tok-1')).toBeNull();
        expect(legs).toEqual({ 'tok-1': promotable });
    });

    it('is missed when the leg it holds cannot be promoted, and is kept', () => {
        const withoutController = { legId: 'sv-1' };
        const withoutPromote = { legId: 'sv-2', controller: { hangup: () => { } } };
        const legs = { 'tok-1': withoutController, 'tok-2': withoutPromote };

        expect(planMonitorLegPromotion(legs, 'tok-1')).toBeNull();
        expect(planMonitorLegPromotion(legs, 'tok-2')).toBeNull();
        expect(legs).toEqual({ 'tok-1': withoutController, 'tok-2': withoutPromote });
    });
});

describe('promoting the monitor call a supervisor took over', () => {
    const call = { id: 'take-1' };
    const other = { id: 'sv-2' };

    it('becomes the phone\'s current call when it holds no other', () => {
        const calls = [other, call];

        expect(planMonitorCallPromotion(calls, call, null, false)).toEqual({ promoted: true, current: call });
        expect(calls).toEqual([other]);
    });

    it('leaves the phone\'s current call as it is when it holds one', () => {
        const current = { id: 'own-call' };
        const calls = [call];

        expect(planMonitorCallPromotion(calls, call, current, false)).toEqual({ promoted: true, current });
        expect(calls).toEqual([]);
    });

    it('is not promoted once the call has ended or the phone is gone, but still leaves the monitor calls', () => {
        const calls = [call, other];

        expect(planMonitorCallPromotion(calls, call, null, true)).toEqual({ promoted: false, current: null });
        expect(calls).toEqual([other]);
    });

    it('is promoted even when the phone no longer lists it as a monitor call', () => {
        const calls = [other];

        expect(planMonitorCallPromotion(calls, call, null, false)).toEqual({ promoted: true, current: call });
        expect(calls).toEqual([other]);
    });
});

// Live (2026-09-26), the supervisor's Bluetooth microphone dropped while they listened. The phone held no call of its own,
// so it took itself for idle and registered again: that hung the monitor leg up, and the next engagement rang the SIP
// address being replaced and was refused (480, "failed to connect"). A monitor leg is live media like any call.
describe('whether the phone holds a monitor leg', () => {
    it('does while any leg is held', () => {
        expect(holdsMonitorLeg({ 'tok-1': { legId: 'sv-1', info: { mode: 'Monitor' } } })).toBe(true);
    });

    it('does not with none', () => {
        expect(holdsMonitorLeg({})).toBe(false);
        expect(holdsMonitorLeg(null)).toBe(false);
        expect(holdsMonitorLeg({ 'tok-1': null })).toBe(false);
    });
});

const statsReport = stats => new Map(stats.map((stat, index) => [stat.id || `s${index}`, stat]));

// Live, the supervisor heard nothing while listening, and the only report was what the phone SENT. Whether the provider
// delivered silence or nothing at all could not be told apart. The report says what arrives and how loud it is, per window.
describe('reading what a monitor leg carries', () => {
    it('reads the bytes each way, the level heard over the window, and the codec', () => {
        const previous = readMonitorLegMedia(statsReport([
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 1000, totalAudioEnergy: 0.1, totalSamplesDuration: 10, codecId: 'c1' },
            { type: 'outbound-rtp', kind: 'audio', bytesSent: 500 },
            { id: 'c1', type: 'codec', mimeType: 'audio/opus', clockRate: 48000 },
        ]), null);

        const media = readMonitorLegMedia(statsReport([
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 17000, totalAudioEnergy: 0.1 + (0.2 * 0.2 * 10), totalSamplesDuration: 20, codecId: 'c1' },
            { type: 'outbound-rtp', kind: 'audio', bytesSent: 16500 },
            { type: 'media-source', kind: 'audio', totalAudioEnergy: 0, totalSamplesDuration: 20 },
            { id: 'c1', type: 'codec', mimeType: 'audio/opus', clockRate: 48000 },
        ]), previous);

        expect(media.bytesSent).toBe(16500);
        expect(media.bytesReceived).toBe(17000);
        expect(media.heardLevel).toBeCloseTo(0.2, 5);
        expect(media.microphoneLevel).toBe(0);
        expect(media.codec).toBe('audio/opus');
    });

    it('says silence arrives when packets flow but carry nothing', () => {
        const media = readMonitorLegMedia(statsReport([
            { type: 'inbound-rtp', kind: 'audio', bytesReceived: 9000, totalAudioEnergy: 0, totalSamplesDuration: 10 },
        ]), null);

        expect(describeMonitorLegMedia('sendrecv/off/live/shared', media))
            .toBe('Monitor leg media: transceivers sendrecv/off/live/shared, sent 0 bytes (mic -), received 9000 bytes (heard 0.000), codec -.');
    });

    it('reports unknown levels as a dash rather than as silence', () => {
        const media = readMonitorLegMedia(statsReport([]), null);

        expect(media.heardLevel).toBe(-1);
        expect(describeMonitorLegMedia('', media))
            .toBe('Monitor leg media: transceivers none, sent 0 bytes (mic -), received 0 bytes (heard -), codec -.');
    });
});
