import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { escapeHtml } from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/browser/html.js';
import { format } from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/browser/text.js';
import { HttpError, request } from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/browser/http.js';
import { bindForms, confirmAction } from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/browser/confirm.js';

const toolkit = '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/browser/index.js';

// The one set of browser helpers every module uses (crestapps-browser, exposed as window.crestapps), instead of a copy
// per module that each behaved a little differently.
describe('crestapps.html.escape', () => {
    it('escapes all five characters, so the result is safe in an attribute as well as in text', () => {
        expect(escapeHtml(`<b class="x">Tom & Jerry's</b>`)).toBe('&lt;b class=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/b&gt;');
    });

    it('turns nothing into an empty string and other values into their text', () => {
        expect(escapeHtml(null)).toBe('');
        expect(escapeHtml(undefined)).toBe('');
        expect(escapeHtml(0)).toBe('0');
    });
});

describe('crestapps.text.format', () => {
    it('fills numbered placeholders in any order, as often as they appear', () => {
        expect(format('{1} of {0}, {1} again', 'ten', 'three')).toBe('three of ten, three again');
    });

    it('leaves a placeholder with no value as it is, and reads nothing as an empty template', () => {
        expect(format('{0} and {1}', 'one')).toBe('one and {1}');
        expect(format(null, 'x')).toBe('');
    });
});

// A page reads the antiforgery token from the hidden input the form tag helper renders, or from a meta tag.
function pageWithToken({ input, meta } = {}) {
    return {
        querySelector: selector => {
            if (selector === 'input[name="__RequestVerificationToken"]') {
                return input === undefined ? null : { value: input };
            }

            if (selector === 'meta[name="RequestVerificationToken"]') {
                return meta === undefined ? null : { getAttribute: name => (name === 'content' ? meta : null) };
            }

            return null;
        },
    };
}

function response(status, headers = {}) {
    return {
        ok: status >= 200 && status < 300,
        status,
        statusText: `Status ${status}`,
        headers: { get: name => headers[name.toLowerCase()] ?? null },
    };
}

describe('crestapps.http.request', () => {
    beforeEach(() => {
        globalThis.fetch = vi.fn(async () => response(200));
    });

    afterEach(() => {
        delete globalThis.fetch;
        delete globalThis.document;
    });

    it('sends the antiforgery token from the page with a post', async () => {
        globalThis.document = pageWithToken({ input: 'token-from-form' });

        await request('/save', { method: 'POST', json: { a: 1 } });

        const [url, init] = globalThis.fetch.mock.calls[0];

        expect(url).toBe('/save');
        expect(init.method).toBe('POST');
        expect(init.credentials).toBe('same-origin');
        expect(init.headers.RequestVerificationToken).toBe('token-from-form');
        expect(init.headers['Content-Type']).toBe('application/json');
        expect(init.body).toBe('{"a":1}');
    });

    it('falls back to the meta tag when the page has no form token', async () => {
        globalThis.document = pageWithToken({ meta: 'token-from-meta' });

        await request('/remove', { method: 'POST' });

        expect(globalThis.fetch.mock.calls[0][1].headers.RequestVerificationToken).toBe('token-from-meta');
    });

    it('sends no token with a get', async () => {
        globalThis.document = pageWithToken({ input: 'token-from-form' });

        await request('/list');

        expect(globalThis.fetch.mock.calls[0][1].method).toBe('GET');
        expect(globalThis.fetch.mock.calls[0][1].headers.RequestVerificationToken).toBeUndefined();
    });

    it('posts a plain object as a form', async () => {
        globalThis.document = pageWithToken();

        await request('/save', { method: 'POST', form: { name: 'A & B', id: 7 } });

        const init = globalThis.fetch.mock.calls[0][1];

        expect(String(init.body)).toBe('name=A+%26+B&id=7');
        expect(init.headers['Content-Type']).toBeUndefined();
    });

    it('returns the response when it succeeds', async () => {
        globalThis.document = pageWithToken();
        const ok = response(204);
        globalThis.fetch = vi.fn(async () => ok);

        await expect(request('/x', { method: 'DELETE' })).resolves.toBe(ok);
    });

    it('throws a typed error on a status outside 2xx, with the seconds to wait from Retry-After', async () => {
        globalThis.document = pageWithToken();
        const limited = response(429, { 'retry-after': '30' });
        globalThis.fetch = vi.fn(async () => limited);

        const error = await request('/pay', { method: 'POST' }).catch(caught => caught);

        expect(error).toBeInstanceOf(HttpError);
        expect(error).toBeInstanceOf(Error);
        expect(error.status).toBe(429);
        expect(error.retryAfter).toBe(30);
        expect(error.response).toBe(limited);
    });

    it('reads a Retry-After date as the seconds until then, and no header as null', async () => {
        globalThis.document = pageWithToken();
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-11-11T10:00:00Z'));
        globalThis.fetch = vi.fn(async () => response(503, { 'retry-after': 'Wed, 11 Nov 2026 10:01:00 GMT' }));

        expect((await request('/a').catch(caught => caught)).retryAfter).toBe(60);

        globalThis.fetch = vi.fn(async () => response(500));

        expect((await request('/a').catch(caught => caught)).retryAfter).toBeNull();
        vi.useRealTimers();
    });
});

describe('crestapps.confirm', () => {
    afterEach(() => {
        delete globalThis.window;
    });

    it('asks through the admin theme\'s dialog when the page has it', async () => {
        const confirmDialog = vi.fn(options => options.callback(true));
        globalThis.window = { confirmDialog, confirm: vi.fn() };

        await expect(confirmAction('Refund', 'Refund $10?')).resolves.toBe(true);
        expect(confirmDialog).toHaveBeenCalledWith(expect.objectContaining({ title: 'Refund', message: 'Refund $10?' }));
        expect(globalThis.window.confirm).not.toHaveBeenCalled();
    });

    // The admin dialog writes its title and message into the page as markup.
    it('hands the admin dialog a question that names a record as text', async () => {
        const confirmDialog = vi.fn(options => options.callback(false));
        globalThis.window = { confirmDialog };

        await expect(confirmAction('Stop <b>Gold</b>', 'Stop "Gold" & co?')).resolves.toBe(false);
        expect(confirmDialog).toHaveBeenCalledWith(expect.objectContaining({
            title: 'Stop &lt;b&gt;Gold&lt;/b&gt;',
            message: 'Stop &quot;Gold&quot; &amp; co?',
        }));
    });

    it('falls back to the browser\'s confirm elsewhere', async () => {
        globalThis.window = { confirm: vi.fn(() => false) };

        await expect(confirmAction('Cancel', 'Cancel this order?')).resolves.toBe(false);
        expect(globalThis.window.confirm).toHaveBeenCalledWith('Cancel this order?');
    });
});

// Just enough of a page for the delegated submit listener: forms with data attributes and submit buttons.
function fakeForm(dataset, buttons = 1) {
    const form = {
        dataset,
        submitted: [],
        matches: selector => (selector === 'form[data-confirm]' && 'confirm' in dataset) || (selector === 'form[data-once]' && 'once' in dataset),
        closest(selector) { return this.matches(selector) ? this : null; },
        requestSubmit(submitter) { this.submitted.push(submitter); },
        buttons: Array.from({ length: buttons }, () => ({ disabled: false, type: 'submit' })),
        querySelectorAll(selector) { return selector === 'button[type="submit"], input[type="submit"], button:not([type])' ? this.buttons : []; },
    };

    return form;
}

function fakePage() {
    const listeners = [];

    return {
        addEventListener: (type, listener, capture) => listeners.push({ type, listener, capture }),
        listeners,
        submit(form, submitter) {
            const event = {
                type: 'submit',
                target: form,
                submitter,
                defaultPrevented: false,
                propagationStopped: false,
                preventDefault() { this.defaultPrevented = true; },
                stopPropagation() { this.propagationStopped = true; },
            };

            listeners.filter(entry => entry.type === 'submit').forEach(entry => entry.listener(event));

            return event;
        },
    };
}

describe('the [data-confirm] and [data-once] form bindings', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
        delete globalThis.window;
    });

    it('asks before a [data-confirm] form submits, and submits it with the same button once confirmed', async () => {
        globalThis.window = { confirm: vi.fn(() => true) };
        const page = fakePage();
        const form = fakeForm({ confirm: 'Remove this address?' });
        const button = form.buttons[0];

        bindForms(page);

        const asked = page.submit(form, button);

        expect(asked.defaultPrevented).toBe(true);
        await vi.runAllTimersAsync();

        expect(globalThis.window.confirm).toHaveBeenCalledWith('Remove this address?');
        expect(form.submitted).toEqual([button]);

        // The second submit is the confirmed one, and goes through.
        const sent = page.submit(form, button);

        expect(sent.defaultPrevented).toBe(false);

        // The listener runs first (capture) and keeps the unconfirmed submit from the admin theme's no-multisubmit, which
        // would otherwise mark the form as submitting and drop the confirmed submit that follows.
        expect(page.listeners.find(entry => entry.type === 'submit').capture).toBe(true);
        expect(asked.propagationStopped).toBe(true);
        expect(sent.propagationStopped).toBe(false);
    });

    // A button with its own formaction (or one that submits another form through form="...") asks its own question, and
    // the form then goes where that button sends it.
    it('asks the question of a [data-confirm] submit button, and submits with that button', async () => {
        globalThis.window = { confirm: vi.fn(() => true) };
        const page = fakePage();
        const form = fakeForm({}, 2);
        const [save, receiveAll] = form.buttons;
        receiveAll.dataset = { confirm: 'Receive everything?' };
        receiveAll.matches = selector => selector === '[data-confirm]';

        bindForms(page);

        expect(page.submit(form, save).defaultPrevented).toBe(false);
        expect(page.submit(form, receiveAll).defaultPrevented).toBe(true);
        await vi.runAllTimersAsync();

        expect(globalThis.window.confirm).toHaveBeenCalledTimes(1);
        expect(globalThis.window.confirm).toHaveBeenCalledWith('Receive everything?');
        expect(form.submitted).toEqual([receiveAll]);
        expect(page.submit(form, receiveAll).defaultPrevented).toBe(false);
    });

    it('does not submit when the question is declined', async () => {
        globalThis.window = { confirm: vi.fn(() => false) };
        const page = fakePage();
        const form = fakeForm({ confirm: 'Cancel?' });

        bindForms(page);
        page.submit(form, form.buttons[0]);
        await vi.runAllTimersAsync();

        expect(form.submitted).toEqual([]);
    });

    it('disables the submit buttons of a [data-once] form after its first submit, keeping the clicked one in the post', async () => {
        globalThis.window = {};
        const page = fakePage();
        const form = fakeForm({ once: '' }, 2);

        bindForms(page);

        expect(page.submit(form, form.buttons[0]).defaultPrevented).toBe(false);
        // Not yet: a button disabled during the submit event would leave its name and value out of the post.
        expect(form.buttons.map(button => button.disabled)).toEqual([false, false]);

        await vi.runAllTimersAsync();

        expect(form.buttons.map(button => button.disabled)).toEqual([true, true]);
    });

    it('leaves a [data-once] form alone when something else stopped the submit', async () => {
        globalThis.window = {};
        const page = fakePage();
        const form = fakeForm({ once: '' });

        bindForms(page);
        const event = page.submit(form, form.buttons[0]);
        event.preventDefault();
        await vi.runAllTimersAsync();

        expect(form.buttons[0].disabled).toBe(false);
    });
});

describe('window.crestapps', () => {
    afterEach(() => {
        delete globalThis.window;
        delete globalThis.document;
    });

    it('exposes the helpers and binds the forms of the page', async () => {
        vi.resetModules();
        const page = fakePage();
        globalThis.window = {};
        globalThis.document = page;
        const added = vi.spyOn(page, 'addEventListener');

        await import(toolkit);

        expect(globalThis.window.crestapps.html.escape('"')).toBe('&quot;');
        expect(globalThis.window.crestapps.text.format('{0}!', 'Hi')).toBe('Hi!');
        expect(globalThis.window.crestapps.http.request.name).toBe('request');
        expect(globalThis.window.crestapps.http.HttpError.name).toBe('HttpError');
        expect(globalThis.window.crestapps.confirm.name).toBe('confirmAction');
        expect(added).toHaveBeenCalledWith('submit', expect.any(Function), true);
    });
});
