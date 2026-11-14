// @vitest-environment node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { JSDOM } from 'jsdom';
import { afterEach, describe, expect, it } from 'vitest';

import { hardenSanitizer, installChatOutputGuard, isAllowedAttribute, watchRenderedOutput } from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/chat-output-guard/guard.js';

// H5 of the final security review, reproduced with this repository's own vendored chat scripts, the Vue 3 build and
// DOMPurify, in jsdom: an assistant message carrying an element with a chat configuration made the chat script start a
// second chat whose messageTemplate Vue compiled, and a template expression ran as script.
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const read = relative => fs.readFileSync(path.join(repo, relative), 'utf8');

const libraries = {
    purify: read('node_modules/dompurify/dist/purify.js'),
    vue: read('node_modules/vue/dist/vue.global.js'),
    marked: read('node_modules/marked/lib/marked.umd.js'),
    markers: read('src/Modules/CrestApps.OrchardCore.Resources/wwwroot/vendors/crestapps/chat-markers.js'),
    carousel: read('src/Modules/CrestApps.OrchardCore.Resources/wwwroot/vendors/crestapps/chat-image-carousel.js'),
    aiChat: read('src/Modules/CrestApps.OrchardCore.AI.Chat/wwwroot/scripts/ai-chat.js'),
    chatInteraction: read('src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions/wwwroot/scripts/chat-interaction.js'),
    builtGuard: read('src/Modules/CrestApps.OrchardCore.Resources/wwwroot/scripts/chat-output-guard.js'),
};

// The template a planted configuration carries: rendering it runs code through the Function constructor.
const payloadTemplate = "<div>{{ ''.constructor.constructor('window.__pwned = (window.__pwned || 0) + 1')() }}</div>";

const pages = [];

afterEach(() => {
    while (pages.length > 0) {
        pages.pop().window.close();
    }
});

// A SignalR client that connects at once and never receives anything, so the chat renders its initial messages.
const signalRStub = `
window.signalR = {
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
    Subject: function () { this.next = function () {}; this.complete = function () {}; },
    HubConnectionBuilder: function () {
        var connection = {
            state: 'Connected',
            on: function () {}, off: function () {}, onclose: function () {}, onreconnecting: function () {}, onreconnected: function () {},
            start: function () { return Promise.resolve(); }, stop: function () { return Promise.resolve(); },
            invoke: function () { return Promise.resolve(); }, send: function () { return Promise.resolve(); },
            stream: function () { return { subscribe: function () { return { dispose: function () {} }; } }; },
        };
        this.withUrl = function () { return this; };
        this.withAutomaticReconnect = function () { return this; };
        this.configureLogging = function () { return this; };
        this.build = function () { return connection; };
    },
};
window.fetch = function () { return new Promise(function () {}); };
`;

function createPage(body) {
    const dom = new JSDOM(`<!doctype html><html><head></head><body>${body}</body></html>`, {
        runScripts: 'outside-only',
        pretendToBeVisual: true,
        url: 'https://site.example/',
    });

    pages.push(dom);

    const { window } = dom;

    window.console.error = () => {};
    window.console.warn = () => {};
    window.HTMLCanvasElement.prototype.getContext = () => null;
    window.eval(signalRStub);
    window.eval(libraries.purify);
    window.eval(libraries.vue);
    window.eval(libraries.marked);
    window.eval(libraries.markers);
    window.eval(libraries.carousel);

    return window;
}

const settle = window => new Promise(resolve => window.setTimeout(resolve, 50));

async function flush(window) {
    for (let i = 0; i < 6; i++) {
        await settle(window);
    }
}

function attribute(value) {
    return JSON.stringify(value).replace(/&/g, '&amp;').replace(/'/g, '&#39;');
}

// The answer: an element carrying a chat configuration, the element it mounts on, and a chart canvas the renderer may
// legitimately give an id.
function plantedAnswer(configAttribute) {
    const planted = {
        signalRHubUrl: '/hub',
        appElementSelector: '#planted-app',
        chatContainerElementSelector: '#planted-app',
        inputElementSelector: '#planted-app',
        sendButtonElementSelector: '#planted-app',
        messageTemplate: payloadTemplate,
    };

    return `Here is the answer.\n\n<div id="planted-app"></div><div ${configAttribute}='${attribute(planted)}'></div>\n`;
}

function aiChatPage(answer) {
    const config = {
        signalRHubUrl: '/hub',
        appElementSelector: '#chat-app',
        chatContainerElementSelector: '#chat-messages',
        inputElementSelector: '#chat-input',
        sendButtonElementSelector: '#chat-send',
        placeholderElementSelector: '#chat-placeholder',
        messageTemplate: '<div class="messages"><div v-for="(message, index) in messages" :key="index" class="message"><div v-html="message.htmlContent"></div></div></div>',
        messages: [{ role: 'assistant', content: answer }],
    };

    return `<div id="chat-messages"><div id="chat-placeholder"></div><div id="chat-app"></div></div>
        <textarea id="chat-input"></textarea><button id="chat-send"></button>
        <div data-coreai-chat-config='${attribute(config)}'></div>`;
}

describe('the vendored ai-chat.js with an answer that carries a chat configuration', () => {
    it('the planted configuration is a working exploit: placed in the page itself, its template runs', async () => {
        // Proves the payload the other cases plant would run if a chat started from it, so their passing means something.
        const window = createPage(aiChatPage('Hello.') + plantedAnswer('data-coreai-chat-config').replace('Here is the answer.', ''));

        installChatOutputGuard(window);
        window.eval(libraries.aiChat);
        await flush(window);

        expect(window.__pwned).toBeGreaterThan(0);
    });

    it('with the built guard loaded first, as the resource dependencies load it, starts nothing and compiles no template', async () => {
        const window = createPage(aiChatPage(plantedAnswer('data-coreai-chat-config')));

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message).not.toBeNull();
        expect(message.textContent).toContain('Here is the answer.');
        expect(window.__pwned).toBeUndefined();
        expect(message.querySelector('[data-coreai-chat-config],[data-coreai-chat-app-element-selector],[id]')).toBeNull();
    });

    it('with only the sanitizer hardened, strips the configuration and the id from the answer', async () => {
        const window = createPage(aiChatPage(plantedAnswer('data-coreai-chat-config')));

        hardenSanitizer(window.DOMPurify);
        window.eval(libraries.aiChat);
        await flush(window);

        expect(window.__pwned).toBeUndefined();
        expect(window.document.querySelector('#planted-app')).toBeNull();
    });

    it('with only the watcher, skips an element added inside the mounted chat, even when the sanitizer let it through', async () => {
        const window = createPage(aiChatPage(plantedAnswer('data-coreai-chat-config')));

        watchRenderedOutput(window);
        window.eval(libraries.aiChat);
        await flush(window);

        expect(window.document.querySelector('#chat-app .message')).not.toBeNull();
        expect(window.__pwned).toBeUndefined();
    });

    it('keeps the chat that the page itself configures working', async () => {
        const window = createPage(aiChatPage('Hello **there**.'));

        installChatOutputGuard(window);
        window.eval(libraries.aiChat);
        await flush(window);

        expect(window.document.querySelector('#chat-app .message strong')?.textContent).toBe('there');
    });
});

describe('the vendored chat-interaction.js with an answer that carries a chat configuration', () => {
    function interactionPage(answer) {
        const config = {
            signalRHubUrl: '/hub',
            appElementSelector: '#chat-app',
            chatContainerElementSelector: '#chat-messages',
            inputElementSelector: '#chat-input',
            sendButtonElementSelector: '#chat-send',
            placeholderElementSelector: '#chat-placeholder',
            messageTemplate: '<div class="messages"><div v-for="(message, index) in messages" :key="index" class="message"><div v-html="message.htmlContent"></div></div></div>',
            messages: [{ role: 'assistant', content: answer }],
        };

        return `<div id="chat-messages"><div id="chat-placeholder"></div><div id="chat-app"></div></div>
            <textarea id="chat-input"></textarea><button id="chat-send"></button>
            <div data-chat-interaction-config='${attribute(config)}'></div>`;
    }

    function plantedInteraction() {
        const planted = {
            signalRHubUrl: '/hub',
            appElementSelector: '#planted-app',
            chatContainerElementSelector: '#planted-app',
            inputElementSelector: '#planted-app',
            sendButtonElementSelector: '#planted-app',
            messageTemplate: payloadTemplate,
        };

        return `Here is the answer.\n\n<div id="planted-app"></div><div data-chat-interaction-config='${attribute(planted)}'></div>\n`;
    }

    it('with the built guard loaded first, starts nothing and compiles no template', async () => {
        const window = createPage(interactionPage(plantedInteraction()));

        window.eval(libraries.builtGuard);
        window.eval(libraries.chatInteraction);
        await flush(window);

        expect(window.__pwned).toBeUndefined();
        expect(window.document.querySelector('#chat-app [data-chat-interaction-config]')).toBeNull();
    });
});

describe('isAllowedAttribute', () => {
    it('refuses every chat configuration attribute and any id but a chart canvas one', () => {
        expect(isAllowedAttribute('DIV', 'data-coreai-chat-config', '{}')).toBe(false);
        expect(isAllowedAttribute('DIV', 'data-coreai-chat-app-element-selector', '#x')).toBe(false);
        expect(isAllowedAttribute('DIV', 'data-chat-interaction-config', '{}')).toBe(false);
        expect(isAllowedAttribute('DIV', 'DATA-COREAI-CHAT-CONFIG', '{}')).toBe(false);
        expect(isAllowedAttribute('DIV', 'id', 'planted-app')).toBe(false);
        expect(isAllowedAttribute('CANVAS', 'id', 'planted-app')).toBe(false);
        expect(isAllowedAttribute('CANVAS', 'id', 'chat_chart_3')).toBe(true);
        expect(isAllowedAttribute('A', 'href', 'https://example.com')).toBe(true);
        expect(isAllowedAttribute('BUTTON', 'data-chart-id', 'chat_chart_3')).toBe(true);
    });
});
