/*
 * Hosted Stripe Checkout initializer for the subscription flow.
 *
 * Unlike the Payment Elements integration, this does not collect card data in the page. When the
 * customer clicks the pay button it creates a Stripe Checkout Session on the server and redirects the
 * browser to Stripe's hosted payment page. Stripe returns the customer to the flow's CheckoutReturn
 * action, which finalizes the subscription.
 */
var stripeSubscriptionCheckout = (function () {
    'use strict';

    function showError(config, message) {
        if (config.errorElement) {
            config.errorElement.textContent = message;
            config.errorElement.classList.remove('d-none');
        }
    }

    function hideError(config) {
        if (config.errorElement) {
            config.errorElement.textContent = '';
            config.errorElement.classList.add('d-none');
        }
    }

    function setBusy(config, busy) {
        if (typeof config.enablePayButtonButton === 'function') {
            config.enablePayButtonButton(!busy);
        }
    }

    async function startCheckout(config) {
        hideError(config);
        setBusy(config, true);

        try {
            const response = await fetch(config.createCheckoutSessionEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ sessionId: config.sessionId })
            });

            if (!response.ok) {
                let message = config.genericErrorMessage;

                try {
                    const problem = await response.json();

                    if (problem && problem.errorMessage) {
                        message = problem.errorMessage;
                    }
                } catch (e) {
                    // Ignore body parsing errors and fall back to the generic message.
                }

                showError(config, message);
                setBusy(config, false);

                return;
            }

            const data = await response.json();

            if (data && data.url) {
                window.location = data.url;

                return;
            }

            showError(config, config.genericErrorMessage);
            setBusy(config, false);
        } catch (e) {
            showError(config, config.genericErrorMessage);
            setBusy(config, false);
        }
    }

    function initialize(config) {
        if (!config || !config.payButtonElement) {
            return;
        }

        config.payButtonElement.addEventListener('click', function (event) {
            // Only handle the click when this processor is the selected payment method. Otherwise let the
            // other method's handler (or the default submit) run.
            if (config.payButtonElement.getAttribute('data-method-name') !== config.processorKey) {
                return;
            }

            event.preventDefault();

            startCheckout(config);
        });
    }

    return {
        initialize: initialize
    };
})();
