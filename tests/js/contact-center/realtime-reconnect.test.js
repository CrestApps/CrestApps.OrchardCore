import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

// The Contact Center hub carries the agent's presence. With SignalR's default reconnect schedule the client gave up
// after about 42 seconds, so a server restart or a longer network drop left the agent offline to routing, with calls
// reporting "No agents are currently available", while the page still looked signed in until it was reloaded.
// These pin that the client keeps trying for as long as it takes, restarts a connection that closed on its own or
// never started, and stays closed when the page stops it on purpose.

class FakeConnection {
    constructor(policy) {
        this.policy = policy;
        this.handlers = {};
        this.startCalls = 0;
        this.startResults = [];
        this.invoke = vi.fn(() => Promise.resolve({}));
        this.stop = vi.fn(() => {
            this.fire('close');

            return Promise.resolve();
        });
    }

    on() { }

    onreconnected(handler) { this.handlers.reconnected = handler; }

    onreconnecting(handler) { this.handlers.reconnecting = handler; }

    onclose(handler) { this.handlers.close = handler; }

    fire(name, argument) { this.handlers[name]?.(argument); }

    start() {
        this.startCalls++;
        const fails = this.startResults.shift();

        return fails ? Promise.reject(new Error('unreachable')) : Promise.resolve();
    }
}

let connection;

beforeAll(async () => {
    globalThis.window = globalThis;

    globalThis.signalR = {
        HubConnectionBuilder: class {
            withUrl() { return this; }

            withAutomaticReconnect(policy) {
                this.policy = policy;

                return this;
            }

            build() {
                connection = new FakeConnection(this.policy);

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

describe('Contact Center hub reconnect', () => {
    it('never stops retrying an interrupted connection', () => {
        connect();

        const { policy } = connection;

        expect(policy?.nextRetryDelayInMilliseconds).toBeTypeOf('function');

        for (const previousRetryCount of [0, 1, 4, 10, 100]) {
            const delay = policy.nextRetryDelayInMilliseconds({ previousRetryCount, elapsedMilliseconds: previousRetryCount * 30000 });

            expect(delay).toBeTypeOf('number');
            expect(delay).toBeLessThanOrEqual(30000);
        }
    });

    it('keeps trying to start when the hub is unreachable as the page loads', async () => {
        const onConnected = vi.fn();

        const builder = globalThis.signalR.HubConnectionBuilder;
        const originalBuild = builder.prototype.build;
        builder.prototype.build = function () {
            const built = originalBuild.call(this);
            built.startResults = [true, true];

            return built;
        };

        try {
            connect({ onConnected });
            await vi.advanceTimersByTimeAsync(0);

            expect(connection.startCalls).toBe(1);
            expect(onConnected).not.toHaveBeenCalled();

            await vi.advanceTimersByTimeAsync(60000);

            expect(connection.startCalls).toBe(3);
            expect(onConnected).toHaveBeenCalledTimes(1);
        } finally {
            builder.prototype.build = originalBuild;
        }
    });

    it('restarts a connection that closed on its own', async () => {
        const onConnected = vi.fn();
        connect({ onConnected });
        await vi.advanceTimersByTimeAsync(0);
        expect(connection.startCalls).toBe(1);

        connection.fire('close', new Error('server went away'));
        await vi.advanceTimersByTimeAsync(60000);

        expect(connection.startCalls).toBe(2);
        expect(onConnected).toHaveBeenCalledTimes(2);
    });

    it('stays closed when the page stops it', async () => {
        const client = connect();
        await vi.advanceTimersByTimeAsync(0);

        await client.stop();
        await vi.advanceTimersByTimeAsync(120000);

        expect(connection.startCalls).toBe(1);
    });
});
