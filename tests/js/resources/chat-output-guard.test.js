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
    widget: read('src/Modules/CrestApps.OrchardCore.AI.Chat/wwwroot/scripts/ai-chat-widget.js'),
    // Orchard Core's built admin theme script (TheAdmin 4.0.0-preview-19225, taken from the package's embedded
    // wwwroot/js/theadmin/TheAdmin.js), whose click listeners act on data-url-af and the element's dataset.
    theAdmin: read('tests/js/resources/fixtures/TheAdmin-4.0.0-preview-19225.js'),
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
            invoke: function () { (window.__hubCalls = window.__hubCalls || []).push(Array.prototype.slice.call(arguments)); return Promise.resolve(); },
            send: function () { return Promise.resolve(); },
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

describe('the vendored ai-chat-widget.js with an answer that carries a widget configuration', () => {
    // The widget script starts a widget from any element added to the page with data-coreai-chat-widget-config, and
    // writes the configuration's icon markup into the toggle button with innerHTML.
    function plantedWidget() {
        const planted = {
            toggleButtonSelector: '#planted-toggle',
            widgetContainerSelector: '#planted-toggle',
            widgetStateName: 'planted',
            closedIconHtml: '<b class="widget-pwned"></b>',
            openIconHtml: '<b class="widget-pwned"></b>',
            chatConfig: { signalRHubUrl: '/hub', appElementSelector: '#planted-toggle', messageTemplate: payloadTemplate, widget: {} },
        };

        return `Here is the answer.\n\n<button id="planted-toggle"></button><div data-coreai-chat-widget-config='${attribute(planted)}'></div>\n`;
    }

    it('the planted widget configuration is a working exploit: placed in the page itself, the widget starts', async () => {
        // Proves the payload the other cases plant would run if a widget started from it, so their passing means something.
        const window = createPage(aiChatPage('Hello.') + plantedWidget().replace('Here is the answer.', ''));

        window.eval(libraries.aiChat);
        window.eval(libraries.widget);
        await flush(window);

        expect(window.document.querySelector('.widget-pwned')).not.toBeNull();
    });

    it('in an answer, is stripped by the package\'s own sanitizer even without the guard (CrestApps.Core #324)', async () => {
        // Since @crestapps/ai-chat-ui 2.0.0-preview.263 the answer sanitizer drops every data-coreai-* attribute and
        // id, so the guard is a second layer for the chat attributes and the only one for the admin theme's.
        const window = createPage(aiChatPage(plantedWidget()));

        window.eval(libraries.aiChat);
        window.eval(libraries.widget);
        await flush(window);

        expect(window.document.querySelector('.widget-pwned')).toBeNull();
        expect(window.document.querySelector('[data-coreai-chat-widget-config]')).toBeNull();
    });

    it('with only the watcher, starts no widget from an element added inside the mounted chat', async () => {
        const window = createPage(aiChatPage(plantedWidget()));

        watchRenderedOutput(window);
        window.eval(libraries.aiChat);
        window.eval(libraries.widget);
        await flush(window);

        expect(window.document.querySelector('#chat-app .message')).not.toBeNull();
        expect(window.document.querySelector('.widget-pwned')).toBeNull();
        expect(window.document.querySelector('#chat-app [data-coreai-chat-widget-config]')).toBeNull();
    });

    it('with the built guard loaded first, starts no widget', async () => {
        const window = createPage(aiChatPage(plantedWidget()));

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        window.eval(libraries.widget);
        await flush(window);

        expect(window.document.querySelector('.widget-pwned')).toBeNull();
    });
});

describe('an answer on an admin page that carries TheAdmin action attributes', () => {
    // HT4: TheAdmin builds and submits a POST form, with the page's antiforgery token, for a click on a link with
    // data-url-af="UnsafeUrl", and writes data-title into its confirm dialog with innerHTML for data-url-af="RemoveUrl".
    const adminAnswer = 'Here is the answer.\n\n'
        + '<a href="/Admin/Users/Delete?id=42&amp;returnUrl=%2F" data-url-af="UnsafeUrl" class="unsafe-link">Delete</a> '
        + '<a href="#" data-url-af="RemoveUrl" data-title="&lt;img src=x class=injected-title&gt;" data-message="Sure?" class="remove-link">Remove</a> '
        + '<a href="/Admin/Settings?x=1" itemprop="UnsafeUrl" class="itemprop-link">Settings</a>\n';

    function adminPage(answer) {
        const window = createPage(`<form><input name="__RequestVerificationToken" type="hidden" value="admin-token"></form>${aiChatPage(answer)}`);
        const submitted = [];

        window.matchMedia = () => ({ matches: false, addEventListener() {}, removeEventListener() {}, addListener() {}, removeListener() {} });
        window.ResizeObserver = class { observe() {} unobserve() {} disconnect() {} };
        window.bootstrap = {
            Modal: class {
                constructor(element) { this.element = element; }
                show() {}
                hide() {}
                static getOrCreateInstance(element) { return new this(element); }
                static getInstance(element) { return new this(element); }
            },
        };
        window.HTMLFormElement.prototype.submit = function () { submitted.push(this); };
        window.eval(libraries.theAdmin);

        // TheAdmin wires its listeners on DOMContentLoaded, which jsdom fires after the page is built unless it already has.
        if (window.document.readyState !== 'loading') {
            window.document.dispatchEvent(new window.Event('DOMContentLoaded'));
        }

        return { window, submitted };
    }

    function click(window, selector) {
        const link = window.document.querySelector(`#chat-app .message ${selector}`);

        expect(link).not.toBeNull();
        link.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));
    }

    it('the attributes are a working exploit without the guard', async () => {
        const { window, submitted } = adminPage(adminAnswer);

        window.eval(libraries.aiChat);
        await flush(window);

        click(window, '.unsafe-link');
        click(window, '.remove-link');

        expect(submitted).toHaveLength(1);
        expect(submitted[0].getAttribute('action')).toBe('/Admin/Users/Delete');
        expect(submitted[0].querySelector('input[name="__RequestVerificationToken"]').value).toBe('admin-token');
        expect(window.document.querySelector('#confirmRemoveModal .modal-title img.injected-title')).not.toBeNull();
    });

    it('with the built guard loaded first, the links keep no action attribute and a click does nothing', async () => {
        const { window, submitted } = adminPage(adminAnswer);

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        await flush(window);

        click(window, '.unsafe-link');
        click(window, '.remove-link');
        click(window, '.itemprop-link');

        expect(submitted).toHaveLength(0);
        expect(window.document.querySelector('#confirmRemoveModal')).toBeNull();
        expect(window.document.querySelector('#chat-app .message [data-url-af],#chat-app .message [data-title],#chat-app .message [itemprop]')).toBeNull();
        expect(window.document.querySelector('#chat-app .message .unsafe-link').getAttribute('href')).toBe('/Admin/Users/Delete?id=42&returnUrl=%2F');
    });

    it('with only the sanitizer hardened, drops every data attribute the chat markup does not need', async () => {
        const { window, submitted } = adminPage(adminAnswer.replace('Here is the answer.', '<span data-bs-toggle="modal" data-bs-target="#x" data-anything="1">x</span>'));

        hardenSanitizer(window.DOMPurify);
        window.eval(libraries.aiChat);
        await flush(window);

        click(window, '.unsafe-link');

        expect(submitted).toHaveLength(0);
        expect(window.document.querySelector('#chat-app .message [data-bs-toggle],#chat-app .message [data-bs-target],#chat-app .message [data-anything]')).toBeNull();
    });
});

describe('an answer that reaches the page\'s own form controls (F5 of the fix-verification review)', () => {
    // A label stretched invisibly over the whole page, tied by for to a control of the page: the next click anywhere
    // activates that control. DOMPurify keeps label, for and class by default, and the package's style check keeps this style.
    const overlay = '<label for="page-submit" class="overlay-submit" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; opacity: 0; z-index: 9999">x</label>'
        + '<label for="Roles_0__IsSelected" class="overlay-role">y</label>';

    // A form of the answer's own that posts off-site.
    const offSiteForm = '<form action="https://other.example/collect" method="post" class="off-site-form"><textarea name="secret">Paste your key</textarea>'
        + '<select name="choice"><option value="a">A</option></select><label>Agree</label><button class="off-site-submit">Send</button></form>';

    function formPage(answer) {
        const window = createPage('<form id="page-form" action="/Admin/Users/Edit/42" method="post">'
            + '<input type="checkbox" id="Roles_0__IsSelected" name="Roles[0].IsSelected"><button type="submit" id="page-submit">Save</button></form>'
            + aiChatPage(answer));
        const submitted = [];

        window.addEventListener('submit', event => {
            submitted.push(event.target);
            event.preventDefault();
        }, true);

        return { window, submitted };
    }

    function click(window, selector) {
        const element = window.document.querySelector(`#chat-app .message ${selector}`);

        expect(element).not.toBeNull();
        element.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));
    }

    it('the stretched label is stopped by the package\'s own sanitizer even without the guard (CrestApps.Core #332)', async () => {
        // Since @crestapps/ai-chat-ui 2.0.0-preview.265 the answer sanitizer drops for, and position, opacity and z-index
        // from a style, so the guard is a second layer here. The label itself stays; the guard removes it.
        const { window, submitted } = formPage(`Here is the answer.\n\n${overlay}\n`);

        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message.querySelector('[for]')).toBeNull();
        expect(message.querySelector('.overlay-submit').getAttribute('style') ?? '').not.toMatch(/position|opacity|z-index/);

        message.querySelectorAll('*').forEach(element => element.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true })));

        expect(submitted).toHaveLength(0);
        expect(window.document.getElementById('Roles_0__IsSelected').checked).toBe(false);
    });

    it('with the built guard loaded first, the answer keeps no label and no for, and the page\'s controls are untouched', async () => {
        const { window, submitted } = formPage(`Here is the answer.\n\n${overlay}\n`);

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message.textContent).toContain('Here is the answer.');
        expect(message.querySelector('label,[for]')).toBeNull();

        message.querySelectorAll('*').forEach(element => element.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true })));

        expect(submitted).toHaveLength(0);
        expect(window.document.getElementById('Roles_0__IsSelected').checked).toBe(false);
    });

    it('the off-site form survives without the guard, though the package drops its fields\' names (CrestApps.Core #332)', async () => {
        // The package's sanitizer keeps form elements, so the guard is the only layer that removes them.
        const { window } = formPage(`Here is the answer.\n\n${offSiteForm}\n`);

        window.eval(libraries.aiChat);
        await flush(window);

        expect(window.document.querySelector('#chat-app .message form.off-site-form textarea')).not.toBeNull();
        expect(window.document.querySelector('#chat-app .message [name]')).toBeNull();
    });

    it('with the built guard loaded first, the answer keeps no form, field or submit button, but keeps their text', async () => {
        const { window, submitted } = formPage(`Here is the answer.\n\n${offSiteForm}\n`);

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message.querySelector('form,textarea,select,option,label,[name],[action]')).toBeNull();
        expect(message.textContent).toContain('Paste your key');
        expect(message.querySelector('button.off-site-submit')?.getAttribute('type')).toBe('button');

        click(window, '.off-site-submit');

        expect(submitted).toHaveLength(0);
    });

    it('keeps the code block\'s copy button, the chart\'s download button and a task list\'s ticked boxes', async () => {
        const answer = 'Done:\n\n- [x] first\n- [ ] second\n\n```js\nconst a = 1;\n```\n';
        const { window } = formPage(answer);

        window.eval(libraries.builtGuard);
        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');
        const boxes = message.querySelectorAll('input[type="checkbox"]');

        expect(message.querySelector('button.ai-code-copy-btn[type="button"]')).not.toBeNull();
        expect(boxes).toHaveLength(2);
        expect(boxes[0].checked).toBe(true);
        expect(boxes[1].checked).toBe(false);

        const chart = window.DOMPurify.sanitize('<button type="button" class="btn download-chart-btn" data-chart-id="chat_chart_1">Download</button>');

        expect(chart).toContain('class="btn download-chart-btn"');
        expect(chart).toContain('data-chart-id="chat_chart_1"');
    });
});

describe('the vendored chat-interaction.js with an answer that forges its settings (F6 of the fix-verification review)', () => {
    // The settings script collects prompt templates from every .prompt-template-card in the document, falls back to
    // tool checkboxes found by name when none of the page's own is ticked, and queues a save for a click on any
    // .prompt-template-add-btn.
    const forgedAnswer = 'Here is the answer.\n\n<div class="prompt-template-card"><input type="checkbox" class="prompt-template-id-input" value="attacker-template" checked>'
        + '<input type="checkbox" class="prompt-template-parameters-input" value="{&quot;x&quot;:1}" checked>'
        + '<input type="checkbox" name="ChatInteraction.Tools[0].IsSelected" checked> <a href="#" class="prompt-template-add-btn forged-add">Show sources</a></div>\n';

    function settingsPage(answer) {
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

        return createPage(`<div class="tools">
                <input type="checkbox" class="capability-checkbox page-tool" data-save-group="toolNames" data-item-id="deleteContentItem" name="ChatInteraction.Tools[0].IsSelected">
                <input type="hidden" name="ChatInteraction.Tools[0].ItemId" value="deleteContentItem">
            </div>
            <div id="chat-messages"><div id="chat-placeholder"></div><div id="chat-app"></div></div>
            <textarea id="chat-input" data-interaction-id="interaction-1"></textarea><button id="chat-send"></button>
            <div data-chat-interaction-config='${attribute(config)}'></div>`);
    }

    const waitForSave = window => new Promise(resolve => window.setTimeout(resolve, 1200));

    function savedSettings(window) {
        return (window.__hubCalls || []).filter(call => call[0] === 'SaveSettings').map(call => call[2]);
    }

    it('is stopped by the package even without the guard: the forged link saves nothing (CrestApps.Core #332)', async () => {
        // Since @crestapps/ai-chat-ui 2.0.0-preview.265 the settings collectors skip everything inside a rendered message,
        // so the guard is a second layer here.
        const window = settingsPage(forgedAnswer);

        window.eval(libraries.chatInteraction);
        await flush(window);

        window.document.querySelector('#chat-app .message .forged-add')?.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));
        await waitForSave(window);

        expect(savedSettings(window)).toHaveLength(0);
    });

    it('with the built guard loaded first, the forged link saves nothing, and a save from the page carries neither', async () => {
        const window = settingsPage(forgedAnswer);

        window.eval(libraries.builtGuard);
        window.eval(libraries.chatInteraction);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message.textContent).toContain('Show sources');
        expect(message.querySelector('[name],[checked],.prompt-template-card,.prompt-template-id-input,.prompt-template-add-btn')).toBeNull();

        message.querySelector('.forged-add').dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));
        await waitForSave(window);

        expect(savedSettings(window)).toHaveLength(0);

        // The user later changes one of the page's own settings.
        window.document.querySelector('.page-tool').dispatchEvent(new window.Event('change', { bubbles: true }));
        await waitForSave(window);

        const saved = savedSettings(window);

        expect(saved).toHaveLength(1);
        expect(saved[0].toolNames).toEqual([]);
        expect(saved[0].promptTemplateIds).toBeUndefined();
    });
});

describe('an answer that carries an approval id (the AG-UI approval click)', () => {
    it('keeps the approval attributes only on a button of the chat\'s own approval box', () => {
        const window = createPage(aiChatPage('Hello.'));

        window.eval(libraries.builtGuard);

        const link = window.DOMPurify.sanitize('<a href="#" class="ai-chat-agui-approval" data-agui-interrupt="i1" data-agui-approved="true">Continue</a>');
        const loose = window.DOMPurify.sanitize('<button type="button" data-agui-interrupt="i1" data-agui-approved="true">Continue</button>');

        expect(link).not.toContain('data-agui-');
        expect(loose).not.toContain('data-agui-');
    });

    it('keeps the approval buttons the vendored ai-chat.js renders for a pending tool call, and drops a link\'s', async () => {
        const window = createPage(aiChatPage('Hello.'));

        window.eval(libraries.builtGuard);

        const page = window.document.querySelector('[data-coreai-chat-config]');
        const config = JSON.parse(page.getAttribute('data-coreai-chat-config'));

        config.messages = [{
            role: 'assistant',
            content: 'Before I go on: <a href="#" class="forged" data-agui-interrupt="i1" data-agui-approved="true">continue</a>',
            toolCalls: [{ id: 'c1', name: 'deleteContentItem', arguments: '{}', status: 'awaitingApproval', interruptId: 'i1' }],
        }];
        page.setAttribute('data-coreai-chat-config', JSON.stringify(config));

        window.eval(libraries.aiChat);
        await flush(window);

        const message = window.document.querySelector('#chat-app .message');

        expect(message.querySelectorAll('.ai-chat-agui-approval button[data-agui-interrupt="i1"][data-agui-approved]')).toHaveLength(2);
        expect(message.querySelector('a.forged').hasAttribute('data-agui-interrupt')).toBe(false);
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

    it('refuses every data attribute except the few the chat scripts write into their own markup', () => {
        expect(isAllowedAttribute('A', 'data-url-af', 'UnsafeUrl')).toBe(false);
        expect(isAllowedAttribute('A', 'data-title', '<img>')).toBe(false);
        expect(isAllowedAttribute('A', 'data-message', 'x')).toBe(false);
        expect(isAllowedAttribute('A', 'data-bs-toggle', 'modal')).toBe(false);
        expect(isAllowedAttribute('DIV', 'data-coreai-chat-widget-config', '{}')).toBe(false);
        expect(isAllowedAttribute('DIV', 'data-chart-id-extra', 'x')).toBe(false);

        for (const name of ['data-agui-tool-call', 'data-doc-index', 'data-error-index',
            'data-carousel-step', 'data-carousel-target', 'data-carousel-size-option', 'data-stack-offset']) {
            expect(isAllowedAttribute('BUTTON', name, '1')).toBe(true);
        }
    });

    it('refuses the attributes that tie an element to another control of the page or carry a form value', () => {
        for (const name of ['for', 'name', 'list', 'form', 'formaction', 'command', 'commandfor', 'popovertarget', 'popovertargetaction', 'FOR']) {
            expect(isAllowedAttribute('LABEL', name, 'page-submit')).toBe(false);
        }

        expect(isAllowedAttribute('BUTTON', 'type', 'button')).toBe(true);
        expect(isAllowedAttribute('A', 'class', 'btn')).toBe(true);
    });

    it('keeps checked only on a disabled checkbox, the way a task list renders, and the approval attributes only in the approval box', () => {
        const { document } = new JSDOM('<div class="ai-chat-agui-approval"><button id="approve"></button></div><div><button id="loose"></button></div>'
            + '<input type="checkbox" disabled id="task"><input type="checkbox" id="box">').window;

        expect(isAllowedAttribute('INPUT', 'checked', '', document.getElementById('task'))).toBe(true);
        expect(isAllowedAttribute('INPUT', 'checked', '', document.getElementById('box'))).toBe(false);
        expect(isAllowedAttribute('INPUT', 'checked', '')).toBe(false);

        for (const name of ['data-agui-interrupt', 'data-agui-approved']) {
            expect(isAllowedAttribute('BUTTON', name, '1', document.getElementById('approve'))).toBe(true);
            expect(isAllowedAttribute('BUTTON', name, '1', document.getElementById('loose'))).toBe(false);
            expect(isAllowedAttribute('BUTTON', name, '1')).toBe(false);
        }
    });
});

describe('the chat markup that goes through the sanitizer', () => {
    it('keeps the attributes the AG-UI approval buttons, the chart download button and the document bar act on', () => {
        const window = createPage(aiChatPage('Hello.'));

        window.eval(libraries.builtGuard);

        const activity = window.DOMPurify.sanitize('<div data-agui-tool-call="c1"><div class="ai-chat-agui-approval"><span>Allow?</span><button type="button" data-agui-interrupt="i1" data-agui-approved="true">Allow</button></div></div>');
        const chart = window.DOMPurify.sanitize('<canvas id="chat_chart_1"></canvas><button data-chart-id="chat_chart_1">Download</button>', { ADD_TAGS: ['canvas'] });
        const documents = window.DOMPurify.sanitize('<button data-doc-index="0"></button><button data-error-index="1"></button>');

        expect(activity).toContain('data-agui-tool-call="c1"');
        expect(activity).toContain('data-agui-interrupt="i1"');
        expect(activity).toContain('data-agui-approved="true"');
        expect(chart).toContain('id="chat_chart_1"');
        expect(chart).toContain('data-chart-id="chat_chart_1"');
        expect(documents).toContain('data-doc-index="0"');
        expect(documents).toContain('data-error-index="1"');
    });
});
