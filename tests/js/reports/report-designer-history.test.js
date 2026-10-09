import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-designer/designer-history.js';

const designer = globalThis.CrestAppsReportDesigner;

// A stand-in for the SignalR connection: records what the page invokes and lets a test raise hub messages.
function fakeConnection(options = {}) {
    const handlers = {};
    let reconnected = null;

    return {
        invocations: [],
        start: vi.fn(() => (options.failStart ? Promise.reject(new Error('offline')) : Promise.resolve())),
        stop: vi.fn(() => Promise.resolve()),
        invoke(method, ...args) {
            this.invocations.push([method, ...args]);

            return Promise.resolve(true);
        },
        on(method, handler) {
            handlers[method] = handler;
        },
        onreconnected(callback) {
            reconnected = callback;
        },
        raise(method, ...args) {
            handlers[method](...args);
        },
        reconnect() {
            reconnected();
        },
    };
}

describe('remote changes', () => {
    it('ignores changes this page already has', () => {
        expect(designer.remoteChangeAction({ kind: 'DraftSaved', revision: 4 }, 4)).toBe('ignore');
        expect(designer.remoteChangeAction({ kind: 'DraftSaved', revision: 3 }, 4)).toBe('ignore');
        expect(designer.remoteChangeAction(null, 4)).toBe('ignore');
    });

    it('reports newer changes and deletions', () => {
        expect(designer.remoteChangeAction({ kind: 'Published', revision: 5 }, 4)).toBe('changed');
        expect(designer.remoteChangeAction({ kind: 'Deleted', revision: 0 }, 4)).toBe('deleted');
    });
});

describe('presence names', () => {
    it('lists each person once, falling back to the user id', () => {
        expect(designer.presenceNames([
            { connectionId: 'a', userName: 'ada' },
            { connectionId: 'b', userName: 'ada' },
            { connectionId: 'c', userId: 'u-7' },
        ])).toEqual(['ada', 'u-7']);
    });
});

describe('real time', () => {
    it('subscribes to the report and tracks who is there', async () => {
        const connection = fakeConnection();
        const presence = [];
        const session = await designer.startRealtime({
            url: '/hub',
            designId: 'r1',
            factory: () => connection,
            onPresence: (people) => presence.push(people.map((person) => person.userName)),
        });

        expect(session).not.toBeNull();
        expect(connection.invocations).toEqual([['Subscribe', 'r1']]);

        connection.raise('PresenceJoined', { connectionId: 'c2', userName: 'ada' });
        connection.raise('PresenceHere', { connectionId: 'c3', userName: 'bob' });
        connection.raise('PresenceLeft', 'c2');

        // A newcomer is told this page is there.
        expect(connection.invocations).toContainEqual(['AnnouncePresence', 'r1', 'c2']);
        expect(presence).toEqual([['ada'], ['ada', 'bob'], ['bob']]);
    });

    it('hands report changes to the page', async () => {
        const connection = fakeConnection();
        const onChange = vi.fn();

        await designer.startRealtime({ url: '/hub', designId: 'r1', factory: () => connection, onChange });
        connection.raise('ReportDesignChanged', { kind: 'Published', revision: 7 });

        expect(onChange).toHaveBeenCalledWith({ kind: 'Published', revision: 7 });
    });

    it('subscribes again and forgets everyone after reconnecting', async () => {
        const connection = fakeConnection();
        const presence = [];

        await designer.startRealtime({ url: '/hub', designId: 'r1', factory: () => connection, onPresence: (people) => presence.push(people.length) });
        connection.raise('PresenceHere', { connectionId: 'c3', userName: 'bob' });
        connection.reconnect();

        expect(presence).toEqual([1, 0]);
        expect(connection.invocations.filter(([method]) => method === 'Subscribe')).toHaveLength(2);
    });

    it('is quiet when real time is not available', async () => {
        expect(await designer.startRealtime({ url: '/hub', designId: 'r1', factory: () => null })).toBeNull();
        expect(await designer.startRealtime({ url: null, designId: 'r1', factory: () => fakeConnection() })).toBeNull();
        expect(await designer.startRealtime({ url: '/hub', designId: 'r1', factory: () => fakeConnection({ failStart: true }) })).toBeNull();
    });
});
