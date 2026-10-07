import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/ringback.js';

const {
    RINGBACK_MAX_MS,
    RINGBACK_CADENCE,
    normalizeRemotePartyState,
    createRingbackMemory,
    noteRemoteParty,
    forgetRemoteParty,
    pruneRemoteParties,
    isRemotePartyRinging,
    ringbackTimeLeftMs,
    planRingback,
} = globalThis.CrestAppsSoftPhone;

const call = (overrides) => ({ callId: 'agent-leg', state: 'Connected', isOnHold: false, browserOriginated: false, ...overrides });

// A number dialed from the keypad is connected on the server: the agent's own leg is answered first and stays silent
// while the number rings, so the agent could not tell whether anything was being dialed.
describe('normalizeRemotePartyState', () => {
    it('reads the state by name or by value', () => {
        expect(normalizeRemotePartyState('Ringing')).toBe('ringing');
        expect(normalizeRemotePartyState('answered')).toBe('answered');
        expect(normalizeRemotePartyState(2)).toBe('ended');
    });

    it('reads nothing it does not know', () => {
        expect(normalizeRemotePartyState('Busy')).toBeNull();
        expect(normalizeRemotePartyState(7)).toBeNull();
        expect(normalizeRemotePartyState(null)).toBeNull();
    });
});

describe('noteRemoteParty', () => {
    it('remembers a ringing party from the first time it is told', () => {
        const memory = createRingbackMemory();

        expect(noteRemoteParty(memory, { callId: 'a', state: 'Ringing' }, 1000)).toBe('ringing');
        expect(noteRemoteParty(memory, { callId: 'a', state: 'Ringing' }, 5000)).toBe('ringing');
        expect(ringbackTimeLeftMs(memory, 'a', 5000)).toBe(RINGBACK_MAX_MS - 4000);
    });

    it('never rings again once the party answered or went', () => {
        const memory = createRingbackMemory();

        noteRemoteParty(memory, { callId: 'a', state: 'Ringing' }, 0);
        noteRemoteParty(memory, { callId: 'a', state: 'Answered' }, 10);
        expect(noteRemoteParty(memory, { callId: 'a', state: 'Ringing' }, 20)).toBe('answered');
        expect(isRemotePartyRinging(memory, 'a', 20)).toBe(false);

        noteRemoteParty(memory, { callId: 'b', state: 'Ended' }, 0);
        expect(noteRemoteParty(memory, { callId: 'b', state: 'Ringing' }, 5)).toBe('ended');
    });

    it('ignores an update that names no call or no state', () => {
        const memory = createRingbackMemory();

        expect(noteRemoteParty(memory, null, 0)).toBeNull();
        expect(noteRemoteParty(memory, { state: 'Ringing' }, 0)).toBeNull();
        expect(noteRemoteParty(memory, { callId: 'a', state: 'Unknown' }, 0)).toBeNull();
        expect(memory.calls).toEqual({});
    });
});

describe('forgetRemoteParty and pruneRemoteParties', () => {
    it('forgets calls that ended, and only those', () => {
        const memory = createRingbackMemory();

        noteRemoteParty(memory, { callId: 'a', state: 'Ringing' }, 0);
        noteRemoteParty(memory, { callId: 'b', state: 'Ringing' }, 0);
        noteRemoteParty(memory, { callId: 'c', state: 'Ringing' }, 0);

        forgetRemoteParty(memory, 'a');
        pruneRemoteParties(memory, ['b']);

        expect(Object.keys(memory.calls)).toEqual(['b']);
    });
});

describe('planRingback', () => {
    const ringing = () => {
        const memory = createRingbackMemory();

        noteRemoteParty(memory, { callId: 'agent-leg', state: 'Ringing' }, 0);

        return memory;
    };

    it('plays while the number the agent is on rings', () => {
        expect(planRingback(ringing(), call(), { nowMs: 1000 })).toEqual({ play: true });
    });

    it('also plays before the server reports the agent leg connected', () => {
        expect(planRingback(ringing(), call({ state: 'Connecting' }), { nowMs: 1000 }).play).toBe(true);
    });

    it('stops the moment the number answers or goes', () => {
        const answered = ringing();
        noteRemoteParty(answered, { callId: 'agent-leg', state: 'Answered' }, 10);
        expect(planRingback(answered, call(), { nowMs: 20 })).toEqual({ play: false, reason: 'answered' });

        const ended = ringing();
        noteRemoteParty(ended, { callId: 'agent-leg', state: 'Ended' }, 10);
        expect(planRingback(ended, call(), { nowMs: 20 })).toEqual({ play: false, reason: 'ended' });
    });

    it('stops when the call ends or is held', () => {
        expect(planRingback(ringing(), call({ state: 'Disconnected' }), { nowMs: 1 }).reason).toBe('call-ended');
        expect(planRingback(ringing(), call({ state: 'Failed' }), { nowMs: 1 }).reason).toBe('call-ended');
        expect(planRingback(ringing(), call({ isOnHold: true }), { nowMs: 1 }).reason).toBe('held');
        expect(planRingback(ringing(), call(), { nowMs: 1, isHeld: true }).reason).toBe('held');
    });

    it('stops by itself when the server never ends it', () => {
        expect(planRingback(ringing(), call(), { nowMs: RINGBACK_MAX_MS })).toEqual({ play: false, reason: 'timeout' });
    });

    it('never plays for a call nobody said is ringing: inbound, Contact Center and browser-dialed calls', () => {
        expect(planRingback(createRingbackMemory(), call(), { nowMs: 1 }).reason).toBe('not-ringing');
        expect(planRingback(ringing(), call({ callId: 'other' }), { nowMs: 1 }).reason).toBe('not-ringing');
        expect(planRingback(ringing(), call({ browserOriginated: true }), { nowMs: 1 }).reason).toBe('browser-call');
        expect(planRingback(ringing(), null, { nowMs: 1 }).reason).toBe('no-call');
    });
});

describe('RINGBACK_CADENCE', () => {
    it('is the North American ringback: 440 Hz and 480 Hz, two seconds on and four off', () => {
        expect(RINGBACK_CADENCE).toEqual({ frequencies: [440, 480], onSeconds: 2, offSeconds: 4 });
    });
});
