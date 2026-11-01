import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp-page-tools.js';
import { isErrorResult, serializeResult } from '../../../src/Modules/CrestApps.OrchardCore.AI.WebMcp/Assets/js/webmcp-results.js';

const pageTools = globalThis.CrestAppsWebMcpPageTools;
const selectors = pageTools.selectors;

function element(tagName, props) {
    const attributes = Object.assign({}, props && props.attributes);
    const events = [];

    return Object.assign({
        tagName: tagName.toUpperCase(),
        textContent: '',
        type: '',
        name: '',
        id: '',
        value: '',
        checked: false,
        disabled: false,
        required: false,
        labels: [],
        options: [],
        events,
        getAttribute: (name) => (name in attributes ? attributes[name] : null),
        dispatchEvent: (event) => {
            events.push(event.type);

            return true;
        },
    }, props);
}

function fakeDocument({ title = 'Page', description = null, headings = [], links = [], fields = [] } = {}) {
    const meta = description === null ? null : element('meta', { attributes: { content: description } });

    return {
        title,
        querySelector: (selector) => (selector === selectors.description ? meta : null),
        querySelectorAll: (selector) => {
            switch (selector) {
                case selectors.headings:
                    return headings;
                case selectors.links:
                    return links;
                case selectors.fields:
                    return fields;
                default:
                    return [];
            }
        },
    };
}

const location = {
    href: 'https://example.com/Admin/Contents',
    origin: 'https://example.com',
    assign: vi.fn(),
};

function tools(doc, isAdmin = true) {
    const list = pageTools.createPageTools({ doc, loc: location, isAdmin });
    const byName = {};

    list.forEach((tool) => {
        byName[tool.name] = tool;
    });

    return byName;
}

async function run(tool, input) {
    const result = await tool.execute(input || {}, { signal: new AbortController().signal });

    return { result, data: isErrorResult(result) ? null : JSON.parse(serializeResult(result)) };
}

describe('createPageTools', () => {
    it('offers the five page tools, marking the ones that only read', () => {
        const list = pageTools.createPageTools({ doc: fakeDocument(), loc: location, isAdmin: true });

        expect(list.map((tool) => tool.name)).toEqual([
            'page_get_info',
            'page_list_links',
            'page_navigate',
            'page_list_form_fields',
            'page_set_form_field',
        ]);
        expect(list.filter((tool) => tool.annotations && tool.annotations.readOnlyHint).map((tool) => tool.name)).toEqual([
            'page_get_info',
            'page_list_links',
            'page_list_form_fields',
        ]);
        list.forEach((tool) => {
            expect(tool.description.length).toBeGreaterThan(10);
            expect(tool.inputSchema.type).toBe('object');
        });
    });
});

describe('page_get_info', () => {
    it('describes the page', async () => {
        const doc = fakeDocument({
            title: 'Content Items',
            description: 'All the content.',
            headings: [
                element('h1', { textContent: '  Content   Items ' }),
                element('h2', { textContent: 'Filters' }),
                element('h2', { textContent: '   ' }),
            ],
        });

        const { data } = await run(tools(doc).page_get_info);

        expect(data).toEqual({
            title: 'Content Items',
            url: 'https://example.com/Admin/Contents',
            description: 'All the content.',
            headings: [{ level: 1, text: 'Content Items' }, { level: 2, text: 'Filters' }],
            isAdmin: true,
        });
    });
});

describe('page_list_links', () => {
    const doc = fakeDocument({
        links: [
            element('a', { textContent: 'Content', attributes: { href: '/Admin/Contents' } }),
            element('a', { textContent: 'Users', attributes: { href: 'https://example.com/Admin/Users' } }),
            element('a', { textContent: 'Content again', attributes: { href: '/Admin/Contents' } }),
            element('a', { textContent: 'Elsewhere', attributes: { href: 'https://other.example/' } }),
            element('a', { textContent: 'Script', attributes: { href: 'javascript:alert(1)' } }),
            element('a', { textContent: 'Empty', attributes: { href: '' } }),
        ],
    });

    it('lists each same-origin link once', async () => {
        const { data } = await run(tools(doc).page_list_links);

        expect(data.links).toEqual([
            { text: 'Content', url: 'https://example.com/Admin/Contents' },
            { text: 'Users', url: 'https://example.com/Admin/Users' },
        ]);
    });

    it('filters the links by their text', async () => {
        const { data } = await run(tools(doc).page_list_links, { filter: 'user' });

        expect(data.links).toEqual([{ text: 'Users', url: 'https://example.com/Admin/Users' }]);
    });
});

describe('page_navigate', () => {
    it('opens a same-origin address', async () => {
        location.assign.mockClear();

        const { data } = await run(tools(fakeDocument()).page_navigate, { url: '/Admin/Users' });

        expect(location.assign).toHaveBeenCalledWith('https://example.com/Admin/Users');
        expect(data).toEqual({ navigatedTo: 'https://example.com/Admin/Users' });
    });

    it('refuses other origins and script addresses', async () => {
        location.assign.mockClear();

        const other = await run(tools(fakeDocument()).page_navigate, { url: 'https://other.example/' });
        const script = await run(tools(fakeDocument()).page_navigate, { url: 'javascript:alert(1)' });
        const missing = await run(tools(fakeDocument()).page_navigate, {});

        expect(isErrorResult(other.result)).toBe(true);
        expect(isErrorResult(script.result)).toBe(true);
        expect(isErrorResult(missing.result)).toBe(true);
        expect(location.assign).not.toHaveBeenCalled();
    });
});

function formFields() {
    return {
        title: element('input', { type: 'text', name: 'TitlePart.Title', id: 'TitlePart_Title', value: 'Hello', required: true, labels: [{ textContent: 'Title' }] }),
        body: element('textarea', { name: 'BodyPart.Body', id: 'BodyPart_Body', value: 'x'.repeat(3000), attributes: { 'aria-label': 'Body' } }),
        published: element('input', { type: 'checkbox', name: 'Published', checked: false, attributes: { placeholder: 'ignored' }, labels: [{ textContent: 'Published' }] }),
        status: element('select', {
            name: 'Status',
            value: 'draft',
            labels: [{ textContent: 'Status' }],
            options: [{ value: 'draft', text: 'Draft', selected: true }, { value: 'live', text: 'Live', selected: false }],
        }),
        token: element('input', { type: 'hidden', name: '__RequestVerificationToken', value: 'secret' }),
        password: element('input', { type: 'password', name: 'Password', value: 'secret' }),
        file: element('input', { type: 'file', name: 'Upload' }),
        submit: element('input', { type: 'submit', name: 'submit.Save' }),
        disabled: element('input', { type: 'text', name: 'Locked', disabled: true }),
    };
}

describe('page_list_form_fields', () => {
    it('lists the editable fields and leaves out secrets, files, buttons and disabled fields', async () => {
        const fields = formFields();
        const doc = fakeDocument({ fields: Object.values(fields) });

        const { data } = await run(tools(doc).page_list_form_fields);

        expect(data.fields.map((field) => field.name)).toEqual(['TitlePart.Title', 'BodyPart.Body', 'Published', 'Status']);
        expect(data.fields[0]).toEqual({
            key: 'TitlePart.Title',
            name: 'TitlePart.Title',
            id: 'TitlePart_Title',
            label: 'Title',
            type: 'text',
            value: 'Hello',
            required: true,
        });
        expect(data.fields[1].label).toBe('Body');
        expect(data.fields[1].type).toBe('textarea');
        expect(data.fields[1].value.length).toBe(2000);
        expect(data.fields[2]).toMatchObject({ type: 'checkbox', value: false });
        expect(data.fields[3]).toMatchObject({
            type: 'select',
            value: 'draft',
            options: [{ value: 'draft', text: 'Draft', selected: true }, { value: 'live', text: 'Live', selected: false }],
        });
    });
});

describe('page_set_form_field', () => {
    it('sets a field found by name and raises input and change', async () => {
        const fields = formFields();
        const doc = fakeDocument({ fields: Object.values(fields) });

        const { data } = await run(tools(doc).page_set_form_field, { field: 'TitlePart.Title', value: 'New title' });

        expect(fields.title.value).toBe('New title');
        expect(fields.title.events).toEqual(['input', 'change']);
        expect(data.field.value).toBe('New title');
    });

    it('finds a field by id or by its label', async () => {
        const fields = formFields();
        const doc = fakeDocument({ fields: Object.values(fields) });

        await run(tools(doc).page_set_form_field, { field: 'BodyPart_Body', value: 'Body text' });
        await run(tools(doc).page_set_form_field, { field: 'published', value: true });

        expect(fields.body.value).toBe('Body text');
        expect(fields.published.checked).toBe(true);
    });

    it('picks a choice by its value or its text', async () => {
        const fields = formFields();
        const doc = fakeDocument({ fields: Object.values(fields) });

        await run(tools(doc).page_set_form_field, { field: 'Status', value: 'Live' });

        expect(fields.status.value).toBe('live');

        const missing = await run(tools(doc).page_set_form_field, { field: 'Status', value: 'Archived' });

        expect(isErrorResult(missing.result)).toBe(true);
    });

    it('refuses fields it does not list', async () => {
        const fields = formFields();
        const doc = fakeDocument({ fields: Object.values(fields) });

        const token = await run(tools(doc).page_set_form_field, { field: '__RequestVerificationToken', value: 'x' });
        const password = await run(tools(doc).page_set_form_field, { field: 'Password', value: 'x' });
        const unknown = await run(tools(doc).page_set_form_field, { field: 'Nope', value: 'x' });

        expect(isErrorResult(token.result)).toBe(true);
        expect(isErrorResult(password.result)).toBe(true);
        expect(isErrorResult(unknown.result)).toBe(true);
        expect(fields.token.value).toBe('secret');
        expect(fields.password.value).toBe('secret');
    });
});

describe('register', () => {
    it('registers every page tool with the page\'s list', () => {
        const api = { registerTool: vi.fn() };

        pageTools.register(api, { doc: fakeDocument(), loc: location, isAdmin: false });

        expect(api.registerTool).toHaveBeenCalledTimes(5);
    });
});
