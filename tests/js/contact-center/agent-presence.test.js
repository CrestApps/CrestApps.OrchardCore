import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/agent-presence.js';

const { presenceLabel, pendingPresenceLabel, breakChoiceLabel, isOwnPresence, normalizePresenceStatus } = globalThis.CrestAppsContactCenter;

const labels = {
    available: 'Available',
    reserved: 'Reserved',
    busy: 'On a call',
    wrapUp: 'Wrap-up',
    break: 'Break',
    breakPending: 'Break pending',
    breakPendingWithReason: 'Break pending: {0}',
};

// Bug: mid-call the soft phone header said "Available". It showed the reason when there was one and otherwise looked
// the state up among the menu's buttons, so every state without a button of its own -- on a call, wrap-up -- printed
// the raw enum name, and a stale reason would have hidden the call entirely.
describe('presenceLabel', () => {
    it('names every on-the-work state rather than printing the enum', () => {
        expect(presenceLabel({ status: 'Reserved' }, labels)).toBe('Reserved');
        expect(presenceLabel({ status: 'Busy' }, labels)).toBe('On a call');
        expect(presenceLabel({ status: 'WrapUp' }, labels)).toBe('Wrap-up');
        expect(presenceLabel({ status: 3 }, labels)).toBe('On a call');
        expect(presenceLabel({ status: '4' }, labels)).toBe('Wrap-up');
    });

    it('ignores a reason kept for a break that has not started yet', () => {
        expect(presenceLabel({ status: 'WrapUp', reason: 'Lunch', requestedStatus: 'Break' }, labels)).toBe('Wrap-up');
        expect(presenceLabel({ status: 'Busy', reason: 'Lunch' }, labels)).toBe('On a call');
    });

    // Bug: a break started with no reason read "Short break" -- the first break button in the menu -- while the audit
    // record said plain "Break"; the agent then had to switch to the reason they meant.
    it('shows a break by its reason, and a reason-less break as just "Break"', () => {
        expect(presenceLabel({ status: 'Break', reason: 'Lunch' }, labels)).toBe('Lunch');
        expect(presenceLabel({ status: 'Break', reason: null }, labels)).toBe('Break');
        expect(presenceLabel({ status: 'Break', reason: '  ' }, labels)).toBe('Break');
    });

    it('falls back to English when a label is not supplied', () => {
        expect(presenceLabel({ status: 'Busy' }, {})).toBe('On a call');
        expect(presenceLabel({ status: 'DoNotDisturb' }, null)).toBe('Do not disturb');
        expect(presenceLabel(null, null)).toBe('Offline');
    });
});

describe('pendingPresenceLabel', () => {
    it('says which break is waiting for the work to end', () => {
        expect(pendingPresenceLabel({ status: 'WrapUp', reason: 'Lunch', requestedStatus: 'Break' }, labels)).toBe('Break pending: Lunch');
        expect(pendingPresenceLabel({ status: 'Busy', requestedStatus: 'Break' }, labels)).toBe('Break pending');
        expect(pendingPresenceLabel({ status: 'Busy', requestedStatus: 5 }, labels)).toBe('Break pending');
    });

    it('says nothing once the break has started or when none is waiting', () => {
        expect(pendingPresenceLabel({ status: 'Break', reason: 'Lunch', requestedStatus: 'Break' }, labels)).toBe('');
        expect(pendingPresenceLabel({ status: 'WrapUp', requestedStatus: 'Available' }, labels)).toBe('');
        expect(pendingPresenceLabel({ status: 'WrapUp', requestedStatus: null }, labels)).toBe('');
    });
});

// Bug: the presence menu headed the break reasons "Request break" even for an Available agent, for whom choosing
// one starts the break at once. Only an agent busy with work -- reserved, on a call, in wrap-up, or holding an offer --
// has a break that waits for the work to end, as the server decides it.
describe('breakChoiceLabel', () => {
    const menuLabels = { ...labels, requestBreak: 'Request break' };

    it('says "Break" when a break would start now', () => {
        expect(breakChoiceLabel({ status: 'Available' }, menuLabels)).toBe('Break');
        expect(breakChoiceLabel({ status: 'Away', reason: 'Away from desk' }, menuLabels)).toBe('Break');
        expect(breakChoiceLabel({ status: 'Break', reason: 'Lunch' }, menuLabels)).toBe('Break');
        expect(breakChoiceLabel({ status: 'Offline' }, menuLabels)).toBe('Break');
        expect(breakChoiceLabel({ status: 1 }, menuLabels)).toBe('Break');
    });

    it('says "Request break" while the agent is busy with work, when the break waits for it to end', () => {
        expect(breakChoiceLabel({ status: 'Reserved' }, menuLabels)).toBe('Request break');
        expect(breakChoiceLabel({ status: 'Busy' }, menuLabels)).toBe('Request break');
        expect(breakChoiceLabel({ status: 'WrapUp', requestedStatus: 'Break' }, menuLabels)).toBe('Request break');
        expect(breakChoiceLabel({ status: 3 }, menuLabels)).toBe('Request break');
        expect(breakChoiceLabel({ status: 'Available', hasActiveReservation: true }, menuLabels)).toBe('Request break');
    });

    it('falls back to English when a label is not supplied', () => {
        expect(breakChoiceLabel({ status: 'Available' }, {})).toBe('Break');
        expect(breakChoiceLabel({ status: 'Busy' }, null)).toBe('Request break');
        expect(breakChoiceLabel(null, null)).toBe('Break');
    });
});

// A supervisor's connection joins the supervisors group, which receives every agent's presence changes; applied as
// they arrive, another agent going Available would have relabelled this agent's header.
describe('isOwnPresence', () => {
    it('accepts only the agent own notifications when both owners are known', () => {
        expect(isOwnPresence({ userId: 'u1', status: 'Busy' }, 'u1')).toBe(true);
        expect(isOwnPresence({ userId: 'u2', status: 'Available' }, 'u1')).toBe(false);
    });

    it('accepts a notification when either owner is unknown', () => {
        expect(isOwnPresence({ status: 'Busy' }, 'u1')).toBe(true);
        expect(isOwnPresence({ userId: 'u2' }, null)).toBe(true);
        expect(isOwnPresence(null, 'u1')).toBe(false);
    });
});

describe('normalizePresenceStatus', () => {
    it('reads names and numbers alike', () => {
        expect(normalizePresenceStatus('Busy')).toBe('Busy');
        expect(normalizePresenceStatus(5)).toBe('Break');
        expect(normalizePresenceStatus(undefined)).toBe('Offline');
    });
});
