import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp.js';
import { isErrorResult, serializeResult, textResult } from '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp-results.js';

const webMcp = globalThis.CrestAppsWebMcp;

function echoTool(name, extra) {
    return Object.assign({
        name,
        description: 'Echoes its input.',
        inputSchema: { type: 'object', properties: { text: { type: 'string' } } },
        execute: (input) => textResult(input.text),
    }, extra);
}

function fakeModelContext() {
    const registered = [];

    return {
        registered,
        registerTool: vi.fn((tool, options) => {
            registered.push({ tool, options });

            return Promise.resolve();
        }),
    };
}

function jsonResponse(body, status = 200) {
    return {
        ok: status >= 200 && status < 300,
        status,
        json: () => Promise.resolve(body),
    };
}

describe('getModelContext', () => {
    it('prefers the document, as the current draft defines it', () => {
        const documentContext = {};
        const navigatorContext = {};

        expect(webMcp.getModelContext({ modelContext: documentContext }, { modelContext: navigatorContext })).toBe(documentContext);
    });

    it('falls back to the navigator, as earlier browser previews define it', () => {
        const navigatorContext = {};

        expect(webMcp.getModelContext({}, { modelContext: navigatorContext })).toBe(navigatorContext);
    });

    it('answers null when the browser has no WebMCP', () => {
        expect(webMcp.getModelContext({}, {})).toBeNull();
        expect(webMcp.getModelContext(undefined, undefined)).toBeNull();
    });
});

describe('createRegistry', () => {
    it('registers a tool and lists it without its callback', () => {
        const registry = webMcp.createRegistry(null);

        registry.registerTool(echoTool('echo', { title: 'Echo', annotations: { readOnlyHint: true } }));

        expect(registry.getTools()).toEqual([{
            name: 'echo',
            title: 'Echo',
            description: 'Echoes its input.',
            inputSchema: { type: 'object', properties: { text: { type: 'string' } } },
            annotations: { readOnlyHint: true },
            source: 'page',
        }]);
    });

    it('refuses a tool without a name, a description or a callback', () => {
        const registry = webMcp.createRegistry(null);

        expect(() => registry.registerTool({ description: 'x', execute: () => null })).toThrow(TypeError);
        expect(() => registry.registerTool({ name: 'x', execute: () => null })).toThrow(TypeError);
        expect(() => registry.registerTool({ name: 'x', description: 'x' })).toThrow(TypeError);
    });

    it('mirrors a tool to the browser and unregisters it there with its signal', () => {
        const modelContext = fakeModelContext();
        const registry = webMcp.createRegistry(modelContext);

        registry.registerTool(echoTool('echo'));
        registry.unregisterTool('echo');

        expect(modelContext.registered).toHaveLength(1);
        expect(modelContext.registered[0].tool.name).toBe('echo');
        expect(typeof modelContext.registered[0].tool.execute).toBe('function');
        expect(modelContext.registered[0].options.signal.aborted).toBe(true);
        expect(registry.getTools()).toEqual([]);
    });

    it('calls unregisterTool on browsers that offer it', () => {
        const modelContext = fakeModelContext();

        modelContext.unregisterTool = vi.fn();

        const registry = webMcp.createRegistry(modelContext);

        registry.registerTool(echoTool('echo'));
        registry.unregisterTool('echo');

        expect(modelContext.unregisterTool).toHaveBeenCalledWith('echo');
    });

    it('keeps the tool on the page when the browser refuses it', () => {
        const modelContext = {
            registerTool: () => {
                throw new Error('Not allowed.');
            },
        };
        const registry = webMcp.createRegistry(modelContext);

        registry.registerTool(echoTool('echo'));

        expect(registry.getTools().map((tool) => tool.name)).toEqual(['echo']);
    });

    it('replaces a tool registered again under the same name', () => {
        const modelContext = fakeModelContext();
        const registry = webMcp.createRegistry(modelContext);

        registry.registerTool(echoTool('echo'));
        registry.registerTool(echoTool('echo', { description: 'Second.' }));

        expect(registry.getTools()).toHaveLength(1);
        expect(registry.getTools()[0].description).toBe('Second.');
        expect(modelContext.registered[0].options.signal.aborted).toBe(true);
        expect(modelContext.registered[1].options.signal.aborted).toBe(false);
    });

    it('executes a tool by name with its input', async () => {
        const registry = webMcp.createRegistry(null);

        registry.registerTool(echoTool('echo'));

        await expect(registry.executeTool('echo', { text: 'hello' })).resolves.toEqual(textResult('hello'));
    });

    it('answers an error result for a tool that does not exist', async () => {
        const registry = webMcp.createRegistry(null);

        const result = await registry.executeTool('missing', {});

        expect(isErrorResult(result)).toBe(true);
    });

    it('tells listeners when the tools change, until they stop listening', () => {
        const registry = webMcp.createRegistry(null);
        const listener = vi.fn();
        const stop = registry.onToolsChanged(listener);

        registry.registerTool(echoTool('echo'));
        registry.unregisterTool('echo');
        stop();
        registry.registerTool(echoTool('other'));

        expect(listener).toHaveBeenCalledTimes(2);
    });

    it('says whether the browser supports WebMCP', () => {
        expect(webMcp.createRegistry(null).isSupported()).toBe(false);
        expect(webMcp.createRegistry(fakeModelContext()).isSupported()).toBe(true);
    });
});

describe('createServerToolProxy', () => {
    const config = {
        invokeUrlTemplate: '/api/ai/webmcp/tools/{name}/invoke',
        antiforgeryHeaderName: 'RequestVerificationToken',
        antiforgeryToken: 'token-1',
    };

    const descriptor = {
        name: 'list_time_zones',
        title: 'List System Time Zones',
        description: 'Lists time zones.',
        inputSchema: { type: 'object', properties: {} },
    };

    it('describes the server tool and marks it as a server tool', () => {
        const proxy = webMcp.createServerToolProxy(descriptor, config, vi.fn());

        expect(proxy.name).toBe('list_time_zones');
        expect(proxy.title).toBe('List System Time Zones');
        expect(proxy.description).toBe('Lists time zones.');
        expect(proxy.inputSchema).toEqual({ type: 'object', properties: {} });
        expect(proxy.source).toBe('server');
    });

    it('posts the input with the antiforgery token and answers the server result', async () => {
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({ content: [{ type: 'text', text: 'UTC' }], isError: false })));
        const proxy = webMcp.createServerToolProxy(descriptor, config, fetchImpl);

        const result = await proxy.execute({ filter: 'u' });

        expect(fetchImpl).toHaveBeenCalledWith('/api/ai/webmcp/tools/list_time_zones/invoke', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                RequestVerificationToken: 'token-1',
            },
            body: JSON.stringify({ filter: 'u' }),
        });
        expect(result).toEqual({ content: [{ type: 'text', text: 'UTC' }], isError: false });
    });

    it('encodes the tool name in the address', async () => {
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({ content: [], isError: false })));
        const proxy = webMcp.createServerToolProxy(Object.assign({}, descriptor, { name: 'a b' }), config, fetchImpl);

        await proxy.execute({});

        expect(fetchImpl.mock.calls[0][0]).toBe('/api/ai/webmcp/tools/a%20b/invoke');
    });

    it('answers an error result when the server refuses the call', async () => {
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({}, 403)));
        const proxy = webMcp.createServerToolProxy(descriptor, config, fetchImpl);

        const result = await proxy.execute({});

        expect(isErrorResult(result)).toBe(true);
        expect(serializeResult(result)).toContain('403');
    });

    it('answers an error result when the network fails', async () => {
        const fetchImpl = vi.fn(() => Promise.reject(new Error('offline')));
        const proxy = webMcp.createServerToolProxy(descriptor, config, fetchImpl);

        const result = await proxy.execute({});

        expect(isErrorResult(result)).toBe(true);
    });
});

describe('loadServerTools', () => {
    it('registers a proxy for every tool the server lists', async () => {
        const registry = webMcp.createRegistry(null);
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({
            tools: [
                { name: 'one', description: 'One.', inputSchema: { type: 'object' } },
                { name: 'two', description: 'Two.', inputSchema: { type: 'object' } },
            ],
        })));

        const count = await webMcp.loadServerTools(registry, { toolsUrl: '/api/ai/webmcp/tools', invokeUrlTemplate: '/api/ai/webmcp/tools/{name}/invoke' }, fetchImpl);

        expect(count).toBe(2);
        expect(fetchImpl).toHaveBeenCalledWith('/api/ai/webmcp/tools', { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        expect(registry.getTools().map((tool) => tool.name)).toEqual(['one', 'two']);
        expect(registry.getTools().every((tool) => tool.source === 'server')).toBe(true);
    });

    it('skips a tool it cannot call and keeps the others', async () => {
        const registry = webMcp.createRegistry(null);
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({
            tools: [
                { description: 'No name.' },
                { name: 'two', description: 'Two.' },
            ],
        })));

        const count = await webMcp.loadServerTools(registry, { toolsUrl: '/api/ai/webmcp/tools', invokeUrlTemplate: '/x/{name}' }, fetchImpl);

        expect(count).toBe(1);
        expect(registry.getTools().map((tool) => tool.name)).toEqual(['two']);
    });

    it('registers nothing when the server refuses the list', async () => {
        const registry = webMcp.createRegistry(null);
        const fetchImpl = vi.fn(() => Promise.resolve(jsonResponse({}, 401)));

        const count = await webMcp.loadServerTools(registry, { toolsUrl: '/api/ai/webmcp/tools' }, fetchImpl);

        expect(count).toBe(0);
        expect(registry.getTools()).toEqual([]);
    });

    it('registers nothing without a list address', async () => {
        const registry = webMcp.createRegistry(null);
        const fetchImpl = vi.fn();

        const count = await webMcp.loadServerTools(registry, {}, fetchImpl);

        expect(count).toBe(0);
        expect(fetchImpl).not.toHaveBeenCalled();
    });
});

describe('readConfig', () => {
    it('reads the configuration block the server writes', () => {
        const doc = {
            getElementById: (id) => id === 'crestapps-webmcp-config'
                ? { textContent: '{"toolsUrl":"/api/ai/webmcp/tools","isAdmin":true}' }
                : null,
        };

        expect(webMcp.readConfig(doc)).toEqual({ toolsUrl: '/api/ai/webmcp/tools', isAdmin: true });
    });

    it('answers null when the block is missing or broken', () => {
        expect(webMcp.readConfig({ getElementById: () => null })).toBeNull();
        expect(webMcp.readConfig({ getElementById: () => ({ textContent: '{broken' }) })).toBeNull();
    });
});

describe('results', () => {
    it('serializes a text result to its text', () => {
        expect(serializeResult(textResult('one'))).toBe('one');
        expect(serializeResult({ content: [{ type: 'text', text: 'a' }, { type: 'text', text: 'b' }] })).toBe('a\nb');
    });

    it('serializes strings as they are and other values as JSON', () => {
        expect(serializeResult('plain')).toBe('plain');
        expect(serializeResult({ count: 2 })).toBe('{"count":2}');
        expect(serializeResult(undefined)).toBe('');
        expect(serializeResult(null)).toBe('');
    });

    it('recognizes error results', () => {
        expect(isErrorResult(textResult('x', true))).toBe(true);
        expect(isErrorResult(textResult('x'))).toBe(false);
        expect(isErrorResult('x')).toBe(false);
    });
});
