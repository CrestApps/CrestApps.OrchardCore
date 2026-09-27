import { beforeEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/ivr-menu/ivr-flow-model.js';

const ivr = globalThis.CrestAppsIvrMenu;
const editorScript = '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/ivr-menu/ivr-menu-editor.js';

// Just enough of the DOM for the IVR menu editor: the element tree, attributes (with hidden and className reflected
// the way the browser reflects them), classList, bubbling clicks, focus, and the attribute selectors the editor uses:
// [name], [name="value"], chained, and comma-separated.
class FakeElement {
    constructor(document, tag, attributes = {}) {
        this.ownerDocument = document;
        this.tagName = tag.toUpperCase();
        this.attributes = new Map(Object.entries(attributes));
        this.children = [];
        this.parentElement = null;
        this.listeners = {};
        this.value = '';
        this.selected = false;
        this.classList = {
            add: (...names) => { this.className = [...new Set([...this.classNames(), ...names])].join(' '); },
            remove: (...names) => { this.className = this.classNames().filter(name => !names.includes(name)).join(' '); },
            contains: name => this.classNames().includes(name),
        };
    }

    get hidden() { return this.attributes.has('hidden'); }
    set hidden(value) { if (value) { this.attributes.set('hidden', ''); } else { this.attributes.delete('hidden'); } }
    get className() { return this.attributes.get('class') ?? ''; }
    set className(value) { this.attributes.set('class', value); }
    get textContent() { return this.children.map(child => child.textContent).join(''); }
    set textContent(value) { this.replaceChildren(new FakeText(String(value))); }

    classNames() { return this.className.split(/\s+/).filter(Boolean); }
    getAttribute(name) { return this.attributes.has(name) ? this.attributes.get(name) : null; }
    setAttribute(name, value) { this.attributes.set(name, String(value)); }
    hasAttribute(name) { return this.attributes.has(name); }
    removeAttribute(name) { this.attributes.delete(name); }

    appendChild(child) {
        child.parentElement = this;
        this.children.push(child);

        return child;
    }

    append(...children) { children.forEach(child => this.appendChild(child)); return this; }
    replaceChildren(...children) { this.children = []; this.append(...children); }
    contains(node) { for (let current = node; current; current = current.parentElement) { if (current === this) { return true; } } return false; }
    elements() { return this.children.filter(child => child instanceof FakeElement).flatMap(child => [child, ...child.elements()]); }
    matches(selector) { return selector.split(',').some(compound => matchesCompound(this, compound.trim())); }
    closest(selector) { for (let node = this; node; node = node.parentElement) { if (node.matches(selector)) { return node; } } return null; }
    querySelectorAll(selector) { return this.elements().filter(node => node.matches(selector)); }
    querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
    addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
    focus() { this.ownerDocument.activeElement = this; }
    select() { this.selected = true; }

    click() {
        const event = { type: 'click', target: this, preventDefault() {} };

        for (let node = this; node; node = node.parentElement) {
            (node.listeners.click ?? []).forEach(listener => listener(event));
        }
    }
}

class FakeText {
    constructor(text) {
        this.textContent = text;
        this.parentElement = null;
    }
}

function matchesCompound(element, compound) {
    const parts = compound.match(/\[[^\]]+\]/g);

    if (!parts || parts.join('') !== compound) {
        throw new Error(`The fake DOM does not know the selector ${compound}`);
    }

    return parts.every(part => {
        const [, name, value] = part.match(/^\[([\w-]+)(?:="([^"]*)")?\]$/);

        return value === undefined ? element.hasAttribute(name) : element.getAttribute(name) === value;
    });
}

function fakeDocument() {
    const document = {
        readyState: 'complete',
        activeElement: null,
        createElement: tag => new FakeElement(document, tag),
        createTextNode: text => new FakeText(text),
        addEventListener() {},
        querySelectorAll: selector => document.body.querySelectorAll(selector),
    };

    document.body = new FakeElement(document, 'body');
    document.activeElement = document.body;

    return document;
}

// A menu with a single key, which sends the caller to voicemail.
const menuJson = JSON.stringify({
    RootNodeId: 'main',
    MaxRetries: 3,
    FallbackAction: null,
    Nodes: [{ NodeId: 'main', Prompt: 'Press 1 to leave a message.', PromptMediaId: null, Options: [{ Digit: '1', Action: { Kind: 'Voicemail', TargetId: null } }] }],
}, null, 2);

// The entry point's Menu card, as ContactCenterEntryPointMenu.Edit.cshtml renders it, with the editor started on it.
async function openEditor({ json = menuJson, navigator = {} } = {}) {
    const document = fakeDocument();
    const el = (tag, attributes, ...children) => new FakeElement(document, tag, attributes).append(...children);

    const parts = {
        toggle: el('button', { 'data-ivr-advanced-toggle': '', hidden: '' }),
        message: el('div', { 'data-ivr-message': '', hidden: '' }),
        visual: el('div', { 'data-ivr-visual': '', hidden: '' }),
        copy: el('button', { 'data-ivr-json-copy': '' }),
        format: el('button', { 'data-ivr-json-format': '' }),
        textarea: el('textarea', { 'data-ivr-json': '' }),
        status: el('div', { 'data-ivr-json-status': '', hidden: '' }),
        apply: el('button', { 'data-ivr-json-apply': '' }),
        cancel: el('button', { 'data-ivr-json-cancel': '' }),
    };

    parts.textarea.value = json;
    parts.tools = el('div', { 'data-ivr-json-tools': '', hidden: '' }, parts.copy, parts.format);
    parts.actions = el('div', { 'data-ivr-json-actions': '', hidden: '' }, parts.apply, parts.cancel);
    parts.panel = el('div', { 'data-ivr-json-panel': '' }, parts.tools, parts.textarea, parts.status, parts.actions);
    parts.root = el('div', { 'data-ivr-menu-editor': '', 'data-ivr-loading': '', 'data-config': '{}' }, parts.toggle, parts.message, parts.visual, parts.panel);
    document.body.appendChild(parts.root);

    globalThis.document = document;
    globalThis.window = { CrestAppsIvrMenu: ivr, navigator };

    // The editor starts itself on load, so each test loads it afresh over its own page.
    vi.resetModules();
    await import(editorScript);

    return parts;
}

const menuAsTheFormsHoldIt = json => ivr.toJson(ivr.parseJson(json).model);

// What the forms show callers hear on the first menu.
const promptShown = editor => editor.visual.querySelector('[data-ivr-field="node:0:prompt"]')?.textContent;

describe('the IVR menu editor, edited as JSON', () => {
    beforeEach(() => {
        delete globalThis.document;
        delete globalThis.window;
    });

    it('starts in the forms, with the JSON put away', async () => {
        const editor = await openEditor();

        expect(editor.visual.hidden).toBe(false);
        expect(editor.panel.hidden).toBe(true);
        expect(editor.toggle.hidden).toBe(false);
        expect(editor.root.hasAttribute('data-ivr-loading')).toBe(false);
        expect(promptShown(editor)).toBe('Press 1 to leave a message.');
    });

    // JSON that does not parse must never replace the menu: the problem is shown beside the JSON, and the menu the
    // forms held is still the one Cancel returns to.
    it('Apply with JSON that does not parse shows why and leaves the menu as it was', async () => {
        const editor = await openEditor();
        editor.toggle.click();
        editor.textarea.value = '{ "RootNodeId": "main", ';

        editor.apply.click();

        expect(editor.status.hidden).toBe(false);
        expect(editor.status.textContent).toContain('The JSON cannot be shown in the visual editor until it is fixed or cleared');
        expect(editor.panel.hidden).toBe(false);
        expect(editor.visual.hidden).toBe(true);

        editor.cancel.click();

        expect(editor.textarea.value).toBe(menuAsTheFormsHoldIt(menuJson));
        expect(promptShown(editor)).toBe('Press 1 to leave a message.');
    });

    it('Apply with a menu that passes every check replaces the menu and goes back to the forms', async () => {
        const editor = await openEditor();
        const edited = menuJson.replace('Press 1 to leave a message.', 'Press 1 for voicemail.');
        editor.toggle.click();
        editor.textarea.value = edited;

        editor.apply.click();

        expect(editor.panel.hidden).toBe(true);
        expect(editor.visual.hidden).toBe(false);
        expect(editor.textarea.value).toBe(menuAsTheFormsHoldIt(edited));
        expect(promptShown(editor)).toBe('Press 1 for voicemail.');
    });

    it('Cancel throws the typed JSON away, writes the menu back and returns to the forms', async () => {
        const editor = await openEditor();
        editor.toggle.click();
        editor.textarea.value = menuJson.replace('Press 1 to leave a message.', 'Typed but never applied.');

        editor.cancel.click();

        expect(editor.textarea.value).toBe(menuAsTheFormsHoldIt(menuJson));
        expect(editor.panel.hidden).toBe(true);
        expect(editor.tools.hidden).toBe(true);
        expect(editor.actions.hidden).toBe(true);
        expect(editor.visual.hidden).toBe(false);
        expect(editor.toggle.hidden).toBe(false);
        expect(promptShown(editor)).toBe('Press 1 to leave a message.');
    });

    // Copy needs the asynchronous clipboard, which a page served over plain HTTP does not get: the text is selected
    // instead, so the operator can copy it themselves.
    it('Copy without clipboard support selects the text and says to copy it by hand', async () => {
        const editor = await openEditor({ navigator: {} });
        editor.toggle.click();

        editor.copy.click();

        expect(editor.textarea.selected).toBe(true);
        expect(editor.status.hidden).toBe(false);
        expect(editor.status.textContent).toBe('The browser did not allow copying. Select the text and copy it instead.');
    });

    it('Copy with clipboard support copies the JSON and says so', async () => {
        const copied = [];
        const editor = await openEditor({ navigator: { clipboard: { writeText: text => { copied.push(text); return Promise.resolve(); } } } });
        editor.toggle.click();

        editor.copy.click();
        await Promise.resolve();

        expect(copied).toEqual([editor.textarea.value]);
        expect(editor.status.textContent).toBe('Copied to the clipboard.');
    });

    it('Format lays the JSON out indented, and leaves JSON that does not parse as typed', async () => {
        const editor = await openEditor();
        editor.toggle.click();
        editor.textarea.value = '{"RootNodeId":"main","Nodes":[{"NodeId":"main"}]}';

        editor.format.click();

        expect(editor.textarea.value).toBe(JSON.stringify({ RootNodeId: 'main', Nodes: [{ NodeId: 'main' }] }, null, 2));

        editor.textarea.value = '{ not json';
        editor.format.click();

        expect(editor.textarea.value).toBe('{ not json');
    });
});
