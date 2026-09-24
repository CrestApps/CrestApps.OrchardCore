import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/agent-hold.js';

const { rememberAgentHold, isAgentHeld, pruneAgentHolds, reconcileAgentHold } = globalThis.CrestAppsSoftPhone;

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
