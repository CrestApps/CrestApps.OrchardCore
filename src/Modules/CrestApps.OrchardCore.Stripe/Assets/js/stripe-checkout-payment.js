/*
 * The Stripe handler for the generic checkout.
 *
 * The card is entered into Stripe's own iframe and confirmed from the browser with the client secret the
 * server returned, so card details never reach this application. After confirmation the checkout script polls
 * the server, which verifies the payment against Stripe's API before recording anything — a confirmation here
 * is a hint, never proof.
 */
(function () {
    'use strict';

    function initialize(options) {
        if (typeof checkoutPayment === 'undefined' || !options || !options.publishableKey) {
            return;
        }

        var stripe = Stripe(options.publishableKey);
        var elements = stripe.elements();
        var card = elements.create('card');

        card.mount(options.cardElementSelector);

        var errorElement = document.querySelector(options.errorSelector);

        card.on('change', function (event) {
            if (errorElement) {
                errorElement.textContent = event.error ? event.error.message : '';
                errorElement.classList.toggle('d-none', !event.error);
            }
        });

        // Set once the card has been tokenized. Reusing that payment method when confirming keeps the
        // subscription's first invoice on the very card the agreement was created against, instead of
        // silently attaching a second one.
        var preparedPaymentMethodId = null;

        function billingName() {
            return options.nameElement ? (document.querySelector(options.nameElement) || {}).value || '' : '';
        }

        function showError(message) {
            if (errorElement) {
                errorElement.textContent = message;
                errorElement.classList.remove('d-none');
            }
        }

        checkoutPayment.register(options.processorKey, {
            prepare: async function () {
                // Only a recurring agreement needs a reusable payment method up front. A one-time charge is
                // confirmed straight from the card element, so tokenizing first would be a wasted round trip.
                if (!options.hasRecurringItems) {
                    return null;
                }

                var result = await stripe.createPaymentMethod({
                    type: 'card',
                    card: card,
                    billing_details: { name: billingName() }
                });

                if (result.error) {
                    showError(result.error.message);

                    return false;
                }

                preparedPaymentMethodId = result.paymentMethod.id;

                return { paymentMethodId: preparedPaymentMethodId };
            },
            confirm: async function (step) {
                if (!step.clientSecret) {
                    // Nothing for the customer to confirm (for example an obligation that was already
                    // settled), so let the checkout move on to polling.
                    return true;
                }

                var result = await stripe.confirmCardPayment(step.clientSecret, {
                    payment_method: preparedPaymentMethodId || {
                        card: card,
                        billing_details: {
                            name: billingName()
                        }
                    }
                });

                if (result.error) {
                    showError(result.error.message);

                    return false;
                }

                // The server still has to verify this against Stripe before anything is recorded as paid.
                return true;
            }
        });
    }

    window.stripeCheckoutPayment = { initialize: initialize };
})();
