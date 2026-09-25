import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-target.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/transfer-service.js';

const {
    serviceDirectoryEntries,
    serviceModes,
    resolveServiceTarget,
    createTransferService
} = globalThis.CrestAppsSoftPhone;

const strings = {
    transferGroupAgents: 'Agents',
    transferGroupQueues: 'Queues',
    transferGroupExternal: 'Outside numbers',
    transferExtension: 'Ext {0}',
    transferWaiting: '{0} waiting',
    presenceAvailable: 'Available',
    presenceBusy: 'On a call'
};

const directory = {
    agents: [
        { id: 'agent-b', name: 'Bea Baker', extension: '201', presence: 'Available', available: true },
        { id: 'agent-c', name: 'Cal Cole', extension: null, presence: 'Busy', available: false }
    ],
    queues: [{ id: 'queue-sales', name: 'Sales', waiting: 3 }],
    externalDestinations: [{ id: 'dest-1', name: 'Billing partner', number: '+15559990000' }],
    canTransferExternally: true,
    allowExternalNumbers: false,
    supportsConsult: true
};

describe('serviceDirectoryEntries', () => {
    it('lists agents, queues and outside numbers as panel entries that name what the transfer sends', () => {
        const entries = serviceDirectoryEntries(directory, strings);

        expect(entries.map((entry) => entry.destination)).toEqual([
            'agent:agent-b',
            'agent:agent-c',
            'queue:queue-sales',
            'external:dest-1'
        ]);

        expect(entries[0]).toMatchObject({
            name: 'Bea Baker',
            kind: 'agent',
            targetType: 'agent',
            targetId: 'agent-b',
            group: 'Agents',
            presence: 'Available',
            status: 'Available',
            detail: 'Ext 201',
            disabled: false
        });
        expect(entries[1]).toMatchObject({ status: 'On a call', disabled: true });
        expect(entries[2]).toMatchObject({ kind: 'queue', targetType: 'queue', targetId: 'queue-sales', detail: '3 waiting', group: 'Queues' });
        expect(entries[3]).toMatchObject({ kind: 'external', targetType: 'external', targetId: 'dest-1', detail: '+15559990000', group: 'Outside numbers' });
    });

    it('lists nothing for a payload that is missing', () => {
        expect(serviceDirectoryEntries(null, strings)).toEqual([]);
    });
});

describe('serviceModes', () => {
    it('offers warm only when the call can be held for a consult', () => {
        expect(serviceModes(directory)).toEqual(['blind', 'warm']);
        expect(serviceModes({ ...directory, supportsConsult: false })).toEqual(['blind']);
        expect(serviceModes(null)).toEqual(['blind']);
    });
});

describe('resolveServiceTarget', () => {
    const entries = serviceDirectoryEntries(directory, strings);

    it('sends a picked agent, queue or outside number by what it is', () => {
        expect(resolveServiceTarget({ selected: entries[0], mode: 'blind', directory }))
            .toEqual({ targetType: 'agent', targetId: 'agent-b', label: 'Bea Baker', refused: '' });
        expect(resolveServiceTarget({ selected: entries[2], mode: 'blind', directory }))
            .toMatchObject({ targetType: 'queue', targetId: 'queue-sales', refused: '' });
        expect(resolveServiceTarget({ selected: entries[3], mode: 'blind', directory }))
            .toMatchObject({ targetType: 'external', targetId: 'dest-1', refused: '' });
    });

    it('refuses an agent who cannot take a call right now', () => {
        expect(resolveServiceTarget({ selected: entries[1], mode: 'blind', directory }).refused).toBe('unavailable');
    });

    it('refuses a queue for a warm transfer, which needs somebody to talk to', () => {
        expect(resolveServiceTarget({ selected: entries[2], mode: 'warm', directory }).refused).toBe('warm-queue');
    });

    it('refuses a typed number unless the tenant allows outside numbers', () => {
        expect(resolveServiceTarget({ query: '+15557654321', mode: 'blind', directory }).refused).toBe('external-not-allowed');

        const allowing = { ...directory, allowExternalNumbers: true };
        expect(resolveServiceTarget({ query: '(555) 765-4321', mode: 'blind', directory: allowing }))
            .toEqual({ targetType: 'external', targetId: '+15557654321', label: '(555) 765-4321', refused: '' });
    });

    it('refuses the tenant\'s own number and a number that is not complete', () => {
        const allowing = { ...directory, allowExternalNumbers: true };

        expect(resolveServiceTarget({ query: '702 555 0100', mode: 'blind', directory: allowing, ownNumbers: ['+17025550100'] }).refused).toBe('own-number');
        expect(resolveServiceTarget({ query: '555 0199', mode: 'blind', directory: allowing }).refused).toBe('invalid-number');
    });

    it('asks for a choice when nothing was picked and no number typed', () => {
        expect(resolveServiceTarget({ query: 'Bea', mode: 'blind', directory }).refused).toBe('empty');
        expect(resolveServiceTarget({ query: '', mode: 'blind', directory }).refused).toBe('empty');
    });

    it('sends the number the country-flag input read, and refuses one it says is incomplete', () => {
        const allowing = { ...directory, allowExternalNumbers: true };

        expect(resolveServiceTarget({ query: '(555) 765-4321', number: { value: '+15557654321', valid: true }, mode: 'blind', directory: allowing }))
            .toEqual({ targetType: 'external', targetId: '+15557654321', label: '(555) 765-4321', refused: '' });
        expect(resolveServiceTarget({ query: '555 765', number: { value: '+1555765', valid: false }, mode: 'blind', directory: allowing }).refused)
            .toBe('invalid-number');
        expect(resolveServiceTarget({ query: '(702) 555-0100', number: { value: '+17025550100', valid: true }, mode: 'blind', directory: allowing, ownNumbers: ['+17025550100'] }).refused)
            .toBe('own-number');
    });

    // Bug: an extension typed into the panel was checked as a phone number. It is somewhere inside the contact center,
    // so it is sent as an extension -- the server resolves it to the agent it rings -- whether or not outside numbers
    // may be typed.
    it('sends a typed extension as an extension, blind or warm', () => {
        expect(resolveServiceTarget({ query: '2', dialMode: 'extension', mode: 'blind', directory }))
            .toEqual({ targetType: 'extension', targetId: '2', label: '2', refused: '' });
        expect(resolveServiceTarget({ query: '2', dialMode: 'extension', mode: 'warm', directory }))
            .toMatchObject({ targetType: 'extension', targetId: '2', refused: '' });
    });

    it('refuses what cannot be an extension', () => {
        expect(resolveServiceTarget({ query: 'Bea', dialMode: 'extension', mode: 'blind', directory }).refused).toBe('invalid-extension');
        expect(resolveServiceTarget({ query: '  ', dialMode: 'extension', mode: 'blind', directory }).refused).toBe('empty');
    });

    it('still sends a picked agent in extension mode', () => {
        const entries = serviceDirectoryEntries(directory, strings);

        expect(resolveServiceTarget({ selected: entries[0], query: '2', dialMode: 'extension', mode: 'blind', directory }))
            .toEqual({ targetType: 'agent', targetId: 'agent-b', label: 'Bea Baker', refused: '' });
    });
});

describe('createTransferService', () => {
    const urls = {
        targetsUrl: '/cc/transfer/targets',
        transferUrl: '/cc/transfer',
        consultUrl: '/cc/transfer/consult',
        consultCompleteUrl: '/cc/transfer/consult/complete',
        consultCancelUrl: '/cc/transfer/consult/cancel'
    };

    function respond(body, ok = true, status = 200) {
        return Promise.resolve({ ok, status, json: () => Promise.resolve(body) });
    }

    it('applies only to a Contact Center call when the tenant published the transfer endpoints', () => {
        const service = createTransferService({ urls, fetch: vi.fn() });

        expect(service.applies({ callId: 'c1', metadata: { interactionId: 'i1' } })).toBe(true);
        expect(service.applies({ callId: 'c1', metadata: {} })).toBe(false);
        expect(createTransferService({ urls: {}, fetch: vi.fn() }).applies({ metadata: { interactionId: 'i1' } })).toBe(false);
    });

    it('asks for the call\'s directory and posts a blind transfer with the antiforgery token', async () => {
        const fetch = vi.fn((url) => respond(url.startsWith(urls.targetsUrl) ? directory : { succeeded: true, message: 'Ringing Bea.' }));
        const service = createTransferService({ urls, antiForgeryToken: 'token-1', fetch });
        const call = { callId: 'c1', metadata: { interactionId: 'i 1' } };

        await expect(service.loadDirectory(call)).resolves.toEqual(directory);
        expect(fetch.mock.calls[0][0]).toBe('/cc/transfer/targets?interactionId=i%201');

        const result = await service.transfer(call, { targetType: 'agent', targetId: 'agent-b' });

        expect(result).toEqual({ succeeded: true, message: 'Ringing Bea.' });
        const [url, init] = fetch.mock.calls[1];
        expect(url).toBe(urls.transferUrl);
        expect(init.method).toBe('POST');
        expect(init.headers.RequestVerificationToken).toBe('token-1');
        expect(JSON.parse(init.body)).toEqual({ interactionId: 'i 1', targetType: 'agent', targetId: 'agent-b' });
    });

    it('names an extension as an extension on the transfer and the consult it posts', async () => {
        const fetch = vi.fn(() => respond({ succeeded: true }));
        const service = createTransferService({ urls, fetch });
        const call = { metadata: { interactionId: 'i1' } };

        await service.transfer(call, { targetType: 'extension', targetId: '2' });
        await service.startConsult(call, { targetType: 'extension', targetId: '2' });

        expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({ interactionId: 'i1', targetType: 'extension', targetId: '2' });
        expect(JSON.parse(fetch.mock.calls[1][1].body)).toEqual({ interactionId: 'i1', targetType: 'extension', targetId: '2' });
    });

    it('reports a refused request as a failure with the server\'s reason rather than throwing', async () => {
        const fetch = vi.fn(() => respond({ detail: 'The call is not available.' }, false, 404));
        const service = createTransferService({ urls, fetch });

        await expect(service.transfer({ metadata: { interactionId: 'i1' } }, { targetType: 'queue', targetId: 'q' }))
            .resolves.toEqual({ succeeded: false, error: 'The call is not available.' });
    });

    it('starts, polls, completes and cancels a consult against the consult endpoints', async () => {
        const fetch = vi.fn(() => respond({ succeeded: true, consult: { id: 'k1', status: 'ringing', live: true } }));
        const service = createTransferService({ urls, fetch });
        const call = { metadata: { interactionId: 'i1' } };

        await service.startConsult(call, { targetType: 'agent', targetId: 'agent-b' });
        await service.getConsult(call, 'k1');
        await service.completeConsult(call, 'k1');
        await service.cancelConsult(call, 'k1');

        expect(fetch.mock.calls.map(([url, init]) => `${init.method} ${url}`)).toEqual([
            'POST /cc/transfer/consult',
            'GET /cc/transfer/consult?interactionId=i1&consultId=k1',
            'POST /cc/transfer/consult/complete',
            'POST /cc/transfer/consult/cancel'
        ]);
        expect(JSON.parse(fetch.mock.calls[2][1].body)).toEqual({ interactionId: 'i1', consultId: 'k1' });
    });
});
