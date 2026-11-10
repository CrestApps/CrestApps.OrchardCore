import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.AgenticCommerce/Assets/js/embedded-checkout.js';

const ecp = globalThis.CrestAppsEmbeddedCheckout;

const HOST = 'https://agent.example';

function flush() {
    return new Promise((resolve) => setTimeout(resolve, 0));
}

// A channel that records what the checkout sends and lets a test answer as the host.
function fakeChannel() {
    const channel = {
        sent: [],
        locked: null,
        upgradedTo: null,
        receive: null,
        send(message) {
            channel.sent.push(message);
        },
        listen(receive) {
            channel.receive = receive;
        },
        lock(origin) {
            channel.locked = origin;
        },
        upgrade(port, receive) {
            channel.upgradedTo = port;
            channel.receive = receive;

            return true;
        },
        answer(result, meta = { origin: HOST, ports: [] }) {
            const request = channel.sent[channel.sent.length - 1];

            channel.receive({ jsonrpc: '2.0', id: request.id, result }, meta);
        },
        methods() {
            return channel.sent.map((message) => message.method);
        },
    };

    return channel;
}

function success(extra = {}) {
    return { ucp: { version: '2026-08-25', status: 'success' }, ...extra };
}

function checkoutState(overrides = {}) {
    return {
        id: 'chk_1',
        status: 'incomplete',
        line_items: [{ id: 'l1', quantity: 1 }],
        buyer: { email: 'ada@example.com' },
        totals: [{ type: 'subtotal', amount: 1000 }, { type: 'total', amount: 1000 }],
        ...overrides,
    };
}

function baseConfig(overrides = {}) {
    return {
        version: '2026-08-25',
        delegate: [],
        continueUrl: 'https://shop.example/agentic-commerce/continue/abc',
        auth: null,
        ...overrides,
    };
}

function session(options = {}) {
    const channel = options.channel || fakeChannel();
    const states = options.states || [];
    const timers = [];
    let loaded = 0;

    const s = ecp.createSession({
        config: baseConfig(options.config),
        channel,
        loadCheckout: () => {
            loaded++;
        },
        fetchCheckout: () => Promise.resolve(states.shift()),
        verifyCredential: options.verifyCredential || (() => Promise.resolve({ valid: true, expires_in: 3600 })),
        setTimeout: (callback, delay) => timers.push({ callback, delay }),
    });

    return { s, channel, timers, loaded: () => loaded };
}

describe('changedMethods', () => {
    it('sends a domain change before the totals it moved, then the messages', () => {
        const before = checkoutState();
        const after = checkoutState({
            line_items: [{ id: 'l1', quantity: 2 }],
            totals: [{ type: 'subtotal', amount: 2000 }, { type: 'total', amount: 2000 }],
            messages: [{ type: 'info', code: 'free_shipping' }],
        });

        expect(ecp.changedMethods(before, after)).toEqual(['ec.line_items.change', 'ec.totals.change', 'ec.messages.change']);
    });

    it('sends nothing when nothing changed', () => {
        expect(ecp.changedMethods(checkoutState(), checkoutState())).toEqual([]);
    });

    it('notices a buyer, fulfillment or payment change', () => {
        const after = checkoutState({ buyer: { email: 'grace@example.com' }, fulfillment: { methods: [] }, payment: { instruments: [] } });

        expect(ecp.changedMethods(checkoutState(), after)).toEqual(['ec.buyer.change', 'ec.fulfillment.change', 'ec.payment.change']);
    });
});

describe('isCheckoutUrl', () => {
    const paths = ['/shop/Checkout/s1', '/shop/Checkout/Confirmation/s1'];

    it('keeps the session\'s checkout pages in the frame', () => {
        expect(ecp.isCheckoutUrl('https://shop.example/shop/checkout/s1/payment', 'https://shop.example', paths)).toBe(true);
        expect(ecp.isCheckoutUrl('https://shop.example/shop/Checkout/Confirmation/s1', 'https://shop.example', paths)).toBe(true);
    });

    it('sends every other page out of the frame', () => {
        expect(ecp.isCheckoutUrl('https://shop.example/shop/Checkout/s10', 'https://shop.example', paths)).toBe(false);
        expect(ecp.isCheckoutUrl('https://shop.example/shop/cart', 'https://shop.example', paths)).toBe(false);
        expect(ecp.isCheckoutUrl('https://evil.example/shop/Checkout/s1', 'https://shop.example', paths)).toBe(false);
    });
});

describe('createWindowChannel', () => {
    function fakeWindow(ancestorOrigins) {
        const listeners = [];
        const parent = { posted: [], postMessage(message, origin) { parent.posted.push({ message, origin }); } };

        return {
            parent,
            location: { ancestorOrigins },
            addEventListener: (type, listener) => listeners.push(listener),
            dispatch: (event) => listeners.forEach((listener) => listener(event)),
        };
    }

    it('posts to each allowed origin until the host answers, then only to the host', () => {
        const win = fakeWindow(undefined);
        const channel = ecp.createWindowChannel(win, [HOST, 'https://other.example']);

        channel.send({ jsonrpc: '2.0', id: 'ec.ready_1', method: 'ec.ready', params: { delegate: [] } });

        expect(win.parent.posted.map((post) => post.origin)).toEqual([HOST, 'https://other.example']);

        channel.lock(HOST);
        win.parent.posted.length = 0;
        channel.send({ jsonrpc: '2.0', method: 'ec.start', params: {} });

        expect(win.parent.posted.map((post) => post.origin)).toEqual([HOST]);
    });

    it('posts only to the parent\'s origin when the browser reports it, and to none when it is not allowed', () => {
        const allowed = fakeWindow([HOST]);
        ecp.createWindowChannel(allowed, [HOST, 'https://other.example']).send({ jsonrpc: '2.0', method: 'x' });

        expect(allowed.parent.posted.map((post) => post.origin)).toEqual([HOST]);

        const stranger = fakeWindow(['https://evil.example']);
        ecp.createWindowChannel(stranger, [HOST]).send({ jsonrpc: '2.0', method: 'x' });

        expect(stranger.parent.posted).toEqual([]);
    });

    it('takes messages only from the parent and an allowed origin, and only the host\'s once locked', () => {
        const win = fakeWindow(undefined);
        const channel = ecp.createWindowChannel(win, [HOST, 'https://other.example']);
        const received = [];

        channel.listen((data, meta) => received.push(meta.origin));

        win.dispatch({ source: win.parent, origin: 'https://evil.example', data: {} });
        win.dispatch({ source: {}, origin: HOST, data: {} });
        win.dispatch({ source: win.parent, origin: HOST, data: {} });

        channel.lock(HOST);
        win.dispatch({ source: win.parent, origin: 'https://other.example', data: {} });

        expect(received).toEqual([HOST]);
    });

    it('moves to the host\'s port when it upgrades the channel', () => {
        const win = fakeWindow(undefined);
        const channel = ecp.createWindowChannel(win, [HOST]);
        const port = { posted: [], postMessage(message) { port.posted.push(message); }, start() { } };

        expect(channel.upgrade(port, () => { })).toBe(true);

        channel.send({ jsonrpc: '2.0', method: 'ec.start' });

        expect(port.posted).toHaveLength(1);
        expect(win.parent.posted).toEqual([]);
    });
});

describe('createNativeChannel', () => {
    it('sends JSON text to the consumer and reads what the app posts', () => {
        const sent = [];
        const win = {};
        const channel = ecp.createNativeChannel(win, { postMessage: (text) => sent.push(text) });
        const received = [];

        channel.listen((data) => received.push(data));
        channel.send({ jsonrpc: '2.0', method: 'ec.start' });
        win.EmbeddedCheckoutProtocol.postMessage('{"jsonrpc":"2.0","id":"1","result":{}}');
        win.EmbeddedCheckoutProtocol.postMessage('not json');

        expect(JSON.parse(sent[0]).method).toBe('ec.start');
        expect(received).toEqual([{ jsonrpc: '2.0', id: '1', result: {} }]);
    });

    it('finds the consumer an app injects', () => {
        const consumer = { postMessage() { } };

        expect(ecp.nativeConsumer({ EmbeddedCheckoutProtocolConsumer: consumer })).toBe(consumer);
        expect(ecp.nativeConsumer({ webkit: { messageHandlers: { EmbeddedCheckoutProtocolConsumer: consumer } } })).toBe(consumer);
        expect(ecp.nativeConsumer({})).toBeNull();
    });
});

describe('createSession', () => {
    it('shakes hands with no delegation, then shows the checkout and starts with its full state', async () => {
        const { s, channel, loaded } = session({ states: [checkoutState()] });

        const started = s.start();

        expect(channel.sent[0]).toMatchObject({ jsonrpc: '2.0', method: 'ec.ready', params: { delegate: [] } });
        expect(channel.sent[0].params.auth).toBeUndefined();

        channel.answer(success());

        expect(await started).toBe(true);
        expect(channel.locked).toBe(HOST);
        expect(loaded()).toBe(1);

        await s.refresh();

        expect(channel.sent[1]).toEqual({ jsonrpc: '2.0', method: 'ec.start', params: { checkout: checkoutState() } });
    });

    it('tells the host what changed on each page, and completes once', async () => {
        const totals = [{ type: 'subtotal', amount: 1000 }, { type: 'tax', amount: 80 }, { type: 'total', amount: 1080 }];
        const completed = checkoutState({ status: 'completed', totals, order: { id: 'ord_1' } });
        const { s, channel } = session({
            states: [
                checkoutState(),
                checkoutState({ totals }),
                completed,
                completed,
            ],
        });

        const started = s.start();
        channel.answer(success());
        await started;

        await s.refresh();
        await s.refresh();
        await s.refresh();
        await s.refresh();

        expect(channel.methods().slice(1)).toEqual(['ec.start', 'ec.totals.change', 'ec.complete']);
        expect(channel.sent[3].params.checkout.order.id).toBe('ord_1');
    });

    it('sends nothing more after the host refuses the handshake', async () => {
        const { s, channel, loaded } = session({ states: [checkoutState()] });

        const started = s.start();
        channel.answer({ ucp: { version: '2026-08-25', status: 'error' }, messages: [{ code: 'security_error' }] });

        expect(await started).toBe(false);

        await s.refresh();

        expect(channel.methods()).toEqual(['ec.ready']);
        expect(loaded()).toBe(0);
        expect(s.state).toBe('closed');
    });

    it('ends the session when the host confirms another version', async () => {
        const { s, channel } = session();

        const started = s.start();
        channel.answer({ ucp: { version: '2026-04-08', status: 'success' } });

        expect(await started).toBe(false);
        expect(channel.sent[1].method).toBe('ec.error');
        expect(channel.sent[1].params.error.messages[0].code).toBe('invalid_state_error');
        expect(channel.sent[1].params.error.continue_url).toBe('https://shop.example/agentic-commerce/continue/abc');
    });

    it('asks for a credential in the handshake when the host must give one, and has the store check it', async () => {
        const checked = [];
        const { s, channel, loaded } = session({
            config: { auth: { type: 'oauth', required: true } },
            verifyCredential: (credential) => {
                checked.push(credential);

                return Promise.resolve({ valid: true, expires_in: 600 });
            },
        });

        const started = s.start();

        expect(channel.sent[0].params.auth).toEqual({ type: 'oauth' });

        channel.answer(success({ credential: 'token-1' }));

        expect(await started).toBe(true);
        expect(checked).toEqual(['token-1']);
        expect(loaded()).toBe(1);
    });

    it('hands the buyer off with ec.error when the credential is refused', async () => {
        const { s, channel, loaded } = session({
            config: { auth: { type: 'oauth', required: true } },
            verifyCredential: () => Promise.resolve({ valid: false }),
        });

        const started = s.start();
        channel.answer(success({ credential: 'bad' }));

        expect(await started).toBe(false);
        expect(loaded()).toBe(0);
        expect(channel.sent[1]).toMatchObject({
            method: 'ec.error',
            params: { error: { ucp: { status: 'error' }, continue_url: 'https://shop.example/agentic-commerce/continue/abc' } },
        });
        expect(channel.sent[1].params.error.messages[0].severity).toBe('unrecoverable');
    });

    it('renews the credential with ec.auth before it runs out', async () => {
        const { s, channel, timers } = session({ config: { auth: { type: 'oauth', required: false, expiresIn: 600 } } });

        const started = s.start();
        channel.answer(success());
        await started;

        expect(timers).toHaveLength(1);
        expect(timers[0].delay).toBe(540 * 1000);

        timers[0].callback();

        expect(channel.sent[channel.sent.length - 1]).toMatchObject({ method: 'ec.auth', params: { type: 'oauth' } });

        channel.answer(success({ credential: 'token-2' }));
        await flush();

        expect(timers).toHaveLength(2);
    });

    it('retries a renewal the host could not give for now, then gives up with ec.error', async () => {
        const { s, channel, timers } = session({ config: { auth: { type: 'oauth', required: false, expiresIn: 600 } } });
        const busy = { ucp: { version: '2026-08-25', status: 'error' }, messages: [{ type: 'error', code: 'timeout_error', severity: 'recoverable' }] };

        const started = s.start();
        channel.answer(success());
        await started;

        timers[0].callback();
        channel.answer(busy);
        await flush();
        timers[1].callback();
        channel.answer(busy);
        await flush();
        timers[2].callback();
        channel.answer(busy);
        await flush();

        expect(channel.methods().filter((method) => method === 'ec.auth')).toHaveLength(3);
        expect(channel.sent[channel.sent.length - 1].method).toBe('ec.error');
        expect(s.state).toBe('closed');
    });

    it('answers a request from the host with method not found', async () => {
        const { s, channel } = session();

        s.start();
        channel.receive({ jsonrpc: '2.0', id: 'h1', method: 'ec.something', params: {} }, { origin: HOST });

        expect(channel.sent[1]).toEqual({ jsonrpc: '2.0', id: 'h1', error: { code: -32601, message: 'Method not found.' } });
    });

    it('shakes hands again over the port the host upgrades to', async () => {
        const { s, channel } = session();
        const port = {};

        const started = s.start();
        channel.answer(success({ upgrade: { port } }));
        await flush();

        expect(channel.upgradedTo).toBe(port);
        expect(channel.sent[1].method).toBe('ec.ready');

        channel.answer(success());

        expect(await started).toBe(true);
    });

    it('ignores answers to requests it never sent', async () => {
        const { s, channel } = session();

        s.start();
        channel.receive({ jsonrpc: '2.0', id: 'forged', result: success() }, { origin: HOST });
        channel.receive('not an object', { origin: HOST });

        expect(s.state).toBe('handshaking');
    });
});

describe('sameOriginUrl', () => {
    const origin = 'https://store.example';

    it('keeps a page of the store', () => {
        expect(ecp.sameOriginUrl('/Checkout/abc', origin)).toBe('https://store.example/Checkout/abc');
        expect(ecp.sameOriginUrl('https://store.example/Checkout/abc?x=1', origin)).toBe('https://store.example/Checkout/abc?x=1');
    });

    it('refuses another site and scripted addresses', () => {
        expect(ecp.sameOriginUrl('https://evil.example/Checkout/abc', origin)).toBeNull();
        expect(ecp.sameOriginUrl('javascript:alert(1)', origin)).toBeNull();
        expect(ecp.sameOriginUrl('data:text/html,<script>alert(1)</script>', origin)).toBeNull();
        expect(ecp.sameOriginUrl('//evil.example/x', origin)).toBeNull();
        expect(ecp.sameOriginUrl(null, origin)).toBeNull();
    });
});
