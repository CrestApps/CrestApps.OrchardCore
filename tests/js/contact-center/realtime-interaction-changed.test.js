import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

// Bug: the agent workspace and the docked agent bar never showed an outbound dialer call while it rang and talked.
// The dial is accepted before it is placed, so the refresh the acceptance caused found nothing live, and nothing
// told the page about the call again until wrap-up. The server now pushes InteractionChanged for the agent's call
// (dial started, connected, held, resumed, ended); these pin that the real-time helper hands that push to the page.

class FakeConnection {
    constructor() {
        this.handlers = {};
        this.invoke = vi.fn(() => Promise.resolve({}));
        this.stop = vi.fn(() => Promise.resolve());
    }

    on(name, handler) { this.handlers[name] = handler; }

    onreconnected() { }

    onreconnecting() { }

    onclose() { }

    start() { return Promise.resolve(); }
}

let connection;

beforeAll(async () => {
    globalThis.window = globalThis;

    globalThis.signalR = {
        HubConnectionBuilder: class {
            withUrl() { return this; }

            withAutomaticReconnect() { return this; }

            build() {
                connection = new FakeConnection();

                return connection;
            }
        },
    };

    await import('../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/contact-center-realtime.js');
});

beforeEach(() => {
    vi.useFakeTimers();
});

afterEach(() => {
    vi.useRealTimers();
});

function connect(options = {}) {
    const client = globalThis.contactCenterRealTime.connect({ hubUrl: '/hub', ...options });
    client.started.catch(() => { });

    return client;
}

describe('Contact Center InteractionChanged push', () => {
    it('hands every InteractionChanged push to onInteractionChanged', () => {
        const onInteractionChanged = vi.fn();
        connect({ onInteractionChanged });

        const notification = {
            interactionId: 'interaction-1',
            eventType: 'CallConnected',
            direction: 'Outbound',
            status: 'Connected',
        };

        connection.handlers.InteractionChanged(notification);
        connection.handlers.InteractionChanged({ ...notification, eventType: 'CallEnded', status: 'Ended' });

        expect(onInteractionChanged).toHaveBeenCalledTimes(2);
        expect(onInteractionChanged).toHaveBeenNthCalledWith(1, notification);
        expect(onInteractionChanged.mock.calls[1][0].eventType).toBe('CallEnded');
    });

    it('does not hand the push to the other callbacks', () => {
        const onPresenceChanged = vi.fn();
        const onOfferReceived = vi.fn();
        const onInteractionChanged = vi.fn();
        connect({ onPresenceChanged, onOfferReceived, onInteractionChanged });

        connection.handlers.InteractionChanged({ interactionId: 'interaction-1' });

        expect(onInteractionChanged).toHaveBeenCalledTimes(1);
        expect(onPresenceChanged).not.toHaveBeenCalled();
        expect(onOfferReceived).not.toHaveBeenCalled();
    });

    // A page that does not listen for it (the supervisor dashboard) must still connect and ignore the push.
    it('ignores the push when the page does not listen for it', () => {
        connect();

        expect(connection.handlers.InteractionChanged).toBeTypeOf('function');
        expect(() => connection.handlers.InteractionChanged({ interactionId: 'interaction-1' })).not.toThrow();
    });
});
