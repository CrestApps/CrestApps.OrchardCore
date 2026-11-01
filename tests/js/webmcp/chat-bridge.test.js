import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp.js';
import '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp-chat-bridge.js';
import { textResult } from '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp-results.js';

const bridgeModule = globalThis.CrestAppsWebMcpChatBridge;

function fakeConnection() {
    const handlers = {};
    const reconnected = [];
    const closed = [];

    return {
        handlers,
        invoke: vi.fn(() => Promise.resolve()),
        start: vi.fn(() => Promise.resolve()),
        on: (name, handler) => {
            handlers[name] = handler;
        },
        onreconnected: (handler) => reconnected.push(handler),
        onclose: (handler) => closed.push(handler),
        reconnect: () => Promise.all(reconnected.map((handler) => handler())),
        close: () => closed.forEach((handler) => handler()),
    };
}

function fakeTarget() {
    const listeners = {};

    return {
        listeners,
        visibilityState: 'visible',
        addEventListener: (name, handler) => {
            listeners[name] = handler;
        },
    };
}

function setup() {
    const api = globalThis.CrestAppsWebMcp.createRegistry(null);
    const connection = fakeConnection();
    const doc = fakeTarget();
    const win = fakeTarget();
    const bridge = bridgeModule.createBridge({ api, connection, doc, win });

    return { api, connection, doc, win, bridge };
}

function pageTool(name, execute) {
    return {
        name,
        title: 'Title ' + name,
        description: 'Does ' + name + '.',
        inputSchema: { type: 'object', properties: { text: { type: 'string' } } },
        execute: execute || ((input) => textResult('ran ' + name + ' with ' + input.text)),
    };
}

describe('toBrowserToolDescriptors', () => {
    it('keeps the page tools with names AI providers accept and sends their schemas as text', () => {
        const descriptors = bridgeModule.toBrowserToolDescriptors([
            { name: 'page_get_info', title: 'Info', description: 'Info.', inputSchema: { type: 'object' }, source: 'page' },
            { name: 'list_time_zones', description: 'Server.', inputSchema: { type: 'object' }, source: 'server' },
            { name: 'has space', description: 'Bad name.', source: 'page' },
            { name: 'x'.repeat(65), description: 'Too long.', source: 'page' },
            { name: 'no_schema', description: 'No schema.', source: 'page' },
        ]);

        expect(descriptors).toEqual([
            { name: 'page_get_info', title: 'Info', description: 'Info.', inputSchema: '{"type":"object"}' },
            { name: 'no_schema', title: null, description: 'No schema.', inputSchema: '{"type":"object","properties":{}}' },
        ]);
    });

    it('sends at most sixty-four tools', () => {
        const tools = [];

        for (let i = 0; i < 70; i++) {
            tools.push({ name: 'tool_' + i, description: 'Tool.', source: 'page' });
        }

        expect(bridgeModule.toBrowserToolDescriptors(tools)).toHaveLength(64);
    });
});

describe('createBridge', () => {
    it('registers the page tools once connected', async () => {
        const { api, connection, bridge } = setup();

        api.registerTool(pageTool('page_one'));
        await bridge.start();

        expect(connection.start).toHaveBeenCalled();
        expect(connection.invoke).toHaveBeenCalledWith('RegisterPageTools', [
            {
                name: 'page_one',
                title: 'Title page_one',
                description: 'Does page_one.',
                inputSchema: '{"type":"object","properties":{"text":{"type":"string"}}}',
            },
        ]);
    });

    it('clears the tools when the page has none', async () => {
        const { connection, bridge } = setup();

        await bridge.start();

        expect(connection.invoke).toHaveBeenCalledWith('ClearPageTools');
    });

    it('sends nothing before it connects', async () => {
        const { connection, bridge } = setup();

        await bridge.sync();

        expect(connection.invoke).not.toHaveBeenCalled();
    });

    it('registers again when the tools change, the tab gets focus, the page shows again or the connection comes back', async () => {
        const { api, connection, doc, win, bridge } = setup();

        await bridge.start();
        connection.invoke.mockClear();

        api.registerTool(pageTool('page_one'));
        await Promise.resolve();
        await win.listeners.focus();
        await doc.listeners.visibilitychange();
        await connection.reconnect();

        expect(connection.invoke.mock.calls.filter((call) => call[0] === 'RegisterPageTools')).toHaveLength(4);
    });

    it('does not register while the page is hidden', async () => {
        const { connection, doc, bridge } = setup();

        await bridge.start();
        connection.invoke.mockClear();
        doc.visibilityState = 'hidden';
        await doc.listeners.visibilitychange();

        expect(connection.invoke).not.toHaveBeenCalled();
    });

    it('stops sending once the connection closes', async () => {
        const { api, connection, bridge } = setup();

        await bridge.start();
        connection.close();
        connection.invoke.mockClear();
        api.registerTool(pageTool('page_one'));
        await bridge.sync();

        expect(connection.invoke).not.toHaveBeenCalled();
    });

    it('runs a tool the server asks for and sends back its text', async () => {
        const { api, connection, bridge } = setup();

        api.registerTool(pageTool('page_one'));
        await bridge.start();
        await connection.handlers.ExecuteTool('call-1', 'page_one', '{"text":"hi"}');

        expect(connection.invoke).toHaveBeenCalledWith('SubmitToolResult', 'call-1', 'ran page_one with hi', false);
    });

    it('sends back an error when the tool fails, is missing or gets broken input', async () => {
        const { api, connection, bridge } = setup();

        api.registerTool(pageTool('page_fails', () => {
            throw new Error('Boom');
        }));
        await bridge.start();
        await connection.handlers.ExecuteTool('call-1', 'page_fails', '{}');
        await connection.handlers.ExecuteTool('call-2', 'page_missing', '{}');
        await connection.handlers.ExecuteTool('call-3', 'page_fails', '{broken');

        const submitted = connection.invoke.mock.calls.filter((call) => call[0] === 'SubmitToolResult');

        expect(submitted).toHaveLength(3);
        expect(submitted[0]).toEqual(['SubmitToolResult', 'call-1', 'The tool failed: Boom', true]);
        expect(submitted[1][1]).toBe('call-2');
        expect(submitted[1][3]).toBe(true);
        expect(submitted[2]).toEqual(['SubmitToolResult', 'call-3', 'The tool input is not valid JSON.', true]);
    });

    it('refuses to run a server tool from the chat', async () => {
        const { api, connection, bridge } = setup();
        const execute = vi.fn();

        api.registerTool(Object.assign(pageTool('server_tool', execute), { source: 'server' }));
        await bridge.start();
        await connection.handlers.ExecuteTool('call-1', 'server_tool', '{}');

        expect(execute).not.toHaveBeenCalled();
        expect(connection.invoke).toHaveBeenCalledWith('SubmitToolResult', 'call-1', 'The tool "server_tool" is not a page tool.', true);
    });
});

describe('startWhenChatAppears', () => {
    function chatDocument(hasChat) {
        const listeners = {};

        return {
            listeners,
            querySelector: (selector) => (hasChat && selector === bridgeModule.CHAT_SELECTOR ? {} : null),
            addEventListener: (name, handler) => {
                listeners[name] = handler;
            },
            removeEventListener: (name) => {
                delete listeners[name];
            },
        };
    }

    it('starts at once when the page already has a chat', () => {
        const start = vi.fn();

        bridgeModule.startWhenChatAppears(chatDocument(true), start);

        expect(start).toHaveBeenCalledTimes(1);
    });

    it('waits for the user to reach a chat, then starts once', () => {
        const start = vi.fn();
        const doc = chatDocument(false);

        bridgeModule.startWhenChatAppears(doc, start);

        expect(start).not.toHaveBeenCalled();

        doc.listeners.focusin({ target: { closest: () => null } });

        expect(start).not.toHaveBeenCalled();

        doc.listeners.focusin({ target: { closest: (selector) => (selector === bridgeModule.CHAT_SELECTOR ? {} : null) } });

        expect(start).toHaveBeenCalledTimes(1);
        expect(doc.listeners.focusin).toBeUndefined();
        expect(doc.listeners.initializingSessionCoreAIChat).toBeUndefined();
    });

    it('starts when a chat session begins', () => {
        const start = vi.fn();
        const doc = chatDocument(false);

        bridgeModule.startWhenChatAppears(doc, start);
        doc.listeners.initializingSessionCoreAIChat({});

        expect(start).toHaveBeenCalledTimes(1);
    });
});
