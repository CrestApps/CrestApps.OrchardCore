import { readFileSync } from 'node:fs';
import { afterEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/offer-actions.js';

const { offerAcceptView, showOfferAccepting, withOfferAcceptTimeout, OFFER_ACCEPT_TIMEOUT_MS } = globalThis.CrestAppsContactCenter;

const labels = { accept: 'Accept', dial: 'Dial', answering: 'Answering…', dialing: 'Dialing…' };

// Bug: accepting an offer from the workspace or the docked agent bar gave no sign anything was happening until the
// round trip finished, so the agent clicked again. From the first click the accept button reads "Answering..." with a
// spinner, and it and its siblings (Decline, Skip) are disabled.
describe('offerAcceptView', () => {
    it('shows an inbound answer in progress', () => {
        expect(offerAcceptView({ pending: true, preview: false }, labels)).toEqual({ disabled: true, spinner: true, label: 'Answering…' });
    });

    it('shows a preview dial in progress', () => {
        expect(offerAcceptView({ pending: true, preview: true }, labels)).toEqual({ disabled: true, spinner: true, label: 'Dialing…' });
    });

    it('shows the resting buttons', () => {
        expect(offerAcceptView({ pending: false, preview: false }, labels)).toEqual({ disabled: false, spinner: false, label: 'Accept' });
        expect(offerAcceptView({ pending: false, preview: true }, labels)).toEqual({ disabled: false, spinner: false, label: 'Dial' });
    });

    it('falls back to English labels', () => {
        expect(offerAcceptView({ pending: true, preview: false }, null).label).toBe('Answering…');
        expect(offerAcceptView({ pending: false, preview: false }, {}).label).toBe('Accept');
    });
});

// An accept that never answers must not leave the buttons disabled for good.
describe('withOfferAcceptTimeout', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('settles with the accept when it answers in time', async () => {
        await expect(withOfferAcceptTimeout(Promise.resolve('ok'))).resolves.toBe('ok');
    });

    it('passes a failed accept through', async () => {
        await expect(withOfferAcceptTimeout(Promise.reject(new Error('boom')))).rejects.toThrow('boom');
    });

    it('gives up on an accept that never answers', async () => {
        vi.useFakeTimers();
        const outcome = withOfferAcceptTimeout(new Promise(() => { })).catch(error => error);

        vi.advanceTimersByTime(OFFER_ACCEPT_TIMEOUT_MS);

        expect((await outcome).offerAcceptTimedOut).toBe(true);
    });
});

// A minimal stand-in for the offer's markup: an accept button and a decline button.
function fakeOffer() {
    const doc = {
        createElement: tag => ({ tag, className: '', attributes: {}, setAttribute(name, value) { this.attributes[name] = value; } }),
        createTextNode: text => ({ text }),
    };
    const button = (accept) => ({
        disabled: false,
        innerHTML: accept ? '<i class="fa-solid fa-phone"></i> Accept' : 'Decline',
        children: [],
        ownerDocument: doc,
        set textContent(value) { this.children = []; this.innerHTML = value; },
        appendChild(node) { this.children.push(node); },
    });
    const accept = button(true);
    const decline = button(false);

    return {
        accept,
        decline,
        querySelectorAll: () => [accept, decline],
        querySelector: selector => (selector === '[data-cc-accept]' ? accept : null),
    };
}

describe('showOfferAccepting', () => {
    it('disables every offer button and shows the spinner on the accept button', () => {
        const offer = fakeOffer();

        showOfferAccepting(offer, { pending: true, preview: false }, labels);

        expect(offer.accept.disabled).toBe(true);
        expect(offer.decline.disabled).toBe(true);
        expect(offer.accept.children[0].className).toContain('spinner-border');
        expect(offer.accept.children[1].text).toBe('Answering…');
    });

    it('puts the buttons back exactly as they were', () => {
        const offer = fakeOffer();

        showOfferAccepting(offer, { pending: true, preview: false }, labels);
        showOfferAccepting(offer, { pending: false, preview: false }, labels);

        expect(offer.accept.disabled).toBe(false);
        expect(offer.decline.disabled).toBe(false);
        expect(offer.accept.innerHTML).toBe('<i class="fa-solid fa-phone"></i> Accept');
    });

    it('ignores a missing offer', () => {
        expect(() => showOfferAccepting(null, { pending: true }, labels)).not.toThrow();
    });
});

const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets.json', 'utf8'));
const helper = 'Assets/js/shared/offer-actions.js';

describe.each([
    ['wwwroot/scripts/agent-workspace.js', 'Assets/js/agent-workspace.js'],
    ['wwwroot/scripts/contact-center-agent-bar.js', 'Assets/js/contact-center-agent-bar.js'],
])('the %s bundle', (output, script) => {
    const group = assets.find(candidate => candidate.output === output);

    it('carries the offer-actions helper ahead of the script that uses it', () => {
        expect(group.inputs).toContain(helper);
        expect(group.inputs.indexOf(helper)).toBeLessThan(group.inputs.indexOf(script));
    });
});
