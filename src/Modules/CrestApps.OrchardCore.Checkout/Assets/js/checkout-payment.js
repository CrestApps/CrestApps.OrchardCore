/*
 * Drives the payment step of the generic checkout.
 *
 * Payment cannot be a plain form post: an embedded provider needs to confirm the card in the browser with a
 * client secret, and a hosted one needs a redirect. So the page starts the payment on the server, hands
 * control to the selected provider's own handler, and then polls the server until it reports the checkout
 * settled. Providers register a handler here instead of this file knowing anything about them.
 */
var checkoutPayment = (function () {
    'use strict';

    var handlers = {};
    var config = null;

    /**
     * Registers a provider handler.
     *
     * @param {string} providerKey The provider's stable key.
     * @param {object} handler An object with two optional async methods:
     *                         prepare(config) runs before the payment is started and returns an object of
     *                         string values to hand the provider on the server (for example a tokenized
     *                         payment method), or false to abort having shown its own error. A recurring
     *                         agreement usually cannot be created without one, and tokenizing in the browser
     *                         is what keeps card details out of this application.
     *                         confirm(step, config) runs after the payment is started. It returns a falsy
     *                         value to abort (having shown its own error), or a truthy value to continue to
     *                         polling. A provider that redirects never returns.
     */
    function register(providerKey, handler) {
        handlers[providerKey] = handler;
    }

    function selectedProviderKey() {
        var checked = document.querySelector('input[name="checkout-payment-method"]:checked');

        return checked ? checked.value : null;
    }

    function showError(message) {
        var element = document.getElementById('checkout-payment-error');

        if (!element) {
            return;
        }

        element.textContent = message || config.genericErrorMessage;
        element.classList.remove('d-none');
    }

    function clearError() {
        var element = document.getElementById('checkout-payment-error');

        if (element) {
            element.textContent = '';
            element.classList.add('d-none');
        }
    }

    function setBusy(busy) {
        var button = document.getElementById('checkout-submit');

        if (!button) {
            return;
        }

        button.disabled = busy;
        button.querySelector('.checkout-submit-label').classList.toggle('d-none', busy);
        button.querySelector('.checkout-submit-busy').classList.toggle('d-none', !busy);
    }

    // Only the selected provider's panel is shown, so the customer never sees card fields for a method they
    // did not choose (and a hidden panel's inputs are never submitted).
    function syncPanels() {
        var key = selectedProviderKey();
        var panels = document.querySelectorAll('[data-checkout-payment-panel]');

        for (var i = 0; i < panels.length; i++) {
            var panel = panels[i];
            panel.classList.toggle('d-none', panel.getAttribute('data-checkout-payment-panel') !== key);
        }
    }

    async function postJson(url, body) {
        var response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify(body || {})
        });

        var payload = null;

        try {
            payload = await response.json();
        } catch (e) {
            // A body that is not JSON is treated as a generic failure below.
        }

        return { ok: response.ok, status: response.status, payload: payload };
    }

    // Asks the server whether the checkout settled. The provider may still be finalizing, so a pending answer
    // is retried rather than treated as a failure; giving up too early would tell a customer whose payment
    // actually succeeded that it did not.
    async function pollUntilSettled() {
        for (var attempt = 0; attempt < config.maxStatusAttempts; attempt++) {
            var result = await postJson(config.statusEndpoint, {});

            if (!result.ok) {
                showError(result.payload && result.payload.errorMessage);

                return false;
            }

            var payload = result.payload || {};

            if (payload.completed) {
                window.location.assign(config.confirmationUrl);

                return true;
            }

            if (payload.status === 'Failed' || payload.status === 'Canceled') {
                showError(payload.errorMessage);

                return false;
            }

            if (payload.status === 'Blocked') {
                window.location.reload();

                return true;
            }

            await new Promise(function (resolve) { setTimeout(resolve, config.statusIntervalMs); });
        }

        // The provider is taking unusually long. The payment is not lost: the reconciliation sweep completes
        // it server-side, so the customer is told to expect confirmation rather than to pay again.
        showError(config.stillProcessingMessage);

        return false;
    }

    async function pay(event) {
        // A submit that names an action is asking the server to change the step, not to take a payment.
        // Applying a promotion code is one of those: swallowing it here would drop the code silently and
        // charge the customer full price.
        var submitter = event.submitter;

        if (submitter && submitter.name === 'checkout-action') {
            return;
        }

        event.preventDefault();

        var providerKey = selectedProviderKey();

        if (!providerKey) {
            showError(config.selectMethodMessage);

            return;
        }

        clearError();
        setBusy(true);

        try {
            var handler = handlers[providerKey];
            var providerData = null;

            if (handler && typeof handler.prepare === 'function') {
                providerData = await handler.prepare(config);

                // A handler that could not produce what it needs has already told the customer why. Starting
                // the payment anyway would create a durable attempt that can never settle.
                if (providerData === false) {
                    setBusy(false);

                    return;
                }
            }

            var begun = await postJson(config.beginEndpoint, {
                providerKey: providerKey,
                returnUrl: config.confirmationUrl,
                cancelUrl: config.cancelUrl,
                providerData: providerData || undefined
            });

            if (!begun.ok) {
                showError(begun.payload && begun.payload.errorMessage);
                setBusy(false);

                return;
            }

            var steps = (begun.payload && begun.payload.steps) || [];

            for (var i = 0; i < steps.length; i++) {
                var step = steps[i];

                if (step.redirectUrl) {
                    window.location.assign(step.redirectUrl);

                    return;
                }

                if (step.requiresAction && handler && typeof handler.confirm === 'function') {
                    var confirmed = await handler.confirm(step, config);

                    if (!confirmed) {
                        setBusy(false);

                        return;
                    }
                }
            }

            await pollUntilSettled();
        } catch (e) {
            showError(null);
        } finally {
            setBusy(false);
        }
    }

    function initialize(options) {
        config = Object.assign({
            statusIntervalMs: 2000,
            maxStatusAttempts: 30,
            genericErrorMessage: 'Your payment could not be completed. Please try again.',
            selectMethodMessage: 'Please choose a payment method.',
            stillProcessingMessage: 'Your payment is still being processed. You will receive a confirmation once it completes.'
        }, options || {});

        var radios = document.querySelectorAll('input[name="checkout-payment-method"]');

        for (var i = 0; i < radios.length; i++) {
            radios[i].addEventListener('change', syncPanels);
        }

        syncPanels();

        var form = document.getElementById('checkout-form');

        if (form) {
            form.addEventListener('submit', pay);
        }
    }

    return {
        initialize: initialize,
        register: register
    };
})();
