import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-target.js';

const {
    transferModes,
    transferModeValue,
    isNumberLike,
    isSameLine,
    filterTransferTargets,
    resolveTransferTarget
} = globalThis.CrestAppsSoftPhone;

const TRANSFER = 1 << 5;
const ATTENDED_TRANSFER = 1 << 11;
const ownNumber = '+17025550100';

describe('transferModes', () => {
    it('offers blind and warm only when the provider supports each', () => {
        expect(transferModes(TRANSFER)).toEqual(['blind']);
        expect(transferModes(ATTENDED_TRANSFER)).toEqual(['warm']);
        expect(transferModes(TRANSFER | ATTENDED_TRANSFER)).toEqual(['blind', 'warm']);
        expect(transferModes(0)).toEqual([]);
        expect(transferModes(undefined)).toEqual([]);
    });

    it('maps each mode to the hub TransferMode value, blind for anything unknown', () => {
        expect(transferModeValue('blind')).toBe(0);
        expect(transferModeValue('warm')).toBe(1);
        expect(transferModeValue('consult')).toBe(0);
    });
});

describe('isNumberLike', () => {
    it('reads digits and phone punctuation as a number, and names or addresses as not one', () => {
        expect(isNumberLike('(702) 555-0199')).toBe(true);
        expect(isNumberLike('+1 702 555 0199')).toBe(true);
        expect(isNumberLike('2001')).toBe(true);
        expect(isNumberLike('Alex')).toBe(false);
        expect(isNumberLike('sip:alex@example.com')).toBe(false);
        expect(isNumberLike('  ')).toBe(false);
        expect(isNumberLike('()-')).toBe(false);
    });
});

describe('isSameLine', () => {
    it('matches a number typed without its country code to the tenant +1 number', () => {
        expect(isSameLine('7025550100', ownNumber)).toBe(true);
        expect(isSameLine('(702) 555-0100', ownNumber)).toBe(true);
        expect(isSameLine('+1 702 555 0100', ownNumber)).toBe(true);
        expect(isSameLine('7025550101', ownNumber)).toBe(false);
        expect(isSameLine('', ownNumber)).toBe(false);
    });
});

describe('filterTransferTargets', () => {
    const entries = [
        { id: 'user-2001', displayName: 'Alex Agent', destination: '2001', extension: '2001' },
        { id: 'user-2002', displayName: 'Sam Supervisor', phoneNumber: '+17025550142' },
        { id: 'nowhere', displayName: 'No destination' }
    ];

    it('lists every entry that has somewhere to send the call when nothing is typed', () => {
        expect(filterTransferTargets(entries, '').map(entry => entry.id)).toEqual(['user-2001', 'user-2002']);
    });

    it('matches by name, case-insensitively', () => {
        expect(filterTransferTargets(entries, 'sam').map(entry => entry.name)).toEqual(['Sam Supervisor']);
    });

    it('matches by extension or number, however the number is written', () => {
        expect(filterTransferTargets(entries, '2001').map(entry => entry.id)).toEqual(['user-2001']);
        expect(filterTransferTargets(entries, '(702) 555-0142').map(entry => entry.id)).toEqual(['user-2002']);
    });

    it('matches nothing when nobody fits', () => {
        expect(filterTransferTargets(entries, 'Zed')).toEqual([]);
    });
});

describe('resolveTransferTarget', () => {
    it('sends the call to the directory entry the agent picked', () => {
        expect(resolveTransferTarget({ query: 'al', selected: { destination: '2001', name: 'Alex Agent' }, ownNumbers: [ownNumber] }))
            .toEqual({ destination: '2001', label: 'Alex Agent', refused: '' });
    });

    it('asks for a target when nothing is picked or typed', () => {
        expect(resolveTransferTarget({ query: '   ', ownNumbers: [] }).refused).toBe('empty');
        expect(resolveTransferTarget({}).refused).toBe('empty');
    });

    it('sends a typed number as its digits, keeping the international prefix', () => {
        expect(resolveTransferTarget({ query: '(702) 555-0199', ownNumbers: [ownNumber] }))
            .toEqual({ destination: '7025550199', label: '(702) 555-0199', refused: '' });
        expect(resolveTransferTarget({ query: '+44 20 7946 0958', ownNumbers: [ownNumber] }).destination).toBe('+442079460958');
    });

    it('accepts a typed extension', () => {
        expect(resolveTransferTarget({ query: '2001', ownNumbers: [ownNumber] }).destination).toBe('2001');
    });

    // The keypad refuses to add a call to the tenant's own number, because it only rings the tenant back. A transfer to
    // it does the same to the caller, so the panel refuses it too -- however the agent writes the number.
    it('refuses the tenant own number, with or without its country code', () => {
        expect(resolveTransferTarget({ query: ownNumber, ownNumbers: [ownNumber] }).refused).toBe('own-number');
        expect(resolveTransferTarget({ query: '702-555-0100', ownNumbers: [ownNumber] }).refused).toBe('own-number');
    });

    it('refuses a number too short or too long to be one', () => {
        expect(resolveTransferTarget({ query: '7', ownNumbers: [] }).refused).toBe('invalid-number');
        expect(resolveTransferTarget({ query: '+1702', ownNumbers: [] }).refused).toBe('invalid-number');
        expect(resolveTransferTarget({ query: '1234567890123456', ownNumbers: [] }).refused).toBe('invalid-number');
    });

    it('passes an address or directory id through for the provider and the server policy to decide', () => {
        expect(resolveTransferTarget({ query: 'sip:alex@example.com', ownNumbers: [ownNumber] }))
            .toEqual({ destination: 'sip:alex@example.com', label: 'sip:alex@example.com', refused: '' });
    });
});

describe('filterTransferTargets with a transfer service directory', () => {
    const entries = [
        { name: 'Bea Baker', destination: 'agent:agent-bea', detail: 'Ext 201', kind: 'agent', targetType: 'agent', targetId: 'agent-bea', presence: 'Available', status: 'Available', disabled: false, group: 'Agents' },
        { name: 'Sales', destination: 'queue:queue-sales', detail: '3 waiting', kind: 'queue', targetType: 'queue', targetId: 'queue-sales', group: 'Queues' }
    ];

    it('keeps what each entry is, and shows the service\'s own detail rather than a number', () => {
        const [bea, sales] = filterTransferTargets(entries, '');

        expect(bea).toMatchObject({ name: 'Bea Baker', detail: 'Ext 201', kind: 'agent', targetId: 'agent-bea', presence: 'Available', disabled: false, group: 'Agents' });
        expect(sales).toMatchObject({ name: 'Sales', detail: '3 waiting', kind: 'queue', targetId: 'queue-sales' });
    });

    it('finds an agent by name or by extension', () => {
        expect(filterTransferTargets(entries, 'bea').map((entry) => entry.targetId)).toEqual(['agent-bea']);
        expect(filterTransferTargets(entries, '201').map((entry) => entry.targetId)).toEqual(['agent-bea']);
    });
});
