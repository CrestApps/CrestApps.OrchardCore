import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/agent-hold.js';

const {
    rememberAgentHold,
    isAgentHeld,
    pruneAgentHolds,
    reconcileAgentHold,
    resolveReportedState,
    holdCommandRoute,
} = globalThis.CrestAppsSoftPhone;

// With browser audio the hold happens in the browser and the provider never hears of it, so every later report from
// the server says the call is connected. Taking those reports at their word took callers off hold a few seconds after
// the agent put them on it.
describe('reconcileAgentHold', () => {
    it('keeps a hold performed in this browser when the server reports the call connected', () => {
        expect(reconcileAgentHold('Connected', false, true, true))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: true });
    });

    it('keeps the hold through every report that follows, not just the first', () => {
        const holds = {};
        rememberAgentHold(holds, 'call-1', true);

        for (let report = 0; report < 5; report++) {
            const outcome = reconcileAgentHold('Connected', false, isAgentHeld(holds, 'call-1'), true);
            rememberAgentHold(holds, 'call-1', outcome.agentHeld);

            expect(outcome.stateName).toBe('OnHold');
        }

        expect(isAgentHeld(holds, 'call-1')).toBe(true);
    });

    it('keeps the hold when the server agrees the call is held', () => {
        expect(reconcileAgentHold('OnHold', true, true, true))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: true });
        expect(reconcileAgentHold('Connected', true, true, false))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: true });
    });

    it('ends the hold when a provider that holds the call itself reports it connected again', () => {
        expect(reconcileAgentHold('Connected', false, true, false))
            .toEqual({ stateName: 'Connected', isOnHold: false, agentHeld: false });
    });

    it('ends the hold when the call ends', () => {
        expect(reconcileAgentHold('Disconnected', false, true, true))
            .toEqual({ stateName: 'Disconnected', isOnHold: false, agentHeld: false });
        expect(reconcileAgentHold('Failed', true, true, true).agentHeld).toBe(false);
    });

    it('leaves a call the agent did not hold as the server reports it', () => {
        expect(reconcileAgentHold('Connected', false, false, true))
            .toEqual({ stateName: 'Connected', isOnHold: false, agentHeld: false });
        expect(reconcileAgentHold('OnHold', false, false, true))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: false });
    });

    it('does not call a ringing call held, whatever flag the report carries', () => {
        expect(reconcileAgentHold('Ringing', true, false, true))
            .toEqual({ stateName: 'Ringing', isOnHold: false, agentHeld: false });
    });
});

describe('rememberAgentHold', () => {
    it('remembers a hold and forgets it on resume', () => {
        const holds = {};

        rememberAgentHold(holds, 'call-1', true);
        expect(isAgentHeld(holds, 'call-1')).toBe(true);

        rememberAgentHold(holds, 'call-1', false);
        expect(isAgentHeld(holds, 'call-1')).toBe(false);
    });

    it('ignores a missing call id or memory', () => {
        const holds = {};

        rememberAgentHold(holds, '', true);
        rememberAgentHold(null, 'call-1', true);

        expect(holds).toEqual({});
        expect(isAgentHeld(null, 'call-1')).toBe(false);
    });
});

describe('pruneAgentHolds', () => {
    it('forgets the holds on calls that are no longer live', () => {
        const holds = {};
        rememberAgentHold(holds, 'call-1', true);
        rememberAgentHold(holds, 'call-2', true);

        pruneAgentHolds(holds, ['call-2', 'call-3']);

        expect(isAgentHeld(holds, 'call-1')).toBe(false);
        expect(isAgentHeld(holds, 'call-2')).toBe(true);
    });

    it('forgets everything when no call is live', () => {
        const holds = {};
        rememberAgentHold(holds, 'call-1', true);

        pruneAgentHolds(holds, []);

        expect(holds).toEqual({});
    });
});

// Bug: the first hold on a pre-dialed Contact Center call came off by itself within seconds; the second one stayed.
// Recording the first hold made the Contact Center push the call to the phone again, described from its interaction,
// which the server had never moved past "Ringing". The phone took "Ringing" as "not up, so no hold to keep" and forgot
// the agent's hold; the refresh a few seconds later said "Connected" and the media adapter took the caller off hold.
// The second hold was already recorded, so no push followed it, and it held.
describe('a stale ringing report on a call the agent holds', () => {
    it('keeps the hold', () => {
        expect(reconcileAgentHold('Ringing', true, true, true))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: true });
        expect(reconcileAgentHold('Connecting', false, true, true))
            .toEqual({ stateName: 'OnHold', isOnHold: true, agentHeld: true });
    });

    it('keeps it through the connected refresh that follows', () => {
        const holds = {};
        rememberAgentHold(holds, 'call-1', true);

        // The hold command's own result.
        let outcome = reconcileAgentHold('OnHold', true, isAgentHeld(holds, 'call-1'), true);
        rememberAgentHold(holds, 'call-1', outcome.agentHeld);

        // The Contact Center's push, describing the call from an interaction still marked ringing.
        const pushed = resolveReportedState(outcome.stateName, 'Ringing');
        outcome = reconcileAgentHold(pushed, true, isAgentHeld(holds, 'call-1'), true);
        rememberAgentHold(holds, 'call-1', outcome.agentHeld);

        // The provider-backed refresh a few seconds later.
        outcome = reconcileAgentHold(resolveReportedState(outcome.stateName, 'Connected'), false,
            isAgentHeld(holds, 'call-1'), true);

        expect(outcome.stateName).toBe('OnHold');
        expect(isAgentHeld(holds, 'call-1')).toBe(true);
    });
});

// A call that has been up cannot start ringing again. A report that says it has is describing something else -- an
// interaction the server has not caught up on -- and taking it at its word reopened the ringing panel over a live
// call and dropped whatever the phone knew about it.
describe('resolveReportedState', () => {
    it('keeps an up call up when a report regresses it to ringing or connecting', () => {
        expect(resolveReportedState('Connected', 'Ringing')).toBe('Connected');
        expect(resolveReportedState('OnHold', 'Ringing')).toBe('OnHold');
        expect(resolveReportedState('Connected', 'Connecting')).toBe('Connected');
    });

    it('takes every other report as it comes', () => {
        expect(resolveReportedState('Connected', 'OnHold')).toBe('OnHold');
        expect(resolveReportedState('OnHold', 'Connected')).toBe('Connected');
        expect(resolveReportedState('Connected', 'Disconnected')).toBe('Disconnected');
        expect(resolveReportedState('Ringing', 'Connected')).toBe('Connected');
        expect(resolveReportedState(null, 'Ringing')).toBe('Ringing');
        expect(resolveReportedState('Connecting', 'Ringing')).toBe('Ringing');
    });
});

// Bug: the agent pressed Resume and the server never heard of it, so the hold kept counting. Hold and resume of a
// call the server tracks both go to the server, which records the hold time and tells the media to swap back; a call
// this browser placed itself has nothing on the server to tell and is held on its own session. One decision serves
// both commands, so they cannot take different paths for the same call.
describe('holdCommandRoute', () => {
    it('sends hold and resume of a server-tracked call to the hub, even when the browser has a controller for it', () => {
        expect(holdCommandRoute({ callId: 'v3:caller', browserOriginated: false }, true)).toBe('hub');
        expect(holdCommandRoute({ callId: 'v3:caller' }, false)).toBe('hub');
    });

    it('holds a call this browser placed on its own session', () => {
        expect(holdCommandRoute({ callId: 'browser-1', browserOriginated: true }, true)).toBe('local');
    });

    it('does nothing for a browser-placed call it has no controller for, or for no call at all', () => {
        expect(holdCommandRoute({ callId: 'browser-1', browserOriginated: true }, false)).toBe('none');
        expect(holdCommandRoute(null, true)).toBe('none');
    });
});
