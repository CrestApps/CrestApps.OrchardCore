stripePaymentProcessing = function () {

    const initialize = (options) => {

        const defaultOptions = {
            processorKey: 'Stripe',
            cardElement: '#card-element'
        };

        // Extend the default options with the provided options.
        const config = { ...defaultOptions, ...options };

        document.addEventListener('DOMContentLoaded', () => {

            const errorElement = document.getElementById('card-errors');
            const showError = (message) => {
                errorElement.textContent = message;
                errorElement.classList.remove('d-none');
            };
            const clearError = () => {
                errorElement.textContent = '';
                errorElement.classList.add('d-none');
            };
            const getFilteredStyleObject = (element) => {
                if (!element) {
                    return {};
                }
                const computedStyle = window.getComputedStyle(element);
                const styleObject = {};
                const propertiesToInclude = [
                    'margin',
                    'padding',
                    'color',
                    'font-family',
                    'font-size',
                    'line-height',
                    'border-width',
                    'border-style',
                    'border-color'
                ];

                propertiesToInclude.forEach(property => {
                    styleObject[property] = computedStyle.getPropertyValue(property);
                });

                return styleObject;
            }
            const applyStylesToStripeCard = (styles) => {
                return {
                    base: {
                        color: styles.color || 'black', // default color if not specified
                        fontSize: styles['font-size'] || '16px', // default font size if not specified
                        fontFamily: styles['font-family'] || 'Arial, sans-serif', // default font family if not specified
                        lineHeight: styles['line-height'] || '1.4', // default line height if not specified
                        border: `${styles['border-width'] || '1px'} ${styles['border-style'] || 'solid'} ${styles['border-color'] || 'black'}` // default border if not specified
                    },
                };
            }
            const stripe = Stripe(config.publishableKey);
            const elements = stripe.elements();

            // Generate the filtered style object
            const filteredStyleObject = getFilteredStyleObject(config.nameOnBankCardElement);

            // Apply styles to Stripe card
            const cardStyles = applyStylesToStripeCard(filteredStyleObject);

            const cardElement = elements.create('card',
                {
                    style: cardStyles
                });
            cardElement.mount(config.cardElement);
            cardElement.on('change', function (event) {
                clearError();
                config.enablePayButtonButton(true);

                if (event.error) {
                    showError(event.error.message);
                }
            });

            config.payButtonElement.addEventListener('click', function (event) {

                if (config.payButtonElement.getAttribute('data-method-name') != config.processorKey) {
                    return;
                }

                event.preventDefault();

                if (config.nameOnBankCardElement && !config.nameOnBankCardElement.value) {
                    showError(config.invalidNameErrorMessage);

                    return;
                }

                config.enablePayButtonButton(false);

                stripe.createPaymentMethod({
                    type: 'card',
                    card: cardElement,
                    billing_details: {
                        name: config.nameOnBankCardElement.value || '',
                    },
                }).then(function (result) {
                    if (result.error) {
                        showError(result.error.message);
                    } else {
                        // Send payment method ID to the server
                        fetch(config.stepIntentEndpoint, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                            },
                            body: JSON.stringify({
                                paymentMethodId: result.paymentMethod.id,
                                sessionId: config.sessionId
                            }),
                        }).then((response) => response.json())
                            .then((data) => {
                                if (data.error) {
                                    showError(data.error);
                                } else {

                                    stripe.confirmCardSetup(data.clientSecret)
                                        .then((result) => {
                                            if (result.error) {
                                                showError(result.error.message);
                                            } else {
                                                var paymentMethodId = result.setupIntent.payment_method;

                                                if (data.processInitialPayment) {
                                                    // Process initial payment
                                                    fetch(config.paymentIntentEndpoint, {
                                                        method: 'POST',
                                                        headers: {
                                                            'Content-Type': 'application/json',
                                                        },
                                                        body: JSON.stringify({
                                                            customerId: data.customerId,
                                                            paymentMethodId: paymentMethodId,
                                                            sessionId: config.sessionId,
                                                        }),
                                                    }).then((response) => response.json())
                                                        .then((paymentData) => {
                                                            if (paymentData.error) {
                                                                showError(paymentData.error);
                                                            } else {
                                                                stripe.confirmCardPayment(paymentData.clientSecret)
                                                                    .then((result) => {
                                                                        if (result.error) {
                                                                            showError(result.error.message);
                                                                        } else {
                                                                            // Handle successful payment and schedule the subscription
                                                                            createSubscription(data.customerId, paymentMethodId);
                                                                        }
                                                                    });
                                                            }
                                                        });
                                                } else {
                                                    // Skip initial payment and schedule the subscription
                                                    createSubscription(data.customerId, paymentMethodId);
                                                }
                                            }
                                        });
                                }
                            });
                    }
                });
            });

            const createSubscription = (customerId, paymentMethodId) => {
                fetch(config.subscriptionEndpoint, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        customerId: customerId,
                        paymentMethodId: paymentMethodId,
                        sessionId: config.sessionId,
                    }),
                }).then((response) => response.json())
                    .then((subscriptionData) => {
                        if (subscriptionData.error) {
                            showError(subscriptionData.error);
                        } else {
                            const form = config.formElement;

                            if (subscriptionData.status == 'requires_action') {
                                stripe.confirmCardPayment(subscriptionData.clientSecret)
                                    .then(function (result) {
                                        if (result.error) {
                                            showError(result.error.message);
                                        } else {
                                            const submitEvent = new Event("submit", { bubbles: true, cancelable: true });
                                            form.dispatchEvent(submitEvent);
                                        }
                                    });
                            } else {
                                const submitEvent = new Event("submit", { bubbles: true, cancelable: true });
                                form.dispatchEvent(submitEvent);
                            }
                        }
                    });
            };
        });
    };

    return {
        initialize: initialize
    };
}();
