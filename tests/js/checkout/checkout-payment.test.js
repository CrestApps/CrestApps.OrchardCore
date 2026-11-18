import { beforeEach, describe, expect, it } from 'vitest';

// Just enough of the DOM for the payment step's method switch: elements with attributes, text, a class list and change
// listeners, found by the selectors the script uses.
class FakeElement {
    constructor(attributes = {}, text = '') {
        this.attributes = new Map(Object.entries(attributes));
        this.textContent = text;
        this.checked = false;
        this.listeners = {};
        this.classes = new Set();
        this.classList = {
            toggle: (name, force) => (force ? this.classes.add(name) : this.classes.delete(name)),
        };
    }

    getAttribute(name) { return this.attributes.has(name) ? this.attributes.get(name) : null; }
    setAttribute(name, value) { this.attributes.set(name, String(value)); }
    hasAttribute(name) { return this.attributes.has(name); }
    addEventListener(type, listener) { this.listeners[type] = listener; }
}

function createPage() {
    const card = new FakeElement({ value: 'stripe' });
    const payLater = new FakeElement({ value: 'pay-later', 'data-defers-payment': 'true' });
    const submitLabel = new FakeElement({}, 'Pay now');
    const dueLabel = new FakeElement({ 'data-checkout-due-label': '', 'data-deferred-text': 'Due later' }, 'Due now');
    const radios = [card, payLater];

    globalThis.document = {
        querySelector(selector) {
            if (selector === 'input[name="checkout-payment-method"]:checked') {
                return radios.find(radio => radio.checked) ?? null;
            }

            return selector === '#checkout-submit .checkout-submit-label' ? submitLabel : null;
        },
        querySelectorAll(selector) {
            if (selector === 'input[name="checkout-payment-method"]') {
                return radios;
            }

            return selector === '[data-checkout-due-label]' ? [dueLabel] : [];
        },
        getElementById: () => null,
    };

    globalThis.window = globalThis;

    return { card, payLater, submitLabel, dueLabel };
}

function choose(page, radio) {
    page.card.checked = radio === page.card;
    page.payLater.checked = radio === page.payLater;
    radio.listeners.change();
}

describe('checkout payment method switch', () => {
    let page;

    beforeEach(async () => {
        page = createPage();
        await import('../../../src/Modules/CrestApps.OrchardCore.Checkout/Assets/js/checkout-payment.js');
        page.card.checked = true;
        globalThis.checkoutPayment.initialize({ deferredSubmitLabel: 'Place order' });
    });

    it('keeps the page\'s own words for a method that pays now', () => {
        expect(page.submitLabel.textContent).toBe('Pay now');
        expect(page.dueLabel.textContent).toBe('Due now');
    });

    it('says the order is placed and the total is due later for a method that collects nothing now', () => {
        choose(page, page.payLater);

        expect(page.submitLabel.textContent).toBe('Place order');
        expect(page.dueLabel.textContent).toBe('Due later');
    });

    it('puts the page\'s own words back when a method that pays now is chosen again', () => {
        choose(page, page.payLater);
        choose(page, page.card);

        expect(page.submitLabel.textContent).toBe('Pay now');
        expect(page.dueLabel.textContent).toBe('Due now');
    });
});
