// fetch with what every request to this site needs: the antiforgery token on anything but a read, same-origin cookies,
// and an error to catch when the response is not a success, carrying the status and how long to wait before retrying.

const safeMethods = ['GET', 'HEAD', 'OPTIONS'];

// The token the form tag helper renders in a hidden input, or the one a layout puts in a meta tag.
export function antiforgeryToken() {
    if (typeof document === 'undefined' || !document) {
        return null;
    }

    const input = document.querySelector('input[name="__RequestVerificationToken"]');

    if (input && input.value) {
        return input.value;
    }

    const meta = document.querySelector('meta[name="RequestVerificationToken"]');

    return meta ? meta.getAttribute('content') : null;
}

// Retry-After is a number of seconds or an HTTP date; either way this returns whole seconds, or null without one.
export function retryAfterSeconds(value) {
    if (value === undefined || value === null || String(value).trim() === '') {
        return null;
    }

    const text = String(value).trim();

    if (/^\d+$/.test(text)) {
        return parseInt(text, 10);
    }

    const date = Date.parse(text);

    return Number.isNaN(date) ? null : Math.max(0, Math.round((date - Date.now()) / 1000));
}

export class HttpError extends Error {
    constructor(response) {
        super(`${response.status} ${response.statusText || ''}`.trim());
        this.name = 'HttpError';
        this.status = response.status;
        this.response = response;
        this.retryAfter = retryAfterSeconds(response.headers ? response.headers.get('Retry-After') : null);
    }
}

function formBody(form) {
    if (typeof FormData !== 'undefined' && form instanceof FormData) {
        return form;
    }

    if (typeof HTMLFormElement !== 'undefined' && form instanceof HTMLFormElement) {
        return new FormData(form);
    }

    const body = new URLSearchParams();

    Object.entries(form).forEach(([name, value]) => {
        if (value !== undefined && value !== null) {
            body.append(name, String(value));
        }
    });

    return body;
}

// The antiforgery token belongs to this site: it goes with a request to the page's own origin only. A relative URL is
// always that; without a page to compare with (a test), the URL is taken as relative.
function isSameOrigin(url) {
    const here = typeof window !== 'undefined' && window.location ? window.location : null;

    if (!here) {
        return true;
    }

    try {
        return new URL(String(url), here.href).origin === here.origin;
    } catch {
        return false;
    }
}

// request(url, { method, json, form, body, headers, signal }) resolves with the Response when its status is 2xx, and
// rejects with an HttpError otherwise. json is sent as JSON; form (an object, a FormData or a form element) as a form.
export async function request(url, options = {}) {
    const method = (options.method || 'GET').toUpperCase();
    const headers = Object.assign({}, options.headers);
    let body = options.body;

    if (options.json !== undefined) {
        headers['Content-Type'] = 'application/json';
        body = JSON.stringify(options.json);
    } else if (options.form !== undefined && options.form !== null) {
        body = formBody(options.form);
    }

    if (!safeMethods.includes(method) && !headers.RequestVerificationToken && isSameOrigin(url)) {
        const token = antiforgeryToken();

        if (token) {
            headers.RequestVerificationToken = token;
        }
    }

    const response = await fetch(url, { method, headers, body, credentials: 'same-origin', signal: options.signal });

    if (!response.ok) {
        throw new HttpError(response);
    }

    return response;
}
