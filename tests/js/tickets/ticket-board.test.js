import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Tickets/Assets/js/ticket-board.js';

const board = globalThis.CrestAppsTicketBoard;

const transitions = {
    New: ['Open', 'Pending', 'OnHold', 'Solved', 'Closed', 'New'],
    Open: ['New', 'Pending', 'OnHold', 'Solved', 'Closed', 'Open'],
    Pending: ['Open', 'OnHold', 'Solved', 'Closed', 'Pending'],
    Solved: ['Open', 'Pending', 'OnHold', 'Closed', 'Solved'],
    Closed: ['Closed'],
};

const columns = [
    { id: 'todo', statuses: [{ id: 'new', category: 'New' }], cards: [{ id: 't1', statusId: 'new' }], total: 1 },
    { id: 'doing', statuses: [{ id: 'open', category: 'Open' }], cards: [], total: 0 },
    { id: 'waiting', statuses: [{ id: 'requester', category: 'Pending' }, { id: 'supplier', category: 'OnHold' }], cards: [], total: 0 },
    { id: 'reopened', statuses: [{ id: 'new-again', category: 'New' }], cards: [], total: 0 },
];

// The board shows a ticket in the column of its status.
describe('columnOf', () => {
    it('finds the column that has the status', () => {
        expect(board.columnOf(columns, 'supplier').id).toBe('waiting');
    });

    it('finds none for a status in no column', () => {
        expect(board.columnOf(columns, 'closed')).toBeNull();
    });
});

// A drag greys out the columns the ticket can't move to; the server still decides.
describe('canDrop', () => {
    it('lets a card the user may move go to a column its life cycle reaches', () => {
        expect(board.canDrop({ canMove: true, statusId: 'new', category: 'New' }, columns[2], transitions)).toBe(true);
    });

    it('refuses a card the user may not move', () => {
        expect(board.canDrop({ canMove: false, statusId: 'new', category: 'New' }, columns[1], transitions)).toBe(false);
    });

    it('refuses a column whose statuses the ticket can no longer reach', () => {
        // A pending ticket can't go back to New.
        expect(board.canDrop({ canMove: true, statusId: 'requester', category: 'Pending' }, columns[3], transitions)).toBe(false);
    });

    it('lets a card go back to its own column', () => {
        expect(board.canDrop({ canMove: true, statusId: 'new', category: 'New' }, columns[0], transitions)).toBe(true);
    });
});

describe('countOf', () => {
    it('says when the column shows only some of its tickets', () => {
        expect(board.countOf({ cards: [{}, {}], total: 5 })).toEqual({ shown: 2, total: 5, partial: true, overLimit: false });
    });

    it('says when the column holds more than its limit', () => {
        expect(board.countOf({ cards: [{}, {}, {}], total: 3, limit: 2 }).overLimit).toBe(true);
        expect(board.countOf({ cards: [{}, {}], total: 2, limit: 2 }).overLimit).toBe(false);
        expect(board.countOf({ cards: [{}], total: 1, limit: null }).overLimit).toBe(false);
    });
});

// A moved card leaves its column and tops its new one, with both counts kept right until the board loads again.
describe('placeCard', () => {
    it('moves the card to the column of its new status', () => {
        const { columns: next, placed } = board.placeCard(columns, { id: 't1', statusId: 'supplier' });

        expect(placed).toBe(true);
        expect(next[0].cards).toEqual([]);
        expect(next[0].total).toBe(0);
        expect(next[2].cards.map((card) => card.id)).toEqual(['t1']);
        expect(next[2].total).toBe(1);
    });

    it('takes the card off the board when its new status is in no column', () => {
        const { columns: next, placed } = board.placeCard(columns, { id: 't1', statusId: 'closed' });

        expect(placed).toBe(false);
        expect(next.every((column) => column.cards.every((card) => card.id !== 't1'))).toBe(true);
    });

    it('leaves the columns it was given as they were', () => {
        board.placeCard(columns, { id: 't1', statusId: 'open' });

        expect(columns[0].cards).toHaveLength(1);
        expect(columns[1].cards).toHaveLength(0);
    });
});

describe('relativeTime', () => {
    it('says how long ago, in the page language', () => {
        const now = Date.parse('2026-11-15T12:00:00Z');

        expect(board.relativeTime('2026-11-15T09:00:00Z', now, 'en')).toBe('3 hours ago');
        expect(board.relativeTime('2026-11-14T12:00:00Z', now, 'en')).toBe('yesterday');
    });

    it('says nothing for a time it can not read', () => {
        expect(board.relativeTime('soon', Date.now(), 'en')).toBe('');
    });
});

describe('format', () => {
    it('fills every placeholder', () => {
        expect(board.format('{0} is now {1}; {0}.', 'T-1', 'Open')).toBe('T-1 is now Open; T-1.');
    });
});

// With SignalR on, the board joins the hub and reloads when pinged; it polls only while it isn't connected.
describe('connectLive', () => {
    function fakeSignalR(subscribed) {
        const handlers = {};
        const connection = {
            on: (name, handler) => { handlers[name] = handler; },
            onreconnecting: (handler) => { handlers.reconnecting = handler; },
            onreconnected: (handler) => { handlers.reconnected = handler; },
            onclose: (handler) => { handlers.close = handler; },
            start: vi.fn(async () => {}),
            invoke: vi.fn(async () => subscribed),
        };
        const builder = { withUrl: () => builder, withAutomaticReconnect: () => builder, build: () => connection };

        return { signalR: { HubConnectionBuilder: function HubConnectionBuilder() { return builder; } }, handlers, connection };
    }

    it('joins, then reloads on each ping', async () => {
        const { signalR, handlers, connection } = fakeSignalR(true);
        const onPing = vi.fn();
        const states = [];

        await board.connectLive('/hubs/tickets', signalR, onPing, (value) => states.push(value));
        handlers.TicketsChanged();

        expect(connection.invoke).toHaveBeenCalledWith('Subscribe');
        expect(states).toEqual([true]);
        expect(onPing).toHaveBeenCalledTimes(1);
    });

    it('polls again while the connection is down, and reloads once it is back', async () => {
        const { signalR, handlers } = fakeSignalR(true);
        const onPing = vi.fn();
        const states = [];

        await board.connectLive('/hubs/tickets', signalR, onPing, (value) => states.push(value));
        handlers.reconnecting();
        await handlers.reconnected();

        expect(states).toEqual([true, false, true]);
        expect(onPing).toHaveBeenCalledTimes(1);
    });

    it('keeps polling without SignalR, or when the user may not join', async () => {
        const states = [];
        const { signalR } = fakeSignalR(false);

        await board.connectLive('', signalR, vi.fn(), (value) => states.push(value));
        await board.connectLive('/hubs/tickets', undefined, vi.fn(), (value) => states.push(value));
        await board.connectLive('/hubs/tickets', signalR, vi.fn(), (value) => states.push(value));

        expect(states).toEqual([false, false, false]);
    });
});
