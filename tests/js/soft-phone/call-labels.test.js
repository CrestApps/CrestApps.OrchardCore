import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-labels.js';

const { friendlyCallLabel, createCallPartyMemory, carryCallParty } = globalThis.CrestAppsSoftPhone;

// Live: a conference row read "v3:UyLVvJ3o7qQklQFQnZpvgJylmV…" -- the provider's id for the agent's own leg -- where the
// dialed number belonged, and the phone sometimes showed the conference's id for a moment.
describe('friendlyCallLabel', () => {
    it('keeps a name or a number', () => {
        expect(friendlyCallLabel('(702) 499-3350', 'Participant')).toBe('(702) 499-3350');
        expect(friendlyCallLabel('Jane Doe · ext 2', 'Participant')).toBe('Jane Doe · ext 2');
    });

    it('never shows a provider call id, a conference name or an id', () => {
        expect(friendlyCallLabel('v3:UyLVvJ3o7qQklQFQnZpvgJylmVeQF5xcRczOzXt9eWt8O2VOc5B7ig', 'Participant')).toBe('Participant');
        expect(friendlyCallLabel('conf-v3:UyLVvJ3o7qQ', 'Participant')).toBe('Participant');
        expect(friendlyCallLabel('ext-v3:Er0k7zVS9tz', 'Participant')).toBe('Participant');
        expect(friendlyCallLabel('198ac59b-a456-4435-95b7-637c0fe9e4b1', 'Caller')).toBe('Caller');
        expect(friendlyCallLabel('call-1234567890abcdef', 'Participant')).toBe('Participant');
    });

    it('falls back when there is nothing to show', () => {
        expect(friendlyCallLabel('', 'Caller')).toBe('Caller');
        expect(friendlyCallLabel(null, 'Caller')).toBe('Caller');
        expect(friendlyCallLabel('   ', '')).toBe('');
    });
});

// The phone reads its calls again every few seconds, and a provider's report of a call may say nothing of whom it is
// with: the row lost its number and fell back to the call's id.
describe('carryCallParty', () => {
    it('stamps the numbers a call was first reported with back onto a report that has none', () => {
        const memory = createCallPartyMemory();
        carryCallParty(memory, { callId: 'a', from: '+17787204596', to: '+17024993350', direction: 0 });

        const later = carryCallParty(memory, { callId: 'a', state: 'Connected' });

        expect(later).toMatchObject({ from: '+17787204596', to: '+17024993350', direction: 0 });
    });

    it('takes what a newer report says over what was remembered', () => {
        const memory = createCallPartyMemory();
        carryCallParty(memory, { callId: 'a', to: '+17024993350' });

        expect(carryCallParty(memory, { callId: 'a', to: '+15551234567' }).to).toBe('+15551234567');
        expect(carryCallParty(memory, { callId: 'a' }).to).toBe('+15551234567');
    });

    it('leaves a call it knows nothing of as it is', () => {
        expect(carryCallParty(createCallPartyMemory(), { callId: 'b' })).toEqual({ callId: 'b' });
        expect(carryCallParty(createCallPartyMemory(), null)).toBeNull();
    });
});
