import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/agent-mute.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/keypad-dial.js';

const {
    rememberAgentMute,
    isAgentMuted,
    pruneAgentMutes,
    reconcileAgentMute,
    muteGroupCallIds,
    conferenceMuted,
    sharedMicrophoneEnabled,
} = globalThis.CrestAppsSoftPhone;

// With browser audio the mute happens in the browser and the provider keeps nothing, so every later report from the
// server says the call is unmuted. Taking those reports at their word turned the agent's microphone back on at the
// next active-call refresh, about five seconds after they pressed Mute.
describe('reconcileAgentMute', () => {
    it('keeps a mute performed in this browser when the server reports the call unmuted', () => {
        expect(reconcileAgentMute(false, true, true)).toEqual({ isMuted: true, agentMuted: true });
    });

    it('keeps the mute through every report that follows, not just the first', () => {
        const mutes = {};
        rememberAgentMute(mutes, 'call-1', true);

        for (let report = 0; report < 5; report++) {
            const outcome = reconcileAgentMute(false, isAgentMuted(mutes, 'call-1'), true);
            rememberAgentMute(mutes, 'call-1', outcome.agentMuted);

            expect(outcome.isMuted).toBe(true);
        }

        expect(isAgentMuted(mutes, 'call-1')).toBe(true);
    });

    it('does not let a stale muted report re-mute a call the agent unmuted here', () => {
        expect(reconcileAgentMute(true, false, true)).toEqual({ isMuted: false, agentMuted: false });
    });

    it('takes the report as it comes where the provider performs the mute', () => {
        expect(reconcileAgentMute(false, true, false)).toEqual({ isMuted: false, agentMuted: false });
        expect(reconcileAgentMute(true, false, false)).toEqual({ isMuted: true, agentMuted: true });
    });
});

describe('rememberAgentMute / pruneAgentMutes', () => {
    it('forgets a mute when the agent unmutes', () => {
        const mutes = {};
        rememberAgentMute(mutes, 'call-1', true);
        rememberAgentMute(mutes, 'call-1', false);

        expect(isAgentMuted(mutes, 'call-1')).toBe(false);
    });

    it('forgets the mutes of calls that are no longer live', () => {
        const mutes = {};
        rememberAgentMute(mutes, 'call-1', true);
        rememberAgentMute(mutes, 'call-2', true);

        pruneAgentMutes(mutes, ['call-2']);

        expect(isAgentMuted(mutes, 'call-1')).toBe(false);
        expect(isAgentMuted(mutes, 'call-2')).toBe(true);
    });

    it('ignores a missing ledger or call id', () => {
        expect(() => rememberAgentMute(null, 'call-1', true)).not.toThrow();
        expect(isAgentMuted({}, null)).toBe(false);
        expect(() => pruneAgentMutes(null, [])).not.toThrow();
    });
});

describe('muteGroupCallIds', () => {
    const conference = (callId, primary) => ({
        callId,
        metadata: { isConference: true, conferencePrimaryCallId: primary },
    });

    it('is the call alone when it is not in a conference', () => {
        const calls = [{ callId: 'a' }, { callId: 'b' }];

        expect(muteGroupCallIds(calls, calls[0])).toEqual(['a']);
    });

    it('is every call of the conference the call is part of', () => {
        const calls = [conference('a', 'a'), conference('b', 'a'), conference('c', 'a'), { callId: 'd' }];

        expect(muteGroupCallIds(calls, calls[1]).sort()).toEqual(['a', 'b', 'c']);
    });

    it('leaves out the calls of another conference', () => {
        const calls = [conference('a', 'a'), conference('b', 'a'), conference('x', 'x')];

        expect(muteGroupCallIds(calls, calls[0]).sort()).toEqual(['a', 'b']);
    });

    it('is nothing without a call', () => {
        expect(muteGroupCallIds([], null)).toEqual([]);
    });
});

describe('conferenceMuted', () => {
    it('carries on as the call the agent was talking on', () => {
        const mutes = { b: true };

        expect(conferenceMuted(['a', 'b'], mutes, 'b')).toBe(true);
        expect(conferenceMuted(['a', 'b'], mutes, 'a')).toBe(false);
    });

    it('is muted when any merged call was, if the agent was on none of them', () => {
        expect(conferenceMuted(['a', 'b'], { b: true }, 'z')).toBe(true);
        expect(conferenceMuted(['a', 'b'], {}, null)).toBe(false);
    });
});

// The shared microphone follows the reconciled reports: a muted call that the server calls unmuted keeps it off.
describe('shared microphone with a remembered mute', () => {
    it('stays off after a refresh reports the muted call unmuted', () => {
        const mutes = {};
        rememberAgentMute(mutes, 'call-1', true);

        const reported = { callId: 'call-1', state: 'Connected', isMuted: false };
        reported.isMuted = reconcileAgentMute(reported.isMuted, isAgentMuted(mutes, 'call-1'), true).isMuted;

        expect(sharedMicrophoneEnabled(reported, [])).toBe(false);
    });
});
